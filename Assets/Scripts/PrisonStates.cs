public enum PrisonerState
{
    InCell,        // esperando dentro de la celda antes de intentar escapar
    OpeningCell,   // forzando la cerradura de su celda
    Escaping,      // caminando hacia la salida más cercana del mapa
    Running,       // corriendo (gasta stamina) porque un guardia está cerca
    Hiding,        // yendo a un escondite o escondido en él
    Distracting,   // haciendo ruido para alejar a un guardia
    Captured,      // atrapado, siendo escoltado por un guardia
    Escaped        // salió del mapa
}

public enum GuardState
{
    Patrolling,    // recorre sus puntos de patrulla
    Investigating, // va a revisar una alarma o un ruido
    Chasing,       // persigue a un preso a la vista
    Escorting      // lleva al preso capturado de vuelta a su celda
}

public enum CellState
{
    Locked,
    Open
}
