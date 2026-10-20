# Catálogo maestro de reglas de negocio — corte histórico Fase 6.1

> **SUPERADO como catálogo autoritativo el 2026-10-16.** Se conserva íntegro para trazabilidad
> de los IDs `RN-*` y `PMV-RN-*`. La fuente vigente es
> [`BUSINESS_RULES_FINAL.md`](BUSINESS_RULES_FINAL.md).

Fecha de corte: 2026-10-06  
Fase: 6.1 — reconciliación maestra de requisitos  
Estado: 116 reglas reconciliadas; `ACCEPTANCE_RULES.md` prevalece para comportamiento esperado y `REQUIREMENTS_TRACEABILITY.md` registra diferencias.

## Convenciones

- **Evidencia**: `CÓDIGO`, `BD`, `TEST`, `NEGOCIO` o `INFERIDA`. Una fila puede combinar fuentes.
- **Config.**: `Sí` cuando el valor procede de tabla/catálogo/turno; `No` cuando está codificado; `Parcial` cuando una parte se configura y otra permanece fija.
- **Cobertura**: `TESTEADA` significa que existe una prueba declarada que ejercita directamente la regla; `PARCIAL`, que solo se prueba una parte o por integración; `SIN TEST`, que no se localizó una prueba directa. No implica que la suite haya pasado en este corte.
- **Fuente de verdad** identifica el dato que manda hoy, no necesariamente el que debería mandar según negocio.

## A. Ingesta ZKTeco, origen e inmutabilidad

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-001 | El formato no se decide por extensión: el contenido despacha a lector TXT o respaldo binario. | CÓDIGO+TEST | No | `LectorMarcadorAutomatico` | TESTEADA | Bajo |
| RN-002 | Planta y equipo son obligatorios; el equipo debe pertenecer a la planta declarada. | CÓDIGO+TEST | Sí | `equipo_marcador.planta_id` | TESTEADA | Alto si se elude |
| RN-003 | El contenido completo se identifica por SHA-256 y puede advertirse una carga previa. | CÓDIGO+TEST | No | `importacion_marcador.hash_contenido` | TESTEADA | Medio: no es unique físico |
| RN-004 | Un archivo vigente ya cargado en otro equipo se rechaza; una carga anulada no bloquea cargarlo correctamente. | CÓDIGO+TEST | No | hash + estado de importación | TESTEADA | Alto |
| RN-005 | Serie de contenido y nombre deben coincidir; la serie de contenido prevalece y puede enseñarse al equipo si este no tenía una. | CÓDIGO+TEST | Parcial | archivo + `equipo_marcador.serie` | TESTEADA | Alto |
| RN-006 | Si ≥5 marcas y ≥50 % coinciden al segundo con otro equipo en los últimos 92 días, se rechaza el origen declarado. | CÓDIGO+TEST | No | `ServicioImportacion` | TESTEADA | Medio: umbrales rígidos |
| RN-007 | Líneas válidas se conservan aunque el ID todavía no tenga trabajador; la carga solo emite un aviso estructurado. | CÓDIGO+TEST | No | `marcacion_cruda` + `error_importacion` | TESTEADA | Medio |
| RN-008 | Líneas ilegibles se registran con número, contenido recortado y motivo; no impiden conservar las válidas. | CÓDIGO+TEST | Parcial | lectores + `error_importacion` | TESTEADA | Bajo |
| RN-009 | En el lote se colapsa la misma `(ID, instante)`; en BD la unicidad real es `(equipo, ID, instante)`. | CÓDIGO+BD+TEST | No | índice unique de `marcacion_cruda` | TESTEADA | Bajo |
| RN-010 | La marcación cruda es inmutable. Toda corrección vive separada y conserva original, corrección, motivo, usuario, fecha, validación y auditoría; REG-01..04 caracterizan el flujo actual. | CÓDIGO+BD+NEGOCIO+TEST | No | requisito RQ-007 + `marcacion_cruda` | TESTEADA | Alto |
| RN-011 | Anular conserva cabecera, marcas y errores, pero excluye sus marcas del cálculo; reactivar revierte el efecto. | CÓDIGO+TEST | No | `importacion_marcador.anulada` | TESTEADA | Alto |
| RN-012 | La importación principal guarda cabecera/errores y después marcas en dos `SaveChanges` sin transacción envolvente. | CÓDIGO | No | `ServicioImportacion.ImportarAsync` | SIN TEST | Alto: cabecera huérfana lógica ante fallo intermedio |
| RN-013 | Cobertura se evalúa por día y aparato esperado, no por conteo global de marcas. | CÓDIGO+TEST | Sí | `CoberturaDeCarga` | TESTEADA | Alto |
| RN-014 | Un aparato se espera desde su primera marca vigente o, si nunca tuvo marcas, desde su alta; inactivos y días futuros no faltan. | CÓDIGO+TEST | Sí | `AparatoEsperado` | TESTEADA | Medio |

## B. Atribución, vínculo y turno histórico

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-015 | El dueño de una marca se resuelve por `(equipo, ID, fecha)` con vigencias inclusivas. | CÓDIGO+TEST | Sí | `asignacion_marcador` | TESTEADA | Alto |
| RN-016 | `0014` y `14` son identificadores distintos en el TXT; no se normalizan los ceros. | CÓDIGO+TEST | No | valor crudo del archivo | TESTEADA | Medio |
| RN-017 | La atribución ocurre al recalcular, no al importar; corregir el padrón mueve retrospectivamente las marcas. | CÓDIGO+TEST | No | `AtribucionMarcaciones` | TESTEADA | Alto |
| RN-018 | Una marca sin vigencia atribuible no pertenece a nadie y no alimenta un tareo. | CÓDIGO+TEST | No | `asignacion_marcador` | TESTEADA | Alto |
| RN-019 | La población del recálculo exige ficha activa y solapamiento de `FechaIngreso/FechaCese` con el periodo. | CÓDIGO+TEST | Sí | campos planos de `trabajador` | PARCIAL | Alto: ignora `periodo_laboral` |
| RN-020 | Cada día vuelve a evaluar el vínculo; fuera de él produce `SinVinculo` y cero minutos. | CÓDIGO+TEST | Sí | `trabajador.FechaIngreso/FechaCese` | TESTEADA | Alto |
| RN-021 | Una asignación de turno representa turno fijo o cuadrilla; si hubiera solapamiento, gana la de inicio más reciente. | CÓDIGO+BD+TEST | Sí | `asignacion_turno` | TESTEADA | Alto |
| RN-022 | Con turno fijo se resuelve ese turno; con cuadrilla se exige programación activa y vigente. | CÓDIGO+TEST | Sí | asignación + programación | TESTEADA | Alto |
| RN-023 | Sin programación vigente de cuadrilla, el día queda `SinHorario` con explicación; no se inventa turno. | CÓDIGO+TEST | Sí | `programacion_cuadrilla` | TESTEADA | Alto |
| RN-024 | Comportamiento actual: módulo sobre tramos desde el ancla. Requisito: admitir programaciones variables, cambios futuros/manuales y no asumir repetición rígida; 14+1+7 es solo patrón. | CÓDIGO+TEST+NEGOCIO | Sí | programación fechada + RQ-004 | TESTEADA | Alto |
| RN-025 | El descanso rotativo es un tramo explícito de un día, sin turno ficticio, con `Tipo` y FK nullable protegidos por dominio, aplicación y SQLite. | CÓDIGO+BD+NEGOCIO+TEST | Sí | RQ-004 | TESTEADA | Bajo |
| RN-026 | El horario histórico se toma de la `VersionTurno` activa y vigente para la fecha. | CÓDIGO+TEST | Sí | `version_turno` | TESTEADA | Alto |
| RN-027 | Si ninguna versión cubre la fecha, se usa el turno actual como fallback. | CÓDIGO | No | `turno` actual | SIN TEST | Alto para fechas prehistóricas o huecos |
| RN-028 | Los turnos candidatos para detectar discrepancia son los activos habilitados en la planta; uno sin plantas aplica a todas. | CÓDIGO+TEST | Sí | `turno_planta` + `turno.activo` | PARCIAL | Medio |

## C. Ventana de jornada y selección de marcas

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-029 | La jornada se imputa a la fecha en que empieza el turno, aunque termine al día siguiente. La atribución no impide segmentar la clasificación económica al entrar al domingo. | CÓDIGO+TEST+NEGOCIO | No | `Turno.CruzaMedianoche` + frontera domingo 00:00 | TESTEADA | Alto |
| RN-030 | La ventana abre `MinutosAnticipacionEntrada` antes del inicio. | CÓDIGO+TEST | Sí | versión/turno | PARCIAL | Medio |
| RN-031 | La ventana cierra seis horas después del fin teórico; el margen es fijo. | CÓDIGO | No | constante `MargenSalida` | SIN TEST | Alto: puede capturar o excluir marcas impropias |
| RN-032 | Una marca solo puede ser reclamada por una jornada; se excluye todo lo no posterior al fin de la jornada anterior. | CÓDIGO+TEST | No | orquestador de recálculo | TESTEADA | Alto |
| RN-033 | Dentro de la ventana, la primera marca cruda es entrada y la última salida. | CÓDIGO+TEST | No | `CalculadoraTareo` | TESTEADA | Alto |
| RN-034 | Eventos separados por menos de dos minutos se consolidan para decidir paridad; los extremos crudos siguen determinando horas. | CÓDIGO+TEST | No | constante de rebote | TESTEADA | Medio |
| RN-035 | Marcas intermedias no cambian el cómputo y se declaran en la observación. | CÓDIGO+TEST | No | `CalculadoraTareo` | TESTEADA | Medio |
| RN-036 | Marcas naturales del día que ninguna jornada reclamó cuentan como fuera de ventana. | CÓDIGO+TEST | No | `SinReclamar` | TESTEADA | Medio |
| RN-097 | En descanso programado, las jornadas con turno reclaman primero. La primera marca válida residual debe caer en D; la salida puede caer en D+1 solo antes de la siguiente jornada y sin reutilizar marcas. | CÓDIGO+NEGOCIO+TEST | No | BD01-D01/D02 | TESTEADA | Bajo |
| RN-098 | Una marca residual o una frontera ambigua produce cero minutos automáticos y requiere validación auditada; no se inventan extremos, duración ni HE. | CÓDIGO+NEGOCIO+TEST | No | BD01-D03 | TESTEADA | Bajo |
| RN-099 | Con dos o más marcas inequívocas en descanso se usan los extremos brutos; las interiores no se emparejan ni generan descuento automático de refrigerio. Todo intervalo reconocido es HE100. | CÓDIGO+NEGOCIO+TEST | No | BD01-D01..D03 | TESTEADA | Bajo |
| RN-100 | La hoja de HE del coordinador puede ser evidencia/autorización, pero no es frontera temporal mientras no contenga programación horaria explícita y confiable. | NEGOCIO CONFIRMADO | No | BD-01 | ESPECIFICADA; SIN IMPLEMENTAR | Medio |
| RN-101 | Feriado ordinario + DescansoProgramado sin trabajo es un solo día físico: cero minutos, cero HE100 y ningún descanso duplicado. | CÓDIGO+NEGOCIO+TEST | No | BD02-D01 | TESTEADA | Bajo |
| RN-102 | Feriado ordinario + DescansoProgramado trabajado usa N minutos físicos únicos y se clasifica como FeriadoTrabajado; no agrega DescansoSemanalTrabajado sobre N. | CÓDIGO+NEGOCIO+TEST | No | BD02-D02/D03/D05 | TESTEADA | Bajo |
| RN-103 | El 1 de mayo coincidente con descanso conserva minutos físicos únicos y clasificación especial; desde P0-FER-007 emite dos derechos diarios y, si trabaja, base + recargo 100 % sin bloquear liquidación. | CÓDIGO+NEGOCIO+TEST | No | BD02-D06/D07 | TESTEADA E2E | Bajo; sustitutorio separado en FER-003 |
| RN-104 | La jornada que cruza hacia feriado conserva su fecha de inicio; la iniciada en feriado mantiene esa clasificación hasta D+1. | NEGOCIO CONFIRMADO | No | BD02-D08/D09 + FER-NAC-05/06 | ESPECIFICADA; SIN IMPLEMENTAR DIRECTAMENTE | Alto |

## D. Cálculo diario, incidencias y nocturnidad

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-037 | Día laborable sin marcas y sin archivo del aparato: `PendienteValidacion`, no falta. | CÓDIGO+TEST | Sí | cobertura por aparato | TESTEADA | Alto |
| RN-038 | Día laborable sin marcas con cobertura: `Falta`. | CÓDIGO+TEST | No | cálculo | TESTEADA | Alto |
| RN-039 | Una sola marca efectiva: `Incompleto`, cero horas; se clasifica como entrada o salida por cercanía al extremo teórico. | CÓDIGO+TEST | No | cálculo | TESTEADA | Alto |
| RN-040 | Sin turno: `SinHorario`; sin vínculo: `SinVinculo`; ambos con cero minutos. | CÓDIGO+TEST | No | cálculo | TESTEADA | Alto |
| RN-041 | Comportamiento actual distingue descanso/feriado; requisito prevalente: domingo es descanso semanal y, si se trabaja, todo minuto es HE al 100 %, sin descanso sustitutorio automático. | CÓDIGO+TEST+NEGOCIO | Sí | RQ-003 | TESTEADA | Alto: DOM-01..03 fallan |
| RN-042 | Si feriado y descanso coinciden, el feriado prevalece. | CÓDIGO+TEST | No | cálculo | TESTEADA | Medio |
| RN-043 | Llegar antes no genera tiempo pagable: el cómputo inicia en el máximo entre entrada e inicio teórico. | CÓDIGO+TEST | No | cálculo | TESTEADA | Alto |
| RN-044 | Salir dentro de la gracia recorta al fin teórico; excederla cuenta todo desde el fin, no solo el exceso. | CÓDIGO+TEST | Sí | `MinutosGraciaSalida` | TESTEADA | Alto |
| RN-045 | Los minutos trabajados son duración computable menos refrigerio completo; no hay prorrateo ni tope al descuento. | CÓDIGO+TEST | Sí | `MinutosRefrigerio` | TESTEADA | Alto |
| RN-046 | L–V: ordinario hasta el **fin teórico del turno** (DIU 17:22, NOC 05:22 +1), por reloj; la tardanza no corre el corte. `MinutosJornadaOrdinaria` ya no interviene en días laborables L–V (R-P0-03.1). | CÓDIGO+TEST+NEGOCIO | Sí | `Turno.HoraFin` / `VersionTurno` | TESTEADA | Medio: la semilla DIU/NOC sigue en 19:00/07:00 hasta su reconfiguración operativa |
| RN-047 | L–V: exceso menor a 15 min sobre el fin teórico = 0 HE; desde 15 min se cuenta entero. Viernes→sábado conserva esa jornada regular. Sábado: todo minuto anterior a domingo 00:00 es HE25/35; desde domingo 00:00 es HE100 sin umbral. | CÓDIGO+TEST+NEGOCIO | Parcial (umbral 15 y 120 constantes) | RQ-001/002/003 | TESTEADA | Bajo: HE-DIU/HE-NOC/HE-SAB/DOM/NOC-WE verdes |
| RN-048 | Tardanza de planilla con tolerancia 0, minuto a minuto desde el inicio teórico (DIU y NOC). `MinutosTolerancia` no interviene. | CÓDIGO+TEST+NEGOCIO | No | inicio teórico | TESTEADA | Bajo: TAR-00..04 y TAR-NOC verdes |
| RN-049 | Salida anticipada de planilla con tolerancia 0, minuto a minuto hasta el fin teórico (`FinTurno`, con cruce de medianoche). `MinutosAnticipacionSalida` no interviene. | CÓDIGO+TEST+NEGOCIO | No | `FinTurno` | TESTEADA | Bajo: SA-00..04 y SA-NOC verdes |
| RN-050 | La salida anticipada no cambia por sí sola `Estado`; el estado ordinario es puntual o tardanza. | CÓDIGO | No | cálculo | SIN TEST | Medio |
| RN-051 | La franja nocturna es 22:00–06:00, fija y repetida por fecha. | CÓDIGO+TEST+NEGOCIO | No | cálculo | TESTEADA | Alto |
| RN-052 | Los minutos nocturnos se calculan sobre intervalo bruto computable y no descuentan el refrigerio. | CÓDIGO+TEST | No | cálculo | PARCIAL | Alto: posible doble base |
| RN-053 | En L–V el sobretiempo es la cola posterior al fin; en sábado empieza con la jornada, primero HE25 y luego HE35 hasta domingo 00:00. Sus subconjuntos nocturnos se calculan por solape; el tramo dominical restante es HE100. | CÓDIGO+TEST+NEGOCIO | No | cálculo | TESTEADA | Alto |
| RN-054 | En día no laborable trabajado no se calcula tardanza y el resultado base queda `Puntual`. | CÓDIGO+TEST | No | cálculo | TESTEADA | Medio |
| RN-055 | Solo se persiste una incidencia por día, en prioridad: justificado ninguna; sin cobertura; fuera de turno; extremo faltante; turno discrepante. La mera existencia de una justificación suprime hoy la incidencia aunque el resultado siga incompleto. | CÓDIGO+TEST | No | `ConIncidencia` | PARCIAL | Alto: INC-01 falla |
| RN-056 | El turno discrepante solo se señala con ≥2 marcas, candidato ≤90 min de desvío y mejora ≥120 min; no corrige minutos ni turno. | CÓDIGO+TEST | No | `DetectorTurnoDiscrepante` | TESTEADA | Medio |

## E. Solicitudes, justificaciones y regularización

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-057 | Solo una justificación `Aprobada` modifica el tareo. | CÓDIGO+TEST | No | `justificacion.estado` | TESTEADA | Alto |
| RN-058 | Solo puede existir una justificación vigente por trabajador y día; rechazadas/canceladas liberan la fecha. | CÓDIGO+BD+TEST | No | índice filtrado + servicio | TESTEADA | Alto |
| RN-059 | Una ausencia aprobada queda `AusenciaJustificada`; paga jornada ordinaria solo si el tipo computa y no es sin goce. | CÓDIGO+TEST | Sí | `tipo_ausencia` | TESTEADA | Alto |
| RN-060 | Justificar ausencia nunca genera extras; si paga, acredita exactamente la jornada ordinaria configurada. | CÓDIGO+TEST | Sí | turno + tipo ausencia | TESTEADA | Alto |
| RN-061 | Una justificación aprobada puede perdonar total/parcialmente tardanza o salida; antes de justificar, ambas se calculan minuto a minuto con tolerancia 0 y sin alterar marcas crudas. | CÓDIGO+TEST+NEGOCIO | Parcial | RQ-005/006/007 | TESTEADA | Alto |
| RN-062 | Permiso parcial solo nace de solicitud por horas y perdona el solape con tardanza/salida; no inventa trabajo. | CÓDIGO+TEST | Sí | solicitud + cálculo | TESTEADA | Alto |
| RN-063 | Regularización acepta horas verificadas, conserva extremos crudos y registra corrección, motivo, actores, fechas, validación y auditoría separadas. REG-01..04 pasan. | CÓDIGO+TEST+NEGOCIO | No | RQ-007 + justificación + marcador | TESTEADA | Alto por sensibilidad, contrato caracterizado |
| RN-064 | Una regularización puede nacer solicitada o aprobada; dos resoluciones se protegen por versión optimista. | CÓDIGO+BD+TEST | No | `justificacion.version` | TESTEADA | Alto |
| RN-065 | Solo quien registró una regularización solicitada puede cancelarla; rechazo exige motivo. | CÓDIGO+TEST | No | justificación | TESTEADA | Medio |
| RN-066 | Toda escritura de justificación/reintegro y toda resolución que afecte el mes exige periodo abierto. | CÓDIGO+TEST | No | `periodo.estado` + concurrencia | TESTEADA | Alto |
| RN-067 | Solicitudes nuevas no pueden solaparse con otra pendiente/aprobada del mismo trabajador. | CÓDIGO | No | `solicitud_ausencia` | PARCIAL | Alto |
| RN-068 | Solo una solicitud pendiente puede editarse, aprobarse o rechazarse; una aprobada se corrige anulando y recreando. | CÓDIGO | No | `solicitud_ausencia.estado` | PARCIAL | Medio |
| RN-069 | Si el tipo exige sustento, se exige al aprobar; el detalle clínico solo se conserva en tipos subsidiados. | CÓDIGO+TEST | Sí | `tipo_ausencia` | TESTEADA | Alto |
| RN-070 | Aprobar genera una justificación por día solo cuando existe periodo abierto y no hay otra; aun con omisiones, la solicitud queda aprobada y devuelve el detalle. | CÓDIGO | No | `ServicioSolicitudes.AprobarAsync` | SIN TEST | Alto: aprobación parcial |
| RN-071 | Permiso por horas debe ser de un solo día y tener fin posterior al inicio. | CÓDIGO+TEST | Sí | solicitud/tipo | TESTEADA | Medio |
| RN-072 | Límites de ausencias cuentan días inclusivos; pendientes y aprobadas consumen cupo; vacaciones separan periodo y subtipo. | CÓDIGO+TEST | Sí | `tipo_ausencia` + solicitudes | TESTEADA | Alto |
| RN-073 | Anular solicitud aprobada elimina sus justificaciones solo si todos sus periodos siguen abiertos. | CÓDIGO | No | servicio solicitudes | PARCIAL | Alto: borrado físico de evidencia derivada |

## F. Reintegros, reportes y Excel

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-074 | Hay una fila de reintegro por trabajador/periodo; guardar actualiza o inserta y todos los valores deben ser no negativos. | CÓDIGO+BD+TEST | No | `reintegro_tareo` | TESTEADA | Alto |
| RN-075 | Los 18 reintegros son manuales y no participan del cálculo diario; se copian al bloque final o salen en cero. | CÓDIGO+TEST | No | `reintegro_tareo` | TESTEADA | Alto |
| RN-076 | Resumen y excepciones aceptan periodo, mes, día o rango; tareo final exige periodo. | CÓDIGO+TEST | No | `ResolutorRangoReporte` | TESTEADA | Medio |
| RN-077 | Excepciones incluye Falta, Tardanza, AusenciaJustificada, Incompleto y PendienteValidacion. | CÓDIGO | No | lista `EstadosDeExcepcion` | PARCIAL | Medio |
| RN-078 | Requisito: todo domingo trabajado va al 100 % y queda disjunto de ordinario/25/35; feriado mantiene su tratamiento propio. La liquidación actual puede sumar la base dos veces. | CÓDIGO+TEST+NEGOCIO | No | RQ-003 + `TipoDiaNoLaborable` | TESTEADA | Alto: LIQ-04 falla |
| RN-079 | Nocturnidad es una dimensión superpuesta a ordinario/extra/100 %, no una categoría excluyente. | CÓDIGO+NEGOCIO | No | `TareoDiario` | PARCIAL | Alto: el archivo modelo sugiere otra lectura |
| RN-080 | Movilidad = días con entrada y salida reales y estado Puntual/Tardanza × tarifa vigente al último día del alcance/periodo. | CÓDIGO+TEST | Sí | tareo + parámetro legal | TESTEADA | Alto |
| RN-081 | Asignación familiar = RMV × porcentaje vigente al fin del periodo, monto mensual fijo si `TieneHijos`, sin prorrateo. | CÓDIGO+TEST+NEGOCIO | Sí | trabajador + parámetros | TESTEADA | Alto |
| RN-082 | Bono nocturno diario = `max(0, RMV×(1+tasa)-sueldo)/días del mes`, redondeado a 2 decimales; se suma una vez por cada día con algún minuto nocturno. | CÓDIGO+TEST+NEGOCIO | Sí | RMV+tasa+sueldo vigentes por día | TESTEADA | Alto |
| RN-083 | Si falta parámetro legal o sueldo para un día nocturno, el reporte falla en vez de usar cero. | CÓDIGO+TEST | Sí | parámetros/sueldo | TESTEADA | Alto |
| RN-084 | Descanso médico, vacaciones y licencias del anexo proceden de solicitudes aprobadas solapadas, no del tareo diario. | CÓDIGO+TEST | Sí | `solicitud_ausencia` | TESTEADA | Alto |
| RN-085 | Suspensión y cinco columnas manuales del formato GCA se emiten vacías; no se simula cero. | CÓDIGO+TEST | No | exportador | TESTEADA | Medio |
| RN-086 | DNI se escribe como texto y todo texto exportado se neutraliza contra fórmulas. | CÓDIGO+TEST | No | exportador | TESTEADA | Alto |
| RN-087 | Resumen pagina después de calcular el alcance completo; el TOTAL suma minutos completos y redondea a horas al final. | CÓDIGO+TEST | No | DTO + servicio | TESTEADA | Medio |
| RN-088 | El estado de vigencia compara el `CalculadoEn` más reciente con siete cambios: marca, anulación, justificación, ID, asignación turno, turno y programación. | CÓDIGO+TEST | No | `EvaluarVigenciaDelCalculoAsync` | PARCIAL | Alto: faltan borrados, fuentes y recálculo parcial |
| RN-089 | Las tasas de liquidación se resuelven a la fecha inicial del periodo; parámetros de tareo final, a la fecha final; bono nocturno, día por día. | CÓDIGO | Sí | servicios de cálculo/reportes | SIN TEST | Alto: tres políticas temporales |
| RN-090 | Comportamiento actual vuelve a sumar base nocturna/no laborable. Requisito: una sola base y primas separadas; domingo trabajado completo al 100 %. | CÓDIGO+TEST+NEGOCIO | Sí | RQ-003/RQ-009 | TESTEADA | Alto: LIQ-03/04 fallan |

## G. Historia, aislamiento e idempotencia

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| RN-091 | El recálculo hace upsert por `(trabajador, fecha)`, conserva el ID y guarda todo el lote en una transacción de `SaveChanges`. | CÓDIGO+BD+TEST | No | `tareo_diario` unique | TESTEADA | Alto |
| RN-092 | Un cierre concurrente impide el guardado del recálculo mediante la versión del periodo. | CÓDIGO+TEST | No | guardia de periodo | TESTEADA | Alto |
| RN-093 | Administrador recalcula y consulta solo plantas visibles; coordinador sin restricción puede cubrir todo el periodo. | CÓDIGO+TEST | Sí | `IProveedorAlcancePlantas` | TESTEADA | Alto |
| RN-094 | Marcador y área del reporte se resuelven históricamente por solapamiento; empresa, planta, DNI, puesto, hijos y CECO salen de la ficha actual. | CÓDIGO+TEST | Parcial | vigencias + `trabajador` actual | PARCIAL | Alto |
| RN-095 | Tareo diario es derivado y se excluye del interceptor de auditoría; el recálculo deja solo log agregado. | CÓDIGO | No | interceptor + logger | SIN TEST | Alto |
| RN-096 | El reporte no recalcula: leer o exportar consume la última proyección persistida aunque esté advertida como obsoleta. | CÓDIGO+NEGOCIO | No | `tareo_diario` | PARCIAL | Alto |

## H. Reglas del módulo provisional PMV

| ID | Regla implementada u observada | Evidencia | Config. | Fuente de verdad | Cobertura | Riesgo |
|---|---|---|---|---|---|---|
| PMV-RN-001 | PMV es provisional y separado: no escribe importación/marcas/tareo oficiales ni modifica trabajadores o asignaciones; lee padrón solo para el cruce temporal. | CÓDIGO+BD+NEGOCIO | No | RQ-008 + tablas `prov_*` | PARCIAL | Bajo mientras siga aislado |
| PMV-RN-002 | Reutiliza el mismo detector y los lectores ZKTeco del flujo principal. | CÓDIGO+TEST | No | `IFuenteDeMarcaciones` | TESTEADA | Bajo |
| PMV-RN-003 | SHA-256 duplicado se rechaza globalmente por aplicación; no existe unique físico del hash. | CÓDIGO+BD+TEST | No | `prov_importacion.hash_contenido` | TESTEADA | Alto en concurrencia |
| PMV-RN-004 | Importación, marcas y errores PMV se guardan en una transacción explícita. | CÓDIGO+TEST | No | servicio PMV | TESTEADA | Bajo |
| PMV-RN-005 | Duplicado natural es `(equipo, ID, instante)`; aplicación y BD lo protegen. | CÓDIGO+BD+TEST | No | unique `prov_marcacion` | TESTEADA | Bajo |
| PMV-RN-006 | La atribución es dinámica por equipo+ID+vigencia; sin dueño la jornada se conserva como “Sin asignar”. | CÓDIGO+TEST+BD | Sí | `asignacion_marcador` | TESTEADA | Alto |
| PMV-RN-007 | La jornada PMV es fecha calendario: una marca = ingreso; ≥2 = primera/última; >2 añade observación. | CÓDIGO+TEST | No | consulta PMV | TESTEADA | Alto si se interpreta como tareo |
| PMV-RN-008 | PMV no calcula tareo, tardanza, salida anticipada, extras, nocturnidad, incidencias, feriados ni pago oficiales. | CÓDIGO+NEGOCIO | No | RQ-008 | SIN TEST | Alto por confusión funcional |
| PMV-RN-009 | Organización es actual; asignación de marcador y turno son históricas; una cuadrilla se muestra como etiqueta, sin resolver rotación. | CÓDIGO+BD+TEST | Parcial | ficha + asignaciones | PARCIAL | Alto |
| PMV-RN-010 | Consulta no tiene máximo; 31–40 días advierte y >40 bloquea solo exportación. | CÓDIGO+TEST | No | controller/consulta | PARCIAL | Medio |
| PMV-RN-011 | Excel PMV contiene 13 columnas en `Marcaciones` y hoja `Trazabilidad`; no contiene Unidad. | CÓDIGO+TEST | No | exportador PMV | TESTEADA | Medio |
| PMV-RN-012 | Comportamiento defectuoso: PMV no aplica alcance de planta; requisito de seguridad transversal exige restringir listar/importar/consultar/exportar. | CÓDIGO+BD+TEST | No | RQ-015 | TESTEADA | Crítico: SEC-01..03 fallan |

## Matriz regla × módulo

| Grupo | Marcaciones | Personal/Turnos | Tareo | Solicitudes/Justificaciones | Reportes/Excel | PMV |
|---|---:|---:|---:|---:|---:|---:|
| RN-001–014 | X |  | X (cobertura) |  |  | comparte parser |
| RN-015–028 | X | X | X |  | X (historia) | X (atribución parcial) |
| RN-029–056 | X | X | X | X (efecto) | X (consume) |  |
| RN-057–073 |  |  | X | X | X (anexos) |  |
| RN-074–090 |  | X (padrón/parámetros) | X | X | X |  |
| RN-091–096 | X | X | X | X | X | parcialmente |
| RN-097–104 | X | X | X | X (validación) | X (valorización) |  |
| PMV-RN-001–012 | comparte lector | lee padrón | aislado | aislado | exportador propio | X |

## Contradicciones negocio ↔ implementación y fuentes de verdad en conflicto

| Ref. | Contradicción o ambigüedad | Evidencia | Impacto |
|---|---|---|---|
| C-01 | `TareoDiario` documenta ordinario “hasta 480”, pero el motor usa `Turno.MinutosJornadaOrdinaria`, incluso 720. | CÓDIGO | Interpretación errónea de horas ordinarias. |
| C-02 | El contrato de importación dice que “resuelve cada marcación contra el padrón”; la implementación solo avisa IDs sin dueño y atribuye al recalcular. | CÓDIGO | Documentación de API engañosa. |
| C-03 | `FilasRechazadas` se describe como inválidas/ID desconocido/periodo cerrado; hoy solo cuenta líneas ilegibles. ID desconocido se guarda y no se valida periodo. | CÓDIGO | Métrica de carga puede interpretarse mal. |
| C-04 | Contratos y comentarios hablan de libro de tres hojas; el exportador genera cuatro, incluyendo `Trazabilidad`. | CÓDIGO+TEST | Integraciones que validen número de hojas. |
| C-05 | Entidad/DTO llaman al bloque manual “AO–BF”; el formato vigente realmente escribe `AN–BE` (57 columnas). | CÓDIGO+TEST | Alto riesgo de mapeo manual/externo. |
| C-06 | El modelo GCA sugiere tramos diurnos/nocturnos excluyentes; la implementación trata nocturnidad como subconjunto superpuesto. | NEGOCIO+CÓDIGO | Posible doble interpretación en planilla. |
| C-07 | La política “nada se borra” contrasta con anular una solicitud, que elimina físicamente sus justificaciones. | CÓDIGO+NEGOCIO | Pérdida de evidencia de efecto intermedio. |
| C-08 | Una solicitud puede quedar `Aprobada` aunque varios días no generen justificación por periodo cerrado, inexistente o choque. | CÓDIGO | Estado cabecera puede ocultar aprobación parcial. |
| C-09 | El periodo laboral histórico existe, pero motor y reportes deciden población/vínculo con campos planos de `Trabajador`. | CÓDIGO+BD | Reingresos y 85 duplicados abiertos quedan fuera del cálculo. |
| C-10 | Tasa de sobretasa admite empresa, pero liquidación y bono nocturno no filtran por empresa y toman el primer valor vigente. | CÓDIGO+BD | Resultado no determinista o tasa de otra razón social. |
| C-11 | `EstadoCalculoTareo` afirma vigencia del periodo usando el `CalculadoEn` máximo; un recálculo parcial por planta puede ocultar filas antiguas. | CÓDIGO+INFERIDA | Excel marcado “al día” sin estarlo integralmente. |
| C-12 | La advertencia de vigencia no contempla feriados, parámetros, sueldos, tipos de ausencia, reintegros, datos de trabajador/CECO ni versiones de turno de forma directa. | CÓDIGO | Reporte puede cambiar o quedar obsoleto sin advertencia. |

## Brechas funcionales y reglas inferidas que requieren confirmación

1. Confirmar si el refrigerio debe descontarse completo en jornadas cortas y de qué tramo/nocturnidad debe salir.
2. Separar tardanza y salida anticipada de planilla (ambas con tolerancia confirmada 0) de cualquier tolerancia operativa/configurada.
3. Confirmar el margen fijo de seis horas y el criterio primera/última frente a turnos partidos o múltiples ingresos.
4. Confirmar si el bono nocturno diario completo corresponde con cualquier minuto nocturno o debe prorratearse por horas.
5. Implementar el descanso rotativo explícito y BD01/BD02 ya cerradas; hoy no existe tramo sin turno.
6. Definir múltiples justificaciones por día; el modelo actual obliga a escoger una.
7. Definir tratamiento de trabajador con dos equipos o áreas dentro del mismo rango: se concatena texto, pero el tareo no se divide.
8. Acordar si datos maestros del Excel de periodo cerrado deben congelarse históricamente.
9. Acordar la fuente única de tasas por empresa y la política temporal uniforme para inicio/fin/día.
10. Confirmar el bloque vigente de columnas del modelo GCA (`AN–BE`) y retirar nombres antiguos (`AO–BF`).
11. Resolver el alcance por planta de PMV, las jornadas sin dueño y el significado de turno para cuadrillas.
12. Decidir si la advertencia de vigencia debe bloquear exportación en lugar de solo avisar.

## Resumen cuantitativo del catálogo

- Reglas principales catalogadas: **104**.
- Reglas PMV catalogadas: **12**.
- Total: **116**.
- Con prueba directa declarada frente a la definición reconciliada: **91**.
- Con cobertura parcial/indirecta: **15**.
- Sin prueba directa localizada: **10**.
- La cifra previa 79/18/11 mezcló la columna `Config.` con `Cobertura`; el corte real de Fase 4 era 83/14/11. Fase 5 reclasificó RN-055 y RN-088 a parcial y agregó cobertura directa para RN-090.
- El corte histórico de Fase 5 fue 82/16/10 y Fase 6.1 quedó en 77/21/10 al ampliar contratos. Fase 6.2 agrega cobertura directa para RN-010, RN-025, RN-041, RN-047, RN-049, RN-063 y PMV-RN-012. PMV-RN-001 sigue parcial.
- “Testeada” describe existencia de prueba directa, no resultado verde: HE-01..05 y LIQ-03/04 son cobertura directa fallida.

## Reglas confirmadas que prevalecen sobre la implementación observada

- Turnos regulares: DIU 07:00–17:22 y NOC 19:00–05:22 (+1 día). Ordinario hasta el fin teórico; no 480 minutos desde la entrada.
- Licencia o ausencia pagada de jornada completa: 480 minutos (8 horas efectivas sin almuerzo), tanto DIU como NOC; el intervalo de 622 minutos no se traslada al crédito. No genera HE ni nocturnidad por sí sola. Los permisos parciales mantienen su cálculo propio.
- L–V: exceso menor a 15 minutos sobre el fin teórico = 0 HE; desde 15 minutos, HE desde el fin teórico (17:37 = 15 HE).
- Sábado: cero ordinarios; todo es HE desde el primer minuto, sin umbral; primeros 120 minutos al 25 % y resto al 35 %.
- Domingo: descanso semanal; si se trabaja, todo el tiempo son horas extra al 100 % desde el primer minuto, sin umbral ni descanso sustitutorio automático.
- La jornada nocturna se atribuye a su fecha de inicio. Viernes→sábado no se reclasifica por el cambio calendario; sábado→domingo sí corta la clasificación económica en domingo 00:00, desde donde todo minuto es HE100 sin umbral.
- 14 diurnos + 1 descanso móvil + 7 nocturnos es un patrón operativo conocido, no una repetición universal. La regla es admitir secuencias/duraciones variables; el modelo vigente representa el descanso móvil explícitamente y termina al agotar la secuencia.
- Franja nocturna 22:00–06:00; fórmula económica pendiente de validación/configuración.
- Tolerancia de planilla confirmada: 0 minutos (07:01 = 1; 19:01 = 1). El campo configurable/semilla de 5 minutos ya no interviene en el cálculo de planilla.
- Salida anticipada de planilla: tolerancia 0 (17:21 = 1; 05:21 = 1). El campo configurable/semilla de 5 ya no interviene.
- La marca cruda permanece inmutable; el contrato completo de regularización separada y auditada está definido en RQ-007 y solo se cumple parcialmente.

## Reconciliación ampliada de las 116 reglas

- **SIGUE VIGENTE (64):** RN-001, RN-002, RN-003, RN-004, RN-005, RN-007, RN-008, RN-011, RN-013, RN-014, RN-015, RN-016, RN-018, RN-019, RN-020, RN-022, RN-023, RN-026, RN-028, RN-029, RN-030, RN-033, RN-037, RN-038, RN-039, RN-040, RN-042, RN-043, RN-051, RN-054, RN-056, RN-057, RN-058, RN-059, RN-060, RN-062, RN-064, RN-065, RN-066, RN-067, RN-068, RN-069, RN-071, RN-072, RN-074, RN-075, RN-076, RN-080, RN-081, RN-083, RN-084, RN-085, RN-086, RN-087, RN-092, RN-093, RN-097, RN-098, RN-099, RN-101, RN-102, RN-103, PMV-RN-002 y PMV-RN-006.
- **REQUIERE ACTUALIZACIÓN (17):** RN-010, RN-024, RN-025, RN-041, RN-046, RN-047, RN-048, RN-049, RN-061, RN-063, RN-078, RN-090, RN-100, RN-104, PMV-RN-001, PMV-RN-008 y PMV-RN-012. Sus filas ya expresan la reconciliación.
- **COMPORTAMIENTO ACTUAL, NO REQUISITO (35):** RN-006, RN-009, RN-012, RN-017, RN-021, RN-027, RN-031, RN-032, RN-034, RN-035, RN-036, RN-044, RN-045, RN-050, RN-052, RN-053, RN-055, RN-070, RN-073, RN-077, RN-079, RN-082, RN-088, RN-089, RN-091, RN-094, RN-095, RN-096, PMV-RN-003, PMV-RN-004, PMV-RN-005, PMV-RN-007, PMV-RN-009, PMV-RN-010 y PMV-RN-011.
- **QUEDÓ OBSOLETA:** ninguna fila completa; quedaron obsoletos fragmentos históricos que usaban 480 como frontera universal de jornada trabajada, tolerancias de 5, domingo genérico y repetición rígida 14+1+7. R-P1-08 confirma 480 para licencia/ausencia pagada completa.

La suma 64 + 17 + 35 = 116. R-P1-05 agrega cobertura directa para
RN-097/098/099/101/102/103; RN-100 y RN-104 conservan su cobertura previa y la
valorización económica completa de RN-103 sigue como gap independiente.

Los casos exactos, su ejecución y sus resultados están en `ACCEPTANCE_RULES.md`.
