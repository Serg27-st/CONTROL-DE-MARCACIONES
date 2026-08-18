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

C# / **.NET 10 LTS** · ASP.NET Core MVC con vistas Razor · Entity Framework Core · **SQLite**.

El sistema se despliega en **una sola PC** como un `.exe` autocontenido, con la base en un
archivo que la aplicación crea al arrancar. No hay motor que instalar ni servicio que
administrar. Ver `docs/05-despliegue.md`.

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
   corrección vive en `Justificaciones`. Si se cargó el archivo equivocado, la salida es
   **anular la importación** (`ImportacionMarcador.Anulada`): sus filas se conservan y el
   motor deja de contarlas. Es reversible; borrar no lo sería.
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
número.

⚠️ **Nunca fijes el tipo de columna a mano con `HasColumnType`.** SQLite solo tiene
`TEXT`/`INTEGER`/`REAL`/`BLOB`, y ese texto viaja literal al SQL generado: un
`HasColumnType("NVARCHAR(MAX)")` heredado de SQL Server hacía morir la migración con
«near "MAX": syntax error» a mitad de crear las tablas. Deja que EF elija el tipo y expresa
la intención con `HasMaxLength`.

⚠️ **Aritmética de dinero**: SQLite no tiene `DECIMAL` y EF guarda los `decimal` como
`TEXT`, así que **comparar, ordenar o sumar decimales dentro de SQL da resultados
incorrectos**. Hoy no ocurre —los 17 campos decimales se leen y se operan en C#, y los
`Sum()` del exportador son en memoria sobre DTOs ya materializados—. Si algún día hace
falta un `Where`/`OrderBy`/`Sum` sobre un decimal traducido a SQL, guarda el valor en
enteros (céntimos) antes de confiar en el resultado.

**Tiempos**: se guardan en **minutos enteros (`int`)**, nunca en decimales de hora. `7.5 h` es
presentación. Aritmética con `DateOnly`, `TimeOnly`, `TimeSpan`. Zona `America/Lima`.

**Documentos de identidad e IDs de marcador**: **siempre `string`**, nunca numérico — los
ceros a la izquierda se pierden y el cruce falla.

**DTOs**: `record` en `Application`. Los controladores **jamás devuelven entidades**.

**Consultas de solo lectura**: `AsNoTracking()`. Reportes: proyección a DTO, nunca entidades
completas.

**Transacciones explícitas**: envuelve el bloque entero en
`Database.CreateExecutionStrategy().ExecuteAsync(...)` en vez de llamar a
`BeginTransactionAsync` suelto, y reinicia dentro cualquier contador o lista que acumules,
porque el bloque puede reejecutarse completo.

Con SQLite la estrategia por omisión ejecuta una sola vez —no hay red que falle, la base es
un archivo—, así que hoy esa reejecución no ocurre. El patrón se mantiene igual porque es
correcto en ambos casos y porque el día que se vuelva a un motor con reintentos
(`EnableRetryOnFailure`), esa estrategia **se niega a convivir con una transacción abierta a
mano** y todo el código que la use tendría que reescribirse a la vez.

**Interfaz**: todo en español, fecha `dd/MM/yyyy`, hora `HH:mm`, cultura `es-PE` fijada en el
arranque. Assets locales en `wwwroot/`, sin CDN: la planta puede no tener salida a internet.

**Filtro en vivo**: un solo parcial, `Views/Shared/_FiltroTabla.cshtml`, con
`filtro-tabla.js`. Filtra las filas **ya cargadas**, sin viajar al servidor, y es para las
tablas que caben enteras en la respuesta —catálogos, feriados de un año, turnos, tipos de
ausencia, usuarios, periodos—. Los listados que **paginan** usan el filtro del formulario:
ahí sí hay filas que no llegaron.
⚠️ **Solo se dibuja a partir de `FiltroTablaVm.MinimoFilas` (12)**, aproximadamente una
pantalla de tabla. Casi todos estos catálogos arrancan con un puñado de filas —cinco
turnos, nueve tipos de ausencia, un periodo—, y una caja de búsqueda sobre cinco filas es
ruido: ocupa sitio y no ahorra nada. El umbral hace que aparezca sola cuando la tabla deja
de caber de un vistazo, sin que haya que acordarse el día que crezca.
**Marcadores queda fuera a propósito**: ya tiene su propia búsqueda por servidor
(código o descripción), y dos cajas de texto compitiendo en la misma pantalla confunden
más de lo que ayudan.
Es insensible a tildes en ambos sentidos ("produccion" encuentra "Producción"), Escape
limpia, y el contador hereda el sustantivo de la tabla (`Sustantivo`), para no pasar de
«13 feriado(s)» a «2 de 13 registro(s)» al primer tecleo.

**Paginación**: un solo parcial, `Views/Shared/_Paginacion.cshtml`, alimentado por
`PaginacionVm.De(resultado, accion, sustantivo, filtros)`. Lo usan los siete listados
paginados. Cada vista aporta **solo sus filtros**, a través de un `RutaFiltros()` en su
propio ViewModel; la clave `pagina` la escribe el parcial, y la omite en la página 1 para
dejar la URL limpia (las acciones reciben `int pagina` y un 0 ausente ya es la primera).
⚠️ El atributo va entre **comillas simples** — `model='PaginacionVm.De(r, "Index", …)'` —
porque la expresión lleva cadenas entre dobles.
⚠️ El parcial resuelve el controlador con `ViewContext.RouteData`: dejar que
`asp-controller` reciba `null` **no** significa "el actual", el generador cae en los
valores por omisión de la ruta y devuelve `/`.
Antes cada vista traía su propia copia —cinco `RutaPagina` hechas a mano, tres con enlaces
de texto en vez de botones y sin el rango «Mostrando 51–100 de 155»—, y a la de Personal
le faltaba `TipoPlanilla`: pasar de página descartaba ese filtro en silencio.

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

Compila limpio (0 advertencias) y corre, con 318 pruebas en verde. La base es **SQLite** y la
crea la propia aplicación al arrancar: 40 tablas, 64 índices y los 16 catálogos sembrados
(`Persistencia/DatosSemilla.cs`, que es la fuente — las migraciones solo son su consecuencia
generada). Empaquetado y despliegue en `docs/05-despliegue.md`.

**Funciona hoy:**

- **Portada** (`/`): panel con el estado del mes, no un menú. Cuatro tarjetas que responden
  lo que se viene a mirar —días del periodo sin marcaciones cargadas, si el tareo está al
  día, faltas y tardanzas, solicitudes esperando decisión— y cada una lleva a la pantalla
  donde se resuelve, ya filtrada. Antes era una tabla de módulos con una columna «Estado»
  que ponía «Disponible» en todas las filas: para saber si el mes iba bien había que entrar
  a cuatro pantallas.
  Lo arma `IServicioPanelInicio` (`Application/Inicio`), un **orquestador**: no consulta la
  base ni conoce ningún `DbSet`, solo compone lo que ya devuelven `IServicioPeriodos`,
  `CoberturaDeRangoAsync`, `ResumirAsync`, `IServicioSolicitudes` y el
  `EstadoCalculoTareo` de Reportes. Así la portada no puede decir algo distinto de la
  pantalla a la que enlaza.
  El periodo es **el más reciente**, el mismo criterio que usa Tareo al entrar sin elegir
  nada — si divergieran, la portada resumiría un mes y enlazaría a otro.
  ⚠️ Cada bloque **tolera su propio fallo**: si falta un parámetro legal y el estado del
  cálculo revienta, se muestra el resto y ese dato queda como «sin calcular», que es la
  lectura conservadora. Una portada que no carga deja al usuario sin punto de partida.
  Las tarjetas se dibujan según lo que el rol puede **abrir**: una tarjeta que enlaza a un
  403 es peor que no mostrarla. El control real sigue siendo el `[Authorize]` de cada
  controlador.
- **Personal**: listado con filtros, paginación y **búsqueda en vivo** (debounce 300 ms) por
  documento, apellidos, nombres o ID de marcador. Ver y editar abren un **panel flotante
  lateral** sobre la misma pantalla, sin navegar — el alta completa sigue teniendo su propia
  página. Asignación de ID de marcador, turno y **sueldo básico**, los tres con vigencia por
  fecha: cambiarlos cierra la asignación vigente y abre una nueva, nunca la reescribe.
  **"Unidad" es sinónimo de planta** (confirmado por GCA): el campo de texto libre `Unidad`
  ya no se captura y el selector de planta se rotula "Unidad (planta)". La columna sigue en
  la tabla, sin uso, igual que `AreaId`/`SeccionId`.
- **Baja del trabajador**: pantalla propia (`/Trabajadores/Baja/{id}`) que exige **fecha de
  cese y motivo** — renuncia voluntaria, término de contrato, no superó el periodo de prueba,
  abandono laboral, despido o destitución (enum `MotivoCese`, no catálogo: son causales
  laborales, no configuración). Registrarla cierra en esa fecha las asignaciones vigentes de
  marcador, turno y sueldo, así que el ID queda libre para su reemplazo. **Cambiar de empresa
  exige que el trabajador esté cesado**: mover a alguien entre Consulting Advisers y CA
  Outsourcing sin cerrar el vínculo reescribiría a qué razón social pertenecen tareos ya
  pagados. La regla vive en `Trabajador.CambiarEmpresa`, no en la vista.
- **Historial del trabajador** (`/Trabajadores/Historial/{id}`): los cuatro datos con rango de
  fechas en una sola pantalla — vínculos laborales (ingreso/cese/motivo, entidad
  `PeriodoLaboral`), sueldo básico, ID de marcador y turno — más la bitácora de auditoría de
  la ficha si quien mira es coordinador.
- **Una sola asignación abierta por trabajador**, ahora sí en la base: los tres índices
  únicos filtrados sobre `asignacion_marcador`, `asignacion_turno` y `asignacion_sueldo`
  estaban escritos en `ConfiguracionesPersonal` pero **nunca existieron en SQL Server** —
  el filtro decía `[FechaFin]`, el nombre de la propiedad, y la columna se llama
  `fecha_fin`, así que el `CREATE INDEX` fallaba—. Corregido y migrado en
  `IndicesCoberturaCargaYAsignacionAbierta`. ⚠️ Al escribir un `HasFilter`, el SQL va
  literal: usa el nombre de la **columna**, no el de la propiedad. Los tests con InMemory
  no lo detectan, porque ese proveedor ignora los índices.
- **Carga masiva de personal** (`/Trabajadores/CargaMasiva`): admite **Excel (`.xlsx`) o CSV**,
  con plantilla descargable. El formato se decide por el contenido y no por la extensión (un
  `.xlsx` es un ZIP: empieza por `PK`). Se aceptan las dos rutas porque GCA trabaja en Excel:
  exigir "guardar como CSV" antes de cada carga es un paso manual que se olvida y que además
  destroza los documentos con ceros a la izquierda cuando Excel decide que son números —le
  pasó a su propio tareo con 23 carnés de extranjería.
  Reconoce las columnas **por nombre y no por posición** (con sinónimos: `unidad`/`planta`,
  `dni`/`documento`, `nro_doc_identidad`, `puesto_trabajador`, `telefono1`, `mail1`…),
  resuelve los catálogos por código o nombre, y en CSV detecta el delimitador (`;`, `,`, tab)
  y la codificación (UTF-8 o Windows-1252). También traduce los valores que la planilla de
  GCA escribe distinto: `CEX` → tipo `CE`, `OBREROS` → planilla Obrero, y la unidad
  `INTRADEVCO-PLANTA DETERGENTES` se resuelve por el tramo posterior al último guion.
  Si el archivo trae `SITUACION_TRABAJADOR`, las filas que no figuren activas se **rechazan
  con su motivo**: son personal retirado y el archivo no trae fecha de cese con la que darles
  de baja. No reimplementa las reglas del alta: arma la misma solicitud y llama a
  `ServicioTrabajadores.CrearAsync`. Las filas válidas se registran aunque otras fallen, y las
  rechazadas se listan con su número de fila de Excel y el motivo. El botón **"Validar sin
  guardar"** procesa todo dentro de una transacción y la deshace: detecta hasta los documentos
  repetidos dentro del propio archivo.
- **Catálogo de marcadores** (`/Marcadores`): cada ID del ZKTeco se da de alta con la
  **planta** donde está el equipo, y el formulario de Personal solo ofrece los de la planta del
  trabajador. El **código es único en todo el sistema, no por planta**: el TXT del G3 no trae
  ninguna columna que diga de qué planta salió cada marcación, así que dos plantas con el
  código "001" harían esas marcaciones inatribuibles. No se puede renombrar un código ya usado
  (las asignaciones lo referencian por texto) ni dar de baja uno con asignaciones vigentes.
- **Auditoría** (`/Auditoria`, solo `COORDINADOR`): un interceptor de `SaveChanges`
  (`InterceptorAuditoria`) registra toda alta, modificación y baja lógica con su autor, su
  fecha y el valor anterior y nuevo de cada campo. Va en el interceptor y no en cada servicio
  a propósito: si auditar fuera responsabilidad de quien escribe, auditaría solo quien se
  acuerde. **Ley 29733**: documento, dirección, teléfono, correo y los secretos de Identity se
  registran por nombre pero nunca por valor — queda constancia de que el campo cambió, no de
  su contenido. Se excluyen cuatro tablas, listadas y justificadas en `TablasExcluidas`:
  `RegistroAuditoria` (se auditaría a sí misma), `MarcacionCruda` y `ErrorImportacion`
  (inmutables, miles de filas por archivo, y `ImportacionMarcador` ya registra quién cargó
  qué) y `TareoDiario` (dato derivado y reconstruible; lo que sí se audita es la apertura y
  el cierre del periodo, que es la decisión humana detrás del recálculo).
- **Catálogos**: puestos, plantas, áreas, secciones y tipos de documento, con código único por
  padre, contador de uso y baja lógica bloqueada cuando el catálogo está en uso.
  La **portada** (`/Catalogos`) es una rejilla de tarjetas con el conteo de activos e
  inactivos de cada catálogo (`IServicioCatalogos.ConteosAsync`) y avisa arriba de los que
  se quedaron **sin ningún elemento activo** —los que dejan un desplegable vacío en los
  formularios que dependen de ellos—. El **listado** trae un filtro en vivo sobre las filas
  ya cargadas (`filtro-tabla.js`, insensible a tildes: "produccion" encuentra "Producción").
  Es client-side a propósito: estos catálogos caben enteros en la respuesta, a diferencia de
  Personal y Tareo, que paginan y sí necesitan volver al servidor.
  ⚠️ La columna **«En uso»** y la regla que bloquea la baja **no cuentan lo mismo**: la
  columna suma hijos del catálogo más trabajadores, y `CambiarEstadoAsync` solo mira a los
  trabajadores. Por eso el botón de desactivar no se deshabilita según la columna —lo haría
  en casos que el servidor sí permite— y la decisión se deja donde corresponde, en
  `Application`.
- **Turnos**: pantalla propia con horarios, refrigerio, tolerancia, **ventanas de anticipación
  de entrada y salida** (configurables por turno) y plantas habilitadas.
- **Ingesta del TXT**: carga del archivo del ZKTeco con deduplicación, detección de archivo
  repetido por hash y reporte de IDs sin trabajador asignado —con su conteo y su rango de
  fechas—. Cubierta por 14 tests.
- **Pantalla de carga de marcaciones** (`/Marcaciones`): el archivo se suelta sobre una
  zona de arrastre que valida extensión y tamaño antes de subir nada, y debajo hay un
  **calendario de cobertura por año → mes → día**. Responde la pregunta que se hace de
  verdad antes de calcular un tareo —**"¿qué días me faltan?"**—, que el historial de
  cargas no contesta: un archivo puede traer medio mes o tres, y una lista de archivos no
  deja ver un hueco de tres días en mitad del periodo. Cada día muestra sus marcaciones y
  cuántas personas distintas marcaron; los días ya transcurridos sin ninguna se rayan en
  rojo y se cuentan en un indicador, y los futuros se distinguen de los faltantes. El
  calendario cuenta **solo cargas vigentes**, igual que el motor, para que no diga que un
  mes está cubierto por un archivo anulado. El historial va plegado por mes de carga, con
  una columna **Periodo cubierto** (los días que trae dentro) distinta de la fecha de
  subida. El año y el mes viajan en la URL (`?anio=&mes=`) y se conservan al anular,
  reactivar o cargar.
- **Anular una carga de marcaciones** (solo `COORDINADOR`): deja la importación sin efecto
  sobre el cálculo **sin borrar ninguna fila**. Exige motivo, registra quién y cuándo, y es
  reversible. Es la respuesta a "subí el archivo equivocado" en un sistema donde borrar
  marcaciones está prohibido: el tareo deja de contarlas en el siguiente recálculo, pero
  siguen consultables y auditables. ⚠️ Anular **no** recalcula: hay que recalcular el
  periodo después.
- **Actualizar en la carga masiva**: una casilla opcional hace que un documento ya
  registrado corrija esa ficha en vez de rechazarse por duplicado. Va apagada por omisión —
  pisar fichas existentes tiene que ser una decisión consciente— y sirve para volver a subir
  el mismo padrón con un dato que faltaba, como los ceses.

- **Tipos de ausencia**: pantalla propia con comportamiento obligatorio, bloqueado si el
  tipo ya está en uso por solicitudes o justificaciones.

- **Periodos**: rango de fechas, ciclo de apertura y cierre con motivo de reapertura
  obligatorio, y detección de días con marcaciones fuera de todo periodo.
- **Motor de cálculo**: `CalculadoraTareo` puro en `Domain`, con 43 tests. Horas ordinarias
  y extras 25/35, nocturnidad, tardanza, salida anticipada, cruce de medianoche, domingo
  o feriado trabajado, y aplicación de justificaciones sobre el cálculo crudo. Recálculo
  idempotente y bloqueado en periodo cerrado.
- **Pantalla de tareo** (`/Asistencia`): **pagina** (50 filas) y filtra por periodo, estado
  y **búsqueda de trabajador** (apellidos, nombres o documento). Antes volcaba el periodo
  entero en una sola tabla, contra la regla que el propio `ResultadoPaginado` declara
  —"todo listado del sistema pagina, sin excepción"—: con 200 trabajadores son 6 000 filas
  de 14 columnas en un HTML que el equipo de planta no termina de pintar.
  Los totales y el reparto por estado salen de `IServicioCalculoTareo.ResumirAsync`,
  **calculado en la base sobre el periodo completo**, no sumando las filas que llegaron a
  la vista: sumarlas daría "el total de esta página", que cambia al pasar de página.
  El resumen **ignora el estado del filtro** a propósito, y sus pastillas son enlaces que
  filtran, con el conteo de cada estado —incluidos los que están en cero—.
  La **observación va en su propia fila** bajo el día que explica, no en un atributo
  `title`: es el invariante "cada resultado se explica", y un tooltip no se imprime, no se
  copia y no existe en pantalla táctil.
  El listado se ordena por **apellidos** y no por `TrabajadorId`, que para quien lee el
  tareo es orden arbitrario; el nombre se repite solo al cambiar de persona.
- **Sobretasas**: liquidación por concepto con la tasa vigente a la fecha del periodo y su
  base legal. El sistema no calcula dinero — entrega minutos y porcentajes para planilla.
- **Solicitudes de ausencia**: alta, aprobación, rechazo y anulación. Aprobar genera una
  justificación por día cubierto; anular las retira, salvo que ya estén en un periodo
  cerrado. **Topes por tipo de ausencia**, guardados como dato en `TipoAusencia` (no en
  código, para que agregar o ajustar un tope siga siendo un `UPDATE`): descanso médico
  máximo 20 días por año calendario, licencia paternal máximo 10 días por evento, licencia
  por maternidad máximo 98 días por evento. **Vacaciones** se registran con subtipo
  (gozadas, máximo 30 días por periodo vacacional; venta, máximo 15 días) y un periodo
  `AAAA-AAAA`. El **sustento se exige al aprobar, no al registrar**, y solo en los tipos
  que lo marcan con `RequiereSustento` —`DM`, `LPAT`, `LMAT`, `SUBE` y `SUBA` en el
  catálogo sembrado—. Es lo que permite anotar la ausencia el mismo día, para que no se
  calcule como falta mientras llega el certificado de EsSalud.
  El listado lo muestra como **Falta / Registrado / No exigido**; cuando falta **no ofrece
  Aprobar** —sería ofrecer una acción que el servidor ya va a rechazar— sino
  **«Registrar sustento»**, que es lo que desbloquea la fila.
  `SolicitudListaDto.PuedeAprobarse` refleja el mismo criterio que `AprobarAsync`, con
  tests que los mantienen en línea. La comprobación del servidor sigue ahí: ocultar un
  botón no es el control.
  ⚠️ `GuardarSolicitudRequest.Sustento` **no lleva `[Required]`**, por el mismo motivo que
  `SubtipoVacaciones`: solo lo exigen cinco tipos de nueve, y pedirlo en el DTO bloquearía
  las vacaciones y los permisos, que no lo necesitan nunca. Lo llevaba, y eso contradecía
  la regla de `AprobarAsync`: en la práctica obligaba a teclear "pendiente" para poder
  registrar un descanso médico el mismo día — y ese texto es el que hereda la
  justificación como explicación ante una inspección.
  `RegistrarSustentoAsync` agrega el sustento que faltaba. Es **deliberadamente estrecha**:
  no toca trabajador, tipo ni fechas —lo que el aprobador ya evaluó—, solo el documento que
  llegó después, y **solo sobre solicitudes pendientes** (una aprobada ya generó
  justificaciones que heredaron ese texto). Antes no existía, y el mensaje "regístrelo
  antes de aprobar" apuntaba a algo que el sistema no permitía: la única salida era anular
  y volver a crear. Los tipos
  **subsidiados** (`DM`, `SUBE`, `SUBA`) exigen además el **detalle clínico** —
  contingencia, diagnóstico y centro de atención —, que es lo que llena las tres columnas
  homónimas de la hoja `Descanso Medico` del tareo final. El bloque solo aparece en el
  formulario cuando el tipo elegido lo exige, y el diagnóstico recibe el trato de dato
  sensible de salud: ver el bullet de Tareo final.
- **Justificaciones**: consulta con trazabilidad completa (origen, estado anterior, quién y
  cuándo) y alta manual para lo puntual. Una sola justificación por trabajador y día.
- **Reportes**: resumen por trabajador y excepciones, exportables a Excel con ClosedXML.
  El alcance temporal ya no es solo el periodo: **periodo, mes calendario, día suelto o
  rango libre** (tope de 366 días), resuelto por `ResolutorRangoReporte`, puro en `Domain`.
  Las consultas filtran el `TareoDiario` **por fecha y no por `PeriodoId`**, porque un rango
  libre puede cruzar dos periodos. El `PeriodoId` sigue haciendo falta donde el periodo *es*
  el sujeto: cierre, reintegros y tareo final.
  El padrón se acota al alcance con **el mismo solapamiento que aplica el motor**
  (`FechaIngreso <= hasta && (FechaCese == null || FechaCese >= desde)`). Antes filtraba solo
  por `Activo`, y como registrar un cese **no** apaga la ficha —`DarDeBaja` solo escribe
  `FechaCese`—, alguien cesado en marzo salía en el tareo de julio en cero y con la
  observación «CESE», como si hubiera cesado ese mes.
  ⚠️ **Todo reporte declara de qué momento son sus cifras** (`EstadoCalculoTareo`): compara
  el último `TareoDiario.CalculadoEn` del alcance contra lo último que pudo cambiarlo —una
  marcación cargada, una carga anulada o reactivada, una justificación creada o modificada—.
  Si algo es posterior, la pantalla **y el propio Excel** avisan que hay que recalcular. El
  aviso va dentro del archivo porque el Excel se reenvía por correo, donde ya no hay pantalla:
  un tareo desactualizado se ve idéntico a uno al día.
  El resumen y las excepciones **paginan** (50 filas), y la exportación **no**: el Excel
  lleva siempre el alcance completo. Son dos rutas del mismo servicio
  (`ExcepcionesInternoAsync` con página nula) para que el archivo que se envía a planilla
  no pueda dejar de coincidir con lo que se vio en pantalla.
  ⚠️ La fila **TOTAL** del resumen sale de `TotalesResumenTareo`, **calculada sobre el
  alcance completo en el servicio**. Antes se sumaba en la vista recorriendo `Model.Filas`:
  eso solo era correcto mientras la pantalla volcaba todo, y al paginar habría pasado a ser
  «el total de esta página» sin que nada lo delatara, en el reporte que se lleva a planilla.
  Las horas del total se redondean **al final, sobre los minutos sumados**, no sumando el
  redondeo de cada fila.
  El resumen recorta en memoria y las excepciones en SQL, a propósito: el resumen es una
  fila por trabajador y sus totales exigen recorrer el alcance entero de todas formas
  —el bono nocturno se resuelve día por día con la RMV vigente en cada fecha—, mientras
  que las excepciones son un día por fila y llegan a decenas de miles.
- **Tareo final**: el libro que se entrega a planilla. Se emite **solo por periodo** y
  rechaza los alcances por mes, día o rango: sus reintegros se cargan por trabajador y
  periodo, y su encabezado declara el estado de cierre — sobre medio mes saldría incompleto
  con aspecto de completo. Lleva una cuarta hoja, **`Trazabilidad`**, con alcance, filtros,
  quién y cuándo lo generó y el estado del cálculo; va **al final y en hoja aparte** porque
  las tres primeras se pegan en la plantilla de planilla y meter filas arriba de `Tareo`
  correría todo el bloque. Las tres hojas del archivo modelo de GCA —
  `Tareo`, `Descanso Medico` y `Vacaciones-Licencias`— siguen primero y en ese orden.
  ⚠️ La columna **SUSPENSION** (`DiasSuspension`) **nunca se alimenta**: no existe el concepto
  de suspensión en el sistema, así que sale siempre vacía pero se suma en los totales, lo que
  la hace parecer calculada. Estructura documentada en
  `docs/04-formato-tareo-final.md`. El bloque de identificación sigue el formato vigente
  (`RESUMEN FIN DE MES ... - PLANTA ...`): **11 columnas** (A-K), con `CENTRO` =
  `Centro.Codigo` (`CO_AREA`), `CENTRO DE COSTO` = `CentroCosto.Codigo` (`CO_SECC`),
  `DESCRIP. CECO` = `CentroCosto.Descripcion` (`DE_SECC`) y `AREA` = área interna del
  operario. Las tres de CECO salen del catálogo real que entregó GCA
  (`Organizacion/Centro` y `Organizacion/CentroCosto`, 106 centros de costo bajo 5 centros
  SAP), asignado por trabajador en Personal con cascada Empresa → Centro → Centro de costo;
  si el trabajador no tiene uno asignado salen **vacías**, sin aproximarse con Área/Sección
  —eso daría un código que no existe en SAP—. `AREA` **no se deriva del CECO** (un mismo
  CECO aparece con varias áreas en el archivo de GCA): Área sigue siendo campo propio de
  Personal y convive con el CECO. Las columnas `Texto Centro` y `C. Costo` del formato
  anterior ya no existen. **Movilidad** (días asistidos × tarifa vigente) y
  **asignación familiar** (10 % de la RMV vigente si el trabajador tiene hijos, monto fijo
  mensual) ya se calculan y escriben. El bloque de **reintegros** (18 columnas AN-BE) se
  captura a mano en su propia pantalla, por trabajador y periodo — el sistema no los calcula,
  solo les da un lugar y los exporta. Ningún cálculo usa un parámetro legal vencido o
  ausente en silencio: si falta RMV, tasa o sueldo vigente para la fecha, el reporte falla
  con un mensaje accionable en vez de entregar un monto en cero.
- **Normativa**: `TasaSobretasa` (porcentajes sobre horas: extras 25/35, nocturno, dominical,
  feriado) y `ParametroLegal` (montos absolutos: RMV, % de asignación familiar, valor de
  movilidad diaria), ambos con vigencia por fecha. **Bono nocturno** (D.S. 007-2002-TR art. 8:
  el trabajador en franja nocturna no puede ganar menos que la RMV con 35 % de sobretasa)
  calculado día por día — usa el sueldo básico y la RMV vigentes en la fecha de cada jornada,
  no una fecha fija del periodo — y visible en el Resumen mensual, no en la hoja `Tareo` del
  tareo final (esa hoja ya está verificada celda por celda contra el archivo real y no tiene
  columna pensada para soles).
- **Feriados** (`/Feriados`, solo `COORDINADOR`): dos caminos que no se reemplazan.
  **Sincronizar** trae los feriados **nacionales** del año de una fuente pública
  (`date.nager.at`) e inserta los que falten, sin borrar ni pisar uno ya cargado.
  **Alta manual** (fecha, descripción, alcance nacional o por planta, baja lógica) cubre lo
  que ninguna fuente pública sabe: el aniversario de una planta, un feriado regional, o un
  nacional decretado que el proveedor todavía no publicó.
  ⚠️ El alta manual **no existía**, pese a que el controlador, `IServicioFeriados` y el
  estado vacío de la pantalla afirmaban los tres que el catálogo se cargaba "desde otras
  rutas del sistema": `new Feriado` aparecía una sola vez en todo el código, dentro del
  propio sync. Un feriado local de planta no se podía registrar, y sin registrar el motor
  trata ese día como laborable — falta para quien no fue y sin el 100 % para quien sí—,
  aunque `Feriado.PlantaId` y el motor (que consulta `f.PlantaId`) siempre lo soportaron.
  ⚠️ La sincronización compara ahora **solo contra los nacionales**. Comparaba contra todas
  las filas de la fecha, así que en cuanto existieran feriados de planta, un aniversario de
  Planta 01 el 28 de julio habría hecho que se saltara Fiestas Patrias y las otras tres
  plantas se habrían quedado sin el feriado nacional.
  No se admite un feriado de planta sobre una fecha que ya es nacional: ese día no se
  trabaja en ninguna planta y la fila sobraría.
  El cálculo del tareo sigue leyendo siempre la tabla local `Feriado`, nunca la red: sigue
  funcionando aunque la planta esté sin internet ese día.
- **Selects con buscador**: componente propio (`searchable-select.js`, sin dependencias
  externas) aplicado a los combos largos de todo el sistema — envuelve el `<select>` nativo
  sin reemplazarlo, así que el binding de Razor y las cascadas AJAX existentes siguen
  funcionando igual.
- **Autenticación**: ASP.NET Core Identity con cuatro roles — `COORDINADOR` (acceso total,
  único que gestiona usuarios), `ADMINISTRADOR` (gestiona el tareo: personal, catálogos,
  marcaciones, justificaciones, asistencia, periodos), `GERENCIA` (solo dashboard y
  reportes) y `TRABAJADOR_SOCIAL` (solo solicitudes de ausencia). Toda pantalla exige sesión
  iniciada. Sin autorregistro: las cuentas las crea un `COORDINADOR` desde `/Usuarios`, con
  apellidos, documento, razón social y, si el rol es `ADMINISTRADOR`, las plantas que
  gestiona (`UsuarioPlanta` — hoy es un dato capturado, todavía no se usa para filtrar lo
  que ese usuario ve). El primer coordinador se crea al arrancar leyendo credenciales de
  configuración externa, nunca de una migración versionada. Cada controlador exige el rol
  que le corresponde según esa matriz, verificado en el servidor y no solo ocultando el
  enlace del menú.

**Reglas confirmadas por GCA:** turno diurno 07:00–19:00; la jornada se computa **desde la
hora del turno**, así que marcar antes no genera sobretiempo; el domingo trabajado va con
**sobretasa del 100 %**; la franja legal de trabajo nocturno es **22:00–06:00** (D.S.
007-2002-TR art. 8) y el bono nocturno es la diferencia entre la RMV con 35 % de sobretasa y
el sueldo básico, prorrateada por los días que el trabajador tuvo franja nocturna en el mes;
la asignación familiar es un monto fijo mensual del 10 % de la RMV, no prorrateado por días.

**El refrigerio no se descuenta: es tiempo pagado y se computa como hora de labor en los
cinco turnos.** `Turno.MinutosRefrigerio` está en **0** en todo el catálogo. El campo se
conserva y sigue siendo editable porque es una política de GCA, no una ley — el D.S.
007-2002-TR excluye el refrigerio de la jornada *salvo pacto*, y aquí lo hay. Consecuencia:
una jornada de 12 h da **720 minutos efectivos, 480 ordinarios y 240 de sobretiempo** (120
al 25 % y 120 al 35 %), y los turnos de 8 h (`SAB`, `DOM1`) dan la jornada ordinaria
completa en vez de quedarse en 435 minutos. ⚠️ Al rotular el campo, ojo: mide **cuánto se
descuenta**, no cuánto dura el refrigerio.

**No existe todavía:** filtrado de datos por las plantas que gestiona un `ADMINISTRADOR`
(`UsuarioPlanta` guarda el dato, pero Personal/Solicitudes/Reportes no lo aplican como
filtro — nadie lo pidió explícitamente y agregarlo es una fase aparte).
Ver `docs/03-roadmap.md`.

**Reingreso de un trabajador cesado:** `PeriodoLaboral` ya lo soporta como modelo — son
varias filas por trabajador — y `ServicioTrabajadores` abre un vínculo nuevo si el
trabajador no tiene ninguno abierto. Lo que **no** existe todavía es una acción explícita
de "reingresar" en la interfaz, que además tendría que limpiar `FechaCese`/`MotivoCese` de
la ficha. Nadie lo ha pedido; cuando se pida, el modelo no hay que tocarlo.

**Formato del TXT del ZKTeco: confirmado.** Muestra real en
`tests/ControlMarcacion.Infrastructure.Tests/Recursos/`. Delimitado por tabuladores, fecha
`yyyy-MM-dd HH:mm:ss`, ID con espacios a la izquierda. La columna de tipo de marcación viene
siempre en `0`: **el equipo no distingue entrada de salida** y el motor debe inferirlo.

⚠️ Las áreas, secciones y puestos sembrados son **provisionales** hasta que GCA entregue la
estructura real de cada planta; ya se pueden cargar desde el módulo de catálogos. Los RUC de
las dos empresas también son marcadores de posición.

**Formato del tareo final: confirmado**, y **sin brechas de datos abiertas**. Estructura
extraída del archivo modelo y documentada en `docs/04-formato-tareo-final.md`. Las dos
brechas que había están cerradas: el CECO real (GCA entregó `CENTROS DE COSTO POR
EMPRESA.xlsx`, 106 centros de costo bajo 5 centros SAP) y el **detalle clínico del descanso
médico** — ver el bullet siguiente.

- **Detalle clínico**: `SolicitudAusencia` guarda contingencia (`ENFERMEDAD COMUN` /
  `ACCIDENTE LABORAL`), diagnóstico y centro de atención (`ESSALUD` / `PARTICULAR`), y las
  tres son **obligatorias** en los tipos cuyo `Comportamiento` es `Subsidiada` — hoy `DM`,
  `SUBE` y `SUBA`. La condición la decide el comportamiento del catálogo y no una lista de
  códigos, así que un tipo subsidiado nuevo hereda la exigencia sin tocar código
  (`ValidadorDetalleClinico`, puro, en `Domain`). Los dos enums se persisten como texto y
  se exportan con la grafía exacta del archivo de GCA, no con `ToString()`.
  **El diagnóstico es dato sensible de salud (Ley 29733 art. 2.5):** la auditoría lo
  registra por nombre y nunca por valor, el listado de solicitudes muestra contingencia y
  centro de atención pero no el diagnóstico, y cambiar una solicitud a un tipo no
  subsidiado borra los tres campos en vez de dejarlos huérfanos.
  La hoja `Descanso Medico` lista los tres: `DM` sale con observación `DESCANSO MEDICO`, y
  `SUBE` y `SUBA` con `SUBSIDIO`. El formato de GCA la define como el detalle de descansos
  médicos **y subsidios**, y dejar fuera al de accidente —el único cuya contingencia es
  siempre `ACCIDENTE LABORAL`— hacía que ese detalle se capturara para no llegar a ninguna
  parte.

**Nocturnidad por tramo:** el motor la calcula y la persiste en `TareoDiario`
(`MinutosExtra25Nocturnos`/`MinutosExtra35Nocturnos`) como **subconjunto** de los tramos
diurnos — así llena las celdas AK/AL/AM de la hoja `Tareo`, que hoy ya no quedan vacías. Si
GCA trata esos tramos como categorías excluyentes en vez de subconjunto (el archivo modelo
tiene filas donde el 25 % nocturno supera al diurno, lo que lo sugiere), el reparto de esas
tres celdas específicas cambiaría — pero eso es distinto del bono nocturno en soles del
D.S. 007-2002-TR, que ya está resuelto y no depende de esta duda. ⚠️ Confirmar con GCA antes
de dar esas tres columnas por definitivas.

Insumos pendientes, detallados en `docs/01-requerimientos-negocio.md`: definición de
"tipo de planilla", corte del periodo, y el tratamiento del trabajo dominical y del turno
especial de sábado. ✅ **"Unidad" ya está resuelto: es sinónimo de planta.**
