# ERD_MARCACIONES — importación y atribución

```mermaid
erDiagram
    EQUIPO_MARCADOR {
        INTEGER id PK
        INTEGER planta_id FK
        TEXT codigo UK
        TEXT serie "nullable, UK parcial"
    }
    MARCADOR {
        INTEGER id PK
        INTEGER equipo_id FK
        INTEGER planta_id FK
        TEXT codigo
    }
    IMPORTACION_MARCADOR {
        INTEGER id PK
        INTEGER equipo_id FK
        TEXT hash_contenido
        INTEGER anulada
    }
    ERROR_IMPORTACION {
        INTEGER id PK
        INTEGER importacion_id FK
    }
    MARCACION_CRUDA {
        INTEGER id PK
        INTEGER equipo_id FK
        INTEGER importacion_id FK
        TEXT id_marcador
        TEXT fecha_hora
    }
    ASIGNACION_MARCADOR {
        INTEGER id PK
        INTEGER equipo_id FK
        INTEGER trabajador_id FK
        TEXT id_marcador
        TEXT fecha_inicio
        TEXT fecha_fin "nullable"
    }
    TRABAJADOR {
        INTEGER id PK
    }

    PLANTA ||--o{ EQUIPO_MARCADOR : planta_id
    EQUIPO_MARCADOR ||--o{ MARCADOR : equipo_id
    PLANTA ||--o{ MARCADOR : planta_id
    EQUIPO_MARCADOR ||--o{ IMPORTACION_MARCADOR : equipo_id
    IMPORTACION_MARCADOR ||--o{ ERROR_IMPORTACION : importacion_id
    IMPORTACION_MARCADOR ||--o{ MARCACION_CRUDA : importacion_id
    EQUIPO_MARCADOR ||--o{ MARCACION_CRUDA : equipo_id
    EQUIPO_MARCADOR ||--o{ ASIGNACION_MARCADOR : equipo_id
    TRABAJADOR ||--o{ ASIGNACION_MARCADOR : trabajador_id
```

`marcacion_cruda` no tiene `trabajador_id`. La relación con el trabajador no es FK: se resuelve
por `(equipo_id, id_marcador, fecha_hora)` contra la vigencia inclusiva de
`asignacion_marcador`. No existe una tabla puente entre marcas consumidas y `tareo_diario`.

