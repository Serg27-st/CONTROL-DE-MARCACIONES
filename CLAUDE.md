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
- **Turnos**: pantalla propia con horarios, refrigerio, tolerancia, **ventanas de anticipación
  de entrada y salida** (configurables por turno) y plantas habilitadas.
- **Ingesta del TXT**: carga del archivo del ZKTeco con deduplicación, detección de archivo
  repetido por hash y reporte de IDs sin trabajador asignado. Cubierta por 14 tests.

- **Tipos de ausencia**: pantalla propia con comportamiento obligatorio, bloqueado si el
  tipo ya está en uso por solicitudes o justificaciones.

- **Periodos**: rango de fechas, ciclo de apertura y cierre con motivo de reapertura
  obligatorio, y detección de días con marcaciones fuera de todo periodo.
- **Motor de cálculo**: `CalculadoraTareo` puro en `Domain`, con 43 tests. Horas ordinarias
  y extras 25/35, nocturnidad, tardanza, salida anticipada, cruce de medianoche, domingo
  o feriado trabajado, y aplicación de justificaciones sobre el cálculo crudo. Recálculo
  idempotente y bloqueado en periodo cerrado.
- **Sobretasas**: liquidación por concepto con la tasa vigente a la fecha del periodo y su
  base legal. El sistema no calcula dinero — entrega minutos y porcentajes para planilla.
- **Solicitudes de ausencia**: alta, aprobación, rechazo y anulación. Aprobar genera una
  justificación por día cubierto; anular las retira, salvo que ya estén en un periodo
  cerrado.
- **Justificaciones**: consulta con trazabilidad completa (origen, estado anterior, quién y
  cuándo) y alta manual para lo puntual. Una sola justificación por trabajador y día.
- **Reportes**: resumen mensual por trabajador y excepciones, exportables a Excel con
  ClosedXML.
- **Tareo final**: el libro que se entrega a planilla, con las tres hojas del archivo modelo
  de GCA — `Tareo`, `Descanso Medico` y `Vacaciones-Licencias`. Estructura verificada contra
  `TAREO NOVIEMBRE 2024_PLANTA 1.xls` y documentada en `docs/04-formato-tareo-final.md`.
  Las columnas de dinero (movilidad, asignación familiar, viáticos, reintegros) y las de
  CECO se emiten **presentes pero vacías**: en el archivo original también las carga
  planilla a mano, y el sistema no calcula remuneración.
- **Autenticación**: ASP.NET Core Identity con los cuatro roles (`ADMINISTRADOR`, `RRHH`,
  `SUPERVISOR`, `CONSULTA`). Toda pantalla exige sesión iniciada. Sin autorregistro: las
  cuentas las crea un administrador. El primer administrador se crea al arrancar leyendo
  credenciales de configuración externa, nunca de una migración versionada. Cerrar/reabrir
  periodo y gestionar usuarios están restringidos a ADMINISTRADOR, verificado en el
  servidor y no solo ocultando el botón.

**Reglas confirmadas por GCA:** turno diurno 07:00–19:00; la jornada se computa **desde la
hora del turno**, así que marcar antes no genera sobretiempo; el domingo trabajado va con
**sobretasa del 100 %**.

**No existe todavía:** matriz fina de permisos por rol (hoy solo ADMINISTRADOR está
diferenciado; RRHH/SUPERVISOR/CONSULTA comparten el mismo acceso salvo lo restringido) y
pantalla de mantenimiento de feriados (la entidad y el cálculo ya la usan, pero nadie
puede cargarlos desde la interfaz). Ver `docs/03-roadmap.md`.

**Formato del TXT del ZKTeco: confirmado.** Muestra real en
`tests/ControlMarcacion.Infrastructure.Tests/Recursos/`. Delimitado por tabuladores, fecha
`yyyy-MM-dd HH:mm:ss`, ID con espacios a la izquierda. La columna de tipo de marcación viene
siempre en `0`: **el equipo no distingue entrada de salida** y el motor debe inferirlo.

⚠️ Las áreas, secciones y puestos sembrados son **provisionales** hasta que GCA entregue la
estructura real de cada planta; ya se pueden cargar desde el módulo de catálogos. Los RUC de
las dos empresas también son marcadores de posición.

**Formato del tareo final: confirmado.** Estructura extraída del archivo modelo y
documentada en `docs/04-formato-tareo-final.md`. Quedan tres brechas de datos, no de
formato:

1. **Nocturnidad por tramo:** ya la calcula el motor y la persiste `TareoDiario`, pero el
   reparto asume que los tramos nocturnos son un **subconjunto** de los diurnos. En el
   archivo modelo hay filas donde el 25 % nocturno supera al diurno, lo que sugiere que GCA
   los trata como excluyentes. ⚠️ Confirmar antes de dar el reporte por bueno.
2. **Detalle clínico del descanso médico:** contingencia, diagnóstico y centro de atención
   no existen en `SolicitudAusencia`; esas tres columnas salen vacías.
3. **CECO:** el centro de costo viene del SAP de Intradevco (168 centros en la hoja `Ceco`
   del archivo modelo). Hoy solo hay Área / Sección / Puesto.

Insumos pendientes, detallados en `docs/01-requerimientos-negocio.md`: definición de
"unidad" y "tipo de planilla", corte del periodo, y el tratamiento del trabajo dominical y
del turno especial de sábado.
