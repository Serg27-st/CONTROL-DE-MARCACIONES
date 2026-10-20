# D02 — Capas y contenedores lógicos

```mermaid
flowchart TB
    U[Usuario / navegador]

    subgraph WEB[Web — MVC / Razor]
        CTRL[Controllers]
        PMVUI[Marcaciones Provisionales]
        VIEW[Vistas Razor]
        JS[JavaScript sin framework]
        AUTH[Identity, autorización y alcance HTTP]
    end

    subgraph APP[Application]
        CONTRACTS[Contratos de casos de uso y consultas]
        ORCH[Orquestadores: trabajadores y panel]
        CALC[Calculadora pura de tareo]
        DTO[DTO y modelos de entrada/salida]
    end

    subgraph INFRA[Infrastructure]
        SERVICES[Servicios de importación, cálculo, reportes y mantenimiento]
        PMVSVC[Servicios PMV: carga, consulta y Excel]
        EF[EF Core / DbContext / configuraciones]
        ID[ASP.NET Core Identity]
        FILES[Lectores y exportadores de archivos]
        AUDIT[Interceptor de auditoría]
    end

    subgraph DOM[Domain]
        ENT[Entidades y agregados]
        RULES[Invariantes temporales y reglas de dominio]
    end

    DB[(SQLite)]
    PMVDB[(Tablas prov_*)]
    EXT[Archivos y Nager.Date]

    U --> VIEW
    VIEW --> CTRL
    JS -->|fetch HTML / JSON| CTRL
    CTRL --> CONTRACTS
    PMVUI --> CONTRACTS
    AUTH --> ID
    ORCH --> CONTRACTS
    SERVICES -. implementan .-> CONTRACTS
    SERVICES --> CALC
    SERVICES --> EF
    SERVICES --> FILES
    PMVSVC -. implementa contratos PMV .-> CONTRACTS
    PMVSVC --> FILES
    PMVSVC --> EF
    ORCH --> ENT
    CALC --> ENT
    EF --> ENT
    EF --> DB
    EF --> PMVDB
    FILES --> EXT
    AUDIT --> EF
```

La dirección entre proyectos es acíclica. No obstante, las responsabilidades de casos de uso se reparten entre `Application` e `Infrastructure`; el diagrama representa la arquitectura observada, no una arquitectura limpia idealizada.
