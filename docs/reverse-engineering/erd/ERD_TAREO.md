# ERD_TAREO — turnos, rotación y resultados

```mermaid
erDiagram
    TURNO {
        INTEGER id PK
        TEXT codigo UK
        TEXT dias_aplicables
    }
    VERSION_TURNO {
        INTEGER id PK
        INTEGER turno_id FK
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
        TEXT dias_aplicables
    }
    TURNO_PLANTA {
        INTEGER id PK
        INTEGER turno_id FK
        INTEGER planta_id FK
    }
    CUADRILLA {
        INTEGER id PK
        INTEGER planta_id FK
    }
    PROGRAMACION_CUADRILLA {
        INTEGER id PK
        INTEGER cuadrilla_id FK
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
        TEXT fecha_ancla
    }
    TRAMO_ROTACION {
        INTEGER id PK
        INTEGER programacion_id FK
        INTEGER turno_id FK "nullable para Descanso"
        INTEGER orden
        TEXT tipo "Trabajo o Descanso"
        INTEGER dias
    }
    TIPO_AUSENCIA {
        INTEGER id PK
    }
    SOLICITUD_AUSENCIA {
        INTEGER id PK
        INTEGER tipo_ausencia_id FK
        INTEGER trabajador_id "sin FK"
    }
    JUSTIFICACION {
        INTEGER id PK
        INTEGER solicitud_ausencia_id FK "nullable"
        INTEGER trabajador_id "sin FK"
        INTEGER periodo_id "sin FK"
    }
    PERIODO {
        INTEGER id PK
    }
    TAREO_DIARIO {
        INTEGER id PK
        INTEGER periodo_id "sin FK"
        INTEGER trabajador_id "sin FK"
        INTEGER turno_id "sin FK nullable"
        INTEGER justificacion_id "sin FK nullable"
    }
    REINTEGRO_TAREO {
        INTEGER id PK
        INTEGER periodo_id "sin FK"
        INTEGER trabajador_id "sin FK"
    }

    TURNO ||--o{ VERSION_TURNO : turno_id
    TURNO ||--o{ TURNO_PLANTA : turno_id
    PLANTA ||--o{ TURNO_PLANTA : planta_id
    PLANTA ||--o{ CUADRILLA : planta_id
    CUADRILLA ||--o{ PROGRAMACION_CUADRILLA : cuadrilla_id
    PROGRAMACION_CUADRILLA ||--o{ TRAMO_ROTACION : programacion_id
    TURNO o|--o{ TRAMO_ROTACION : turno_id
    TIPO_AUSENCIA ||--o{ SOLICITUD_AUSENCIA : tipo_ausencia_id
    SOLICITUD_AUSENCIA o|--o{ JUSTIFICACION : solicitud_ausencia_id
```

No existen tablas `rotacion`, `version_rotacion`, `jornada` ni `dias_aplicables`. La secuencia
real es finita: `programacion_cuadrilla` contiene tramos ordenados y solo resuelve fechas entre
su ancla y el total materializado. `tramo_rotacion.tipo` usa `Trabajo` o `Descanso`; el segundo
es `DescansoProgramado`, dura un día y exige `turno_id IS NULL`.

