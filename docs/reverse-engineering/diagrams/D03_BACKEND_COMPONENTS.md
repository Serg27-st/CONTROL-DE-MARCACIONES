# D03 — Componentes del backend

```mermaid
flowchart LR
    subgraph Web
        C1[TrabajadoresController]
        C2[ImportacionesController]
        C3[Tareo e incidencias]
        C4[Solicitudes y justificaciones]
        C5[ReportesController]
        C6[Administración]
        C7[MarcacionesProvisionalesController]
        SCOPE[ProveedorAlcancePlantasHttp]
    end

    subgraph Contratos_Application[Contratos Application]
        WT[IConsultaTrabajadores / ITrabajadorRepository]
        CAT[IConsultaCatalogos]
        IMP[IServicioImportacion]
        TAR[IServicioCalculoTareo]
        AUS[IServicioSolicitudes / IServicioJustificaciones]
        REP[IServicioReportes]
        PER[IServicioPeriodos]
        SEG[IServicioUsuarios / IProveedorAlcancePlantas]
        PMVC[IServicioImportacionProvisional / IServicioConsultaMarcacionProvisional / IExportadorMarcacionesProvisionales]
    end

    subgraph Implementaciones
        ST[ServicioTrabajadores]
        SI[ServicioImportacion]
        SC[ServicioCalculoTareo]
        SS[Servicios de solicitudes y justificaciones]
        SR[ServicioReportes]
        SP[Servicios de periodos, turnos y cuadrillas]
        SU[ServicioUsuarios]
        CM[CargaMasivaTrabajadores]
        PMVS[Servicios de importación, consulta y exportación PMV]
    end

    subgraph Dominio
        P[Personal y organización]
        H[Turnos, versiones y rotaciones]
        M[Marcaciones e importaciones]
        T[Tareo, ausencias y reintegros]
        MP[Importación / marcación / error provisional]
    end

    DB[(MarcacionDbContext)]
    FILES[TXT / DAT / XLSX]

    C1 --> WT
    C1 --> CAT
    C2 --> IMP
    C3 --> TAR
    C4 --> AUS
    C5 --> REP
    C6 --> PER
    C6 --> SEG
    C7 --> PMVC

    ST -. implementa .-> WT
    SI -. implementa .-> IMP
    SC -. implementa .-> TAR
    SS -. implementa .-> AUS
    SR -. implementa .-> REP
    SP -. implementa .-> PER
    SU -. implementa .-> SEG
    SCOPE -. implementa .-> SEG
    PMVS -. implementa .-> PMVC

    CM --> ST
    CM --> WT
    SI --> FILES
    PMVS --> FILES
    PMVS --> MP
    SI --> M
    SC --> H
    SC --> M
    SC --> T
    SR --> P
    SR --> T
    ST --> P
    SP --> H
    SU --> DB
    SCOPE --> DB
    SI --> DB
    SC --> DB
    SS --> DB
    SR --> DB
    SP --> DB
    ST --> DB
    PMVS --> DB
```

Las flechas muestran dependencias funcionales efectivas. En particular, cálculo y reportes atraviesan varios módulos mediante consultas directas al contexto de datos. PMV comparte lectores y `DbContext`, pero persiste solo en `prov_*`; su consulta lee padrón, organización y turnos sin escribirlos.
