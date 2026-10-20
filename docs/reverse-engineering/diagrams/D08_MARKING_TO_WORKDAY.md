# D08 — De marcación cruda a jornada candidata

```mermaid
flowchart TD
    MC[(marcacion_cruda vigente)] --> AT[Atribución equipo + ID + fecha]
    AM[(asignacion_marcador)] --> AT
    AT -->|sin vigencia| UN[Sin dueño: no alimenta tareo]
    AT -->|trabajador| W[Marcas ordenadas del trabajador]

    AS[(asignacion_turno)] --> RT{Turno fijo o cuadrilla}
    PC[(programacion_cuadrilla + tramos)] --> RT
    RT --> VT[VersionTurno vigente]
    T[(turno actual)] --> VT
    VT --> WIN[Ventana: inicio-anticipación a fin+6h]
    W --> WIN
    PREV[Fin de jornada anterior] --> CUT[Excluir marcas ≤ fin anterior]
    WIN --> CUT
    CUT --> CLAIM[Marcas reclamadas por una sola jornada]
    CLAIM --> RAW[Primera y última marca crudas]
    CLAIM --> BOUNCE[Consolidar rebotes <2 min para contar eventos]
    W --> OUT[Marcas naturales del día no reclamadas]

    RAW --> EC[EntradaCalculo]
    BOUNCE --> EC
    OUT --> EC
    FER[(feriado nacional/planta)] --> EC
    COV[aparatos sin carga] --> EC
    JUS[(justificación aprobada)] --> EC
    LINK[FechaIngreso/FechaCese] --> EC
```

Una jornada nocturna pertenece a su fecha de inicio. La primera/última marca y el margen de seis horas son reglas implementadas, no hechos confirmados por negocio.

