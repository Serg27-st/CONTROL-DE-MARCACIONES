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

- Mantenimiento de trabajadores completo: listado con filtros y paginación, alta y edición.
- Asignación de ID de marcador y de turno **con vigencia por fecha**: al cambiarlas se cierra
  la anterior y se abre una nueva, en vez de reescribir la existente.
- Desplegables encadenados empresa → planta → área → sección, y planta → turnos habilitados
  (el turno SAB solo aparece al elegir Planta 01).
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

- **Autenticación con ASP.NET Core Identity** y los cuatro roles `ADMINISTRADOR`, `RRHH`,
  `SUPERVISOR`, `CONSULTA`, sembrados con Id fijo desde migración. `[Authorize]` global:
  toda pantalla exige sesión, `CuentaController` es la única puerta abierta.
- **Sin autorregistro.** Las cuentas las crea un ADMINISTRADOR desde `/Usuarios`; el
  primer administrador se crea al arrancar la aplicación leyendo
  `Seguridad:AdminCorreo` / `Seguridad:AdminClave` de configuración externa — **nunca**
  de una migración ni de código versionado, porque una contraseña es un credencial.
  Solo actúa si la tabla de usuarios está vacía.
- **Cerrar y reabrir un periodo, y gestionar usuarios, quedan restringidos a
  ADMINISTRADOR** — la restricción es del servidor, verificada con un POST directo
  saltándose la interfaz, no solo un botón oculto.
- Desactivar una cuenta bloquea el inicio de sesión de inmediato (vía `LockoutEnd`, no
  solo el flag visible), con el mismo mensaje genérico que un correo inexistente o una
  contraseña incorrecta — no delata qué correos tienen cuenta.

Verificado end-to-end contra la aplicación real: acceso anónimo redirige a login; login
con el administrador de arranque funciona; RRHH puede entrar pero `/Usuarios` y
`Periodos/Cerrar` le redirigen a Acceso Denegado, incluso por POST directo; desactivar la
cuenta RRHH bloquea su siguiente intento de login.

**Pendiente:** la matriz fina de qué puede hacer cada rol en cada pantalla (RRHH vs.
SUPERVISOR vs. CONSULTA) no está confirmada por GCA. Hoy el único control por rol es
ADMINISTRADOR en Usuarios y en cierre/reapertura de periodo; el resto de pantallas solo
exige "alguna sesión iniciada".

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

---

## Fase 4 — Motor de cálculo del tareo ⚠️ *núcleo funcionando*

Hecho, con **27 tests unitarios** en `ControlMarcacion.Domain.Tests/Asistencia/`:

- `CalculadoraTareo` es una clase **pura** en `Domain`: sin base de datos ni reloj, probable
  con fechas literales.
- Emparejamiento de marcaciones contra la ventana del turno, refrigerio descontado,
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
- Descuento del refrigerio fijo por turno.
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
  acotados a un solo día.
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

## Fase 6 — Tareo final y reportes ⚠️ *primera versión, columnas provisionales*

> El **archivo modelo del tareo** de GCA sigue sin llegar. En vez de esperar, se construyó
> una primera versión con las columnas que el sistema puede sustentar hoy con el tareo ya
> calculado. Cuando llegue la plantilla oficial, se ajustan las columnas — la infraestructura
> (filtros, agregación, exportación) no cambia.

Hecho:

- **Resumen mensual**: un trabajador por fila — días puntual/tardanza/falta/ausencia
  justificada/incompleto/no laborable trabajado, y horas ordinarias, extra 25 %, extra 35 %,
  nocturnas y de tardanza acumuladas. Fila de totales.
- **Excepciones**: un día por fila, solo los estados que necesitan revisión (falta, tardanza,
  ausencia justificada, marcación incompleta). Descanso, feriado y días sin vínculo no
  aparecen: no son una anomalía.
- Filtro por periodo (obligatorio) y por empresa/planta/área/sección en cascada.
- **Exportación a Excel con ClosedXML** (MIT, no EPPlus): cada archivo lleva periodo, filtros
  aplicados, si el periodo seguía abierto, quién lo generó y cuándo, y una nota explícita de
  que el sistema entrega minutos y horas — nunca dinero.
- Verificado contra datos reales: el resumen de julio 2026 reprodujo los mismos totales que
  ya estaban verificados en Asistencia; el Excel exportado se abrió, se inspeccionó su XML
  interno y contiene el encabezado de trazabilidad y los datos correctos.

Pendiente: reportes diario/semanal y por rango arbitrario, y CSV/PDF — se agregan cuando
haya una necesidad concreta o llegue el archivo modelo que fije el formato esperado.

---

## Fase 7 — Endurecimiento

- Auditoría completa de las acciones sensibles.
- Revisión de seguridad con el agente `seguridad`.
- Rendimiento con volumen real (varios miles de marcaciones por carga).
- Despliegue y respaldo de la base.

---

## Insumos pendientes de GCA

Los dos primeros bloquean fases completas:

1. **Archivo TXT real** del ZKTeco G3 → desbloquea la fase 3.
2. **Archivo modelo del tareo final** → desbloquea la fase 6.
3. Definición de "unidad" y "tipo de planilla".
4. Corte del periodo (mes calendario o del 26 al 25).
5. Convenio colectivo o política de sobretasas, si mejora los mínimos legales.
