
using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Cell Settings")]
    public CellState state = CellState.Locked;
    [Min(0.1f)] public float pickTimeRequired = 5f;
    [Min(0f)] public float pickProgress = 0f;
    [Min(0f)] public float autoCloseDelay = 8f;
    [Min(0.1f)] public float occupantRadius = 1.5f;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Color lockedColor = Color.red;
    public Color openColor = Color.green;

    private bool beingPicked;
    private float openTimer;

    public bool IsLocked => state == CellState.Locked;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateVisual();
    }

    public void Simulate(float h)
    {
        if (h <= 0f) return;

        if (state == CellState.Locked)
        {
            if (!beingPicked)
                pickProgress = Mathf.Max(0f, pickProgress - h * 0.5f);
        }
        else
        {
            openTimer += h;

            if (openTimer >= autoCloseDelay && !HasOccupant())
                Close();
        }

        // Se reinicia después de cada tick.
        beingPicked = false;
    }

    public bool PickLock(float amount)
    {
        if (state == CellState.Open)
            return true;

        if (amount <= 0f)
            return false;

        beingPicked = true;
        pickProgress += amount;

        if (pickProgress >= pickTimeRequired)
        {
            Open();
            return true;
        }

        return false;
    }

    public void Open()
    {
        state = CellState.Open;
        pickProgress = 0f;
        openTimer = 0f;
        beingPicked = false;
        UpdateVisual();

        Debug.Log($"Celda {name}: abierta.", this);
    }

    public void Close()
    {
        state = CellState.Locked;
        pickProgress = 0f;
        openTimer = 0f;
        beingPicked = false;
        UpdateVisual();

        Debug.Log($"Celda {name}: cerrada.", this);
    }

    public bool HasOccupant()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            occupantRadius,
            LayerMask.GetMask("Prisoners")
        );

        foreach (Collider2D hit in hits)
        {
            Prisoner prisoner = hit.GetComponentInParent<Prisoner>();

            if (prisoner != null &&
                prisoner.gameObject.activeInHierarchy &&
                prisoner.currentState != PrisonerState.Escaped)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                state == CellState.Locked ? lockedColor : openColor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, occupantRadius);
    }
}