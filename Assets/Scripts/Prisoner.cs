using System;
using UnityEngine;

public class Prisoner : MonoBehaviour
{
    // Aviso de fuga: los guardias se suscriben a este evento (ver Guard.OnEnable).
    public static event Action<Vector3> EscapeAlarm;

    [Header("Prisoner Settings")]
    public float speed = 1.5f;
    public float runMultiplier = 1.8f;
    public float stamina = 10f;
    public float maxStamina = 10f;
    public float staminaDrain = 2f;       // stamina que gasta por segundo corriendo
    public float staminaRecovery = 1f;    // stamina que recupera por segundo si no corre
    public float visionRange = 6f;        // mayor que la visión del guardia: el preso lo ve primero
    public float lockpickSkill = 1f;      // multiplicador de la velocidad para forzar la cerradura
    public float escapeDelay = 3f;        // segundos en la celda antes de intentar escapar

    [Header("Evasion")]
    public float runDistance = 3.5f;      // si el guardia está más cerca que esto, corre
    public float hideSearchRange = 4f;    // radio donde busca escondites
    public float hideDuration = 4f;
    public float hideCooldown = 3f;       // tiempo mínimo entre un escondite y el siguiente
    public float distractCooldown = 8f;
    public float distractActionTime = 0.5f;
    public float distractRange = 5f;      // a qué distancia detrás del guardia genera el ruido

    [Header("Prisoner States")]
    public PrisonerState currentState = PrisonerState.InCell;
    public Cell homeCell;
    public bool isHidden = false;
    public bool hasEscaped = false;

    [HideInInspector] public Bounds map;

    private Vector3 destination;
    private float currentSpeed;
    private float h;

    private float waitTimer = 0f;
    private float hideTimer = 0f;
    private float hideCooldownTimer = 0f;
    private float distractTimer = 0f;
    private float actionTimer = 0f;
    private Transform hideSpot;
    private Guard threat;

    // Un preso "libre" es el que ya salió de su celda y no está capturado.
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

    private void Start()
    {
        destination = transform.position;
    }

    public void Simulate(float h)
    {
        if (currentState == PrisonerState.Escaped) return;

        this.h = h;
        currentSpeed = 0f;
        distractTimer = Mathf.Max(0f, distractTimer - h);
        hideCooldownTimer = Mathf.Max(0f, hideCooldownTimer - h);

        EvaluateState();

        switch (currentState)
        {
            case PrisonerState.InCell:
                WaitInCell();
                break;
            case PrisonerState.OpeningCell:
                OpenCell();
                break;
            case PrisonerState.Escaping:
                Escape();
                break;
            case PrisonerState.Running:
                Run();
                break;
            case PrisonerState.Hiding:
                Hide();
                break;
            case PrisonerState.Distracting:
                Distract();
                break;
            case PrisonerState.Captured:
                // Lo mueve el guardia que lo escolta.
                break;
        }

        Move();
        RecoverStamina();
        CheckEscaped();
    }

    void EvaluateState()
    {
        // Solo reevalúa mientras escapa o corre. Los demás estados se
        // gestionan solos y avisan cuando terminan (ver cada método).
        if (currentState != PrisonerState.Escaping && currentState != PrisonerState.Running) return;

        threat = FindNearestGuard();

        // 1. Sin guardias a la vista -> seguir hacia la salida
        if (threat == null)
        {
            currentState = PrisonerState.Escaping;
            return;
        }

        float dist = Vector2.Distance(transform.position, threat.transform.position);

        // 2. Guardia a la vista pero todavía lejos (aún no nos ve): distraer o esconderse
        if (dist > runDistance)
        {
            if (distractTimer <= 0f)
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
            return;
        }

        // 3. Guardia cerca: correr si queda stamina
        if (stamina > 0f)
        {
            currentState = PrisonerState.Running;
            return;
        }

        // 4. Sin stamina: último recurso, esconderse si hay un escondite cerca
        Transform nearSpot = FindHidingSpot();
        if (nearSpot != null)
        {
            StartHide(nearSpot);
            return;
        }

        currentState = PrisonerState.Escaping;
    }

    void WaitInCell()
    {
        waitTimer += h;
        if (waitTimer >= escapeDelay)
        {
            currentState = PrisonerState.OpeningCell;
        }
    }

    void OpenCell()
    {
        // PickLock devuelve true cuando la celda quedó abierta (o ya lo estaba).
        if (homeCell == null || homeCell.PickLock(lockpickSkill * h))
        {
            currentState = PrisonerState.Escaping;

            if (EscapeAlarm != null)
            {
                EscapeAlarm(transform.position);
            }
        }
    }

    void Escape()
    {
        destination = GetNearestExit();
        currentSpeed = speed;
    }

    void Run()
    {
        // Corre hacia la salida pero desviándose para alejarse del guardia.
        Vector3 exitDir = (GetNearestExit() - transform.position).normalized;
        Vector3 awayDir = Vector3.zero;

        if (threat != null)
        {
            awayDir = (transform.position - threat.transform.position).normalized;
        }

        Vector3 dir = (exitDir * 0.5f + awayDir * 0.5f).normalized;
        destination = transform.position + dir * visionRange;
        currentSpeed = speed * runMultiplier;

        stamina = Mathf.Max(0f, stamina - staminaDrain * h);
    }

    void StartHide(Transform spot)
    {
        hideSpot = spot;
        hideTimer = 0f;
        isHidden = false;
        currentState = PrisonerState.Hiding;
    }

    void Hide()
    {
        if (hideSpot == null)
        {
            EndHide();
            return;
        }

        if (!isHidden)
        {
            // Primero camina hasta el escondite...
            destination = hideSpot.position;
            currentSpeed = speed;

            if (Vector2.Distance(transform.position, hideSpot.position) < 0.2f)
            {
                isHidden = true;
            }
        }
        else
        {
            // ...y una vez dentro, espera inmóvil y oculto.
            hideTimer += h;
            if (hideTimer >= hideDuration)
            {
                EndHide();
            }
        }
    }

    void EndHide()
    {
        isHidden = false;
        hideSpot = null;
        hideCooldownTimer = hideCooldown;
        currentState = PrisonerState.Escaping;
    }

    void StartDistract()
    {
        distractTimer = distractCooldown;
        actionTimer = 0f;
        currentState = PrisonerState.Distracting;

        // El ruido se genera detrás del guardia, para atraerlo lejos del preso.
        Vector3 away = (threat.transform.position - transform.position).normalized;
        threat.Distract(threat.transform.position + away * distractRange);
    }

    void Distract()
    {
        actionTimer += h;
        if (actionTimer >= distractActionTime)
        {
            currentState = PrisonerState.Escaping;
        }
    }

    // La llama el guardia al atraparlo.
    public void Capture()
    {
        currentState = PrisonerState.Captured;
        isHidden = false;
        hideSpot = null;
    }

    // La llama el guardia al dejarlo en su celda.
    public void ReturnToCell(Cell cell)
    {
        currentState = PrisonerState.InCell;
        waitTimer = 0f;
        stamina = maxStamina;
        isHidden = false;
        transform.position = cell.transform.position;
        destination = transform.position;
    }

    void Move()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            currentSpeed * h
        );
    }

    void RecoverStamina()
    {
        if (currentState != PrisonerState.Running)
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRecovery * h);
        }
    }

    void CheckEscaped()
    {
        if (!IsFree) return;

        Vector3 p = transform.position;
        bool inside = p.x >= map.min.x && p.x <= map.max.x
                   && p.y >= map.min.y && p.y <= map.max.y;

        if (!inside)
        {
            currentState = PrisonerState.Escaped;
            hasEscaped = true;
            Debug.Log("El preso " + name + " escapó de la prisión");
            gameObject.SetActive(false);
        }
    }

    // Punto justo fuera del mapa, sobre el borde más cercano.
    Vector3 GetNearestExit()
    {
        Vector3 p = transform.position;
        float margin = 1f;

        float left = p.x - map.min.x;
        float right = map.max.x - p.x;
        float down = p.y - map.min.y;
        float up = map.max.y - p.y;
        float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));

        if (min == left) return new Vector3(map.min.x - margin, p.y, 0f);
        if (min == right) return new Vector3(map.max.x + margin, p.y, 0f);
        if (min == down) return new Vector3(p.x, map.min.y - margin, 0f);
        return new Vector3(p.x, map.max.y + margin, 0f);
    }

    Guard FindNearestGuard()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Guards"));
        Guard nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Guard guard = hit.GetComponent<Guard>();
            if (guard != null)
            {
                float dist = Vector2.Distance(transform.position, guard.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = guard;
                }
            }
        }

        return nearest;
    }

    Transform FindHidingSpot()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hideSearchRange, LayerMask.GetMask("HidingSpots"));
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
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
