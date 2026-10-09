
using System;
using UnityEngine;

public class Prisoner : MonoBehaviour
{
    public static event Action<Vector3> EscapeAlarm;

    [Header("Prisoner Settings")]
    [Min(0.1f)] public float speed = 1.5f;
    [Min(1f)] public float runMultiplier = 1.8f;
    [Min(0f)] public float stamina = 10f;
    [Min(0.1f)] public float maxStamina = 10f;
    [Min(0f)] public float staminaDrain = 2f;
    [Min(0f)] public float staminaRecovery = 1f;
    [Min(0.1f)] public float visionRange = 6f;
    [Min(0.01f)] public float lockpickSkill = 1f;
    [Min(0f)] public float escapeDelay = 3f;

    [Header("Evasion")]
    [Min(0.1f)] public float runDistance = 3.5f;
    [Min(0.1f)] public float hideSearchRange = 4f;
    [Min(0f)] public float hideDuration = 4f;
    [Min(0f)] public float hideCooldown = 3f;
    [Min(0f)] public float distractCooldown = 8f;
    [Min(0f)] public float distractActionTime = 0.5f;
    [Min(0f)] public float distractRange = 5f;

    [Header("Prisoner States")]
    public PrisonerState currentState = PrisonerState.InCell;
    public Cell homeCell;
    public bool isHidden;
    public bool hasEscaped;

    [HideInInspector] public Bounds map;

    private Vector3 destination;
    private float currentSpeed;
    private float waitTimer;
    private float hideTimer;
    private float hideCooldownTimer;
    private float distractTimer;
    private float actionTimer;
    private Transform hideSpot;
    private Guard threat;
    private bool mapInitialized;

    public bool IsFree
    {
        get
        {
            return currentState == PrisonerState.Escaping
                || currentState == PrisonerState.Running
                || currentState == PrisonerState.Hiding
                || currentState == PrisonerState.Distracting;
        }
    }

    private void Awake()
    {
        destination = transform.position;
        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }

    // Llamado por SimulationManager antes del primer tick.
    public void InitializeMap(Bounds bounds)
    {
        map = bounds;
        mapInitialized = bounds.size.x > 0f && bounds.size.y > 0f;
    }

    public void Simulate(float h)
    {
        if (h <= 0f || hasEscaped ||
            currentState == PrisonerState.Escaped)
            return;

        currentSpeed = 0f;
        distractTimer = Mathf.Max(0f, distractTimer - h);
        hideCooldownTimer = Mathf.Max(0f, hideCooldownTimer - h);

        EvaluateState(h);

        switch (currentState)
        {
            case PrisonerState.InCell:
                waitTimer += h;

                if (waitTimer >= escapeDelay)
                {
                    if (homeCell == null)
                    {
                        Debug.LogError(
                            $"{name}: no tiene homeCell asignada.",
                            this
                        );
                        break;
                    }

                    if (homeCell.state == CellState.Open)
                        BeginEscape();
                    else
                        currentState = PrisonerState.OpeningCell;
                }
                break;

            case PrisonerState.OpeningCell:
                OpenCell(h);
                break;

            case PrisonerState.Escaping:
                destination = GetNearestExit();
                currentSpeed = speed;
                break;

            case PrisonerState.Running:
                Run(h);
                break;

            case PrisonerState.Hiding:
                Hide(h);
                break;

            case PrisonerState.Distracting:
                actionTimer += h;

                if (actionTimer >= distractActionTime)
                    currentState = PrisonerState.Escaping;
                break;

            case PrisonerState.Captured:
                // El guardia controla el movimiento durante la escolta.
                break;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            currentSpeed * h
        );

        if (currentState != PrisonerState.Running)
            stamina = Mathf.Min(maxStamina, stamina + staminaRecovery * h);

        CheckEscaped();
    }

    private void EvaluateState(float h)
    {
        if (currentState != PrisonerState.Escaping &&
            currentState != PrisonerState.Running)
            return;

        threat = FindNearestGuard();

        if (threat == null)
        {
            currentState = PrisonerState.Escaping;
            return;
        }

        float distance = Vector2.Distance(
            transform.position, threat.transform.position
        );

        if (distance <= runDistance && stamina > 0f)
        {
            currentState = PrisonerState.Running;
            return;
        }

        if (distance > runDistance && distractTimer <= 0f)
        {
            StartDistract();
            return;
        }

        if (hideCooldownTimer <= 0f)
        {
            Transform spot = FindHidingSpot();

            if (spot != null)
            {
                StartHide(spot);
                return;
            }
        }

        currentState = PrisonerState.Escaping;
    }

    private void OpenCell(float h)
    {
        if (homeCell == null)
        {
            Debug.LogError(
                $"{name}: no puede forzar una celda porque homeCell es null.",
                this
            );
            return;
        }

        if (homeCell.PickLock(lockpickSkill * h))
            BeginEscape();
    }

    private void BeginEscape()
    {
        if (homeCell == null)
            return;

        if (homeCell.state != CellState.Open)
            return;

        currentState = PrisonerState.Escaping;
        destination = GetNearestExit();

        EscapeAlarm?.Invoke(transform.position);

        Debug.Log($"{name}: comienza la fuga.", this);
    }

    private void Run(float h)
    {
        Vector3 exitDirection =
            (GetNearestExit() - transform.position).normalized;

        Vector3 awayDirection = Vector3.zero;

        if (threat != null)
        {
            awayDirection =
                (transform.position - threat.transform.position).normalized;
        }

        Vector3 direction = (exitDirection + awayDirection).normalized;

        if (direction.sqrMagnitude < 0.01f)
            direction = exitDirection;

        destination = transform.position + direction * visionRange;
        currentSpeed = speed * runMultiplier;
        stamina = Mathf.Max(0f, stamina - staminaDrain * h);

        if (stamina <= 0f)
            currentState = PrisonerState.Escaping;
    }

    private void StartHide(Transform spot)
    {
        hideSpot = spot;
        hideTimer = 0f;
        isHidden = false;
        currentState = PrisonerState.Hiding;
    }

    private void Hide(float h)
    {
        if (hideSpot == null)
        {
            EndHide();
            return;
        }

        if (!isHidden)
        {
            destination = hideSpot.position;
            currentSpeed = speed;

            if (Vector2.Distance(transform.position, hideSpot.position) < 0.2f)
            {
                isHidden = true;
                destination = transform.position;
                currentSpeed = 0f;
            }
        }
        else
        {
            destination = transform.position;
            currentSpeed = 0f;
            hideTimer += h;

            if (hideTimer >= hideDuration)
                EndHide();
        }
    }

    private void EndHide()
    {
        isHidden = false;
        hideSpot = null;
        hideCooldownTimer = hideCooldown;
        currentState = PrisonerState.Escaping;
    }

    private void StartDistract()
    {
        if (threat == null)
        {
            currentState = PrisonerState.Escaping;
            return;
        }

        distractTimer = distractCooldown;
        actionTimer = 0f;
        currentState = PrisonerState.Distracting;

        Vector3 direction =
            (threat.transform.position - transform.position).normalized;

        threat.Distract(
            threat.transform.position + direction * distractRange
        );
    }

    public void Capture()
    {
        if (hasEscaped)
            return;

        currentState = PrisonerState.Captured;
        isHidden = false;
        hideSpot = null;
        currentSpeed = 0f;
        destination = transform.position;
    }

    public void ReturnToCell(Cell cell)
    {
        if (cell == null)
        {
            Debug.LogError($"{name}: ReturnToCell recibió una celda nula.", this);
            return;
        }

        homeCell = cell;
        transform.position = cell.transform.position;
        destination = transform.position;

        currentState = PrisonerState.InCell;
        waitTimer = 0f;
        stamina = maxStamina;
        isHidden = false;
        hasEscaped = false;
        hideSpot = null;

        cell.Close();

        Debug.Log($"{name}: regresó a la celda {cell.name}.", this);
    }

    private Vector3 GetNearestExit()
    {
        Vector3 p = transform.position;
        float margin = 1f;

        float left = Mathf.Abs(p.x - map.min.x);
        float right = Mathf.Abs(map.max.x - p.x);
        float bottom = Mathf.Abs(p.y - map.min.y);
        float top = Mathf.Abs(map.max.y - p.y);

        float nearest = Mathf.Min(left, right, bottom, top);

        if (nearest == left)
            return new Vector3(map.min.x - margin, p.y, p.z);

        if (nearest == right)
            return new Vector3(map.max.x + margin, p.y, p.z);

        if (nearest == bottom)
            return new Vector3(p.x, map.min.y - margin, p.z);

        return new Vector3(p.x, map.max.y + margin, p.z);
    }

    private void CheckEscaped()
    {
        if (!IsFree || !mapInitialized)
            return;

        Vector3 p = transform.position;

        bool outside =
            p.x < map.min.x || p.x > map.max.x ||
            p.y < map.min.y || p.y > map.max.y;

        if (!outside)
            return;

        hasEscaped = true;
        currentState = PrisonerState.Escaped;
        isHidden = false;

        Debug.Log($"{name}: ¡fuga completada!", this);

        // Se desactiva después de registrar la fuga.
        gameObject.SetActive(false);
    }

    private Guard FindNearestGuard()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            visionRange,
            LayerMask.GetMask("Guards")
        );

        Guard nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Guard guard = hit.GetComponentInParent<Guard>();

            if (guard == null)
                continue;

            float distance = Vector2.Distance(
                transform.position, guard.transform.position
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = guard;
            }
        }

        return nearest;
    }

    private Transform FindHidingSpot()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            hideSearchRange,
            LayerMask.GetMask("HidingSpots")
        );

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            float distance = Vector2.Distance(
                transform.position, hit.transform.position
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, runDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, destination);
    }
}