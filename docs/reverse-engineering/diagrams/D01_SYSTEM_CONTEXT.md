# D01 — Contexto del sistema

```mermaid
flowchart LR
    RRHH[Personal de RR. HH.] -->|mantiene trabajadores, turnos y periodos| S[Control de Marcaciones]
    SUP[Supervisor de asistencia] -->|importa, calcula y valida tareos| S
    ADM[Administrador] -->|usuarios, roles, plantas y catálogos| S
    AUD[Auditor / consulta] -->|consulta reportes y trazabilidad| S

    ZK[Archivos de marcadores ZKTeco] -->|TXT / XLSX| S
    S -->|libros XLSX| EXCEL[Excel / consumidores de reporte]
    S -->|consulta feriados| NAGER[Nager.Date]
    S --> DB[(SQLite)]

    subgraph Limite[Aplicación web ASP.NET Core MVC]
        S
    end
```

Notas:

- La importación es por archivos; no se confirmó integración en línea con los equipos.
- Autenticación, roles y alcance por planta forman parte de la misma aplicación.
- Los reportes son proyecciones derivadas; no existe una entidad persistida `Reporte`.
