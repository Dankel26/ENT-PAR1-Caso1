
using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Simulation Settings")]
    [Min(0.02f)]
    public float secondsPerIteration = 0.1f;

    [Min(1)]
    public int maxStepsPerFrame = 5;

    private float timeAccumulator;

    [Header("Map Boundaries")]
    public Collider2D mapBoundsCollider;
    public Bounds mapBounds;

    [Header("Entities")]
    public List<Cell> cells = new List<Cell>();
    public List<Prisoner> prisoners = new List<Prisoner>();
    public List<Guard> guards = new List<Guard>();

    private void Start()
    {
        RefreshMapBounds();
        FindEntities();
        InitializeEntities();
    }

    private void RefreshMapBounds()
    {
        if (mapBoundsCollider != null)
        {
            mapBounds = mapBoundsCollider.bounds;
        }
        else
        {
            Debug.LogError(
                "SimulationManager: asigna Map Bounds Collider en el Inspector.",
                this
            );

            mapBounds = new Bounds(
                Vector3.zero,
                new Vector3(30f, 30f, 0f)
            );
        }
    }

    private void FindEntities()
    {
        cells = new List<Cell>(
            FindObjectsByType<Cell>(FindObjectsSortMode.InstanceID)
        );

        prisoners = new List<Prisoner>(
            FindObjectsByType<Prisoner>(FindObjectsSortMode.InstanceID)
        );

        guards = new List<Guard>(
            FindObjectsByType<Guard>(FindObjectsSortMode.InstanceID)
        );

        Debug.Log(
            $"Simulación inicializada: {cells.Count} celdas, " +
            $"{prisoners.Count} presos y {guards.Count} guardias.",
            this
        );
    }

    private void InitializeEntities()
    {
        foreach (Prisoner prisoner in prisoners)
        {
            if (prisoner == null)
                continue;

            prisoner.InitializeMap(mapBounds);

            if (prisoner.homeCell == null)
            {
                prisoner.homeCell = FindNearestCell(
                    prisoner.transform.position
                );
            }

            if (prisoner.homeCell == null)
            {
                Debug.LogError(
                    $"El preso {prisoner.name} no tiene una celda asignada " +
                    "y no se encontró ninguna celda en la escena.",
                    prisoner
                );
            }
        }
    }

    private void Update()
    {
        float step = Mathf.Max(0.02f, secondsPerIteration);

        timeAccumulator += Time.deltaTime;

        int steps = 0;

        while (timeAccumulator >= step && steps < maxStepsPerFrame)
        {
            SimulateStep(step);
            timeAccumulator -= step;
            steps++;
        }

        // Si hay demasiados pasos pendientes, descartamos el exceso
        // para evitar que la simulación se quede atrapada en un bucle.
        if (steps >= maxStepsPerFrame && timeAccumulator >= step)
        {
            timeAccumulator = Mathf.Min(timeAccumulator, step);
        }
    }

    private void SimulateStep(float step)
    {
        // 1. Celdas
        foreach (Cell cell in cells)
        {
            if (cell != null && cell.gameObject.activeInHierarchy)
            {
                cell.Simulate(step);
            }
        }

        // 2. Presos
        foreach (Prisoner prisoner in prisoners)
        {
            if (prisoner != null &&
                prisoner.gameObject.activeInHierarchy &&
                prisoner.currentState != PrisonerState.Escaped)
            {
                prisoner.Simulate(step);
            }
        }

        // 3. Guardias
        foreach (Guard guard in guards)
        {
            if (guard != null && guard.gameObject.activeInHierarchy)
            {
                guard.Simulate(step);
            }
        }
    }

    private Cell FindNearestCell(Vector3 position)
    {
        Cell nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Cell cell in cells)
        {
            if (cell == null)
                continue;

            float distance = Vector2.Distance(
                position,
                cell.transform.position
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = cell;
            }
        }

        return nearest;
    }

    private void OnDrawGizmos()
    {
        Bounds boundsToDraw = mapBounds;

        if (mapBoundsCollider != null)
        {
            boundsToDraw = mapBoundsCollider.bounds;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            boundsToDraw.center,
            boundsToDraw.size
        );
    }
}