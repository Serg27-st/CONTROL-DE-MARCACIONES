# Roadmap por fases

Orden de construcción del sistema. Cada fase deja algo verificable; las que dependen de un
insumo externo están marcadas para que no se arranquen a ciegas.

---

## Fase 0 — Estructura y dominio ✅ *completada*

Solución .NET de 4 capas, modelo de dominio, agentes por rol y documentación de requerimientos.

---

## Fase 1 — Persistencia ✅ *completada*

- `MarcacionDbContext` con conversión automática a snake_case en español.
- 21 configuraciones `IEntityTypeConfiguration<T>`, con las entidades de `Domain` limpias
  de atributos de EF.
- Migraciones `EsquemaInicial` y `CatalogosProvisionales`, aplicadas sobre SQL Server 2025.
- **Índices críticos creados y verificados:**
  - `uq_marcacion_cruda_trabajador_fecha_marcador`
  - `uq_tareo_diario_trabajador_fecha`
  - Índices de vigencia en `asignacion_turno` y `asignacion_marcador`
- Datos semilla cargados: Intradevco, las 2 empresas, las 4 plantas, los 5 turnos (con SAB
  restringido a Planta 01), 4 tipos de documento, 9 tipos de ausencia y las 5 tasas legales.

---

## Fase 2 — Seguridad y personal ✅ *completada*

Hecho:

- Mantenimiento de trabajadores completo: listado con filtros, paginación y búsqueda en vivo
  (debounce 300 ms). Ver y editar abren un **panel flotante lateral** sin navegar; el alta
  completa mantiene su propia página.
- Asignación de ID de marcador, turno **y sueldo básico**, los tres **con vigencia por
  fecha**: al cambiarlos se cierra la anterior y se abre una nueva, en vez de reescribir la
  existente.
- Desplegables encadenados empresa → planta → área → sección, y planta → turnos habilitados
  (el turno SAB solo aparece al elegir Planta 01) — todos con buscador en vivo
  (`searchable-select.js`), sin romper la cascada AJAX.
- Reglas verificadas contra la aplicación en ejecución: documento duplicado rechazado,
  ID de marcador solapado rechazado, caso válido aceptado.

- **Mantenimiento de catálogos** funcionando: puestos, plantas, áreas, secciones y tipos de
  documento, con código único por padre, contador de uso y baja lógica bloqueada si el
  catálogo está en uso.

- **Turnos** con pantalla propia: horarios, refrigerio, tolerancia, **ventanas de
  anticipación de entrada y salida configurables por turno**, días y plantas habilitadas.
- **Tipos de ausencia** con pantalla propia: el comportamiento es obligatorio y no puede
  cambiarse una vez que hay solicitudes o justificaciones que dependen de él.
- Pantalla dedicada de **periodos**, con su ciclo de apertura y cierre.

- **Autenticación con ASP.NET Core Identity** y cuatro roles con matriz de acceso propia:
  `COORDINADOR` (todo, único que gestiona usuarios), `ADMINISTRADOR` (gestiona el tareo:
  personal, catálogos, marcaciones, justificaciones, asistencia, periodos), `GERENCIA`
  (solo dashboard y reportes), `TRABAJADOR_SOCIAL` (solo solicitudes). Reemplazaron a los
  cuatro roles originales (`ADMINISTRADOR`/`RRHH`/`SUPERVISOR`/`CONSULTA`) en una migración
  que reasignó las cuentas `ADMINISTRADOR` existentes a `COORDINADOR` sin tocar su correo
  ni contraseña. `[Authorize]` global: toda pantalla exige sesión, `CuentaController` es la
  única puerta abierta.
- **Sin autorregistro.** Las cuentas las crea un `COORDINADOR` desde `/Usuarios`, con
  apellidos, tipo y número de documento, razón social y, si el rol es `ADMINISTRADOR`, las
  plantas que gestiona. El primer coordinador se crea al arrancar la aplicación leyendo
  `Seguridad:AdminCorreo` / `Seguridad:AdminClave` de configuración externa — **nunca**
  de una migración ni de código versionado, porque una contraseña es un credencial.
  Solo actúa si la tabla de usuarios está vacía.
- **Cada controlador exige el rol que le corresponde según la matriz** — la restricción es
  del servidor, verificada con un POST directo saltándose la interfaz, no solo un enlace
  oculto en el menú.
- Desactivar una cuenta bloquea el inicio de sesión de inmediato (vía `LockoutEnd`, no
  solo el flag visible), con el mismo mensaje genérico que un correo inexistente o una
  contraseña incorrecta — no delata qué correos tienen cuenta.

Verificado end-to-end contra la aplicación real: acceso anónimo redirige a login; login
con las cuentas de cada rol funciona; un intento de acceso o de POST directo a una acción
fuera de la matriz del rol redirige a Acceso Denegado; desactivar una cuenta bloquea su
siguiente intento de login.

**Pendiente:** las plantas que gestiona un `ADMINISTRADOR` se capturan
(`UsuarioPlanta`) pero todavía no se usan para filtrar lo que ese usuario ve en
Personal, Solicitudes o Reportes — nadie lo pidió explícitamente todavía; si hace falta,
el dato ya está listo para esa fase.

---

## Fase 3 — Ingesta del marcador ✅ *completada*

Verificado contra el archivo real de producción (2509 marcaciones, 50 trabajadores, julio 2026):

| Prueba | Resultado |
|---|---|
| Lectura del archivo completo | 2509 marcaciones, 0 líneas rechazadas |
| IDs sin trabajador asignado | 50 reportados con rango de fechas y conteo |
| Carga con 3 trabajadores registrados | 162 marcaciones insertadas |
| Recarga del mismo archivo | 0 insertadas, 162 duplicadas |
| Doble fichada del ID 30 | 53 líneas → 50 insertadas, 3 dedazos colapsados |
| `0014` frente a `14` | atribuidos por separado, sin colisión |

14 tests xUnit sobre el archivo real en
`tests/ControlMarcacion.Infrastructure.Tests/Marcaciones/`.

---

## Fase 3 — Notas de diseño

> El archivo TXT real ya fue entregado y su formato está confirmado y documentado en los
> requerimientos. La muestra vive en
> `tests/ControlMarcacion.Infrastructure.Tests/Recursos/AEH2200760069_attlog.txt`.
>
> **Ojo con esto:** la columna de tipo de marcación viene siempre en `0`. El equipo no
> distingue entrada de salida, así que el emparejamiento lo tiene que resolver el motor
> contra la ventana del turno.

- Lector del TXT como adaptador en `Infrastructure`, detrás de `IFuenteDeMarcaciones`.
- Plantilla de importación configurable (separador, formato de fecha, codificación).
- Resolución ID de marcador → trabajador **por vigencia a la fecha de la marcación**.
- Deduplicación y detección de archivo ya cargado por hash.
- Pantalla de resultado con filas insertadas, duplicadas y rechazadas **con línea y motivo**.

**Se puede verificar:** se carga un TXT, se recarga el mismo y no se duplica nada.

### Verificar el origen de la carga ✅ *hecho el 22/09/2026*

> Observación registrada el 11/09/2026, tras el testeo integral con dos aparatos por planta.

> **Implementado** con los cuatro puntos del diseño de abajo, más tres decisiones que el
> diseño no fijaba: un cuarto valor, `SerieAprendida`, para encontrar la carga que enseñó la
> serie; la coincidencia con otro equipo como **proporción** (50 %, mínimo 5) sobre los días
> en que el otro equipo tiene datos y los últimos 92 días del archivo; y la misma
> comprobación al **reactivar** una carga anulada. Además, el mismo archivo (mismo hash)
> vigente en otro aparato se rechaza siempre, sin casilla de confirmación. Detalle en
> CLAUDE.md, «Verificación del origen de la carga».

**El problema.** Lo único que une un archivo con su aparato es la opción que elige el
operador en la pantalla de carga. Como **todos los ZKTeco numeran a su personal desde el 1**,
elegir mal no produce ningún error: cada marcación encuentra dueño —el equivocado— y ni
siquiera aparece en «IDs sin trabajador». En las plantas con dos aparatos (Planta 01 A-L /
M-Z, Detergentes / Lava vajillas) es el error más probable y el más caro.

`ComprobarSerie` existe, pero hoy está **dormida por partida doble**: `LectorTxtZkteco` no
entrega ninguna serie (el contenido del `attlog` no trae ni una letra), y ninguno de los seis
equipos tiene serie registrada. Solo actuaría con el respaldo binario, y solo si el equipo
tuviera la suya.

**La restricción que decide el diseño.** El usuario **ya no tiene acceso físico a los
marcadores**: no se puede leer la serie en el menú del aparato ni en su etiqueta. La serie
tiene que salir **de los propios archivos**.

**Evidencia** (share `\\192.168.13.182\Asistencia`, 11/09/2026): de 22 archivos del marcador,
**19 llevan la serie en el nombre** (`AEWD193260084_attlog.dat`) y **3 fueron renombrados**
a mano (`03-03-2025.txt`, `03-07-2025.txt`, `NOV-POR TERMINAR.txt`, los tres con marcaciones
reales). También aparecen sufijos agregados: `AEWD193260084_attlog PAT.dat`,
`AEH2200760069_attlog_prueba.txt`. Series vistas: `AEWD193260084`, `AEH2200760069` y
`VGU6251600671` (esta en la cabecera del binario); ninguna está asociada todavía a uno de
los seis equipos.

**Diseño propuesto.**

1. Serie del archivo: primero la del contenido (cabecera del binario, que no se puede
   renombrar); si no hay, la del nombre con un patrón anclado al inicio,
   `^([A-Za-z0-9]{6,20})_attlog`, que ignora lo que venga después (`PAT`, `_prueba`, `(1)`).
   Si contenido y nombre traen series distintas, se rechaza.
2. **Aprender la serie del primer archivo** de cada equipo, en vez de exigir que alguien la
   teclee, **con cruce contra los demás equipos**:

   | Serie del archivo | Serie del equipo elegido | Resultado |
   |---|---|---|
   | Igual | Registrada | Verificado |
   | Distinta | Registrada | Rechazo, nombrando el equipo al que corresponde |
   | La tiene otro equipo | Vacía | Rechazo: el archivo es de ese otro equipo |
   | Ningún equipo la tiene | Vacía | Se registra en este equipo y se avisa en el resultado |
   | No se encuentra (renombrado) | Cualquiera | Se acepta, marcado «origen no verificado» |

   Riesgo conocido: si el primer archivo de un aparato se sube al equipo equivocado, se
   aprende la serie equivocada. El cruce hace que el error aparezca en la **siguiente**
   carga de cualquiera de los dos aparatos, en vez de no aparecer nunca; se corrige editando
   la serie en `/Equipos` y anulando esa primera carga.
3. Columna `OrigenVerificado` en `ImportacionMarcador` (`PorContenido` / `PorNombre` /
   `NoVerificado`), visible en el historial de cargas: el aviso en pantalla se pierde, y al
   investigar meses después un día mal atribuido hay que saber qué cargas nunca se
   verificaron.
4. **Segunda fase, para los renombrados:** el `attlog` acumula el historial del aparato
   (`AEWD193260084_attlog PAT.dat` tiene 14 332 líneas), así que cada descarga repite
   marcaciones ya cargadas. Si las de un archivo coinciden en ID y segundo exacto con las ya
   cargadas de **otro** equipo, el archivo es de ese equipo. No depende del nombre; no
   protege el primer archivo de cada aparato ni aparatos cuya memoria se borre tras descargar.

**Descartado:** exigir el `.dat` binario (pierde ceros a la izquierda: `0014` y `14` son
personas distintas); rechazar todo archivo sin serie en el nombre (habría rechazado 3 de los
22 reales); comparar los IDs del archivo con los del equipo (A-L y M-Z numeran ambos del 1
al 100, justo el caso a proteger).

---

## Fase 4 — Motor de cálculo del tareo ⚠️ *núcleo funcionando*

Hecho, con **27 tests unitarios** en `ControlMarcacion.Domain.Tests/Asistencia/`:

- `CalculadoraTareo` es una clase **pura** en `Domain`: sin base de datos ni reloj, probable
  con fechas literales.
- Emparejamiento de marcaciones contra la ventana del turno, refrigerio computado como
  labor (0 minutos a descontar, confirmado por GCA),
  jornada ordinaria y extras al 25 % / 35 %, minutos nocturnos en la franja 22:00–06:00,
  tardanza con tolerancia inclusiva y salida anticipada.
- Turno que cruza medianoche: la jornada se imputa al día de inicio.
- Marcación impar → `Incompleto` con cero minutos. **Nunca se inventa la hora faltante.**
- Domingo y feriado trabajados quedan marcados como no laborables para la sobretasa.
- `ServicioCalculoTareo` orquesta y persiste; el periodo cerrado bloquea el recálculo.

Verificado contra las marcaciones reales de julio 2026 (6 trabajadores, 186 días):

| Prueba | Resultado |
|---|---|
| Recálculo idempotente | huella idéntica, 0 creados / 186 actualizados |
| Domingo trabajado | 8 jornadas marcadas como no laborables |
| Entrada anticipada | 06:47 con turno 07:00 → computa desde las 07:00 |
| Doble fichada ya deduplicada | 02/07 quedó 06:47 – 19:04, 664 min |
| Periodo cerrado | rechaza el recálculo incluso por POST directo |

- **Sobretasas aplicadas.** `TipoDiaNoLaborable` distingue descanso semanal (D. Leg. 713
  art. 3) de feriado (art. 9): hoy ambos pagan 100 % pero son tasas independientes.
  La liquidación agrupa los minutos por concepto y les aplica la tasa **vigente a la fecha
  del periodo**, con su base legal, y avisa si algún concepto acumuló minutos sin tasa.

  El sistema **no calcula dinero**: entrega minutos y porcentajes para que planilla
  multiplique. Verificado sobre julio 2026: 1 479,62 horas equivalentes, idéntico al
  cálculo manual.

Pendiente:

- Los casos borde restantes del agente `motor-asistencia`.
- Decisiones 8 y 9 de los requerimientos (nocturno de sábado y turno especial de sábado).

---

## Fase 4 — Decisiones que faltan confirmar

> **Parcialmente bloqueada:** faltan las decisiones 7 y 8 de los requerimientos (trabajo en
> día de descanso y feriado, y a qué día pertenecen las horas del nocturno de sábado).
> El resto se puede construir.

- Emparejamiento de las 2 marcaciones diarias con el turno vigente.
- Refrigerio: ✅ **no se descuenta**, es tiempo pagado en los cinco turnos.
- Jornada ordinaria de 480 minutos; excedente a horas extras 25 % / 35 %.
- Minutos nocturnos caídos en la franja 22:00–06:00.
- Tardanza con tolerancia inclusiva.
- Los 13 casos borde listados en el agente `motor-asistencia`.
- Recálculo idempotente por periodo.

**Se construye con tests primero.** Es el módulo donde un error se paga en soles.

---

## Fase 5 — Solicitudes, justificaciones y cierre ✅ *completada*

- **Solicitudes de ausencia**: alta con validación de solapamiento, ciclo pendiente →
  aprobada / rechazada / anulada. Los tipos por horas exigen hora de inicio y fin y quedan
  acotados a un solo día. **Topes por tipo**, guardados como dato en `TipoAusencia` (no
  como `switch` en código): descanso médico 20 días/año calendario, licencia paternal 10
  días/evento, licencia por maternidad 98 días/evento. Vacaciones con subtipo (gozadas 30
  días/periodo, venta 15 días/periodo) y periodo `AAAA-AAAA`. Sustento obligatorio en todos
  los casos. Validador puro en `Domain` (`ValidadorLimitesSolicitud`), sin tocar base de
  datos, cubierto con tests de casos límite.
- **Aprobar genera una justificación por cada día cubierto**, con el motivo, el sustento y
  el estado que tenía el tareo antes de justificarlo. Si un día cae en un periodo cerrado,
  sin periodo, o ya tiene otra justificación, se omite y se informa — nunca se sobrescribe
  en silencio.
- **Anular una solicitud aprobada retira sus justificaciones**, salvo que alguna ya esté en
  un periodo cerrado: ahí se bloquea la anulación completa hasta reabrir el periodo.
- **Justificación manual** para lo puntual (una tardanza, una marca impar de un día) que no
  amerita el trámite de una solicitud. Exige motivo, respeta el periodo abierto y no admite
  una segunda justificación para el mismo trabajador y día — el motor solo resuelve una.
- **El motor aplica la justificación sobre el cálculo crudo**, no en su lugar: el resultado
  final conserva la hora real de marcación, el estado que tenía antes, y una observación
  que dice tanto lo que pasó como lo que se perdonó (`"tardanza de 27 min. TARDANZA
  JUSTIFICADA: se perdonaron 10 min, quedan 17 min"`). Justificar una ausencia nunca genera
  horas extras: paga como máximo la jornada ordinaria del turno, y solo si el tipo de
  ausencia computa como asistencia.
- 15 tests nuevos sobre la aplicación de justificaciones en `CalculadoraTareoTests`.

Verificado de punta a punta contra datos reales: solicitud → aprobación → 3 justificaciones
→ recálculo → 3 días de `Falta` a `AusenciaJustificada`, pagados con la jornada ordinaria.
Anular la solicitud y recalcular revierte el tareo exactamente a como estaba.

**Pendiente:** el rechazo de una solicitud no genera justificación (correcto, no debería),
pero no hay pantalla dedicada al cierre previo del periodo más allá del cierre/reapertura
que ya existe en Periodos.

---

## Fase 6 — Tareo final y reportes ✅ *completada, una brecha de datos documentada*

El **archivo modelo del tareo** de GCA (`TAREO NOVIEMBRE 2024_PLANTA 1.xls`) llegó, se
extrajo celda por celda con Excel COM (valores y fórmulas) y está documentado en
`docs/04-formato-tareo-final.md`. El tareo final replica sus tres hojas exactas.

Hecho:

- **Resumen mensual**: un trabajador por fila — días puntual/tardanza/falta/ausencia
  justificada/incompleto/no laborable trabajado, horas ordinarias, extra 25 %, extra 35 %,
  nocturnas y de tardanza acumuladas, y el **bono nocturno en soles**. Fila de totales.
- **Excepciones**: un día por fila, solo los estados que necesitan revisión (falta, tardanza,
  ausencia justificada, marcación incompleta). Descanso, feriado y días sin vínculo no
  aparecen: no son una anomalía.
- Filtro por periodo (obligatorio) y por empresa/planta/área/sección en cascada.
- **Tareo final**: las tres hojas del archivo modelo — `Tareo`, `Descanso Medico`,
  `Vacaciones-Licencias` — con **Centro/Ceco resueltos desde el catálogo real de SAP**
  (`CENTROS DE COSTO POR EMPRESA.xlsx`, entregado por GCA: 106 centros de costo bajo 5
  centros SAP, asignable por trabajador en Personal; cae a la aproximación con Área/Sección
  solo mientras un trabajador no tenga uno asignado), movilidad y asignación familiar
  calculadas contra `ParametroLegal` vigente, y el bloque de 18 columnas de reintegros
  capturable a mano por trabajador y periodo desde su propia pantalla.
- **Exportación a Excel con ClosedXML** (MIT, no EPPlus): cada archivo lleva periodo, filtros
  aplicados, si el periodo seguía abierto, quién lo generó y cuándo.
- Verificado contra datos reales: el resumen de julio 2026 reprodujo los mismos totales que
  ya estaban verificados en Asistencia; el tareo final se generó y su `.xlsx` se abrió e
  inspeccionó celda por celda (Centro, movilidad, asignación familiar) contra los valores
  esperados.

✅ **La última brecha de datos está cerrada**: `SolicitudAusencia` ya guarda contingencia,
diagnóstico y centro de atención, obligatorios en los tipos subsidiados (`DM`, `SUBE`,
`SUBA`), y la hoja `Descanso Medico` los escribe con el texto exacto del archivo de GCA.
El diagnóstico se trata como dato sensible de salud (Ley 29733): la auditoría lo registra
por nombre y nunca por valor, y no aparece en el listado de solicitudes. Detalle en
`docs/04-formato-tareo-final.md`.

Pendiente: reportes diario/semanal y por rango arbitrario, y CSV/PDF — se agregan cuando
haya una necesidad concreta.

---

## Fase 7 — Endurecimiento

- ✅ **Auditoría completa de las acciones sensibles.** Interceptor de `SaveChanges`
  (`InterceptorAuditoria`) sobre todas las tablas salvo cuatro exclusiones justificadas,
  con pantalla de consulta en `/Auditoria` para el coordinador. Los datos personales de la
  Ley 29733 se registran por nombre pero nunca por valor.
- Revisión de seguridad con el agente `seguridad`.
- Rendimiento con volumen real (varios miles de marcaciones por carga).
- Despliegue y respaldo de la base.

---

## Insumos pendientes de GCA

Los que bloqueaban fases completas ya llegaron: el **archivo TXT real** del ZKTeco G3
(fase 3), el **archivo modelo del tareo final** (fase 6) y el **catálogo real de CECO**
(`CENTROS DE COSTO POR EMPRESA.xlsx`, fase 6). Lo que queda no bloquea nada construido
hasta ahora:

1. Definición de "tipo de planilla". ✅ *"Unidad" resuelto: es sinónimo de planta, y así
   quedó en el formulario de Personal y en la carga masiva.*
2. Corte del periodo (mes calendario o del 26 al 25).
3. Convenio colectivo o política de sobretasas, si mejora los mínimos legales.
4. ~~Detalle clínico del descanso médico~~ — ✅ **implementado**: contingencia, diagnóstico
   y centro de atención se capturan en la solicitud y salen en la hoja `Descanso Medico`,
   que lista los tres tipos subsidiados (`DM`, `SUBE` y `SUBA`).
5. Confirmación de si los tramos nocturnos 25 %/35 %/100 % del tareo final son subconjunto
   de los diurnos (como está modelado hoy) o categorías excluyentes — ver
   `docs/04-formato-tareo-final.md`.
