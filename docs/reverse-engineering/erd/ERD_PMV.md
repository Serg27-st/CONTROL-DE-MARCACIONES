# ERD_PMV — marcaciones provisionales

```mermaid
erDiagram
    EQUIPO_MARCADOR {
        INTEGER id PK
    }
    PROV_IMPORTACION {
        INTEGER id PK
        INTEGER equipo_id FK
        TEXT hash_contenido
    }
    PROV_MARCACION {
        INTEGER id PK
        INTEGER equipo_id FK
        INTEGER importacion_provisional_id FK
        TEXT id_marcador
        TEXT fecha_hora
    }
    PROV_ERROR_IMPORTACION {
        INTEGER id PK
        INTEGER importacion_provisional_id FK
    }

    EQUIPO_MARCADOR ||--o{ PROV_IMPORTACION : equipo_id
    EQUIPO_MARCADOR ||--o{ PROV_MARCACION : equipo_id
    PROV_IMPORTACION ||--o{ PROV_MARCACION : importacion_provisional_id
    PROV_IMPORTACION ||--o{ PROV_ERROR_IMPORTACION : importacion_provisional_id
```

PMV es un módulo físicamente separado. No tiene FK ni escritura hacia `marcacion_cruda`,
`importacion_marcador`, `tareo_diario` o planilla. La atribución a trabajador se calcula en
consulta mediante equipo + identificador + vigencia de `asignacion_marcador`; no se persiste.

