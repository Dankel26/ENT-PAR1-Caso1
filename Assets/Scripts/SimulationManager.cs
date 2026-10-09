using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Simulation Settings")]
    public float secondsPerIteration = 1.0f;
    private float time = 0f;

    [Header("Map Boundaries")]
    public Collider2D mapBoundsCollider;
    public Bounds mapBounds;

    [Header("Entities")]
    public List<Cell> cells = new List<Cell>();
    public List<Prisoner> prisoners = new List<Prisoner>();
    public List<Guard> guards = new List<Guard>();

    void Start()
    {
        // 1. Configuración de los límites del mapa para los presos
        if (mapBoundsCollider != null)
        {
            mapBounds = mapBoundsCollider.bounds;
        }
        else
        {
            mapBounds = new Bounds(Vector3.zero, new Vector3(30f, 30f, 0f));
        }

        // 2. Búsqueda de todas las entidades en la escena
        Cell[] foundCells = FindObjectsByType<Cell>(FindObjectsSortMode.InstanceID);
        cells = new List<Cell>(foundCells);

        Prisoner[] foundPrisoners = FindObjectsByType<Prisoner>(FindObjectsSortMode.InstanceID);
        prisoners = new List<Prisoner>(foundPrisoners);

        Guard[] foundGuards = FindObjectsByType<Guard>(FindObjectsSortMode.InstanceID);
        guards = new List<Guard>(foundGuards);

        // 3. Inicialización de referencias requeridas en los agentes
        InitializeEntities();
    }

    void InitializeEntities()
    {
        foreach (Prisoner prisoner in prisoners)
        {
            if (prisoner != null)
            {
                // Asignación de límites para que determine la salida más cercana
                prisoner.map = mapBounds;

                // Si no se asignó una celda manualmente en el Inspector, se asigna la más cercana
                if (prisoner.homeCell == null)
                {
                    prisoner.homeCell = FindNearestCell(prisoner.transform.position);
                }
            }
        }
    }

    void Update()
    {
        time += Time.deltaTime;

        if (time >= secondsPerIteration)
        {
            time = 0f;
            SimulateStep();
        }
    }

    void SimulateStep()
    {
        // 1. Simular Celdas (degradación de forzado de cerradura y temporizador de autocierre)
        foreach (Cell c in cells)
        {
            if (c != null)
            {
                c.Simulate(secondsPerIteration);
            }
        }

        // 2. Simular Presos (espera, forzado de celda, huida, cansancio, distracción y escondites)
        foreach (Prisoner p in prisoners)
        {
            if (p != null && p.gameObject.activeSelf && p.currentState != PrisonerState.Escaped)
            {
                p.Simulate(secondsPerIteration);
            }
        }

        // 3. Simular Guardias (patrulla, investigación de alarmas, persecución y retorno a celda)
        foreach (Guard g in guards)
        {
            if (g != null && g.gameObject.activeSelf)
            {
                g.Simulate(secondsPerIteration);
            }
        }
    }

    Cell FindNearestCell(Vector3 position)
    {
        Cell nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Cell cell in cells)
        {
            float dist = Vector3.Distance(position, cell.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = cell;
            }
        }

        return nearest;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(mapBounds.center, mapBounds.size);
    }
}