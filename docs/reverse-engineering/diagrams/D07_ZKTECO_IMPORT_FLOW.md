# D07 — Flujo de importación ZKTeco principal

```mermaid
flowchart TD
    U[Usuario: archivo + planta + equipo] --> VE{Equipo existe, activo y pertenece a planta}
    VE -->|no| REJ[Rechazo]
    VE -->|sí| H[SHA-256 del contenido]
    H --> L[LectorMarcadorAutomatico]
    L -->|texto por contenido| TXT[LectorTxtZkteco]
    L -->|respaldo por contenido| BIN[LectorRespaldoZkteco]
    TXT --> R[Marcas válidas + líneas rechazadas]
    BIN --> R
    R --> S{Serie/nombre coherentes con equipo}
    S -->|no| REJ
    S -->|sí| X{Hash o ≥50 % de marcas en otro equipo}
    X -->|sí| REJ
    X -->|no| D[Dedupe lote por ID+instante]
    D --> A[Detectar IDs sin asignación histórica]
    A --> I[ImportacionMarcador + ErrorImportacion]
    I --> C1[(SaveChanges 1)]
    C1 --> M[MarcacionCruda nuevas]
    M --> C2[(SaveChanges 2)]
    C2 --> OK[Resultado: leídas, insertadas, duplicadas, rechazadas, sin dueño]

    C1 -. fallo antes de C2 .-> GAP[Cabecera/errores pueden quedar sin marcas]
    AN[Anular carga] --> FLAG[Anulada=true; conserva filas]
    FLAG --> REC[Próximo recálculo excluye sus marcas]
    REA[Reactivar] --> REC2[Próximo recálculo vuelve a incluirlas]
```

Notas:

- La base protege `(equipo, ID, instante)`; el hash no es unique físico.
- ID sin dueño no es fila rechazada: la marca queda disponible para una asignación posterior.
- Anular/reactivar no recalcula automáticamente.

