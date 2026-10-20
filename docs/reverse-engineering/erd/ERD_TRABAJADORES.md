# ERD_TRABAJADORES — vínculo y organización

```mermaid
erDiagram
    TRABAJADOR {
        INTEGER id PK
        INTEGER empresa_id "proyección actual, sin FK"
        INTEGER planta_id "proyección actual, sin FK"
        TEXT fecha_ingreso "proyección actual"
        TEXT fecha_cese "proyección actual, nullable"
    }
    PERIODO_LABORAL {
        INTEGER id PK
        INTEGER trabajador_id FK
        INTEGER empresa_id FK
        TEXT fecha_ingreso
        TEXT fecha_cese "nullable"
    }
    ASIGNACION_PLANTA {
        INTEGER id PK
        INTEGER periodo_laboral_id FK
        INTEGER planta_id FK
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }
    ASIGNACION_AREA {
        INTEGER id PK
        INTEGER trabajador_id FK
        INTEGER area_id FK
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }
    ASIGNACION_MARCADOR {
        INTEGER id PK
        INTEGER trabajador_id FK
        INTEGER equipo_id FK
        TEXT id_marcador
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }
    ASIGNACION_TURNO {
        INTEGER id PK
        INTEGER trabajador_id FK
        INTEGER turno_id FK "nullable XOR"
        INTEGER cuadrilla_id FK "nullable XOR"
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }
    ASIGNACION_SUELDO {
        INTEGER id PK
        INTEGER trabajador_id FK
        TEXT sueldo_basico_mensual
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }

    TRABAJADOR ||--o{ PERIODO_LABORAL : trabajador_id
    EMPRESA ||--o{ PERIODO_LABORAL : empresa_id
    PERIODO_LABORAL ||--o{ ASIGNACION_PLANTA : periodo_laboral_id
    PLANTA ||--o{ ASIGNACION_PLANTA : planta_id
    TRABAJADOR ||--o{ ASIGNACION_AREA : trabajador_id
    AREA ||--o{ ASIGNACION_AREA : area_id
    TRABAJADOR ||--o{ ASIGNACION_MARCADOR : trabajador_id
    EQUIPO_MARCADOR ||--o{ ASIGNACION_MARCADOR : equipo_id
    TRABAJADOR ||--o{ ASIGNACION_TURNO : trabajador_id
    TURNO o|--o{ ASIGNACION_TURNO : turno_id
    CUADRILLA o|--o{ ASIGNACION_TURNO : cuadrilla_id
    TRABAJADOR ||--o{ ASIGNACION_SUELDO : trabajador_id
```

`PeriodoLaboral` es la autoridad histórica del vínculo y `AsignacionPlanta` la autoridad
histórica de planta dentro del vínculo. Los cuatro campos planos indicados de `trabajador` son
proyección actual/compatibilidad, no historia. `AsignacionTurno` exige físicamente exactamente
una de `turno_id` o `cuadrilla_id`.

