# Sistema de Control de Marcación — GCA

Sistema web de control de asistencia y cálculo de tareo para **GCA**, cliente **Intradevco**.

Convierte el archivo TXT extraído por USB de un marcador **ZKTeco G3** en un tareo mensual
auditable, calculando horas extras conforme a la normativa laboral peruana.

```
Marcador (dato duro, inmutable)  +  Justificaciones (editable)  =  Tareo final
```

## Alcance

| Empresa | Plantas |
|---|---|
| Consulting Advisers | Detergentes · 20 mil |
| CA Outsourcing | Planta 01 · Sulfonación |

Trabajadores obreros (`OBM`, rotan de turno) y empleados (`EMP`, turno fijo), en cinco turnos
configurables: diurno, nocturno, especial sábado, dominical 1 y dominical 2.

## Requisitos

- **SDK de .NET 10** — `winget install Microsoft.DotNet.SDK.10`
- **SQL Server** 2019 o superior (Express sirve para desarrollo)

## Puesta en marcha

```bash
git clone <url> && cd "CONTROL DE MARCACION"
```

Configura la cadena de conexión sin versionarla:

```bash
dotnet user-secrets set "ConnectionStrings:BaseDatos" "Server=localhost;Database=ControlMarcacion;Trusted_Connection=True;TrustServerCertificate=True" --project src/ControlMarcacion.Web
```

Define las credenciales del **primer administrador** — sin esto no hay forma de entrar,
porque no hay autorregistro. Solo se usan una vez, mientras la tabla de usuarios esté vacía:

```bash
dotnet user-secrets set "Seguridad:AdminCorreo" "admin@tu-dominio.local" --project src/ControlMarcacion.Web
dotnet user-secrets set "Seguridad:AdminClave" "UnaClaveSegura123" --project src/ControlMarcacion.Web
```

Crea la base y levanta la aplicación:

```bash
dotnet ef database update --project src/ControlMarcacion.Infrastructure --startup-project src/ControlMarcacion.Web
dotnet run --project src/ControlMarcacion.Web
```

## Estructura

```
├── src/
│   ├── ControlMarcacion.Domain/          Entidades y cálculo puro. No referencia a nadie.
│   ├── ControlMarcacion.Application/     Servicios de caso de uso y DTOs.
│   ├── ControlMarcacion.Infrastructure/  EF Core, lector del TXT, exportadores.
│   └── ControlMarcacion.Web/             ASP.NET Core MVC, vistas Razor, Identity.
├── tests/
├── docs/                                 Requerimientos, arquitectura, roadmap y ADRs.
└── .claude/agents/                       Agentes especializados por rol.
```

## Módulos

| Módulo | Función |
|---|---|
| Seguridad | Autenticación y roles de operador |
| Organización | Cliente, empresa, planta, área, sección |
| Personal | Ficha del trabajador y su vínculo con el ID de marcador |
| Catálogos | Turnos, horarios, plantas, áreas, secciones, periodos |
| Marcaciones | Carga del TXT del marcador |
| Solicitudes | Vacaciones, descansos médicos, licencias, permisos y subsidios |
| Justificaciones | Corrección auditable de ausencias y tardanzas |
| Asistencia | Motor de cálculo del tareo y horas extras |
| Reportes | Tareo final y exportaciones |

## Documentación

- [Requerimientos de negocio](docs/01-requerimientos-negocio.md) — reglas, módulos y pendientes
- [CLAUDE.md](CLAUDE.md) — convenciones de desarrollo e invariantes del dominio

## Estado

En construcción. Estructura, modelo de dominio y documentación listos; falta la implementación
de persistencia, servicios e interfaz. Ver la sección de pendientes en los requerimientos.
