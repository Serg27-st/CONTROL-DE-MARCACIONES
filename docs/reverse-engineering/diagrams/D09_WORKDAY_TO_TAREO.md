# D09 — De jornada candidata a `TareoDiario`

```mermaid
flowchart TD
    E[EntradaCalculo] --> LINK{Tiene vínculo}
    LINK -->|no| SV[SinVinculo / cero]
    LINK -->|sí| SHIFT{Tiene turno}
    SHIFT -->|no| SH[SinHorario / cero]
    SHIFT -->|sí| NL{Feriado o día no aplicable}
    NL --> COUNT{Eventos efectivos}

    COUNT -->|0 no laborable| REST[Descanso o Feriado]
    COUNT -->|0 laborable sin cobertura| PEND[PendienteValidacion]
    COUNT -->|0 laborable cubierto| ABS[Falta]
    COUNT -->|1| INC[Incompleto / cero horas]
    COUNT -->|≥2| J[Jornada]

    J --> BASE[inicio=max real/teórico; fin con gracia]
    BASE --> WORK[bruto - refrigerio]
    WORK --> SPLIT[ordinario + 25 % primeras 120 + 35 % resto]
    SPLIT --> NIGHT[solapes 22:00–06:00]
    NIGHT --> DELAY[tardanza + salida anticipada]

    SV --> JUST
    SH --> JUST
    REST --> JUST
    PEND --> JUST
    ABS --> JUST
    INC --> JUST
    DELAY --> JUST[Aplicar justificación aprobada]
    JUST --> ISSUE[Asignar una incidencia por prioridad]
    ISSUE --> VOL[Volcar todos los campos]
    VOL --> UPSERT{Existe trabajador+fecha}
    UPSERT -->|sí| UPD[Actualizar conservando Id]
    UPSERT -->|no| INS[Insertar]
    UPD --> SAVE[(SaveChanges único del recálculo)]
    INS --> SAVE
```

El estado y la incidencia son dimensiones distintas. Un día puntual puede requerir validación por turno discrepante; un incompleto justificado puede dejar de requerirla.

