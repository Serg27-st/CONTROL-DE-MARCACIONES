# D11 — Solicitud, justificación, regularización y reintegro

```mermaid
flowchart TD
    S[SolicitudAusencia pendiente] --> V[Validar fechas, solape, horas, cupos, clínica]
    V --> DEC{Decisión}
    DEC -->|rechazar| SR[Solicitud rechazada con motivo]
    DEC -->|aprobar| LOOP[Recorrer fechas inclusivas]
    LOOP --> P{Periodo existe y abierto}
    P -->|no| OMIT[Registrar fecha omitida]
    P -->|sí| FREE{Fecha sin justificación vigente}
    FREE -->|no| OMIT
    FREE -->|sí| J[Crear Justificacion aprobada]
    J -->|tipo por horas| PP[PermisoParcial]
    J -->|otro tipo| AU[Ausencia]
    OMIT --> SA[Cabecera queda Aprobada]
    PP --> SA
    AU --> SA

    M[Justificación manual] --> OPEN{Periodo abierto}
    OPEN -->|sí| ONE{Día libre}
    ONE --> JM[Aprobada por defecto]

    R[Regularización con hora verificada] --> STATE{Rol/flujo}
    STATE -->|directo| RA[Aprobada]
    STATE -->|solicitud| RS[Solicitada]
    RS -->|aprobar| RA
    RS -->|rechazar| RR[Rechazada]
    RS -->|retira autor| RC[Cancelada]

    SA --> RECALC[Recalcular periodo manualmente]
    JM --> RECALC
    RA --> RECALC
    RECALC --> TD[(TareoDiario)]

    RT[ReintegroTareo: 18 campos manuales] --> RO{Periodo abierto}
    RO --> UPS[Upsert trabajador+periodo]
    UPS --> XLSX[Se copia al bloque AN:BE]
    TD --> XLSX
```

Solicitud/justificación alteran el resultado diario después de recalcular. Reintegro no altera el tareo: complementa directamente el Excel.

