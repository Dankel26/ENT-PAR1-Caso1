using UnityEngine;

public class Guard : MonoBehaviour
{
    [Header("Guard Settings")]
    public float patrolSpeed = 1f;
    public float chaseSpeed = 2.2f;
    public float visionRange = 4f;           // menor que la del preso: el preso lo ve primero
    public float hiddenDetectionRange = 1f;  // un preso escondido solo se ve a esta distancia
    public float catchRange = 0.4f;
    public float hearingRange = 12f;         // hasta dónde oye la alarma de fuga
    public float investigateTime = 4f;       // segundos que revisa el lugar de una alarma
    public float loseTargetTime = 2f;        // segundos persiguiendo sin ver al preso antes de rendirse

    [Header("Patrol")]
    public Transform[] patrolPoints;

    [Header("Guard States")]
    public GuardState currentState = GuardState.Patrolling;
    public Prisoner target;

    private int patrolIndex = 0;
    private Vector3 destination;
    private Vector3 investigatePos;
    private Vector3 lastKnownPos;
    private float currentSpeed;
    private float investigateTimer = 0f;
    private float lostTimer = 0f;
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
            destination = patrolPoints[0].position;
        }
    }

    public void Simulate(float h)
    {
        this.h = h;
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

    void EvaluateState()
    {
        // Mientras escolta a un preso no se distrae con nada más.
        if (currentState == GuardState.Escorting) return;

        // 1. Si ve a un preso libre -> perseguirlo
        Prisoner seen = FindVisiblePrisoner();
        if (seen != null)
        {
            target = seen;
            lastKnownPos = seen.transform.position;
            lostTimer = 0f;
            currentState = GuardState.Chasing;
            return;
        }

        // 2. Si perseguía y lo perdió de vista -> insistir un rato y luego investigar
        if (currentState == GuardState.Chasing)
        {
            lostTimer += h;
            if (lostTimer >= loseTargetTime)
            {
                target = null;
                BeginInvestigation(lastKnownPos);
            }
        }

        // 3. Investigating y Patrolling se resuelven solos en sus métodos.
    }

    void Patrol()
    {
        currentSpeed = patrolSpeed;

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        destination = patrolPoints[patrolIndex].position;

        if (Vector2.Distance(transform.position, destination) < 0.2f)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    void Investigate()
    {
        currentSpeed = patrolSpeed * 1.5f;
        destination = investigatePos;

        if (Vector2.Distance(transform.position, investigatePos) < 0.3f)
        {
            investigateTimer += h;

            if (investigateTimer >= investigateTime)
            {
                currentState = GuardState.Patrolling;
            }
        }
    }

    void Chase()
    {
        // Si el preso ya no es perseguible (capturado, escapó, volvió a celda), vuelve a patrullar.
        if (target == null || !target.gameObject.activeSelf || !target.IsFree)
        {
            target = null;
            currentState = GuardState.Patrolling;
            return;
        }

        currentSpeed = chaseSpeed;
        destination = lastKnownPos;

        if (Vector2.Distance(transform.position, target.transform.position) <= catchRange)
        {
            CatchTarget();
        }
    }

    void CatchTarget()
    {
        target.Capture();
        currentState = GuardState.Escorting;

        if (target.homeCell != null)
        {
            destination = target.homeCell.transform.position;
        }
    }

    void Escort()
    {
        if (target == null || target.homeCell == null)
        {
            target = null;
            currentState = GuardState.Patrolling;
            return;
        }

        currentSpeed = patrolSpeed;
        destination = target.homeCell.transform.position;

        // El preso va pegado al guardia mientras lo escoltan.
        target.transform.position = transform.position + new Vector3(0.5f, 0f, 0f);

        if (Vector2.Distance(transform.position, destination) < 0.3f)
        {
            Cell cell = target.homeCell;
            target.ReturnToCell(cell);
            cell.Close();

            target = null;
            currentState = GuardState.Patrolling;
        }
    }

    void Move()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            currentSpeed * h
        );
    }

    void BeginInvestigation(Vector3 pos)
    {
        currentState = GuardState.Investigating;
        investigatePos = pos;
        investigateTimer = 0f;
    }

    // Se dispara cuando un preso abre su celda (evento Prisoner.EscapeAlarm).
    void HearAlarm(Vector3 pos)
    {
        if (currentState == GuardState.Chasing || currentState == GuardState.Escorting) return;
        if (Vector2.Distance(transform.position, pos) > hearingRange) return;

        BeginInvestigation(pos);
    }

    // Lo llama un preso que hace ruido para distraerlo.
    public void Distract(Vector3 pos)
    {
        if (currentState == GuardState.Chasing || currentState == GuardState.Escorting) return;

        BeginInvestigation(pos);
    }

    Prisoner FindVisiblePrisoner()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange, LayerMask.GetMask("Prisoners"));
        Prisoner nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            Prisoner p = hit.GetComponent<Prisoner>();
            if (p == null || !p.IsFree) continue;

            float dist = Vector2.Distance(transform.position, p.transform.position);

            // Un preso escondido solo se detecta desde muy cerca.
            if (p.isHidden && dist > hiddenDetectionRange) continue;

            if (dist < minDist)
            {
                minDist = dist;
                nearest = p;
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
