# D04 — Navegación y composición del frontend

```mermaid
flowchart TB
    LOGIN[Inicio de sesión] --> LAYOUT[_Layout: navegación filtrada por roles]

    LAYOUT --> HOME[Panel de inicio]
    LAYOUT --> WORKERS[Trabajadores]
    LAYOUT --> MARKS[Importaciones y tareo]
    LAYOUT --> PMV[Marcaciones Provisionales]
    LAYOUT --> ABS[Solicitudes / justificaciones]
    LAYOUT --> REPORTS[Reportes]
    LAYOUT --> ADMIN[Catálogos, periodos, usuarios]

    subgraph Patron[Patrón de interacción]
        PAGE[Vista Razor renderizada en servidor]
        FORM[Formularios HTML + antiforgery]
        SCRIPT[JavaScript vanilla]
        FETCH[fetch a acciones MVC]
        PARTIAL[HTML parcial o JSON]
        PAGE --> FORM
        PAGE --> SCRIPT
        SCRIPT --> FETCH
        FETCH --> PARTIAL
        PARTIAL -->|actualiza fragmento o selector| PAGE
    end

    WORKERS --> PAGE
    MARKS --> PAGE
    PMV --> PAGE
    ABS --> PAGE
    REPORTS --> PAGE
    ADMIN --> PAGE
```

No se observó SPA, almacén de estado cliente ni una capa separada de cliente API. La autorización se aplica en el servidor; la navegación visible por rol es una ayuda de interfaz, no la frontera de seguridad. PMV usa formularios Razor para carga/consulta y una descarga XLSX; el controller exige Coordinador o Administrador.
