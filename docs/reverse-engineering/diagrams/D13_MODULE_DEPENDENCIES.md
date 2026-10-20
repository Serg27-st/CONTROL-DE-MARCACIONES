# D13 — Dependencias funcionales entre módulos

```mermaid
flowchart LR
    ORG[Organización]
    PER[Personal]
    CAT[Catálogos laborales]
    DEV[Equipos y marcadores]
    SCH[Turnos y cuadrillas]
    IMP[Importaciones]
    RAW[Marcaciones crudas]
    ATT[Tareo e incidencias]
    ABS[Ausencias y justificaciones]
    REI[Reintegros]
    RPT[Reportes]
    SEC[Seguridad y alcance]
    AUD[Auditoría]
    PMV[Marcaciones Provisionales]

    PER --> ORG
    PER --> DEV
    PER --> SCH
    DEV --> ORG
    SCH --> ORG
    IMP --> DEV
    IMP --> RAW
    RAW --> DEV
    ATT --> PER
    ATT --> SCH
    ATT --> RAW
    ATT --> ABS
    ATT --> CAT
    ABS --> PER
    ABS --> CAT
    REI --> PER
    REI --> ATT
    RPT --> PER
    RPT --> ORG
    RPT --> ATT
    RPT --> REI
    RPT --> CAT
    SEC --> ORG
    PMV --> DEV
    PMV --> PER
    PMV --> ORG
    PMV --> SCH

    SEC -. filtra .-> PER
    SEC -. filtra .-> ATT
    SEC -. filtra .-> ABS
    SEC -. filtra .-> RPT
    AUD -. observa cambios EF .-> PER
    AUD -. observa cambios EF .-> ATT
    AUD -. observa cambios EF .-> ABS
    PMV -. no alimenta .-> RAW
    PMV -. no alimenta .-> ATT
```

Los módulos son fronteras funcionales inferidas; no existen como ensamblados independientes. Las dependencias más densas se concentran en tareo y reportes. PMV lee equipos, padrón, organización y turnos, pero su persistencia queda en tablas `prov_*` y no alimenta el motor principal. La autorización por rol existe, pero el filtro de alcance por planta de Seguridad no se aplica a PMV.
