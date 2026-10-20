# D10 — Resolución histórica de turno y rotación

```mermaid
flowchart TD
    WD[Trabajador + fecha] --> AV[AsignacionTurno vigente inclusiva]
    AV -->|ninguna| NONE[Sin turno]
    AV -->|varias anómalas| LATEST[Gana FechaInicio más reciente]
    AV -->|TurnoId| FIX[Turno fijo]
    AV -->|CuadrillaId| PROG[Programación activa y vigente]
    LATEST --> FIX
    LATEST --> PROG
    PROG -->|ninguna| NOP[SinHorario: cuadrilla sin ciclo]
    PROG --> MOD[desfase = fecha - ancla]
    MOD --> CYCLE[módulo positivo por suma de Dias]
    CYCLE --> SEG[TramoRotacion según Orden y Dias]
    SEG --> TID[TurnoId del día]
    FIX --> VER[VersionTurno vigente]
    TID --> VER
    VER -->|existe| HIST[Turno hidratado con horario histórico]
    VER -->|no existe| CUR[Fallback a turno actual]
    HIST --> DAYS[DiasAplicables decide trabajo/descanso]
    CUR --> DAYS
```

No hay un tramo “descanso”. Cada tramo apunta a un turno y el turno decide los días aplicables. El tareo persiste `TurnoId`, pero no `VersionTurnoId` ni `ProgramacionCuadrillaId`.

