# D14 — Flujo de datos de Marcaciones Provisionales

```mermaid
flowchart LR
    FILE[TXT / attlog.dat / BKtransaction.dat]
    HASH[SHA-256 de bytes completos]
    PREV{Hash ya registrado}
    AUTO[LectorMarcadorAutomatico]
    TXT[LectorTxtZkteco]
    BIN[LectorRespaldoZkteco]
    VALID[Validación y dedupe por equipo + ID + instante]

    subgraph PMV[Persistencia provisional — transacción única]
        PI[(prov_importacion)]
        PM[(prov_marcacion)]
        PE[(prov_error_importacion)]
    end

    ASSIGN[(asignacion_marcador)]
    WORKER[(trabajador)]
    ORG[(planta / area / centro_costo)]
    SHIFT[(asignacion_turno / turno / cuadrilla)]
    DAY[Agrupación equipo + ID + día]
    VIEW[Vista Razor]
    XLSX[Excel: Marcaciones + Trazabilidad]

    FILE --> HASH --> PREV
    PREV -->|sí| REJECT[Rechazo de archivo]
    PREV -->|no| AUTO
    AUTO -->|texto por contenido| TXT
    AUTO -->|respaldo por contenido| BIN
    TXT --> VALID
    BIN --> VALID
    VALID --> PI
    VALID --> PM
    VALID --> PE
    PI --> PM
    PI --> PE

    PM --> DAY
    ASSIGN -->|equipo + ID + vigencia| DAY
    DAY -->|trabajador opcional| WORKER
    WORKER --> ORG
    WORKER --> SHIFT
    ORG --> DAY
    SHIFT --> DAY
    DAY --> VIEW
    DAY --> XLSX

    PMV -. ninguna escritura .-> MAIN[(marcacion_cruda / tareo_diario / periodo)]
```

Precisiones:

- La flecha punteada hacia el sistema principal expresa una frontera verificada: PMV no escribe esas tablas.
- `trabajador` es opcional en el resultado. Una marca sin asignación histórica se conserva y se muestra como “Sin asignar”.
- La atribución del ID es histórica; planta, área y centro de costo se leen de la ficha actual.
- Más de 40 días bloquea el Excel, no la consulta.
