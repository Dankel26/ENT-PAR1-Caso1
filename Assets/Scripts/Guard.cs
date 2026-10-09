
using UnityEngine;

public class Guard : MonoBehaviour
{
    [Header("Guard Settings")]
    [Min(0f)] public float patrolSpeed = 1f;
    [Min(0.1f)] public float chaseSpeed = 2.2f;
    [Min(0.1f)] public float visionRange = 4f;
    [Min(0.1f)] public float hiddenDetectionRange = 1f;
    [Min(0.1f)] public float catchRange = 0.5f;
    [Min(0f)] public float hearingRange = 12f;
    [Min(0f)] public float investigateTime = 4f;
    [Min(0f)] public float loseTargetTime = 2f;

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Guard States")]
    public GuardState currentState = GuardState.Patrolling;
    public Prisoner target;

    private int patrolIndex;
    private Vector3 destination;
    private Vector3 investigatePos;
    private Vector3 lastKnownPos;
    private float currentSpeed;
    private float investigateTimer;
    private float lostTimer;
    private float h;

    private void OnEnable()
    {
        Prisoner.EscapeAlarm += HearAlarm;
    }

    private void OnDisable()
    {
        Prisoner.EscapeAlarm -= HearAlarm;
    }

    private void Start()
    {
        destination = transform.position;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = 0;

            if (patrolPoints[0] != null)
                destination = patrolPoints[0].position;
        }
    }

    public void Simulate(float step)
    {
        if (step <= 0f)
            return;

        h = step;
        currentSpeed = 0f;

        EvaluateState();

        switch (currentState)
        {
            case GuardState.Patrolling:
                Patrol();
                break;

            case GuardState.Investigating:
                Investigate();
                break;

            case GuardState.Chasing:
                Chase();
                break;

            case GuardState.Escorting:
                Escort();
                break;
        }

        Move();
    }

    private void EvaluateState()
    {
        if (currentState == GuardState.Escorting)
            return;

        Prisoner seen = FindVisiblePrisoner();

        if (seen != null)
        {
            target = seen;
            lastKnownPos = seen.transform.position;
            lostTimer = 0f;
            currentState = GuardState.Chasing;
            return;
        }

        if (currentState == GuardState.Chasing)
        {
            lostTimer += h;

            if (lostTimer >= loseTargetTime)
            {
                Vector3 lastPosition = lastKnownPos;
                target = null;
                BeginInvestigation(lastPosition);
            }
        }
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            destination = transform.position;
            return;
        }

        if (patrolPoints[patrolIndex] == null)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            return;
        }

        destination = patrolPoints[patrolIndex].position;
        currentSpeed = patrolSpeed;

        if (Vector2.Distance(transform.position, destination) < 0.2f)
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
    }

    private void Investigate()
    {
        destination = investigatePos;
        currentSpeed = patrolSpeed * 1.5f;

        if (Vector2.Distance(transform.position, investigatePos) < 0.3f)
        {
            investigateTimer += h;

            if (investigateTimer >= investigateTime)
            {
                investigateTimer = 0f;
                currentState = GuardState.Patrolling;
            }
        }
    }

    private void Chase()
    {
        if (target == null ||
            !target.gameObject.activeInHierarchy ||
            !target.IsFree)
        {
            target = null;
            currentState = GuardState.Patrolling;
            return;
        }

        currentSpeed = chaseSpeed;

        float distance = Vector2.Distance(
            transform.position, target.transform.position
        );

        if (distance <= visionRange)
        {
            lastKnownPos = target.transform.position;
            lostTimer = 0f;
        }

        destination = lastKnownPos;

        if (distance <= catchRange)
            CatchTarget();
    }

    private void CatchTarget()
    {
        if (target == null)
            return;

        // Sin celda de retorno no iniciamos una escolta imposible.
        if (target.homeCell == null)
        {
            Debug.LogError(
                $"{target.name}: no tiene celda asignada; no se puede escoltar.",
                target
            );
            return;
        }

        target.Capture();
        currentState = GuardState.Escorting;
        destination = target.homeCell.transform.position;

        Debug.Log($"{name}: capturó a {target.name}.", this);
    }

    private void Escort()
    {
        if (target == null || target.homeCell == null)
        {
            Debug.LogError(
                $"{name}: se interrumpió la escolta porque falta el preso o su celda.",
                this
            );

            target = null;
            currentState = GuardState.Patrolling;
            return;
        }

        Cell cell = target.homeCell;
        destination = cell.transform.position;
        currentSpeed = patrolSpeed;

        // Mover primero al guardia; el preso acompaña su posición.
        Vector3 nextGuardPosition = Vector3.MoveTowards(
            transform.position,
            destination,
            currentSpeed * h
        );

        transform.position = nextGuardPosition;
        target.transform.position =
            nextGuardPosition + new Vector3(0.5f, 0f, 0f);

        if (Vector2.Distance(transform.position, destination) <= 0.3f)
        {
            target.ReturnToCell(cell);

            target = null;
            currentState = GuardState.Patrolling;
            currentSpeed = 0f;
            destination = transform.position;
        }
    }

    private void Move()
    {
        // Escort ya gestiona el movimiento y el acompañamiento.
        if (currentState == GuardState.Escorting)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            currentSpeed * h
        );
    }

    private void BeginInvestigation(Vector3 position)
    {
        currentState = GuardState.Investigating;
        investigatePos = position;
        investigateTimer = 0f;
    }

    private void HearAlarm(Vector3 position)
    {
        if (currentState == GuardState.Chasing ||
            currentState == GuardState.Escorting)
            return;

        if (Vector2.Distance(transform.position, position) > hearingRange)
            return;

        BeginInvestigation(position);
    }

    public void Distract(Vector3 position)
    {
        if (currentState == GuardState.Chasing ||
            currentState == GuardState.Escorting)
            return;

        BeginInvestigation(position);
    }

    private Prisoner FindVisiblePrisoner()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            visionRange,
            LayerMask.GetMask("Prisoners")
        );

        Prisoner nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Prisoner prisoner = hit.GetComponentInParent<Prisoner>();

            if (prisoner == null ||
                !prisoner.gameObject.activeInHierarchy ||
                !prisoner.IsFree)
                continue;

            float distance = Vector2.Distance(
                transform.position, prisoner.transform.position
            );

            if (prisoner.isHidden && distance > hiddenDetectionRange)
                continue;

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = prisoner;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, hiddenDetectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, destination);
    }
}