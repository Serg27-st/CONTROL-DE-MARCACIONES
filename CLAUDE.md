# CLAUDE.md — Sistema de Control de Marcación (GCA)

Instrucciones para trabajar en este repositorio. Léelas antes de escribir código.

## Qué es esto

Sistema web que convierte el archivo TXT de un marcador **ZKTeco G3** en un **tareo mensual
auditable**, cruzando las marcaciones con solicitudes de ausencia y justificaciones editables.

Cliente: **Intradevco**, atendido por dos razones sociales de GCA — **Consulting Advisers**
(plantas Detergentes y 20 mil) y **CA Outsourcing** (plantas 01 y Sulfonación).

**La regla que gobierna todo el sistema:**

```
Marcador (dato duro, inmutable)  +  Justificaciones (editable)  =  Tareo final
```

## Stack

C# / **.NET 10 LTS** · ASP.NET Core MVC con vistas Razor · Entity Framework Core · SQL Server.

## Comandos

```bash
dotnet build                                          # compilar la solución
dotnet run --project src/ControlMarcacion.Web         # levantar la aplicación
dotnet test                                           # toda la suite
dotnet test --filter "FullyQualifiedName~Asistencia"  # solo el motor de cálculo
```

Migraciones:

```bash
dotnet ef migrations add NombreDescriptivo --project src/ControlMarcacion.Infrastructure --startup-project src/ControlMarcacion.Web
dotnet ef database update --project src/ControlMarcacion.Infrastructure --startup-project src/ControlMarcacion.Web
```

> Requiere el SDK de .NET 10: `winget install Microsoft.DotNet.SDK.10`.
> La máquina tiene los *runtimes* 8.0.29, que ejecutan pero no compilan.

## Arquitectura: 4 capas, dependencias hacia adentro

```
ControlMarcacion.Web            → Application, Infrastructure, Domain
ControlMarcacion.Infrastructure → Application, Domain
ControlMarcacion.Application    → Domain
ControlMarcacion.Domain         → (nada)
```

**`Domain` no referencia a nadie.** Sin EF Core, sin ASP.NET, sin paquetes de infraestructura.
Es lo que permite probar el motor de cálculo del tareo en un test unitario sin base de datos.
Si necesitas meter `using Microsoft.EntityFrameworkCore` en `Domain`, el diseño está mal.

| Capa | Contiene |
|---|---|
| `Domain` | Entidades POCO, enums, value objects, cálculo puro, **interfaces** de repositorio |
| `Application` | Servicios de caso de uso, DTOs (`record`), validación, orquestación transaccional |
| `Infrastructure` | `DbContext`, Fluent API, repositorios, lector del TXT, exportadores |
| `Web` | Controladores, vistas Razor, Identity, inyección de dependencias |

Módulos (carpeta por módulo dentro de cada capa): `Seguridad` · `Organizacion` · `Personal` ·
`Catalogos` · `Marcaciones` · `Solicitudes` · `Justificaciones` · `Asistencia` · `Reportes`.

Un módulo llama a otro **solo por sus servicios públicos de `Application`**, nunca por su
repositorio ni su `DbSet`.

## Invariantes que no se rompen sin un ADR

1. **Las marcaciones crudas son inmutables.** Se insertan; jamás se editan ni se borran. Toda
   corrección vive en `Justificaciones`.
2. **El tareo es reconstruible e idempotente.** Recalcular un periodo dos veces da idéntico
   resultado, sin duplicar filas.
3. **Periodo cerrado = nada se escribe.** Sin "modo administrador", sin excepciones silenciosas.
   La validación va en `Application`, no en la vista: ocultar el botón no es un control.
4. **Las tasas legales son datos con vigencia por fecha**, nunca constantes. Si escribes
   `* 1.25` en C#, está mal: recalcular un periodo antiguo debe usar las tasas de entonces.
5. **El ID de marcador tiene vigencia por fecha.** Se reasigna entre personas; resolverlo por
   la asignación actual corrompe tareos históricos.
6. **El turno se resuelve por la asignación vigente a la fecha**, nunca por "el último turno
   del trabajador": los obreros rotan.
7. **Nada se borra físicamente.** Baja lógica o fecha de cese.
8. **Cada resultado se explica.** Todo `TareoDiario` guarda una observación legible.

## Convenciones

**Entidades**: POCOs en `Domain`, **sin atributos de EF Core**. Todo el mapeo va en
`IEntityTypeConfiguration<T>` dentro de `Infrastructure/Persistencia/Configuraciones/`.

**Base de datos**: tablas y columnas en **snake_case y español**, tabla en singular
(`trabajador`, `marcacion_cruda`, `tareo_diario`). Enums persistidos **como texto**, no como
número. `NVARCHAR` siempre (hay tildes y ñ). `DATETIME2(0)`, nunca `DATETIME`. `DECIMAL`,
nunca `FLOAT`.

**Tiempos**: se guardan en **minutos enteros (`int`)**, nunca en decimales de hora. `7.5 h` es
presentación. Aritmética con `DateOnly`, `TimeOnly`, `TimeSpan`. Zona `America/Lima`.

**Documentos de identidad e IDs de marcador**: **siempre `string`**, nunca numérico — los
ceros a la izquierda se pierden y el cruce falla.

**DTOs**: `record` en `Application`. Los controladores **jamás devuelven entidades**.

**Consultas de solo lectura**: `AsNoTracking()`. Reportes: proyección a DTO, nunca entidades
completas.

**Interfaz**: todo en español, fecha `dd/MM/yyyy`, hora `HH:mm`, cultura `es-PE` fijada en el
arranque. Assets locales en `wwwroot/`, sin CDN: la planta puede no tener salida a internet.

**Tests**: xUnit + **Shouldly** + NSubstitute. No uses FluentAssertions — desde la v8 exige
licencia paga para uso comercial. Fechas literales (`new DateOnly(2026, 7, 15)`), nunca
`DateTime.Now`.

**Warnings como errores** está activo. En un sistema que calcula planilla, un warning ignorado
hoy es una hora extra mal pagada mañana.

## Seguridad

Datos personales de trabajadores: aplica la **Ley 29733**. Nunca versiones cadenas de conexión
ni credenciales. Nunca registres DNI, dirección, teléfono ni correo en logs. Los archivos TXT
del marcador **no se versionan** (ya están en `.gitignore`); las muestras para tests deben ir
anonimizadas.

## Agentes por rol

Definidos en `.claude/agents/`. Invoca al que corresponda antes de trabajar en su área:

| Agente | Área |
|---|---|
| `arquitecto` | Decisiones transversales, límites entre capas, ADRs |
| `dominio-backend` | Organizacion, Personal, Catalogos: entidades y CRUD |
| `motor-asistencia` | Cálculo del tareo y horas extras. **El módulo más delicado** |
| `solicitudes-justificaciones` | Ausencias, justificaciones, apertura y cierre de periodo |
| `ingesta-marcaciones` | Lectura del TXT del ZKTeco G3 |
| `datos-migraciones` | Esquema SQL Server, migraciones EF, índices, rendimiento |
| `reportes` | Tareo final y exportaciones |
| `frontend-ui` | Vistas Razor |
| `seguridad` | Identity, roles, auditoría, Ley 29733 |
| `qa-pruebas` | Tests y verificación honesta |

## Estado actual

Compila limpio (0 advertencias) y corre. La base `ControlMarcacion` está creada en la instancia
local de SQL Server con las 22 tablas, los índices críticos y los catálogos sembrados.

**Funciona hoy:**

- **Personal**: listado con filtros y paginación, alta y edición, asignación de ID de marcador
  y turno con vigencia por fecha.
- **Catálogos**: puestos, plantas, áreas, secciones y tipos de documento, con código único por
  padre, contador de uso y baja lógica bloqueada cuando el catálogo está en uso.

**No existe todavía:** autenticación (la auditoría registra `"sistema"`), pantallas propias
para turnos/periodos/tipos de ausencia, ingesta del TXT, motor de cálculo, solicitudes,
justificaciones y reportes. Ver `docs/03-roadmap.md`.

**Formato del TXT del ZKTeco: confirmado.** Muestra real en
`tests/ControlMarcacion.Infrastructure.Tests/Recursos/`. Delimitado por tabuladores, fecha
`yyyy-MM-dd HH:mm:ss`, ID con espacios a la izquierda. La columna de tipo de marcación viene
siempre en `0`: **el equipo no distingue entrada de salida** y el motor debe inferirlo.

⚠️ Las áreas, secciones y puestos sembrados son **provisionales** hasta que GCA entregue la
estructura real de cada planta; ya se pueden cargar desde el módulo de catálogos. Los RUC de
las dos empresas también son marcadores de posición.

Insumos pendientes, detallados en `docs/01-requerimientos-negocio.md`: archivo modelo del
tareo, definición de "unidad" y "tipo de planilla", corte del periodo, y el tratamiento del
trabajo dominical y del turno especial de sábado.
