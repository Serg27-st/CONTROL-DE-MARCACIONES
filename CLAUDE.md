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

El paquete de instalación lo arma **`tools/despliegue/publicar.ps1`**: pruebas, `.exe`
autocontenido, prueba de arranque contra una base temporal y ZIP. `instalar.ps1` y la
plantilla de configuración viven a su lado, **versionados**; la salida (`despliegue/`) no se
versiona porque lleva la contraseña del coordinador. ⚠️ Los `.ps1` se escriben con mensajes
**sin tildes**: Windows PowerShell 5.1 lee un `.ps1` sin BOM como ANSI, y una tilde mal leída
dentro de una cadena rompe el script.

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

> Con la aplicación abierta (`dotnet run`), agrega `--configuration Release`: el proceso
> bloquea `bin/Debug` de Web y la compilación falla al copiar las DLL. `Program.cs` ya no
> sale por «hay otra instancia» cuando lo ejecuta la herramienta (`EF.IsDesignTime`).

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
   del trabajador": los obreros rotan. Si la asignación es a una **cuadrilla**, el turno sale
   de la programación de la cuadrilla vigente en esa misma fecha
   ([ADR 0002](docs/adr/0002-rotacion-por-cuadrilla.md)).
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

Compila limpio (0 advertencias) y corre, con 642 pruebas en verde. La base es **SQLite** y la
crea la propia aplicación al arrancar: 45 tablas, 81 índices y los 16 catálogos sembrados
(`Persistencia/DatosSemilla.cs`, que es la fuente — las migraciones solo son su consecuencia
generada). Empaquetado y despliegue en `docs/05-despliegue.md`.

**Funciona hoy:**

- **Portada** (`/`): panel con el estado del mes, no un menú. Cinco tarjetas que responden
  lo que se viene a mirar —días del periodo sin marcaciones cargadas, si el tareo está al
  día, faltas y tardanzas, **días que el sistema no pudo decidir**, solicitudes esperando
  decisión— y cada una lleva a la pantalla donde se resuelve, ya filtrada.
  ⚠️ Las incidencias son una tarjeta **aparte** de las excepciones porque responden otra
  pregunta: las excepciones son días que el sistema resolvió y alguien debe revisar; las
  incidencias son días que **no pudo** resolver, y **no cuentan como falta**. Sin esta
  tarjeta, la portada diría «nada pendiente» sobre un periodo con media planta sin archivo
  cargado (lo fija `Los_dias_pendientes_de_validar_cuentan_como_pendientes`). Antes era una tabla de módulos con una columna «Estado»
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
  documento, apellidos, nombres o ID de marcador. Los filtros de uso diario van a la vista
  —**Trabajador** (documento, apellidos, nombres o número de marcador **exacto**: «2» no trae
  al 23), planta, **aparato**, **N.º de marcador con/sin número** (la lista de pendientes
  para completar el padrón) y situación— y el resto en **«Más filtros»**, plegado salvo que
  alguno esté puesto: empresa, centro, centro de costo, área, puesto, **turno** (con «Sin
  turno asignado», `FiltroTrabajadores.SinTurno` = 0), tipo de planilla, **rango de
  ingreso** y filas por página. Aparato, número y turno miran la asignación **vigente**.
  Área se ofrece solo con planta elegida: el mismo nombre de área existe en varias plantas.
  Todo filtro nuevo va también en `ListadoTrabajadoresVm.RutaFiltros()`, o se pierde al
  pasar de página. Ver y editar abren un **panel flotante
  lateral** sobre la misma pantalla, sin navegar — el alta completa sigue teniendo su propia
  página. Asignación de número de marcador, turno y **sueldo básico**, los tres con vigencia
  por fecha: turno y sueldo cambian cerrando la vigente y abriendo otra, nunca la reescriben.
  **El turno lleva «Turno rige desde»** (opcional, `TurnoVigenteDesde`) para anotar una
  rotación **el día en que ocurrió**: sin fecha, el cambio regía desde mañana y los días entre
  la rotación y su registro se calculaban con el turno viejo — en un obrero que pasa a noche,
  eso fabrica jornadas diurnas que nunca ocurrieron (lo fija
  `Una_rotacion_anotada_con_fecha_pasada_corrige_el_tareo_al_recalcular`). La regla es
  «desde esa fecha, ese turno», y solo mueve la frontera con la asignación inmediatamente
  anterior: una fecha posterior con otro turno es la rotación; igual al inicio de la vigente
  con otro turno, corrección en sitio; anterior, la rotación fue antes (la anterior se
  acorta); posterior con el **mismo** turno, fue después (la anterior se alarga). Se rechaza
  si borraría entera la anterior, si dejaría días sin turno, si es anterior al ingreso o si
  alcanza días de un **periodo cerrado**. Ver `ReasignarTurno`. Vacío, todo sigue igual que
  antes (la carga masiva no la usa).
  Junto al turno va **«Cuadrilla (rota)»**: elegir una cuadrilla es la misma operación —la
  misma línea de tiempo, el mismo «Turno rige desde»— y, si llegan los dos, **manda la
  cuadrilla** y el turno fijo se ignora, porque el formulario envía siempre el turno que la
  persona tenía. La cuadrilla tiene que ser de la planta del trabajador.
  **El número del marcador se asigna con tres campos**: el **aparato** (desplegable de los de
  la planta; con uno solo se preselecciona), el **número en el aparato** (texto: el de la
  lista de usuarios del equipo, con sus ceros) y **«Rige desde»** (opcional). Si el número no
  está en el catálogo de Marcadores, la ficha lo registra al guardar
  (`RegistrarMarcadorSiFalta`, que fija el controlador y no el formulario; la carga masiva no
  lo usa porque registra ella misma con su casilla e informe).
  «Rige desde» vacío: la primera asignación rige desde el ingreso y un cambio, desde mañana.
  Una fecha **igual o anterior** al inicio del número vigente es una **corrección en sitio**
  (el número estaba mal desde el principio; la auditoría guarda el anterior); una **posterior**
  cierra el vigente el día previo y abre el nuevo. No se admite antes del ingreso. La
  ocupación del número se comprueba desde la fecha en que regiría, no desde el ingreso: un
  número que otro dejó al cesar se reutiliza desde el día siguiente. Ver `ReasignarMarcador`.
  ⚠️ Antes era un desplegable de los IDs **ya dados de alta** en el catálogo, agrupado por
  aparato: obligaba a pasar por Marcadores antes de cada alta, y un número mal puesto desde el
  ingreso no tenía corrección (todo cambio cortaba «hoy»).
  **"Unidad" es sinónimo de planta** (confirmado por GCA): el campo de texto libre `Unidad`
  ya no se captura y el selector de planta se rotula "Unidad (planta)". La columna sigue en
  la tabla, sin uso, igual que `SeccionId`.
  ⚠️ **`AreaId` sí se usa**, aunque el formulario tampoco la capture: entra por la carga
  masiva del padrón, filtra el listado, es el respaldo de CENTRO y llena la columna `AREA`
  del tareo final. Y desde `AsignacionArea` tiene **vigencia por fecha** — ver el bullet de
  Reportes.
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
  y la codificación (UTF-8 o Windows-1252).
  Admite una columna **`equipo`** (sinónimos `aparato`, `reloj`, `dispositivo`) que dice de
  qué marcador sale el ID de la fila. Es **opcional**: con un solo aparato en la planta no
  hay nada que preguntar y el sistema lo resuelve; con dos, es `ServicioTrabajadores` el que
  rechaza la fila nombrando los candidatos. ⚠️ A diferencia del área, un valor que **no
  existe sí rechaza la fila**: el área es clasificación, pero el equipo decide a quién se le
  atribuyen las marcaciones.
  **Registra en el catálogo los ID de marcador que falten** (casilla marcada por omisión,
  `AsegurarMarcadorEnCatalogoAsync`): sin acceso físico a los aparatos, el padrón es la
  única fuente de qué ID lleva cada persona, y exigir el alta previa eran cien altas
  manuales por aparato antes del primer archivo. Solo agrega —nunca cambia ni reactiva un
  código—, conserva los ceros a la izquierda y lista en el resultado lo registrado, agrupado
  por aparato, para que un error de tipeo salte a la vista. Si la planta tiene dos aparatos
  y la fila no dice cuál, la fila se rechaza en vez de adivinar. En «Validar sin guardar» la
  transacción deshace también los códigos. ⚠️ Si la fila falla **después** de registrar el
  código (un documento repetido), el código queda en el catálogo, libre: es el que esa
  persona tendrá al corregir la fila. Lo prueba `CargaMasivaRegistroDeMarcadoresTests`, sobre
  **SQLite en memoria**: InMemory no admite la transacción de la carga.
  ⚠️ `ValidarContraCatalogoAsync` tomaba el único candidato **sin mirar el equipo
  declarado**: una fila «14 en M-Z», con solo el 14 de A-L en el catálogo, quedaba asignada
  al de A-L en silencio. Ahora el equipo declarado manda y, si no coincide, se rechaza.
  ⚠️ La fila de ejemplo de la plantilla es una cadena escrita a mano y el encabezado se
  genera del enum `Columna`: agregar una columna la descuadra en silencio y todo lo que va
  después se lee corrido. Hay un test que compara la cantidad de campos de las dos. También traduce los valores que la planilla de
  GCA escribe distinto: `CEX` → tipo `CE`, `OBREROS` → planilla Obrero, y la unidad
  `INTRADEVCO-PLANTA DETERGENTES` se resuelve por el tramo posterior al último guion.
  Si el archivo trae `SITUACION_TRABAJADOR`, las filas que no figuren activas se **rechazan
  con su motivo**: son personal retirado y el archivo no trae fecha de cese con la que darles
  de baja. No reimplementa las reglas del alta: arma la misma solicitud y llama a
  `ServicioTrabajadores.CrearAsync`. Las filas válidas se registran aunque otras fallen, y las
  rechazadas se listan con su número de fila de Excel y el motivo. El botón **"Validar sin
  guardar"** procesa todo dentro de una transacción y la deshace: detecta hasta los documentos
  repetidos dentro del propio archivo.
- **Equipos marcadores** (`/Equipos`): el **aparato ZKTeco físico**, con su planta, su
  código, su nombre y —opcional— su **número de serie**. Es el catálogo que hay que llenar
  primero: sin al menos un equipo no se pueden dar de alta marcadores ni cargar archivos.
  La serie se usa para reconocer de qué aparato salió un archivo (el respaldo binario la
  trae en la cabecera y el attlog en el nombre, `SERIE_attlog.dat`), así que la carga puede
  rechazar un archivo que contradiga el equipo declarado. Es opcional, y normalmente **no
  se teclea**: el equipo la **aprende del primer archivo suyo** que la trae (ver
  «Verificación del origen de la carga»).
  No se puede cambiar de planta un equipo con marcadores registrados —arrastraría a otra
  planta a todo su personal— ni darlo de baja mientras tenga marcadores activos.
- **Catálogo de marcadores** (`/Marcadores`): cada ID del ZKTeco se da de alta contra el
  **equipo** que lo emite; la planta se hereda del equipo y no se captura aparte. La columna
  **«Asignado a»** dice quién tiene hoy cada número (o «Libre»), que es lo que se busca aquí
  antes de asignar uno. La ficha de Personal registra el número si falta, así que esta
  pantalla es sobre todo de consulta.
  ⚠️ **El código es único por equipo, no en todo el sistema.** Lo fue hasta que se modeló
  `EquipoMarcador`, y la razón era buena: el TXT del G3 no trae ninguna columna que diga de
  qué aparato salió cada marcación. Hoy ese dato lo aporta **el operador al cargar**
  —planta y marcador son obligatorios en la pantalla de carga— y el archivo lo confirma por
  su serie. Eso es lo que permite que Planta Detergentes tenga un aparato para apellidos
  A–L y otro para M–Z, ambos numerados del 1 al 100.
  No se puede renombrar un código ya usado (las asignaciones lo referencian por texto) ni
  dar de baja uno con asignaciones vigentes; cambiar un marcador de equipo se bloquea por
  lo mismo que antes se bloqueaba cambiarlo de planta.
  El **aparato se muestra junto al ID en todas partes** donde aparece —tareo, listado y
  panel de Personal, historial del trabajador, reportes, historial de cargas, resultado de
  una carga y el aviso de archivo repetido—: con dos equipos, «14» a secas lo llevan dos
  personas, y el testeo integral lo encontró así en la pantalla de tareo.
  En el **listado de Personal** son dos columnas propias, «ID marcador» y «Aparato», y no
  el nombre en letra pequeña bajo el número: en la columna va el **código** (`DET-AL`), que
  es como GCA nombra el aparato y lo único que cabe en una tabla de trece columnas —el
  nombre completo la partiría en dos líneas y queda en el `title` de la celda y en el panel—.
  Las dos van **junto al nombre**, no al final: identifican a la persona igual que el
  documento, y la tabla mide ~1 430 px, así que su último tercio solo se ve al desplazarse.
  `TrabajadorListaDto` lleva `IdMarcador`, `Equipo` (nombre) y `EquipoCodigo`, los tres de la
  asignación **vigente**; sin número asignado los tres son nulos y la fila muestra «—».
  ⚠️ En los Excel de resumen y excepciones va **en la misma celda** («14 · Planta 01 A-L»,
  `MarcadorConEquipo`) y no en una columna nueva: `EscribirTotales` escribe la fila TOTAL en
  **columnas fijas (8 a 20)**, así que una columna más correría los datos y dejaría cada
  total bajo el título equivocado, sin ningún error, en el archivo que va a planilla.
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
  de entrada y salida** (configurables por turno), **gracia de salida** y plantas habilitadas.
  La **gracia de salida** (`MinutosGraciaSalida`, 0 a 30 min, **0 en los cinco turnos**) es
  el simétrico de la entrada: si la salida cae hasta N minutos después del fin, se computa
  como salida a la hora del turno y no genera sobretiempo. Es inclusiva, y pasada la
  gracia se cuenta **todo** desde el fin (margen, no corrimiento). Las marcas reales se
  conservan y la observación dice cuántos minutos no se computaron.
  ⚠️ **El valor lo decide GCA, no el sistema**: el art. 9 del TUO del D.S. 007-2002-TR
  considera autorizado tácitamente el sobretiempo que se acredita aunque nadie lo ordenara,
  así que la gracia cubre la cola frente al lector y nada más. En 0 el cálculo es idéntico
  al de antes de existir el campo. Migración `GraciaDeSalidaPorTurno`.
- **El horario del turno tiene vigencia por fecha** (`VersionTurno`, migración
  `VigenciaDelHorarioDeTurno`). Era el único dato del sistema que no la tenía: `Turno`
  guardaba un solo horario válido para siempre, así que corregir DIU en octubre y recalcular
  julio —por una justificación tardía, por un archivo que faltaba— recalculaba julio **con el
  horario de octubre**, y las tardanzas y el sobretiempo de un mes ya pagado cambiaban sin que
  nada en ese mes hubiera cambiado. `EstadoCalculoTareo` lo avisaba; nada lo impedía.
  El reparto es **`Turno` = identidad y valores vigentes hoy** (lo que se edita en el
  catálogo) y **`VersionTurno` = el horario que rigió en cada fecha** (lo que usa el cálculo).
  Se eligió así, y no mudando el contenido del turno a otra entidad, porque el reparto limpio
  tocaba 14 archivos de `src/` y 10 de tests incluido `CalculadoraTareo`: el contrato del
  motor no cambió y ninguna prueba existente se tocó.
  `ResolverTurno` **hidrata** el turno con la versión vigente a la fecha (`HorariosPorFecha`,
  una copia por versión que comparte el periodo entero). Es el **tercer nivel de vigencia**
  del recálculo: asignación → programación de cuadrilla → horario.
  ⚠️ `VersionTurno.AplicarA` devuelve **una instancia nueva** y nunca toca la que recibe: el
  recálculo comparte un solo `Turno` entre todos los trabajadores y días, y escribirle encima
  haría que el resultado dependiera del orden en que se recorrió el periodo.
  **«Rige desde»** (opcional, `GuardarTurnoRequest.RigeDesde`) es la frontera: vacío, desde
  hoy; una fecha **igual o anterior** al inicio de la versión vigente la corrige **en sitio**
  (el horario estaba mal desde el principio, y abrir otra dejaría una vigencia de cero días);
  posterior, cierra la vigente el día previo y abre la nueva. Se rechaza si alcanza un
  **periodo cerrado**. Renombrar el turno o cambiarlo de planta **no** parte su historia:
  solo cuentan los campos que el cálculo mira (`MismoHorarioQue`).
  ⚠️ La semilla abre la versión de los cinco turnos en `VersionTurno.VigenciaOriginal`
  (2000-01-01) y la migración rellena los que dio de alta el coordinador con un
  `INSERT … WHERE NOT EXISTS`: el día de la migración **ningún recálculo cambia de
  resultado**. Va también en `DatosSemilla` porque las pruebas crean la base con
  `EnsureCreated`, que no ejecuta migraciones. Un turno **sin** versión cae en los valores de
  hoy, que es como se calculaba antes: conservador y sin regresión.
  ⚠️ `EstadoCalculoTareo` **sigue mirando `Turno.ModificadoEn`** y por eso ahora
  **sobre-avisa**: editar un horario desde hoy marca como desactualizado un julio que ya no
  cambia. Se deja así a propósito —nunca deja de avisar, que es el fallo caro— y afinarlo
  por el solapamiento de la versión es trabajo aparte.
  Lo fijan `VigenciaDelHorarioDeTurnoTests` (SQLite real, por el índice único filtrado de
  una sola versión abierta), con un test que compara el tareo de julio antes y después de
  mover el horario y otro que verifica que `Turno` y su versión abierta no deriven.
- **Cuadrillas** (`/Cuadrillas`, desde Catálogos; [ADR 0002](docs/adr/0002-rotacion-por-cuadrilla.md)):
  el grupo de una planta que **rota de turno a la vez**. Se programa una vez su **ciclo**
  —tramos `(turno, días)`, p. ej. DIU 14 → NOC 14— y el turno de cada día de cada integrante
  sale de ahí: la rotación de la semana que viene ya está decidida sin que nadie la aplique.
  Reemplaza editar ficha por ficha en cada rotación, que era una edición por persona y
  fabricaba jornadas diurnas falsas en quien se olvidaba.
  `ProgramacionCuadrilla` tiene vigencia por fecha y un **ancla** aparte (el día en que
  empieza el primer tramo): `TurnoEn` es `(fecha − ancla) mod ciclo`, puro, en `Domain`,
  con el resto siempre positivo para fechas anteriores al ancla. Los domingos de descanso
  los pone el turno (`DiasAplicables`), no el ciclo.
  Una programación **no se edita, se reemplaza** («desde esta fecha, este ciclo»): la que
  cubría el día previo se cierra y las que empezaban en la fecha o después pasan a **baja
  lógica**. **«Correr la rotación»** es lo mismo con el ciclo vigente y el ancla movida N
  días (−1: rotaron un día antes), y es una sola edición para todo el grupo. Nada de eso
  entra en un periodo cerrado.
  ⚠️ **Correr la rotación un múltiplo del ciclo se rechaza**: mover el ancla 28 días sobre
  un ciclo de 28 la deja donde estaba —`(fecha − ancla) mod ciclo` no cambia— y ni un día
  cambiaba de turno. Se aceptaba: decía «rotación corrida», dejaba una programación más en
  el historial y el tareo salía idéntico. Medio ciclo sí rota (intercambia los tramos) y
  sigue permitido.
  ⚠️ **La baja pasa por la misma regla venga de donde venga.** `CambiarEstadoAsync` (el
  botón del listado) siempre exigió que no quedaran integrantes vigentes, pero la casilla
  «Activa» del formulario de edición llegaba a `GuardarAsync`, que escribía `Activo` sin
  mirar nada: la cuadrilla desaparecía del listado y ya no se podía reprogramar
  —`CuadrillaActivaAsync` la rechaza— mientras su ciclo seguía decidiendo el turno de sus
  integrantes en cada recálculo. Las dos rutas llaman ahora a
  `GarantizarSinIntegrantesVigentesAsync`.
  ⚠️ **`ResolverTurno` mira `ProgramacionCuadrilla.Activo`, nunca `Cuadrilla.Activo`**, y es
  deliberado: dar de baja la cuadrilla hoy no debe cambiar el turno con que se recalcula un
  mes en que sí existía. La vigencia la decide la programación, no el estado del catálogo.
  La pantalla de la cuadrilla muestra turno de hoy, próximas rotaciones, integrantes e
  historial de programaciones, y **suma integrantes de a varios**
  (`ServicioTrabajadores.AsignarCuadrillaAsync`, la misma regla que la ficha, **todo o
  nada**: si uno no puede, no se suma nadie y el aviso dice quién).
  ⚠️ `AsignacionTurno` lleva **`TurnoId` o `CuadrillaId`, exactamente uno** (`CHECK` en la
  base). Quien lea el turno de una asignación tiene que mirar las dos: el listado de
  Personal muestra «Cuadrilla A», el filtro por turno resuelve en memoria qué cuadrillas
  están hoy en ese turno, y dar de baja un turno se bloquea si está en el ciclo vigente de
  alguna cuadrilla. La observación del día empieza por «Cuadrilla A: turno NOC.».
  Migración `RotacionPorCuadrilla`.
- **Ingesta del TXT**: carga del archivo del ZKTeco con deduplicación, detección de archivo
  repetido por hash y aviso de los números sin trabajador asignado —con su conteo y su rango
  de fechas—. Cubierta por 14 tests.
- **La marcación se atribuye al calcular, no al cargar** ([ADR 0001](docs/adr/0001-atribucion-de-marcaciones-al-calcular.md)).
  `MarcacionCruda` guarda solo equipo, número y hora —**no el trabajador**— y **toda** línea
  del archivo se guarda, tenga o no dueño ese día. El motor decide de quién es cada una con
  `AtribucionMarcaciones` (pura, en `Domain`): del que tenía ese número en ese aparato **en
  la fecha de la marcación**. La misma regla usan el aviso de la ingesta y la lista viva de
  **números sin asignar** de la pantalla de Marcaciones (`NumerosSinAsignarAsync`).
  Consecuencia práctica: asignar o corregir un número en la ficha y **recalcular** basta; ya
  no hay que volver a subir archivos. Antes el dueño quedaba grabado al cargar: un número sin
  asignar ese día se descartaba, y uno mal asignado dejaba las marcaciones a nombre de otra
  persona para siempre.
  ⚠️ Doble fichada y deduplicación de la ingesta operan por **(equipo, número)**; el motor
  consulta por el índice único `(equipo, número, instante)`. Si alguien vuelve a resolver el
  dueño al cargar o por la asignación de hoy, fallan `AtribucionAlCalcularTests` y
  `AtribucionMarcacionesTests`.
- **Los dos `.dat` del marcador** (analizado 25/08/2026, `docs/06-formato-archivos-marcador.md`).
  El menú del ZKTeco ofrece juntas dos operaciones que producen `.dat` y no se parecen en
  nada: la **descarga de marcaciones** genera `SERIE_attlog.dat`, que es **el mismo texto
  tabulado del TXT** y `LectorTxtZkteco` procesa sin un solo cambio —verificado sobre 14 332
  marcaciones reales, cero rechazos—; el **respaldo de datos** genera `BKtransaction.dat`,
  un volcado binario de las tablas del equipo, que lee `LectorRespaldoZkteco`.
  `LectorMarcadorAutomatico` es el único `IFuenteDeMarcaciones` registrado y reparte **por
  el contenido, nunca por la extensión ni el nombre**: los dos llegan como `.dat` y un
  archivo renombrado a mano no debe cambiar cómo se interpreta.
  El respaldo **no está cifrado** (entropía 2,85 de 8, con la serie y el firmware legibles
  en claro). Su tiempo **no es epoch Unix**: ZKTeco trata todos los meses como de 31 días.
  ⚠️ **El layout se deduce, no se supone.** Los 7 campos declarados suman 16 bytes y el
  registro mide 28; el relleno no está declarado en ninguna parte, así que el lector prueba
  todos los desplazamientos y se queda con el que da fechas válidas en todos los registros.
  La fecha sola **no basta para desempatar**: leerla tres bytes antes da 2027-02-23, tan
  plausible como la buena, y convierte el ID `2` en `33 554 432`. Por eso se exige además
  que quede **alineada a 4 bytes**. Hay un test que fija ese caso exacto.
  ⚠️ **Dos límites del binario que el texto no tiene**, y por los que el `attlog` sigue
  siendo el archivo preferido: el ID viaja como entero, así que **se pierden los ceros a la
  izquierda** (`0014` y `14` son personas distintas en el padrón real), y de los siete
  campos **solo están confirmados el primero y el último** — los cuatro bytes intermedios se
  leen pero no deciden nada. Un registro con ID en cero se rechaza en vez de atribuirse: en
  la muestra real son los que el equipo escribe para sí mismo.
  **El identificador está confirmado** (GCA, 25/08/2026): en el equipo de prueba `2` y `3`
  son usuarios y `1` es el **master**, la cuenta de administrador del propio marcador. El
  parser extrae exactamente ese conjunto, y el registro del master es además **el único con
  otro modo de verificación** — dos señales independientes sobre el mismo campo.
  ⚠️ **El master no entra al tareo y no hace falta código para lograrlo**: basta con no
  asignarle ese ID a ningún trabajador. El padrón decide, y sus registros se reportan como
  ID sin asignar. Una regla que descartara «el ID 1» sería peor: en otro equipo ese número
  puede ser una persona.
- **Pantalla de carga de marcaciones** (`/Marcaciones`): antes de nada se elige el
  **marcador de origen**, que es obligatorio — el archivo no dice de qué aparato salió, y
  dentro de una planta el mismo código identifica a dos personas distintas. Al elegirlo, la
  **planta se rellena sola y de solo lectura** (`carga-marcaciones.js`): es un dato del
  equipo, no del archivo, así que pedirlo aparte solo permitía construir combinaciones
  inválidas que el servidor rechazaba recién después de subir el archivo. El botón de
  cargar arranca **inhabilitado** hasta que hay marcador.
  ⚠️ Eso es ayuda, no control: el guardia real son el `plantaId <= 0 || equipoId <= 0` del
  controlador y las validaciones del servicio (`ValidarEquipoAsync` y `VerificarOrigenAsync`), que
  un formulario manipulado no puede saltarse.
  El archivo se suelta sobre una
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
  Debajo del calendario, **«Números sin trabajador»** del mes elegido: aparato, número,
  marcaciones, días y rango de los que marcaron sin que nadie tuviera ese número. Es la
  lista de trabajo para completar el padrón y se vacía sola a medida que se asignan (se
  calcula en vivo, con la misma regla que el motor). Las personas del calendario se cuentan
  como números distintos por aparato.
  **La cobertura se mira aparato por aparato** (`CoberturaDeCarga`, pura, en `Domain`). Antes
  un día contaba como cubierto si tenía marcaciones de **cualquier** aparato: en Detergentes,
  cargar el A-L y olvidar el M-Z dejaba el mes «cubierto» en el calendario y en la portada,
  y la mitad de la planta salía en falta al calcular. Ahora un día puede estar **incompleto**
  (marcaciones de unos aparatos y no de otro que se esperaba): franja naranja y «falta 1
  aparato» en la celda, conteo en la pastilla del mes, un indicador propio, un aviso que
  lista los aparatos con días faltantes y, con dos o más aparatos, una **tabla aparato ×
  día** con las personas que marcaron en cada uno (✕ rayada donde falta).
  Un aparato **se espera desde su primera marcación vigente** —el attlog trae toda la
  memoria del equipo, así que antes de eso no se usaba— o, si nunca se cargó nada suyo,
  **desde su alta** (`CreadoEn`): ese es justamente el olvido que hay que mostrar. Uno dado
  de baja no se espera nunca. La portada (`CoberturaDeRango.DiasIncompletos`,
  `AparatosConFaltantes`) usa la misma regla, y `HayHuecos` ahora cuenta también los días
  incompletos. Lo fijan `CoberturaDeCargaTests` y `CoberturaPorAparatoTests` (SQLite real).
  ⚠️ Las celdas de esa tabla van con `position: relative`: el texto `.solo-lectores` es
  `absolute` y, sin ancestro posicionado, escapaba del contenedor con scroll y ensanchaba
  la página entera.
- **Anular una carga de marcaciones** (solo `COORDINADOR`): deja la importación sin efecto
  sobre el cálculo **sin borrar ninguna fila**. Exige motivo, registra quién y cuándo, y es
  reversible. Es la respuesta a "subí el archivo equivocado" en un sistema donde borrar
  marcaciones está prohibido: el tareo deja de contarlas en el siguiente recálculo, pero
  siguen consultables y auditables. ⚠️ Anular **no** recalcula: hay que recalcular el
  periodo después.
- **El mismo archivo no puede quedar vigente en dos aparatos.** La deduplicación es por
  (equipo, número, instante), así que subirlo otra vez declarando otro aparato lo insertaba
  entero: cada marcación contaba dos veces, a nombre de quien tuviera ese número en cada
  equipo. El aviso de archivo repetido lo permitía con una casilla y afirmaba que «no
  duplica nada». Ahora `ImportarAsync` lo rechaza sin casilla que valga, y `ReactivarAsync`
  también, porque reactivar la carga anulada es la otra puerta. La salida es anular la carga
  equivocada y volver a cargar. Repetir en el **mismo** aparato sigue permitido. Lo fija
  `ArchivoCargadoEnOtroEquipoTests`.
- **Verificación del origen de la carga** (roadmap Fase 3, hecho). Todos los ZKTeco numeran
  desde el 1, así que declarar el aparato equivocado no daba ningún error: cada marcación
  encontraba dueño, el equivocado. `ComprobarSerie` existía pero estaba **dormida por
  partida doble**: `LectorTxtZkteco` no entrega serie y ningún equipo tenía la suya. Ahora:
  - **Serie del archivo** (`SerieDelArchivo`): la de la cabecera del respaldo, o la del
    nombre con `^([A-Za-z0-9]{6,20})_attlog`, que tolera los sufijos reales (` PAT`,
    `_prueba`, ` (1)`). Si contenido y nombre traen series distintas, se rechaza.
  - **Se aprende del primer archivo** de cada equipo (`IServicioEquipos.RegistrarSerieAsync`),
    porque no hay acceso físico a los aparatos, **con cruce contra los demás**: una serie
    que ya es de otro equipo rechaza el archivo nombrando a ese equipo. Solo se aprende si
    la carga pasa todo lo demás. Riesgo conocido: si el primer archivo se sube al equipo
    equivocado, se aprende la serie equivocada; el cruce hace que salte en la siguiente
    carga de cualquiera de los dos, y se corrige editando la serie en `/Equipos` y anulando
    esa carga.
  - **Coincidencia con otro aparato**, para los archivos renombrados (3 de 22 en el share):
    si la mayoría de las marcaciones del archivo ya están vigentes en otro equipo con el
    mismo número y al segundo exacto, el archivo es de ese equipo. ⚠️ Es una **proporción**
    (50 %, mínimo 5) y no un conteo: en el cambio de turno, el 14 de A-L y el 14 de M-Z
    coinciden al segundo por azar unas pocas veces al mes. Solo se comparan los días en que
    el otro equipo tiene datos, y solo los últimos 92 días del archivo (el attlog acumula
    historial; más atrás es costo sin ganancia). También al **reactivar** una carga.
  - Cada carga guarda **`SerieArchivo`** y **`OrigenVerificado`** (`PorContenido`,
    `PorNombre`, `SerieAprendida`, `NoVerificado`), visibles en el resultado y en el
    historial. `SerieAprendida` es el cuarto valor que el roadmap no tenía: marca **la carga
    que enseñó la serie**, que es la primera que hay que revisar si luego aparece un choque.
    Las cargas anteriores quedan `NoVerificado` (migración `OrigenVerificadoDeLaCarga`; el
    valor por omisión se fijó a mano, porque el `""` que genera EF no se puede leer como
    enum).
  Lo fijan `OrigenDeLaCargaTests` (sobre SQLite real) y `SerieDelArchivoTests`.
- **Actualizar en la carga masiva**: una casilla opcional hace que un documento ya
  registrado corrija esa ficha en vez de rechazarse por duplicado. Va apagada por omisión —
  pisar fichas existentes tiene que ser una decisión consciente— y sirve para volver a subir
  el mismo padrón con un dato que faltaba, como los ceses.

- **Tipos de ausencia**: pantalla propia con comportamiento obligatorio, bloqueado si el
  tipo ya está en uso por solicitudes o justificaciones.

- **Periodos**: rango de fechas, ciclo de apertura y cierre con motivo de reapertura
  obligatorio, y detección de días con marcaciones fuera de todo periodo.
- **Motor de cálculo**: `CalculadoraTareo` puro en `Domain`, con 50 tests. Horas ordinarias
  y extras 25/35, nocturnidad, tardanza, salida anticipada, cruce de medianoche, domingo
  o feriado trabajado, y aplicación de justificaciones sobre el cálculo crudo. Recálculo
  idempotente y bloqueado en periodo cerrado.
  **La jornada va de la primera marca a la última**: lo que quede en medio —el paso al
  baño, el almuerzo de quien marca por costumbre— se ignora, y queda declarado en la
  observación para que quien audita distinga "se ignoraron a propósito" de "el sistema
  perdió marcas".
  Antes de decidir si el día tiene jornada se **consolidan los rebotes** del lector
  (`VentanaRebote`, 2 min): dos marcas a 40 segundos son una persona entrando, no una
  entrada y una salida. Sin esto, quien marcaba dos veces y **olvidaba marcar la salida**
  obtenía un día con jornada y tardanza en vez de Incompleto, así que no aparecía en el
  filtro donde RRHH busca lo que hay que justificar: una salida olvidada se disfrazaba de
  día trabajado.
  ⚠️ Consolidar **cuenta pasos por el lector, no mueve las horas**: los extremos siguen
  saliendo de la lista en bruto. Tomar la salida de la lista consolidada recortaría hasta
  2 minutos de sobretiempo a quien marca dos veces al salir, y esos minutos se trabajaron.
  Hay un test que fija ese caso exacto.
  ⚠️ Cada marca se compara contra la última **aceptada**, no contra la anterior en bruto:
  si no, una cadena de marcas separadas por un minuto colapsaría una jornada entera.
  ⚠️ La ventana es una **constante y no un campo por turno** a propósito: mide una
  característica física del lector, no una política de GCA. Editable, alguien la subiría a
  media hora para "limpiar" marcas incómodas — y reescribir la jornada es trabajo de
  Justificaciones, donde queda auditado y con autor.
  ⚠️ **Hay dos capas de anti-rebote y conviene no confundirlas.** La ingesta ya traía la
  suya —`ColapsarDobleFichada`, ventana `Marcador:VentanaDeduplicacionSegundos`, **60 s por
  omisión**—, que **no inserta** la segunda marca y la cuenta como duplicada en el resumen
  de la carga. La del motor (`VentanaRebote`, 2 min) actúa después, sobre lo que sí se
  guardó. Las dos comparan contra la última marca **aceptada**, no contra la anterior en
  bruto.
  No son redundantes: la de la ingesta es **configurable** —alguien puede bajarla a 0— y
  deja pasar los pares separados por 60 a 120 s, que son los que recoge la del motor. Pero
  al depurar un día raro hay que mirar las dos: si una marca no está en `marcacion_cruda`,
  la descartó la ingesta y el motor nunca la vio.
- **Pantalla de tareo** (`/Asistencia`): **pagina por trabajador** (10 personas por página,
  `ListarPorTrabajadorAsync`) y filtra por periodo, estado y **búsqueda de trabajador**
  (apellidos, nombres o documento). Antes volcaba el periodo entero en una sola tabla,
  contra la regla que el propio `ResultadoPaginado` declara —"todo listado del sistema
  pagina, sin excepción"—: con 200 trabajadores son 6 000 filas en un HTML que el equipo de
  planta no termina de pintar.
  ⚠️ **La unidad de página es la persona, no la fila.** Paginar por filas partía el mes de
  alguien entre dos páginas —días 1 al 20 en una, 21 al 31 en la siguiente— en una pantalla
  que se llama «detalle por trabajador y día». Es además lo que hace correctos los totales
  por trabajador **sin consultar nada más**: la página trae a cada persona entera, así que
  sumar sus días es sumar todo lo que le corresponde bajo el filtro. Si algún día se
  volviera a paginar por filas, esos totales pasarían a ser «lo que se ve» en silencio.
  Cada persona es un `<tbody>` con **cabecera propia** (nombre, ID de marcador y sus
  faltas/tardanzas/horas extra) y sus días debajo. Las columnas Trabajador e ID salieron
  del cuerpo —viven en esa cabecera—, de 14 columnas a 12. Es **una sola tabla** con
  cabeceras intercaladas y no una tabla por persona, para que las columnas queden alineadas
  entre unos y otros. Con filtro de estado activo, la cabecera rotula lo filtrado
  («12 día(s) con estado Falta»), no un total del mes que no sería cierto.
  Los filtros aplicados se muestran como **pastillas con aspa**, que permiten soltar uno
  sin perder el otro — lo que «Quitar filtros» no dejaba hacer. Los filtros van en una
  **barra de una línea** (`.barra-filtros`) y no en la rejilla de formulario: son tres
  campos cortos, y apilarlos en columnas de 220 px con la etiqueta encima ocupaba media
  pantalla antes del primer dato. **Recalcular** vive en el encabezado, junto al título:
  tenía una tarjeta entera para un solo botón.
  La tabla se dibuja como **rejilla**: separador de celda muy tenue y línea de 2 px en los
  límites de grupo (Jornada / Tiempo computado / Desvíos), que bajan por toda la tabla y
  no solo por la cabecera. Con doce columnas, solo líneas horizontales hacen perder la
  fila a media lectura.
  ⚠️ El padding compacto va con `table.tabla-tareo td`, no con `.tabla-tareo td`: la regla
  general `table.tabla td` lleva un elemento más y gana en especificidad.
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
  cuándo) y alta manual para lo puntual.
  ⚠️ El listado lleva columna **Estado** —«Aplicada», «Esperando autorización», «Rechazada»,
  «Retirada»— con quién y cuándo la resolvió. Sin ella, una regularización **solicitada se
  veía idéntica a una aplicada**: quien la registró no tenía dónde comprobar si se la
  autorizaron, y quien auditaba veía una fila que parecía estar cambiando el día sin
  estarlo. En una regularización se muestran además **las horas verificadas** bajo el tipo:
  la corrección *es* la hora, y «marcación regularizada» a secas no dice qué se corrigió. **Una sola justificación por trabajador y día**, y
  ahora lo garantiza el índice único de `(trabajador_id, fecha)` y no solo la validación en
  `Application`. Era un consultar-e-insertar sin transacción: dos altas simultáneas —o una
  aprobación de solicitud corriendo junto a un alta manual— insertaban las dos, y el
  siguiente recálculo del periodo **entero** moría al indexar las justificaciones por fecha,
  con un error que no decía nada. El alta va dentro de
  `CreateExecutionStrategy().ExecuteAsync(...)` y traduce el rechazo del índice al mismo
  mensaje legible que da la comprobación previa. ⚠️ Migración
  `JustificacionUnicaPorDiaYLicenciaSinGoce`: si la base ya trae duplicados, el
  `CREATE UNIQUE INDEX` falla — la migración lleva la consulta para detectarlos y **no los
  borra a propósito**, porque elegir cuál sobrevive es una decisión con autor.
  ⚠️ **Qué día se paga lo decide el `Comportamiento` del tipo de ausencia, no la casilla
  `ComputaComoAsistencia`** (`JustificacionAplicable.PagaJornadaOrdinaria`): son dos
  condiciones, el día tiene que contar como asistido **y** el tipo tiene que ser remunerado.
  Colgaba solo de la casilla, que es editable desde la pantalla, y `LSG` se sembró con ella
  en verdadero: **la licencia sin goce pagaba ocho horas**. Corregido en el motor y en la
  semilla, para que el catálogo diga lo que el sistema hace.
  ⚠️ **Una justificación sin tipo de ausencia no computa como asistencia ni paga nada.** El
  orquestador resolvía `TipoAusenciaId == null` como «sí computa», así que completar una
  marca impar dejaba el día con la jornada ordinaria plana: en el turno de doce horas eso
  borraba cuatro horas y todo el sobretiempo, y etiquetaba como ausencia un día trabajado.
  Cero no es la respuesta definitiva —lo correcto es registrar la hora que faltó— pero es la
  conservadora: el sistema no sabe hasta qué hora estuvo.
- **Permiso por horas** (`TipoJustificacion.PermisoParcial`): **no es una tardanza**. Se
  registraba como tal, así que un permiso de 15:00 a 17:00 descontaba de un contador que ese
  día valía cero y dejaba intactos los 120 minutos de salida anticipada, con el permiso
  aprobado. Solo funcionaba por casualidad cuando el permiso arrancaba con el turno.
  La ventana viaja con la justificación —sale de la solicitud que la generó— y el motor
  cruza intervalos, sin heurística: descuenta de la tardanza lo que la ventana cubre de
  `[inicio del turno, entrada real]` y de la salida anticipada lo que cubre de
  `[salida real, fin del turno]`. Uno en mitad de la jornada no descuenta nada y la
  observación lo dice. En turno que cruza medianoche la hora se ancla al día que
  corresponde. No se da de alta a mano: sin ventana el motor tendría que suponer.
  ⚠️ **No acredita los minutos del permiso como trabajados**: un permiso con goce de dos
  horas deja el día con la jornada realmente marcada. Si GCA paga esas horas es una regla
  que hay que confirmar antes de escribirla.
- **Bandeja de incidencias** (`/Incidencias`): los días que el sistema **no puede resolver
  solo**. Fecha, trabajador, turno, entrada, salida, qué pasa, qué hacer y el botón que lo
  resuelve. Antes había que salir a `/Justificaciones/Crear` y volver a teclear trabajador
  y fecha, y el único arreglo disponible era declarar el día ausencia justificada —falso en
  un día trabajado, y encima pagando una jornada ordinaria plana—.
  **Quién hace qué**: el `ADMINISTRADOR` registra la regularización, que queda *solicitada*
  y **no modifica el tareo** hasta que se autorice; el `COORDINADOR` aprueba o rechaza, y
  cuando la registra él mismo queda aprobada en un paso —exigirle que se apruebe a sí mismo
  no agrega control y convertiría el cierre en una fila de trámites suyos—. El reparto real
  son los `[Authorize]` y las validaciones de `Application`; lo que la vista oculte es
  ayuda, no control.
  ⚠️ El motor **solo aplica las justificaciones `Aprobada`**: una regularización pedida que
  ya cambiara el día convertiría la aprobación en un trámite decorativo.
  **Desde el tareo se llega a la bandeja**: la fila de observación de un día que requiere
  validación lleva el nombre de la incidencia y un enlace «Resolver» ya filtrado por esa
  persona y ese periodo. Sin eso había que salir a otra pantalla y volver a teclear
  trabajador y fecha, justo cuando uno ya tiene el día delante.
  ⚠️ La comprobación de «una por día» tiene que usar **el mismo criterio que el índice
  filtrado** (ignorar `Rechazada` y `Cancelada`). Lo tenía más estricto y un rechazo
  bloqueaba esa fecha para siempre; lo encontró
  `FlujoDeRegularizacionTests.Una_regularizacion_rechazada_no_aplica_y_libera_el_dia`.
  Una validación en C# más estricta que la garantía de la base es lo peor de los dos
  mundos: el error sale con un mensaje que afirma algo que la base sí permite.
- **Un día sin archivo cargado ya no es falta.** Era el defecto más caro del sistema: en
  Detergentes, cargar el aparato A-L y olvidar el M-Z sacaba **una falta por cada persona
  del aparato olvidado**, indistinguible de una falta real en el Excel que va a planilla, y
  con una observación que afirmaba lo que el sistema no sabía («no registró ninguna
  marcación»). Ahora el día queda `PendienteValidacion` con incidencia
  `SinCoberturaDeCarga`. La regla de cobertura es **una sola** en todo el sistema
  (`IServicioImportacion.AparatosSinCargaAsync`, la misma que pinta el calendario y la
  portada): si el motor tuviera la suya, el calendario podría decir «mes cubierto» mientras
  el tareo saca faltas de los días que faltan. La salida normal es **cargar el archivo y
  recalcular**, que resuelve la incidencia sola; regularizar es para cuando el archivo no
  existe. Lo fija `FaltaMasivaPorArchivoSinCargarTests`, contra el servicio de importación
  **real** y no un doble — con un doble la prueba pasaría aunque cada módulo contara los
  días a su manera.
- **Las marcas fuera de la ventana del turno se declaran**, con su hora, en la observación
  del día. Se descartaban en silencio: una marca a las 05:30 con turno de 08:00 no aparecía
  en ningún lado salvo en `marcacion_cruda`, que nadie consulta a mano. Solo generan
  incidencia (`FueraDeTurno`) si el día salió falta o incompleto —donde podrían haber
  cambiado el resultado—; con la jornada ya resuelta se declaran sin mandar a nadie a
  revisar un día correcto, porque una bandeja con ruido no se revisa.
- **`DetectorTurnoDiscrepante` ya no es código muerto.** Existía, tenía tests y **ningún
  archivo de `src/` lo llamaba**: una noche calculada contra el turno diurno daba tardanza
  enorme o sobretiempo inventado y nada lo señalaba. Ahora el motor lo consulta con los
  turnos habilitados en la planta y marca el día `TurnoDiscrepante`. **No cambia ni un
  minuto del cálculo**: corregir la rotación es una decisión con autor, y si el motor
  reescribiera el turno el tareo dejaría de ser reconstruible desde el padrón.
- **Una sola incidencia por día**, por orden de gravedad: sin cobertura → fuera de turno →
  ingreso/salida sin registrar → turno discrepante. Acumular varias llenaría la bandeja de
  filas repetidas del mismo día y ninguna diría por dónde empezar. Un día ya justificado no
  genera incidencia: alguien ya lo resolvió.
- **Regularizar completa la jornada, no la sustituye.** Si el marcador registró la salida y
  se regulariza la entrada, la salida que entra al cálculo sigue siendo la del aparato. El
  día pasa por `Jornada`, el mismo camino que cualquier otro —no hay una segunda aritmética
  para los días regularizados—, así que conserva su tardanza, su sobretiempo y su
  nocturnidad reales. La hora verificada **no perdona la tardanza**: si entró tarde, entró
  tarde; perdonarla es otra decisión, con otro rastro. Una regularización que no alcanza
  para armar una jornada deja el día como estaba y lo dice, en vez de inventar el otro
  extremo. Se valida que la hora caiga en el día que corrige o el siguiente (un turno
  nocturno cierra al día siguiente): sin tope, un error de tipeo produce una jornada de
  meses.
- **Registro de eventos** (`ILogger<T>` con `[LoggerMessage]`): recálculo del periodo —con
  cuántos días quedaron pendientes de validar, que es el número a mirar antes de cerrar el
  mes— y alta y resolución de regularizaciones. ⚠️ Ley 29733: solo identificadores y
  conteos, **nunca** documento, dirección, teléfono, correo ni diagnóstico. Se generan con
  `[LoggerMessage]` y no con `LogInformation` directo porque el analizador lo exige
  (CA1848/CA1873) y porque evita armar el mensaje con el nivel apagado.
- **Incidencias y regularizaciones: el modelo** (migración `IncidenciasYRegularizaciones`).
  - **`TipoIncidencia`** dice *qué hay que resolver*, aparte del estado, que dice *cómo
    quedó el día*. Son preguntas distintas: un `Incompleto` puede necesitar la entrada o la
    salida —lo que se regulariza es otro— y un día `Puntual` puede necesitar revisión igual
    si sus marcas contradicen el turno. Cinco valores, todos **producibles por el motor**:
    `IngresoNoRegistrado`, `SalidaNoRegistrada`, `FueraDeTurno`, `TurnoDiscrepante` y
    `SinCoberturaDeCarga`. ⚠️ Quedaron fuera a propósito «ambas marcas olvidadas» (nada la
    delata: es un motivo de regularización, no algo detectable), «marcación de trabajador
    inactivo» (no hay día de tareo donde ponerla; vive en «números sin trabajador») y
    «doble marcación» (el motor ya consolida rebotes y lo declara; como incidencia llenaría
    la bandeja de días correctos). Un enum con valores que nada genera es una bandeja con
    filtros que nunca devuelven nada.
  - **`EstadoTareo.PendienteValidacion`** **no reemplaza a `Incompleto`**, que ya es
    correcto. Cubre lo que `Incompleto` no puede: el día sin archivo cargado del aparato de
    esa persona, donde no hay marca impar que señalar y hoy sale `Falta`. La diferencia es
    de qué se duda: en `Incompleto` se sabe que vino y falta una hora; aquí no se sabe ni si
    vino, porque el dato nunca llegó. `RequiereValidacion` (bandera) y `TipoIncidencia`
    cubren los días que necesitan revisión **sin** cambiar de estado.
  - **`Justificacion.EntradaRegularizada`/`SalidaRegularizada`** son la hora que el marcador
    no registró, verificada por quien autoriza. ⚠️ **No tocan ni reemplazan la marcación
    original**: la marca del aparato sigue intacta y el día conserva su
    `EntradaReal`/`SalidaReal`. Es un dato aparte, con autor y fecha, que el cálculo suma
    encima — la diferencia entre corregir un tareo y falsificar un registro biométrico.
    Las dos pueden venir llenas: es el día en que se olvidaron las dos marcas.
  - **`EstadoJustificacion`** (`Solicitada`/`Aprobada`/`Rechazada`/`Cancelada`)
    ⚠️ **No hay «en revisión», y se quitó después de escribirlo.** Existía para que dos
    personas no tomaran la misma regularización a la vez, pero de esa colisión ya se encarga
    el token de concurrencia —la segunda recibe un mensaje que dice quién se adelantó— y un
    estado «tomado» que nadie libera deja la fila trabada sin que ninguna pantalla explique
    por qué. Un valor que nada produce sale en los filtros y nunca devuelve nada.
    **`Cancelada` sí está cableada** (`CancelarAsync`): quien registró la regularización la
    retira mientras nadie la resolvió —el caso corriente es darse cuenta de que la hora
    estaba mal antes de que el coordinador la mire— y el día queda libre, porque el índice
    filtrado no cuenta las canceladas. ⚠️ **Solo la retira quien la pidió**: cancelar la de
    otro sería resolverla sin dejar constancia de la decisión, que es justo lo que la
    aprobación existe para evitar; quien no esté de acuerdo la rechaza, con motivo.
    separa quien pide de quien autoriza. Todo lo ya existente nace `Aprobada`: lo que viene
    de una solicitud ya pasó por su propia autorización, y dejarlo pendiente desharía
    ausencias ya decididas. ⚠️ El motor **solo debe aplicar las `Aprobada`** — una
    regularización pedida y no autorizada que ya modificara el tareo convertiría la
    aprobación en un trámite decorativo. No hay `SolicitadaPor` aparte: es
    `RegistradoPorUsuario`, y dos campos que significan lo mismo se desincronizan.
  - **El índice único de `(trabajador_id, fecha)` pasa a filtrado**
    (`estado NOT IN ('Rechazada', 'Cancelada')`): un rechazo se conserva como evidencia pero
    no puede bloquear ese día para siempre. ⚠️ El filtro lleva la grafía del enum escrita a
    mano, así que **renombrar un valor rompería el filtro sin un solo error**; hay un test
    que compara el texto persistido contra esa grafía.
  - **`Justificacion.Version`** es el token de concurrencia: SQLite no tiene `rowversion`,
    así que es un entero que incrementa quien escribe y EF mete en el `WHERE` del `UPDATE`.
    Hoy es inerte —una justificación se crea y se retira, nunca se modifica— y se puso ahora
    porque el ciclo de aprobación es justo lo que introduce la carrera: dos personas
    resolviendo la misma regularización, una aprobando y otra rechazando.
  - ⚠️ **El valor por omisión de `estado` se fijó a mano en la migración.** EF genera
    `defaultValue: ""` para un enum persistido como texto, y esa cadena vacía no se lee de
    vuelta: cada justificación existente reventaría al materializarse. Es la misma trampa de
    `OrigenVerificadoDeLaCarga`; revisar el SQL generado antes de aplicar una migración no
    es opcional en este proyecto.
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
  **Filtro por aparato** (`FiltroReporte.EquipoId`), para pedir «el resumen del A-L» cuando
  la planta tiene dos: entra quien tuvo un número de ese aparato **dentro del rango**, con el
  mismo solapamiento que el padrón. Por la asignación abierta hoy habría sido el invariante 5
  roto otra vez —quien cambió de aparato en agosto desaparecía del reporte de julio, donde sus
  marcaciones sí salieron de ese aparato—.
  ⚠️ **El tareo sigue siendo por persona, no por aparato**: quien cambió a mitad de rango sale
  en los reportes de los dos, con sus totales completos. Partirlo daría dos medias jornadas
  que no le corresponden a nadie.
  ⚠️ **El área de la fila también sale del rango, no de hoy**
  (`ResolverAreasDelRangoAsync`, `AsignacionArea`, migración `VigenciaDelAreaDelTrabajador`).
  Salía de `Trabajador.AreaId`, que es la de hoy: mover a alguien de área reescribía el pasado
  —un resumen de julio emitido en setiembre lo mostraba ya en su área nueva, y la columna
  `AREA` del tareo final cambiaba sola en un mes ya entregado a planilla—. Mismo error que el
  invariante 5 previene para el ID de marcador, y misma solución. Un cambio dentro del rango
  se muestra con las dos en la misma celda («AEROSOLES → ENVASADO»), por lo mismo que el
  marcador. Sin vigencia registrada se conserva lo proyectado desde la ficha: es mejor
  respaldo que un «-» que rompería el cruce con SAP.
  ⚠️ **El área no se cierra al cesar**, a diferencia del marcador, el turno y el sueldo:
  aquellos se cierran para liberar el número y dejar de calcular, y el área no gobierna nada
  del cálculo. Dejarla abierta hace que el reporte del mes del cese siga resolviéndola.
  ⚠️ **No hay «área rige desde» en la ficha**: el formulario de Personal ya no captura el área
  —Centro y Centro de costo cumplen ese rol— y entra sobre todo por la carga masiva del
  padrón, así que lo normal es una sola fila por trabajador y la vigencia importa el día que
  se vuelve a subir el padrón con «actualizar». Un cambio rige **desde hoy**, y una fecha
  igual o anterior al inicio de la vigente la corrige en sitio. El relleno de la migración
  abre la primera desde el **ingreso**, con lo que ningún reporte cambia el día de migrar.
  **Falta la línea de tiempo del área en `/Trabajadores/Historial`**: la tabla existe pero
  ninguna pantalla la muestra todavía.
  Lo fija `VigenciaDelAreaEnReportesTests` (SQLite real).
  ⚠️ **El número y el aparato de la fila salen del rango, no de hoy**
  (`ResolverMarcadoresDelRangoAsync`). Salían de `FechaFin == null`: un resumen de julio
  emitido en setiembre mostraba el número que la persona tiene *ahora*, y el mismo trabajador
  aparecía con un aparato en el reporte y con otro en la pantalla de Tareo, que sí atribuye
  por fecha. Un cambio dentro del rango se muestra con los dos, en la **misma celda**
  («14 → 27»), por lo que ya advierte el bullet de Marcadores: la fila TOTAL del Excel va en
  columnas fijas y una columna más correría todos los datos.
  El aparato filtrado se nombra en la trazabilidad, que viaja dentro del Excel. En el
  desplegable, con planta elegida se ofrecen los suyos **más el ya elegido**: si no, al
  cambiar de planta el selector mostraba «Todos» mientras el `EquipoId` seguía filtrando en
  la query y el reporte salía vacío sin decir por qué. Lo fija
  `FiltroPorAparatoEnReportesTests` (SQLite real).
  ⚠️ **Todo reporte declara de qué momento son sus cifras** (`EstadoCalculoTareo`): compara
  el último `TareoDiario.CalculadoEn` del alcance contra lo último que pudo cambiarlo —una
  marcación cargada, una carga anulada o reactivada, una justificación creada o modificada,
  una asignación de número de marcador nueva o corregida (la marcación se atribuye al
  calcular), un cambio de turno que rija en el rango (una rotación anotada con fecha
  pasada), la edición de un turno que alguien tiene en el rango (su horario, sus ventanas o
  su gracia de salida: el turno no tiene vigencia por fecha), un cambio en la programación de
  una cuadrilla en el rango (incluida la que se da de baja al reemplazarla)—.
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
  ⚠️ La columna **SUSPENSION** sale **vacía, no en cero**. No existe el concepto de suspensión:
  nada lo registra y nada lo calcula. Tenía una propiedad `DiasSuspension` que nadie escribía,
  salía siempre en cero y **se sumaba en la fila TOTAL**, lo que la hacía parecer calculada —y
  con el formato de número el cero se pinta como «-», indistinguible de «esta persona no tuvo
  suspensiones», que es afirmar algo que nadie comprobó en el archivo que va a planilla—. La
  propiedad se eliminó y el hueco viaja como `null` dentro del arreglo
  (`ExportadorReportesExcel.SuspensionSinDato`). ⚠️ **La columna se conserva en su posición**:
  el bloque de días se escribe por índice desde la 12, así que quitarla correría todo lo que va
  después y dejaría cada total bajo el título equivocado, sin ningún error. Lo fija
  `La_columna_suspension_sale_vacia_y_no_corre_a_las_demas`. Estructura documentada en
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
  Dibuja los **`<optgroup>`** como cabecera, sangra sus opciones, las incluye en el texto
  que busca (teclear "M-Z" encuentra los códigos de ese aparato) y muestra el grupo también
  con el desplegable cerrado. ⚠️ Antes los **aplanaba**, y eso dejaba dos entradas «21»
  indistinguibles en el marcador de personal —una por aparato, dos personas distintas—,
  que es justo lo que el agrupado viene a resolver. Al filtrar, una cabecera solo se
  muestra si alguna de sus opciones sobrevive.
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

**Catálogos tomados de los padrones de planta (18/09/2026)**, migración `PadronesPlanta18Set`:
empresa y plantas se llaman como las escriben los coordinadores (`CA OUTSOURCING SAC`,
`PLANTA DETERGENTES LURÍN`, `PLANTA INTRACAN/FEM` = la antigua Planta 20 mil, `PLANTA 1`);
las **áreas se reemplazaron** por las de la columna ÁREA de cada padrón (las anteriores
quedan inactivas, Sulfonación conserva la genérica); y se agregaron 7 centros de costo que
el padrón usa bajo un centro donde SAP no los tenía (Ids 107–113).
**Puestos: un catálogo general** de 13, en mayúsculas y compartido por las dos empresas
(`EmpresaId` nulo), migración `PuestosGenerales`. Unificó los genéricos, los códigos de
planilla de Consulting Advisers y los textos de los padrones, que repetían el mismo puesto
con otra grafía («Operario», «OPERARIO», «Operario de producción»); esos quedan inactivos.
La carga masiva sigue aceptando las grafías de los padrones y los códigos de planilla
(`26001`…) por la tabla `EquivalentePuesto` de `CargaMasivaTrabajadores`: un sinónimo nuevo
se agrega ahí, **no como otro puesto**.
⚠️ La carga masiva resuelve **puesto y centro de costo dentro de la empresa de la fila**, no
por código global (un puesto universal vale para las dos), y `PE21430001` existe en tres
centros de CA Outsourcing. Un código repetido se desempata con `Centro.PlantaId` —el centro que
atiende a la planta del trabajador—, un vínculo **opcional** (Intracloro no tiene planta).
⚠️ El lector de CSV respeta los **saltos de línea entre comillas**: Excel los escribe en
una celda con Alt+Enter, y el padrón de Planta 1 traía así el encabezado (`"tipo⏎documento"`).
Secciones siguen siendo provisionales. Los RUC de las dos empresas son marcadores de
posición.

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
"tipo de planilla", corte del periodo, el tratamiento del trabajo dominical y del turno
especial de sábado, y **cuántos minutos de gracia de salida** aplica cada turno (hoy 0). ✅ **"Unidad" ya está resuelto: es sinónimo de planta.**
