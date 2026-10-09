using UnityEngine;

public class Cell : MonoBehaviour
{
    [Header("Cell Settings")]
    public CellState state = CellState.Locked;
    public float pickTimeRequired = 5f;   // "segundos de trabajo" necesarios para forzar la cerradura
    public float pickProgress = 0f;
    public float autoCloseDelay = 8f;     // una celda abierta y vacía se vuelve a cerrar pasado este tiempo
    public float occupantRadius = 1.5f;   // radio para saber si todavía hay un preso dentro

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Color lockedColor = Color.red;
    public Color openColor = Color.green;

    private bool beingPicked = false;
    private float openTimer = 0f;

    public bool IsLocked
    {
        get { return state == CellState.Locked; }
    }

    private void Start()
    {
        UpdateVisual();
    }

    public void Simulate(float h)
    {
        switch (state)
        {
            case CellState.Locked:
                // Si nadie forzó la cerradura en el último tick, el progreso se va perdiendo.
                if (!beingPicked)
                {
                    pickProgress = Mathf.Max(0f, pickProgress - h * 0.5f);
                }
                break;

            case CellState.Open:
                openTimer += h;
                if (openTimer >= autoCloseDelay && !HasOccupant())
                {
                    Close();
                }
                break;
        }

        beingPicked = false;
    }

    // Lo llama el preso cada tick mientras trabaja la cerradura.
    // Devuelve true cuando la celda quedó abierta.
    public bool PickLock(float amount)
    {
        if (state == CellState.Open) return true;

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
        UpdateVisual();
    }

    public void Close()
    {
        state = CellState.Locked;
        pickProgress = 0f;
        openTimer = 0f;
        UpdateVisual();
    }

    bool HasOccupant()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, occupantRadius, LayerMask.GetMask("Prisoners"));
        return hit != null;
    }

    void UpdateVisual()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = (state == CellState.Locked) ? lockedColor : openColor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, occupantRadius);
    }
}
