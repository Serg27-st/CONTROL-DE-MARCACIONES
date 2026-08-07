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
local de SQL Server con 34 tablas, los índices críticos y los catálogos sembrados.

**Funciona hoy:**

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
- **Carga masiva de personal** (`/Trabajadores/CargaMasiva`): CSV con plantilla descargable.
  Reconoce las columnas **por nombre y no por posición** (con sinónimos: `unidad`/`planta`,
  `dni`/`documento`…), resuelve los catálogos por código o nombre, y detecta el delimitador
  (`;`, `,`, tab) y la codificación (UTF-8 o Windows-1252, que es lo que exporta Excel en
  español). No reimplementa las reglas del alta: arma la misma solicitud y llama a
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
  cerrado. **Topes por tipo de ausencia**, guardados como dato en `TipoAusencia` (no en
  código, para que agregar o ajustar un tope siga siendo un `UPDATE`): descanso médico
  máximo 20 días por año calendario, licencia paternal máximo 10 días por evento, licencia
  por maternidad máximo 98 días por evento. **Vacaciones** se registran con subtipo
  (gozadas, máximo 30 días por periodo vacacional; venta, máximo 15 días) y un periodo
  `AAAA-AAAA`. El sustento es obligatorio para todos los tipos, sin excepción.
- **Justificaciones**: consulta con trazabilidad completa (origen, estado anterior, quién y
  cuándo) y alta manual para lo puntual. Una sola justificación por trabajador y día.
- **Reportes**: resumen mensual por trabajador y excepciones, exportables a Excel con
  ClosedXML.
- **Tareo final**: el libro que se entrega a planilla, con las tres hojas del archivo modelo
  de GCA — `Tareo`, `Descanso Medico` y `Vacaciones-Licencias`. Estructura documentada en
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
- **Feriados**: sin pantalla de mantenimiento manual a propósito — un botón de sincronización
  (solo Coordinador) trae los feriados oficiales de Perú de una fuente pública
  (`date.nager.at`) e inserta los que falten, sin borrar ni pisar uno ya cargado. El cálculo
  del tareo sigue leyendo siempre la tabla local `Feriado`, nunca la red: sigue funcionando
  aunque la planta esté sin internet ese día.
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

**No existe todavía:** filtrado de datos por las plantas que gestiona un `ADMINISTRADOR`
(`UsuarioPlanta` guarda el dato, pero Personal/Solicitudes/Reportes no lo aplican como
filtro — nadie lo pidió explícitamente y agregarlo es una fase aparte); y el detalle clínico
del descanso médico (contingencia, diagnóstico, centro de atención) en `SolicitudAusencia`.
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

**Formato del tareo final: confirmado.** Estructura extraída del archivo modelo y
documentada en `docs/04-formato-tareo-final.md`. Queda una brecha de datos, no de formato:
**detalle clínico del descanso médico** — contingencia, diagnóstico y centro de atención no
existen en `SolicitudAusencia`; esas tres columnas salen vacías. El CECO real (antes la otra
brecha) ya se resolvió: GCA entregó `CENTROS DE COSTO POR EMPRESA.xlsx` (106 centros de
costo bajo 5 centros SAP), sembrado en `Organizacion/Centro`/`Organizacion/CentroCosto` y
asignable por trabajador — ver el bullet de Tareo final más arriba.

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
