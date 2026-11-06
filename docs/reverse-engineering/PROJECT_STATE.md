# Estado actual del proyecto

> Punto de entrada compacto para nuevas sesiones. Fecha de consolidación: 2026-10-23.

Fases 1–6.1 completadas. Comprensión consolidada aproximada: 96 %.

## 1. Qué es

Control de Marcaciones es una aplicación local de control de asistencia.
Importa marcaciones ZKTeco, las atribuye a trabajadores, calcula tareo y genera reportes Excel.
La solución es un monolito modular ASP.NET Core MVC/Razor.
Usa .NET 10, EF Core 10 y SQLite.
La autenticación usa ASP.NET Core Identity con cookies.
ClosedXML produce los archivos Excel.
Nager.Date aporta feriados externos que después se persisten localmente.

## 2. Arquitectura en una frase

Web orquesta UI y composición; Application define contratos; Infrastructure implementa persistencia e integraciones; Domain contiene reglas puras.

Dependencias observadas:
Web → Application.
Web → Infrastructure en la raíz de composición y bordes técnicos.
Infrastructure → Application y Domain.
Application → Domain.
Domain no depende de las otras capas.

## 3. Módulos

Personal administra ficha, vigencias, padrón, cese y carga masiva.
Marcaciones importa TXT/DAT y conserva marcas crudas.
Turnos y rotaciones resuelve horarios, cuadrillas y programación por fecha.
Asistencia/tareo construye la jornada y persiste resultados diarios.
Solicitudes/justificaciones gestiona ausencias, sustento y regularización.
Reportes consulta tareo persistido y exporta Excel.
Seguridad aplica roles y alcance por planta.
PMV almacena marcaciones provisionales en un ramal aislado.

## 4. Flujo principal

El formato ZKTeco se detecta por contenido, no solo por extensión.
Los lectores admiten TXT y respaldo binario DAT.
La importación crea cabecera, errores y `MarcacionCruda`.
La atribución es dinámica por equipo, identificador de marcador y fecha.
El sistema no persiste una entidad Jornada.
La jornada se reconstruye al calcular.
El motor selecciona entrada/salida y resuelve turno, rotación y vigencias.
`CalculoTareo` produce el resultado de dominio.
`TareoDiario` persiste el resultado.
Los reportes consumen tareo persistido; no recalculan.

## 5. Flujo PMV

PMV reutiliza los mismos lectores de archivos.
Persiste solo `prov_importacion`, `prov_marcacion` y `prov_error_importacion`.
No persiste el trabajador; atribuye al consultar.
No escribe importación principal, marcas crudas ni tareo.
Consulta rangos de 1 a 41+ días.
1–30 días: rango normal.
31–40 días: advertencia.
41+ días: consulta permitida, exportación bloqueada.
PMV aplica alcance por planta en catálogo, historial, importación y consulta; exportación reutiliza la consulta protegida.

## 6. Reglas de negocio vigentes críticas

Turnos regulares: DIU 07:00–17:22; NOC 19:00–05:22 del día siguiente.
De lunes a viernes el ordinario llega hasta el fin teórico del turno; no se infiere sumando 480 minutos a la entrada.
Una licencia o ausencia pagada de jornada completa acredita 480 minutos (8 horas efectivas sin almuerzo), tanto en DIU como en NOC; no hereda los 622 minutos de intervalo del turno ni genera HE o nocturnidad por sí sola.
Umbral HE de lunes a viernes: un exceso menor a 15 minutos sobre el fin teórico no es HE; desde 15 minutos se cuenta entero desde el fin teórico (17:37 = 15 HE; 05:37 = 15 HE).
La tardanza no corre el corte.
Las primeras dos horas extra van al 25 % y el resto al 35 %.
El sábado, todo minuto trabajado es extra desde el primer minuto, sin umbral.
Las primeras dos horas del sábado son 25 % y el resto 35 %.
El domingo es descanso semanal.
Si se trabaja domingo, todo el tiempo son horas extra al 100 % desde el primer minuto, sin umbral.
No se reparte como ordinario, 25 % o 35 % y no se crea descanso sustitutorio automático.
La jornada nocturna se atribuye a su fecha de inicio, pero la clasificación económica puede segmentarse: viernes→sábado conserva jornada regular; sábado→domingo corta HE25/35 a las 00:00 y desde allí todo minuto es HE100 sin umbral.

Para planilla, la tolerancia de tardanza es 0 minutos en DIU y NOC.
07:00 y 19:00 son puntuales; 07:01 y 19:01 representan un minuto de tardanza.
Un campo técnico/configurable de cinco minutos no debe perdonar tardanza de planilla.

La salida anticipada de planilla también tiene tolerancia 0 contra el fin teórico.
17:22 o 05:22 producen 0 minutos; 17:21 o 05:21 producen 1; 17:17 o 05:17 producen 5.
Los contratos TAR/SA ya aplican tolerancia cero. Los valores configurables permanecen almacenados, pero no intervienen en el descuento de planilla.

La franja nocturna observada es 22:00–06:00.
La nocturnidad sigue siendo una dimensión superpuesta a la base trabajada. Desde R-P1-01, liquidación suma únicamente su prima y evita repetir la base; descanso/HE100 aporta base y recargo una sola vez.

14+1+7 es un patrón operativo observado.
No es un ciclo universal ni infinito.
La regla requerida es secuencia y duración flexibles.
`TramoRotacion` distingue ahora `Trabajo` y `Descanso`; `TurnoId` es nullable y los
constraints físicos impiden combinaciones inválidas. No existe turno ficticio.
El descanso confirmado dentro de la rotación dura un día.
El diseño D-ROT-DESC aprobó `TipoTramoRotacion = Trabajo | Descanso` con `TurnoId`
nullable y una invariante cruzada; ver `ADR_ROTATION_REST_MODEL.md`. S-ROT-DESC está
convirtiendo ese diseño en contratos en `ROTATION_REST_ACCEPTANCE_SPEC.md`. La
implementación, migración y adaptación del tareo quedaron completadas en R-P1-05 y
`ROT-DESC-01` está verde. BD-01 ya tiene reconstrucción de código, análisis de cinco alternativas,
alternativa híbrida aprobada mediante BD01-D01..D03 y casos BD01-01..14 en
`BD_01_REST_WORK_BOUNDARIES.md`. BD-02 también está cerrada mediante BD02-D01..D09 y sus
contratos ROT-FER en `BD_02_HOLIDAY_REST_COINCIDENCE.md`. La especificación de ROT-DESC
está implementada y verificada.

`MarcacionCruda` es inmutable.
Una corrección debe vivir en una regularización separada con original, corrección, motivo, actor, fecha, validación y auditoría.
REG-01..04 confirman que el flujo caracterizado conserva y audita esa información.
La importación principal confirma atómicamente cabecera, errores, serie aprendida, auditoría y marcaciones crudas. Un fallo técnico revierte la operación completa; una línea inválida sigue siendo un error funcional persistido dentro de una importación válida.

## 7. Fuentes de verdad

Las reglas confirmadas están en `ACCEPTANCE_RULES.md`.
La diferencia requisito/sistema y el impacto están en `REQUIREMENTS_TRACEABILITY.md`.
El catálogo autoritativo vigente está en `BUSINESS_RULES_FINAL.md`; `BUSINESS_RULES.md`
permanece como corte histórico de Fase 6.1 y mapa de IDs legados.
El esquema físico está en `DATABASE.md`.
Las prioridades están en `REMEDIATION_PLAN.md`.
La explicación integrada está en `MASTER_DOSSIER.md`.
El enrutamiento para IA está en `AI_CONTEXT_MAP.md`.

Cuando código y negocio divergen, la divergencia debe permanecer explícita.
No se debe reescribir la conducta actual como si fuera la regla correcta.

## 8. Base de datos

La base canónica se inspeccionó en modo lectura.
Tiene 49 tablas físicas.
Dos son de infraestructura de migraciones/Identity y 47 corresponden a aplicación según el inventario físico.
Tiene 45 claves foráneas.
Tiene 87 índices explícitos.
Tiene 32 índices únicos.
Tiene 9 índices únicos parciales.
Tiene un `CHECK` observado.
No tiene triggers.
No tiene vistas.
Tiene 14 migraciones aplicadas.
La integridad estructural SQLite resultó correcta.

**OBSOLETO — corte histórico anterior a Fase 7/3B:** se observaron 85 grupos de
`PeriodoLaboral` abierto duplicado y 170 filas. Esa cifra no debe proyectarse sobre otra base.

**SUPERADO EN COPIA CONTROLADA 3B:** el perfil contemporáneo encontró 307 trabajadores con
múltiples abiertos, 868 filas abiertas involucradas y 900 pares solapados. El saneamiento
idempotente dejó 352 trabajadores, 352 periodos, 0 múltiples abiertos, 0 solapamientos y 352
`AsignacionPlanta` de evidencia Nivel A. Producción no fue inspeccionada ni modificada; cualquier
operación futura debe repetir el perfil sobre una copia contemporánea.
La base canónica no contiene una muestra representativa del flujo principal de tareo/rotación.

## 9. Pruebas y aceptación

Línea base previa: 677 de 677 pruebas pasaban. En Fase 5: 701 totales, 687 verdes, 14 rojas deliberadas y 0 omitidas.

Corte previo (R-P0-02): 724 totales, 708 verdes, 16 rojos deliberados.

Corte tras R-P0-03: 735 totales, 727 verdes, 8 rojos deliberados.

Corte tras R-P0-03.1: 758 pruebas totales.
750 pasan.
8 fallan deliberadamente como contratos de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Corte tras R-P0-HE.2: 764 pruebas totales.
756 pasan.
8 fallan deliberadamente como contratos de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Corte tras R-P1-01: 765 pruebas totales.
759 pasan.
6 fallan deliberadamente como contratos de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Corte tras R-P1-02: 765 pruebas totales.
761 pasan.
4 fallan deliberadamente como contratos de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Corte tras R-P1-03: 766 pruebas totales.
765 pasan.
1 falla deliberadamente como contrato de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Corte tras R-P1-07: 785 pruebas totales.
784 pasan.
1 falla deliberadamente como contrato de aceptación (P1).
0 fallos inesperados y 0 omitidas.

Domain: 368 totales, 368 pasan.
Infrastructure: 435 totales, 435 pasan.

Corte tras R-P1-05 / ROT-DESC: 803 pruebas totales, 803 verdes, 0 deliberadas,
0 inesperadas y 0 omitidas.

No quedan fallos deliberados. `ROT-DESC-01` y los contratos ROT-DESC/MARK/FER/REP pasan.

Corte final de `P1-DATA-PERIODOLABORAL` (Fase 3E-D): build Release 0 errores/0 warnings;
Domain 419/419, Infrastructure 490/490 y Web 28/28; total 937/937, 0 fallos y 0 omitidos.
Regresión dirigida transversal: 108/108.

**SUPERADA por Fase 3D:** la afirmación anterior indicaba que el repositorio de escritura
cargaba únicamente el vínculo abierto. Ahora carga toda la historia con sus plantas para validar
solapamientos y marca selectivamente solo la raíz y los hijos realmente modificados. PL-01/02
siguen verdes; PL3D protege cese exacto, historia cerrada y rollback.

INC-01..03 están verdes desde R-P1-03: la incidencia se decide sobre el resultado
posterior a la justificación, no por la mera presencia de `JustificacionId`. Una
regularización parcial permanece en la bandeja y una completa sale de pendientes.

HE-01..05, DOM-01..03 y NOC-WE-01..06 están verdes, sin cambiar expectativas para acomodar defectos (HE-01..05 usan desde R-P0-03.1 el fixture DIU 07:00–17:22).
R-P0-03 actualizó seis tests históricos que fijaban 480 ordinarios. R-P0-03.1 actualizó diez pruebas por requisito posterior: horario regular DIU/NOC y umbral de 15 min. El detalle está en `CONTEXT_REGISTER.md`.

SEC-01..04 están verdes: P0-SEC quedó resuelto sin regresiones.
TAR-00..04, SA-00..04 y el cruce nocturno están verdes: P0-TARD y P0-SA quedaron resueltos.

Cobertura vigente tras R-P1-04:
85 reglas con cobertura directa.
15 con cobertura parcial.
8 sin cobertura.

## 10. Baseline sintético PMV

Importar 1,000 filas: 295.56 ms y 6,334,744 bytes asignados.
Importar 20,000 filas: 5,187.13 ms y 65,656,336 bytes asignados.
Consulta 1 día: 127.17 ms; incluye arranque frío.
Consulta 30 días: 1.65 ms.
Consulta 31 días: 6.72 ms.
Consulta 40 días: 2.00 ms.
Consulta 41 días: 4.57 ms.

Es una sola ejecución sintética.
No es un SLA.
No cubre tareo principal ni exportación completa.

## 11. P0 actual

P0-01: horas extra y jornada — **RESUELTO EN CÓDIGO** (R-P0-03 + R-P0-03.1).
R-P0-HE.2 verificó cruces nocturnos de fin de semana y corrigió la segmentación sábado→domingo: viernes→sábado permanece regular; desde domingo 00:00 el tramo dominical es HE100 sin umbral. La fecha de jornada sigue siendo la de inicio.
`CalculadoraTareo`: L–V ordinario hasta `FinTurno` teórico con umbral HE de 15 min; la tardanza no corre el corte; sábado sin ordinarios ni umbral; domingo trabajado todo al 100 % sin umbral.
**CONFIGURACIÓN DE DATOS REQUIERE PLAN DE ACTUALIZACIÓN:** la semilla vigente define DIU 07:00–19:00 y NOC 19:00–07:00 (VersionTurno desde 2000-01-01). Hay que crear una nueva versión con «Rige desde» desde `/Turnos` (sin migración ni reescritura histórica) y recalcular los periodos abiertos. Hasta entonces, el ordinario y la salida anticipada de datos reales se miden contra 19:00/07:00.
**OP-01 validado en copia** (`OP_01_SHIFT_VERSION_UPDATE.md`): «Rige desde» cierra la versión antigua sin reescribirla, el recálculo piloto cumple todos los contratos y crear la versión no recalcula automáticamente. Base canónica intacta. Producción hoy: 0 asignaciones de turno y 0 tareos, así que el impacto inmediato es nulo.
**PENDIENTE OPERATIVO:** definir FechaRigeDesde para producción (no hay evidencia documental). Ticket DEPLOY-01: las nuevas instalaciones siguen sembrando los horarios obsoletos.
Procedimiento de producción y rollback verificado: `OP_01_PRODUCTION_PROCEDURE.md`. Riesgo crítico: en la primera edición, un «Rige desde» ≤ 2000-01-01 reescribe la historia. La frontera puede atrasarse, pero no adelantarse desde la aplicación.

No quedan P0 abiertos en código.

P0-02: seguridad PMV — **RESUELTO**.
El backend filtra listado e historial y rechaza importación, consulta y exportación fuera de alcance.

P0-03: tardanza — **RESUELTO**.
Planilla cuenta cada minuto posterior a la entrada teórica.

P0-04: salida anticipada — **RESUELTO**.
Planilla cuenta cada minuto anterior a la salida teórica.

## 12. P1 actual

P1-01: doble conteo en liquidación nocturna/no laborable — **RESUELTO** (R-P1-01).
P1-02: duplicados y cierre de `PeriodoLaboral` — **RESUELTO EN CÓDIGO** (R-P1-02).
`P1-DATA-PERIODOLABORAL`: **CERRADO TÉCNICA Y DOCUMENTALMENTE** mediante 3B–3E-E.
El saneamiento y la migración fueron validados solo sobre copia controlada; código, invariantes,
consumidores históricos, cuadrillas, PMV y reingreso quedaron consolidados. La unicidad física
del vínculo abierto existe en la migración validada y la concurrencia está cubierta. Producción
permanece intacta: este cierre no equivale a despliegue ni saneamiento productivo.
P1-03: incidencia incompleta suprimida por justificación parcial — **RESUELTO** (R-P1-03).
P1-04: alerta de frescura incompleta y falso “actualizado” — **RESUELTO** (R-P1-04).
P1-05 / ROT-DESC: **RESUELTO** (R-P1-05). Tipo explícito, FK nullable, secuencia finita,
resolución discriminada, dos pasadas BD-01, precedencia BD-02, UI y reportes implementados.
La migración se validó en SQLite temporal y no se aplicó a producción. Descanso
sustitutorio, día no laborable compensable y valorización adicional del 1 de mayo
permanecen como gaps separados.
OP-02B amplió el ensayo sobre una copia con evidencia **CONTROLADA REPRESENTATIVA**: siete
programaciones, trece tramos, cinco cuadrillas, vigencias cerradas/futura, asignaciones,
marcaciones y tareo. Las 34 resoluciones históricas coincidieron antes/después, el backfill
dejó los 13 tramos en Trabajo y un restore real recuperó byte a byte el backup pre-migración.
R-WEB-ROT-DESC corrigió después los dos bloqueantes Web: extrajo el script incompatible con
CSP y sustituyó el binding directo del `IReadOnlyList` de Application por un ViewModel Web
mutable con validación condicional y mapeo explícito. UI-ROT-01..07 y 807/807 pruebas pasan.
**OP-02B está VALIDADO** y OP-03 puede prepararse, pero no fue ejecutado. EF sigue advirtiendo
que el cambio de `foreign_keys` no es transaccional; el despliegue requerirá parada de
escrituras y respaldo restaurable. Ver `R_WEB_ROT_DESC_FORM_FIX.md`.
OP-03 ya tiene runbook productivo preparado. OP-03A generó el release frozen
`ControlMarcacion_ROT_DESC_20261009_op03a01.zip` (SHA-256
`29B8F78BD7041A6C82F649799DD034EBBE29352447DC98E3C7712532DCA4C1DE`), con build
Release 0/0 y suite 807/807. La base productiva candidata de ProgramData conserva ocho
migraciones y termina en `RotacionPorCuadrilla`; la cadena real de siete migraciones hasta
ROT-DESC se ensayó completa sobre copia con exit 0, integridad `ok`, FK 0 y conteos
preservados. **OP-03A NO VALIDADO / ventana NO calendarizable**: falta confirmar la ruta
efectiva de la instancia, smoke autenticado del ZIP, aprobación de PlantaId 3/período 2,
responsables nominales, destino definitivo de backup y cuenta `ADMINISTRADOR` limitada al
piloto. No se ejecutó migración ni recálculo productivo. Ver
`OP_03A_ROT_DESC_PRODUCTION_READINESS.md`.
P1-06: reporte histórico mutable.
P1-07: importación principal no atómica — **RESUELTO** (R-P1-07; transacción SQLite e IMP-AT-01..07).
P1-08: crédito de 480 minutos en licencia/ausencia pagada completa — **CERRADO COMO REQUISITO VIGENTE** (R-P1-08; el código ya cumplía).

La regularización estándar no queda como P1 separado: REG-01..04 pasan.
La composición con otras justificaciones permanece dentro de incidencias/aprobación/borrado.

## 13. P2/P3 relevante

La autorización PMV está cubierta en servicios con SQLite; una prueba de controlador extremo a extremo sigue como deuda no bloqueante.
AUS-007 cerró la aprobación parcial: una solicitud se aprueba para todos sus días o permanece
pendiente sin filas nuevas. Persisten preguntas sobre refrigerio/nocturnidad y tasas por empresa.
Constantes de dominio están dispersas.
`MinutosTolerancia` y `MinutosAnticipacionSalida` siguen en BD/UI/versionado, pero ya no afectan planilla; son configuración obsoleta candidata a limpieza futura.
La observabilidad operativa es limitada.
PMV necesita criterio de retiro.

## 14. Decisiones arquitectónicas importantes

**OBSOLETA/SUPERADA:** usar campos planos de `Trabajador` en cálculo y reporte fue una decisión
transitoria anterior al saneamiento. Hoy `PeriodoLaboral` es autoridad histórica de vínculo y
empresa, y `AsignacionPlanta` de planta; los cuatro campos planos son solo proyección actual y
compatibilidad.
El acceso Web directo a DbContext está acotado a composición/seguridad.
PMV está funcionalmente aislado del tareo principal.
El tareo no congela todos los insumos usados para calcular.
La vigencia del tareo usa la fila calculada más antigua del periodo + plantas visibles y
observa las fuentes efectivamente consumidas; reutiliza auditoría para borrados físicos.

## 15. Qué no hacer

No tratar 480 como frontera universal de jornada trabajada; sí es la regla vigente del crédito de una licencia/ausencia pagada completa.
No tratar cinco minutos como tolerancia de planilla.
No generalizar 14+1+7.
No describir domingo solo como “feriado”: todo minuto trabajado va al 100 %.
No suponer que PMV hereda el alcance del flujo principal.
No recalcular periodos cerrados automáticamente.
No reparar datos junto con un cambio de esquema sin plan auditable.
No usar backups o ejecutables como fuentes de verdad.
No releer el repositorio completo para cada tarea, salvo que exista evidencia de que la documentación quedó obsoleta.

## 16. Inicio de una tarea

Lee `MASTER_DOSSIER.md` si necesitas la visión integrada.
Consulta `AI_CONTEXT_MAP.md` para escoger la ficha modular.
Abre solo 2–5 archivos críticos y sus pruebas dirigidas.
Amplía a documentos de fase únicamente para trazabilidad o contradicción.

## 17. Siguiente acción recomendada

P0 cerrados en código (SEC, TARD, SA, HE) y P1-01/P1-02/P1-03/P1-04 resueltos en código.
Antes de usar resultados reales: que negocio defina FechaRigeDesde y ejecutar en producción el procedimiento validado de OP-01 (respaldo → `/Turnos` DIU 17:22 y NOC 05:22 con «Rige desde» → recálculo de los periodos abiertos por el Coordinador).
P1-04 y ROT-DESC están cerrados. OP-02B validó migración, restore, regresión histórica y el
formulario Web autenticado; UI-ROT-01..07 y 807/807 pruebas pasan. OP-03 está documentado y
OP-03A produjo un release frozen y ensayó la cadena real completa, pero quedó **NO VALIDADO**.
Cerrar sus seis bloqueantes antes de calendarizar; mantener parada de escrituras, backup
restaurable y verificación inmediata. La advertencia no transaccional de EF impide tratar el
`Down` como única salvaguarda. OP-03 no fue ejecutado. Durante R-WEB hubo una apertura
accidental de la ruta productiva cuyos
cambios quedaron solo en WAL y fueron retirados con autorización; el archivo principal
conservó su SHA y no quedó una migración productiva persistente.
**SUPERADA:** la recomendación anterior de iniciar `DATA-PERIODOLABORAL` fue cumplida en
3B–3E-E sobre código y copias controladas. No repetir esas fases. Una eventual intervención
productiva sigue siendo una operación nueva, independiente, reversible y auditable.

## 18. Fase 7 — hardening previo a producción

Fecha de corte: 2026-10-09. El primer bloque de Fase 7 quedó cerrado como **inventario y
priorización**, sin cambios de código ni datos. Build Release: 0 errores/0 warnings; suite:
807/807, 0 fallos, 0 omitidos. La cobertura confirmó un hueco Web importante: 19 de 21
controladores aparecen con 0 % de líneas en el reporte de esta ejecución.

Se registraron 23 hallazgos: 2 P0, 6 P1, 11 P2 y 4 P3. Los P0 son la presencia de valores de
bootstrap administrativo en configuración versionada/artefactos normales y bases/backups
SQLite con información sensible dentro del árbol Git o sin exclusión. No se copiaron secretos,
no se borraron archivos y no se reescribió historia.

La base SQLite local incluida en el árbol pasó integridad/FK, pero reveló que
`DATA-PERIODOLABORAL` está más desactualizado de lo documentado: 307 trabajadores con más de
un periodo abierto, 868 filas abiertas en esos grupos y 900 pares solapados. Esta evidencia
corresponde a la base local del repositorio, no a producción.

El siguiente bloque recomendado es 7A, contención de seguridad y datos, con coordinación
humana para rotación y política de retención. OP-03 sigue no ejecutado y no debe calendarizarse.
Detalle y contratos: `PHASE_07_HARDENING_INVENTORY.md`.

## 19. Fase 7A — contención P0

Fecha: 2026-10-09. La configuración fuente y los builds reconstruidos ya no contienen la
credencial de bootstrap. El proyecto conserva `UserSecretsId`; desarrollo usa user-secrets y
una futura operación usa temporalmente `Seguridad__AdminCorreo` / `Seguridad__AdminClave`.
Una base sin usuarios y sin ambos valores ahora falla explícitamente. Build 0/0 y suite
809/809.

En este corte, P0-SEC-01 estaba contenido localmente: el ZIP afectado fue retirado y marcado en
cuarentena, pero seguía abierto hasta rotar/inutilizar la credencial expuesta. Ese estado quedó
**SUPERADO** por el cierre del §27. P0-DATA-01 está **CONTENIDO**:
75 archivos SQLite/sidecar/backup y el ZIP fueron copiados con 76/76 hashes coincidentes y sus
originales retirados; los cuatro archivos Git salieron del índice actual y `.gitignore` evita
reincorporarlos. Historia Git y producción permanecen intactas.

Ver `PHASE_07A_SECURITY_DATA_CONTAINMENT.md` y `PHASE_07A_SQLITE_INVENTORY.md`. Producción,
OP-03 e historia Git permanecen intactas.

## 20. P1-STARTUP-HEALTH

Fecha: 2026-10-09. **RESUELTO**. Antes, `MigrateAsync` estaba dentro de un `try/catch` que
solo registraba el error y `/estado` devolvía 200 incondicional. Ahora `/vivo` identifica el
proceso y `/estado` es readiness read-only: compara migraciones aplicadas/esperadas, ejecuta
`quick_check`, comprueba FK y verifica tablas/columnas del modelo. Devuelve 503 con códigos
genéricos mientras la base no sea compatible y recupera 200 sin reinicio cuando una reparación
externa la deja lista. El endpoint nunca migra.

Endurecimiento 2026-10-13: un estado thread-safe bloquea con 503 todas las rutas funcionales
durante `Starting` o `Failed`; solo `/vivo` y `/estado` quedan disponibles. Una comprobación
readiness satisfactoria rehabilita el tráfico sin reiniciar. Baseline vigente: Domain 398,
Infrastructure 441 y Web 23; total 862/862. Build 0 errores/0 warnings.

Build Release 0 errores/0 warnings. Suite: Domain 368, Infrastructure 436, Web 9; total
813/813, 0 fallos, 0 omitidos. No se tocó producción, OP-03 ni reglas de negocio. Documento:
`P1_STARTUP_HEALTH.md`.
# Actualización 2026-10-12 — P1-DESKTOP-INSTALLER

Hosting temporal preparado para instalación Windows local: loopback exclusivo, fallback de
puerto, readiness antes de navegador, instancia única por usuario y datos persistentes en
ProgramData. La política de logs rota diariamente y a 20 MB, conserva 30 días y excluye DB,
backups y auditoría SQLite. Ciclo UAC real instalación/reinstalación/desinstalación/
reinstalación: `VALIDO`; DB preservada. Build 0/0 y suite 855/855. Estado:
`P1-DESKTOP-INSTALLER = COMPLETADO`. El hosting futuro previsto continúa siendo servidor.
No se tocó producción.

## 21. P1-LOCAL-FIRST-RUN

Fecha: 2026-10-13. El cierre silencioso de la instalación 1.0.0 quedó diagnosticado: SQLite
era íntegra y compatible, pero no contenía usuarios; al faltar `Seguridad__AdminCorreo` y
`Seguridad__AdminClave`, el bootstrap lanzaba `CredencialesBootstrapFaltantesException` y el
proceso terminaba con `-532462766`. No era migración, corrupción, puerto, mutex, ruta ni ACL.

La versión 1.0.1 introduce `/setup` de un solo uso y estados Starting/SetupRequired/Ready/Failed.
El instalador ya no captura credenciales; el launcher abre `/setup` o `/` según `/estado`, y los
fallos pre-HTTP muestran un diálogo. Build 0/0; suite 862/862; publish aislado válido. SHA del
instalador: `2F7DE84936E0F240B9BD462E26BD5C92EA933F5566D41025078FD4B483350D9C`.
Queda pendiente el ciclo elevado final sobre la instalación real, que requiere que el usuario
elija e introduzca su credencial. ProgramData y producción no se borraron ni alteraron.

## 22. P1-ZKTECO-SDK

Fecha: 2026-10-13. El PULL SDK 2.2.0.220 sí ofrece parsing offline mediante
`ProcessBackupDataExt`, confinado a Infrastructure y cargado dinámicamente solo en modos
`Sdk`/`Comparar`. `Interno` sigue siendo el valor por defecto y la fuente autoritativa.

En `BKtransaction(5).dat` el SDK reconoce 981 filas, pero produce 0 marcaciones válidas;
el lector interno obtiene 851 válidas, 130 rechazadas y 127 IDs. Se generó evidencia en
`artifacts/bktransaction-validation/`. Redistribución: NO DETERMINADA; ninguna DLL se agregó
al repositorio, portable o instalador. Véanse `ZKTECO_SDK_INTEGRATION.md` y
`BKTRANSACTION_SDK_COMPARISON.md`. Build Release: 0 errores/0 warnings. Suite vigente:
Domain 398, Infrastructure 453 y Web 23; total 874/874. Producción y migraciones permanecen
intactas.

## 23. P1-DATA-PERIODOLABORAL — FASE 3C

Fecha: 2026-10-13. **COMPLETADA.** Los consumidores históricos críticos de vigencia,
empresa/planta, reportes y tareo resuelven ahora `PeriodoLaboral` y `AsignacionPlanta` mediante
una consulta central precargada por rango. Preingreso, postcese y gaps quedan `SinVinculo`; la
seguridad de tareos usa la planta histórica. No cambió la atribución física de marcaciones ni
se eliminaron los campos legacy.

Evidencia: 26 contratos PL3C dirigidos; build 0 errores/0 warnings; 900/900 pruebas verdes.
Sobre copia de la copia 3B: 775 pares comparados, 0 deltas, integridad `ok`, FK 0, `/vivo` 200 y
`/estado` 200 `LISTO`. Producción, saneamiento productivo, migración productiva, OP-03 y commit:
NO. Siguiente estado máximo: **LISTO PARA FASE 3D**.

La frase “LISTO PARA FASE 3D” se conserva como hito histórico y quedó **SUPERADA** por las fases
3D y 3E-A..D.

## 24. P1-DATA-PERIODOLABORAL — CIERRE FORMAL 3E-E

Fecha: 2026-10-15. **CERRADO.**

Fuente de verdad vigente:

```text
PeriodoLaboral       = autoridad histórica de vínculo y empresa
AsignacionPlanta     = autoridad histórica de planta dentro del vínculo
Trabajador.FechaIngreso / FechaCese / EmpresaId / PlantaId
                     = proyección actual y compatibilidad
```

Trazabilidad: 3B saneó y migró una copia controlada; 3C centralizó la resolución histórica y
migró reportes/tareo; 3D consolidó escrituras e invariantes; 3E-A clasificó 36 usos legacy;
3E-B corrigió los cuatro usos históricos incorrectos; 3E-C expuso el reingreso Application/UI;
3E-D cerró la regresión integrada con 937/937 y 108/108 dirigidos; 3E-E reconcilió los documentos
maestros. Los dossiers de cada fase se conservan como evidencia y no fueron reescritos.

Deuda residual explícita:

- los cuatro campos planos y su dual-write permanecen por compatibilidad de UI/importación;
- edición de ficha e importación masiva siguen acopladas a la corrección del vínculo abierto;
- SQLite no impide todos los solapamientos cerrados ni posee CHECK de fechas del periodo;
- `PeriodoLaboral` no tiene token propio de concurrencia; el índice parcial protege el abierto;
- P1-06 continúa para maestros históricos no cubiertos por vínculo/empresa/planta;
- cualquier operación productiva debe repetir inventario, dry-run, backup y validación sobre una
  copia contemporánea.

Producción: no tocada. OP-03: no ejecutado. Migración productiva: no. Commit: no.

## 25. P1-BUSINESS-RULES-FINAL

Fecha: 2026-10-16. **CERRADO DOCUMENTALMENTE.**

`BUSINESS_RULES_FINAL.md` es el catálogo autoritativo vigente: 95 reglas/estados explícitos
(86 reglas operativas y 9 enunciados históricos), con fuente, implementación, prueba, estado
y vínculo al ID legado. El corte de Fase 6.1 se conserva en `BUSINESS_RULES.md` marcado como
superado, sin borrar su evidencia. Se distinguen brechas reales de conductas ya resueltas.

No se modificaron código productivo, tests ni migraciones. Producción intacta; OP-03 no
ejecutado.

## 26. P1-REQUIREMENTS-TRACEABILITY-FINAL

Fecha: 2026-10-16. **CERRADO DOCUMENTALMENTE.**

`REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md` mapea las 86 reglas operativas vigentes/no
determinadas del catálogo final y separa 9 históricas/superadas. El corte original fue de 68
implementadas sin gap y 18 con acción concreta. Tras los cierres posteriores, incluidos
P2-AUS-006/007/008, P2-PMV-004 y P2-AUD-001 del 2026-10-23, el estado vigente es 79 sin gap y 7 con
acción: 0 bugs, 3 gaps funcionales, 1 deuda de modelo, 0 de test, 0 riesgos de seguridad,
1 riesgo de datos y 2 no determinados. Prioridad vigente: 1 P0, 2 P1 y 4 P2.

`REQUIREMENTS_TRACEABILITY.md` se conserva como corte histórico de Fase 6.1. Baseline vigente:
build Release 0 errores/0 warnings y 994/994 tests verdes. Producción y OP-03 permanecen intactos.

Validación de esta fase: build Release 0 errores/0 warnings. La única ejecución de la suite
quedó en `INCIDENCIA_RUNNER`: `vstest.console` no conectó con `testhost` tras 120 segundos;
no se repitió. Sin cambios funcionales, la baseline autoritativa continúa en 948/948.

## 27. P0-SEG-006-FINAL — ROTACIÓN MANUAL SEGURA

Fecha: 2026-10-19. **CERRADO.** Sobre la única instalación local autorizada en
`C:\ProgramData\ControlMarcacion` se creó por `/Usuarios` un coordinador sustituto con
credencial introducida manualmente, se validaron dos ciclos de logout/login, acceso a gestión de
usuarios y acceso operativo a marcaciones. Después se desactivó la cuenta comprometida mediante
el flujo Web normal.

Estado comprobado: cuenta antigua inactiva, bloqueo indefinido hasta `9999-12-31`,
`SecurityStamp` rotado respecto del respaldo y un único intento con la credencial histórica
rechazado. La cuenta sustituta volvió a iniciar sesión y conserva rol `COORDINADOR` y autonomía
administrativa. Identity queda con 1 cuenta activa y 1 inactiva.

Respaldo previo:
`C:\ProgramData\ControlMarcacion\backups\P0-SEG-006-before-rotation-20261019-090952.db`,
6,873,088 bytes, SHA-256
`AFD9DC13BDBE65894F805492763326F69FA4F5065BD728909913AA203F011E96`; permanece intacto como
evidencia/rollback. SQLite: integridad `ok`, FK 0. Escaneo posterior: 18 configuraciones, 4 ZIP y
7 logs, cero coincidencias; User Secrets 0 y variables bootstrap 0.

No se registraron contraseñas ni hashes Identity. No se modificó código, no se ejecutó OP-03,
no se aplicaron migraciones productivas, no se tocó producción y no se creó commit. La historia
Git y las copias offline conservan exposición histórica contenida; no deben reutilizarse para
despliegue. Evidencia completa: `P0_SEG006_CREDENTIAL_ROTATION.md`.

## 28. P0-FER-002 + P0-FER-007 — CIERRE LEGAL/ECONÓMICO

Fecha: 2026-10-19. **CERRADOS.** La liquidación distingue derechos de remuneración diaria de
minutos físicamente trabajados. Un feriado trabajado expone N minutos de base y N de recargo al
100 % sobre una única base física; el parcial usa los minutos reales.

El 1 de mayo coincidente con descanso semanal/programado emite dos derechos diarios si no se
trabaja y, si se trabaja, añade base + recargo sobre N minutos únicos. Se retiró el bloqueo de
liquidación que protegía la fórmula incompleta. Domingo y descanso por turno también conservan
la coincidencia especial; los cruces nocturnos quedaron protegidos por contratos directos.

Validación: build Release 0 errores/0 advertencias; regresión dirigida 15/15; suite completa
958/958, 0 fallos y 0 omitidos. Sin migración ni cambio de esquema. Producción intacta, OP-03
no ejecutado y sin commit.

FER-003 quedó cerrado el 2026-10-20 mediante `DescansoSustitutorio`: relación explícita entre
feriado trabajado y día completo posterior, alta/anulación auditada, unicidad y concurrencia,
cero minutos sin incidencias en el día concedido y supresión del adicional general sin borrar
minutos físicos. Migración aditiva `20261020143105_DescansoSustitutorioFeriado`, no aplicada a
producción. Baseline: 975/975 verdes, build 0 errores/0 warnings. Evidencia:
`P1_FER003_DESCANSO_SUSTITUTORIO.md`.

Deuda separada: FER-005 (día no laborable compensable).

## 29. P1-FER-001 — REVALIDACIÓN DE FERIADO NO TRABAJADO

Fecha: 2026-10-22. **CERRADO / SIN GAP VIGENTE.**

El contexto de entrada clasificaba FER-001 como `GAP_FUNCIONAL/P1`, pero esa fotografía quedó
superada por P0-FER-002/007. La fuente autoritativa, la matriz vigente, el motor, la liquidación
y el contrato SQLite directo coinciden: el feriado nacional no trabajado conserva un derecho
ordinario separado y genera cero minutos físicos, HE100 y concepto de trabajo en feriado.

No se modificaron código productivo, tests ni migraciones y no se añadieron contratos
duplicados. Regresión dirigida: 15/15 verdes. Build Release: 0 errores/0 warnings. Suite
completa: 975/975 verdes, 0 fallos y 0 omitidos. Evidencia completa: `P1_FER001.md`.

## 30. P1-FER-004 — CIERRE DE DEUDA DE TEST

Fecha: 2026-10-22. **CERRADO.**

FER-004 ya estaba implementado mediante clasificación por fecha de inicio de jornada, pero
las pruebas de dominio recibían `EsFeriado` ya resuelto y no demostraban la frontera completa
catálogo→servicio→tareo. Se agregaron `FER004-01` y `FER004-02` sobre SQLite real: normal
19:00→feriado 05:22 conserva el día normal; feriado 19:00→D+1 05:22 conserva `Feriado`.

Código productivo y migraciones: sin cambios. Regresión dirigida: 7/7 verdes. Build Release:
0 errores/0 warnings. Suite completa: 977/977 verdes, 0 fallos y 0 omitidos. Evidencia:
`P1_FER004_TEST_DEBT.md`.

## 31. P2-AUS-007 — APROBACIÓN ATÓMICA DE AUSENCIAS

Fecha: 2026-10-23. **CERRADO.**

Se reprodujo sobre SQLite real que una solicitud de tres días con choque en el día intermedio
creaba las otras dos justificaciones y quedaba `Aprobada`. `AUS007-01` falló antes del arreglo
porque el servicio no lanzó la excepción de negocio esperada.

`ServicioSolicitudes.AprobarAsync` ahora prevalida todo el rango antes de agregar filas. Periodo
cerrado, día sin periodo o justificación preexistente conservan la cabecera `Pendiente` y generan
cero filas nuevas. En el camino válido, cabecera y detalle se guardan juntos; el índice único
protege carreras de justificación y cada periodo abierto afirma su versión frente a cierres
concurrentes.

Contratos `AUS007-01..03`: 3/3 verdes. Regresión inmediata: 123/123. Suite completa: 980/980,
0 fallos y 0 omitidos. Build Release: 0 errores/0 warnings. Sin migración, datos productivos,
OP-03 ni commit. Evidencia completa: `P2_AUS007_BUG.md`.

## 32. P2-AUS-008 — PRESERVACIÓN DE EVIDENCIA AL ANULAR

Fecha: 2026-10-23. **CERRADO.**

Se reprodujo sobre SQLite real que `ServicioSolicitudes.AnularAsync` eliminaba físicamente las
justificaciones derivadas. Tres de cuatro contratos iniciales fallaron por ausencia de las filas.

La anulación ahora conserva cada `Justificacion` como `Cancelada`, registra actor, fecha y motivo,
incrementa `Version` y afirma concurrentemente todos los periodos afectados. Cabecera, detalle y
guardas se confirman en un único guardado. El índice parcial y la aprobación ignoran historia
terminal, y la bandeja prioriza una justificación vigente cuando convive con una cancelada.

La base canónica del workspace se auditó mediante copia completa `.db/.db-wal/.db-shm`: integridad
`ok`, FK 0, 0 solicitudes y 0 justificaciones; no hubo filas que sanear ni datos fuente modificados.
No se infirió evidencia perdida ni se auditó producción.

Contratos `AUS008-01..06`: 6/6 verdes. Suite Infrastructure: 520/520. Suite completa: 986/986,
0 fallos y 0 omitidos. Build Release: 0 errores/0 warnings. Sin migración, saneamiento, producción,
OP-03 ni commit. Evidencia completa: `P2_AUS008_DATA_RISK.md`.

## 33. P2-AUS-006 — CIERRE DE DEUDA DE TEST DE SOLAPAMIENTOS

Fecha: 2026-10-23. **CERRADO.**

La ruta productiva ya aplicaba la regla vigente: para el mismo trabajador, una solicitud
`Pendiente` o `Aprobada` reserva inclusivamente su rango; `Rechazada` y `Anulada` lo liberan.
La deuda era exclusivamente la ausencia de un contrato integrado contra SQLite que demostrara
esa matriz de estados y la frontera temporal.

Se agregaron `AUS006-01/02` en `SolapamientoSolicitudesTests`: cuatro casos prueban los estados
y un quinto demuestra que compartir el día extremo se rechaza mientras el día siguiente se
admite. No hubo cambios en código productivo ni migraciones.

Contratos AUS-006: 5/5 casos verdes. Regresión dirigida de solicitudes, ausencias y
justificaciones: 127/127. Suite completa: 991/991, 0 fallos y 0 omitidos. Build Release:
0 errores/0 warnings. Producción intacta; OP-03 y commit no ejecutados. Evidencia completa:
`P2_AUS006_TEST_DEBT.md`.

## 34. P2-PMV-004 — CIERRE DE DEUDA DE TEST DEL MÓDULO PROVISIONAL

Fecha: 2026-10-23. **CERRADO.**

La implementación ya mantenía PMV fuera del motor oficial: el importador escribe únicamente
`prov_importacion`, `prov_marcacion` y `prov_error_importacion`; la consulta produce DTOs en
memoria y el exportador genera las 13 columnas provisionales sin conceptos de planilla. Faltaba
un único contrato negativo que atravesara las tres operaciones y observara ambos modelos.

`PMV004-01` ejecuta importación, consulta y exportación reales sobre SQLite. Verifica las filas
`prov_*`, ausencia de cambios en importación oficial, errores oficiales, `MarcacionCruda`,
`TareoDiario` y `PeriodoLaboral`, y ausencia de tardanza, salida anticipada, HE, nocturnidad,
incidencias, feriados o pago en la salida.

Contrato PMV-004: 1/1 verde. Regresión PMV completa: 42/42. Suite completa: 992/992,
0 fallos y 0 omitidos. Build Release: 0 errores/0 warnings. Sin cambios productivos ni
migraciones; producción intacta, OP-03 y commit no ejecutados. Evidencia completa:
`P2_PMV004_TEST_DEBT.md`.

## 35. P2-AUD-001 — CIERRE DE DEUDA DE TEST TRANSVERSAL DE AUDITORÍA

Fecha: 2026-10-23. **CERRADO.**

La infraestructura existente ya auditaba por interceptor todas las entidades no excluidas y
registraba el recálculo de tareo mediante el evento agregado 4200. Faltaba un contrato SQLite
que demostrara conjuntamente la inclusión de una operación sensible, la exclusión del dato
derivado y la persistencia de actor, identidad, operación, timestamp y cambios.

`AUD001-01` usa otorgamiento y anulación de descanso sustitutorio para verificar inserción,
baja lógica, estado anterior/nuevo y motivo exacto. `AUD001-02` confirma que el recálculo crea
tareo sin filas de auditoría `tareo_diario` y emite un único log agregado. La atomicidad negativa
se traza al contrato existente `IMP_AT_01`, que prueba que un fallo no deja auditoría falsa.

Contratos AUD-001: 2/2 verdes. Regresión dirigida total: 4/4. Suite completa: 994/994,
0 fallos y 0 omitidos. Build Release: 0 errores/0 warnings. Sin cambios productivos ni
migraciones; producción intacta, OP-03 y commit no ejecutados. Evidencia completa:
`P2_AUD001_TEST_DEBT.md`.

## 36. R-QA-CAT-001 — ID DE ALTA DE CATÁLOGOS

Fecha: 2026-10-28. **CERRADO.**

`QA-E2E-002` reprodujo que una planta se persistía con clave SQLite válida, pero
`ServicioCatalogos.GuardarAsync` devolvía `0`. Los auxiliares copiaban `Entidad.Id` antes del
único `SaveChangesAsync`; la clave se materializaba después en la entidad seguida, no en el
`long` ya capturado.

Los auxiliares de puesto, planta, área, sección y tipo de documento ahora devuelven la entidad.
El servicio conserva un solo guardado y retorna `entidad.Id` únicamente después de persistir.
No hay consulta por nombre/código, guardado adicional, migración ni cambio transaccional.

Contratos `QACAT-001..006`: 9/9 ejecuciones SQLite verdes. Regresión dirigida: 112/112.
Suite completa: 1,037/1,037, 0 fallos y 0 omitidos. Build Release: 0 errores/0 warnings.
Producción intacta; OP-03 y commit no ejecutados. Evidencia: `R_QA_CAT001.md`.

## 37. CAT-DYN-001 — IMPORTACIÓN INTELIGENTE DE UNIDADES, EQUIPOS Y MARCADORES

Fecha: 2026-10-28. **COMPLETADO.**

La entidad técnica `Planta`, la tabla `planta`, `PlantaId`, `UsuarioPlanta` y la historia
laboral se preservan. La interfaz visible utiliza `Unidad/Unidades`. La plantilla reconoce
`Unidad`, conserva `Planta` como alias y rechaza contradicciones entre ambas.

La carga concilia `Empresa → Unidad → Equipo → Marcador → Trabajador → Asignación`, propone
altas solo con datos suficientes y muestra estados existentes, nuevos, ambiguos, inválidos,
no aplicables o pendientes. La previsualización revierte su transacción y la confirmación
revalida una huella conjunta. Cada fila dispone de savepoint anterior a cualquier alta, por
lo que una fila rechazada no deja unidad, equipo, marcador ni puesto huérfano. No se amplían
alcances `UsuarioPlanta`.

Contratos `CATDYN-01..30`: 30/30 verdes sobre SQLite. Regresión dirigida de carga masiva:
80/80. Suite completa: 1,067/1,067, 0 fallos y 0 omitidos. Build Release: 0 errores y
0 advertencias. Sin migraciones; producción intacta; OP-03 y commit no ejecutados.
Evidencia: `CAT_DYN_001.md`.

## 38. CAT-DYN-003 — PREPARACIÓN AUTOMÁTICA PARA TAREO

Fecha: 2026-10-29. **COMPLETADO EN CÓDIGO Y PRUEBAS.**

Se incorporó `IValidadorPreparacionTareo` como diagnóstico histórico, explicable y de solo
lectura. Distingue `LISTO`, `INCOMPLETO` y `CONFLICTO` por fecha e intervalo con precedencia de
conflicto, y valida vínculo, empresa, unidad, equipo, catálogo/asignación de marcador, turno,
descanso, periodo, consistencia histórica y alcance efectivo por unidad.

La pantalla reutilizable está disponible desde Personal, después de una carga masiva confirmada
y antes del recálculo de tareo. No lee marcas ni tareos, no escribe, no autorrepara, no bloquea
globalmente y no altera el motor económico. El catálogo de unidades y los trabajadores
mostrados respetan `IProveedorAlcancePlantas`.

Contratos `CATREADY-01..20`: 20/20 verdes sobre SQLite real. Suite completa: 1,087/1,087,
0 fallos y 0 omitidos. Build Release: 0 errores/0 advertencias. Sin migraciones; producción
intacta; OP-03, despliegue y commit no ejecutados. Evidencia: `CAT_DYN_003.md`.

## 39. CAT-DYN-004 — IMPORTACIÓN DE TURNOS Y DESCANSOS

Fecha: 2026-11-02. **IMPLEMENTADO Y VALIDADO EN PRUEBAS DIRIGIDAS.**

Importador Excel con previsualización de solo lectura, expansión de intervalos, resolución
histórica, conciliación de turnos, descansos explícitos, periodo cerrado, token revalidado,
transacción, idempotencia, autorización por unidad y reevaluación CATREADY.

Reutiliza `AsignacionTurno`, `Cuadrilla`, `ProgramacionCuadrilla` y `TramoRotacion`; no crea
otra autoridad ni migración. No lee ni modifica marcaciones, tareo o PMV. Contratos
`CATTURNN-01..30` y una secuencia integral con cinco trabajadores: 11/11 verdes. Suite completa:
1.113/1.113, 0 fallos y 0 omitidos. Build Release: 0 errores/0 advertencias. Evidencia:
`CAT_DYN_004.md`.

