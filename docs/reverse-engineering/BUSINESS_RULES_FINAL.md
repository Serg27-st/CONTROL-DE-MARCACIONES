# P1-BUSINESS-RULES-FINAL — catálogo autoritativo

Fecha de corte: 2026-10-16  
Baseline de entrada: build Release 0 errores/0 warnings; 948/948 pruebas verdes.  
Alcance: documentación; código, tests, migraciones, datos y producción no se modifican.

## Convenciones y precedencia

Cada fila separa `regla → fuente → implementación → test → estado`. La fuente más reciente
confirmada prevalece sobre descripciones históricas; código y tests prueban implementación, no
crean por sí mismos una regla. Precedencia: decisión confirmada reciente → este catálogo →
`ACCEPTANCE_RULES.md` → dossier específico → código/tests.

Estados únicos: `VIGENTE_IMPLEMENTADA`, `VIGENTE_PARCIAL`, `VIGENTE_NO_IMPLEMENTADA`,
`HISTORICA_OBSOLETA`, `SUPERADA`, `NO_DETERMINADA`. `Legado` conserva la conexión con los
IDs `RN-*`/`PMV-RN-*` de Fase 6.1.

## Horarios y asistencia

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| ASIS-001 | DIU L–V 07:00–17:22; NOC 19:00–05:22 D+1; semana objetivo 48 h. `9.36` no son 9,36 h decimales. | R-P0-03.1 | `Turno`, `VersionTurno`, `CalculadoraTareo` | `JornadaOrdinariaYHorasExtraAcceptanceTests` | VIGENTE_PARCIAL | RN-026/046; semilla 19:00/07:00 requiere OP-01 |
| ASIS-002 | La jornada pertenece al día de inicio aunque cruce medianoche. | FER-NAC-05/06; R-P0-HE.2 | cálculo/tareo | `CrucesNocturnosFinDeSemanaAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-029 |
| ASIS-003 | Sin vínculo histórico: `SinVinculo`, cero falta y minutos; sin horario: `SinHorario`, sin turno inventado. | 3C; RQ-004 | `ConsultaContextoLaboral`, `ServicioCalculoTareo` | `PeriodoLaboralTareoFase3CTests`; `RotacionTurnosTests` | VIGENTE_IMPLEMENTADA | RN-020/023/040 |
| ASIS-004 | Sin marcas y sin cobertura del aparato: `PendienteValidacion`; con cobertura: `Falta`. | cobertura vigente | `CoberturaDeCarga` | `FaltaMasivaPorArchivoSinCargarTests` | VIGENTE_IMPLEMENTADA | RN-037/038 |
| ASIS-005 | Una marca efectiva es incompleta y produce cero minutos automáticos. | cálculo; BD01-D03 | `CalculadoraTareo` | `CalculadoraTareoTests` | VIGENTE_IMPLEMENTADA | RN-039/098 |
| ASIS-006 | Llegar antes no genera tiempo pagable; el ordinario inicia como máximo en la hora teórica. | R-P0-03.1 | `CalculadoraTareo` | `JornadaOrdinariaYHorasExtraAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-043 |

## Tardanza y salida anticipada

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| TAR-001 | Tardanza de planilla: tolerancia 0; 07:01/19:01 = 1 min. | R-P0-02; RQ-005 | inicio teórico en `CalculadoraTareo` | `TardanzaPlanillaAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-048 |
| TAR-002 | `MinutosTolerancia` histórico/configurado no perdona tardanza de planilla. | R-P0-02 | campo conservado, fuera del cálculo | mismo test | VIGENTE_IMPLEMENTADA | tolerancia 5 SUPERADA |
| TAR-003 | Salida anticipada: tolerancia 0; 17:21/05:21 = 1 min. | R-P0-02; RQ-006 | `FinTurno` en `CalculadoraTareo` | `DomingoYSalidaAnticipadaAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-049 |
| TAR-004 | Justificación aprobada puede perdonar tardanza/salida total o parcial sin ocultar otras incidencias. | R-P1-03; RQ-007 | cálculo de incidencias | `IncidenciaConJustificacionParcialAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-055/061 actualizadas |

## Horas extra y nocturnidad

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| HE-001 | L–V: menos de 15 min tras `FinTurno` = 0 HE; desde 15 se cuenta todo desde el fin (17:37/05:37 = 15). | R-P0-03.1 | `CalculadoraTareo` | `ReglasConfirmadasHorasExtraAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-047 |
| HE-002 | HE ordinaria: primeros 120 min al 25 %, resto al 35 %. | RQ-001 | reparto de cálculo | `JornadaOrdinariaYHorasExtraAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-047/053 |
| HE-003 | Sábado: cero ordinarios, sin umbral; 120 min HE25 y resto HE35. | RQ-002 | cálculo | `ReglasConfirmadasHorasExtraAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-047/053 |
| HE-004 | Domingo: descanso semanal; trabajado = HE100 desde primer minuto, sin ordinario, HE25/35 ni sustitutorio automático. | RQ-003 | cálculo y liquidación | `DomingoYSalidaAnticipadaAcceptanceTests`; `LiquidacionPeriodoAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-041/078/090 |
| HE-005 | Cada minuto tiene una sola base física; primas/sobretasas se valorizan separadas. | R-P1-01; FER-NAC-08 | liquidación | `LiquidacionPeriodoAcceptanceTests` | VIGENTE_IMPLEMENTADA | doble conteo SUPERADO |
| NOC-001 | Franja nocturna 22:00–06:00. | regla confirmada | `CalculadoraTareo` | `CalculadoraTareoTests` | VIGENTE_IMPLEMENTADA | RN-051 |
| NOC-002 | Nocturnidad es dimensión superpuesta y aporta prima, no segunda base. | R-P1-01 | `TareoDiario`, liquidación | `LiquidacionPeriodoAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-079/090 |
| NOC-003 | Vie 19:00→sáb 05:22 es regular; sáb 19:00→dom 05:22 = 120 HE25 + 180 HE35 + 322 HE100. | R-P0-HE.2 | cálculo segmentado | `CrucesNocturnosFinDeSemanaAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-029/053 |
| NOC-004 | Fórmula normativa final del bono nocturno requiere validación; la conducta actual no es confirmación universal. | `ACCEPTANCE_RULES` §1.5 | `CalculadoraBonoNocturno` | `CalculadoraBonoNocturnoTests` (conducta actual) | NO_DETERMINADA | RN-082 |

## Rotaciones, cuadrillas y descanso programado

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| ROT-001 | 14 DIU + 1 descanso + 7 NOC es referencial, no ciclo universal; manda programación explícita fechada. | RQ-004; ADR | `ProgramacionCuadrilla` | `RotacionesVariablesAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-024 |
| ROT-002 | La secuencia es finita y no se repite implícitamente al agotarse. | R-P1-05 | resolución de programación | mismo test | VIGENTE_IMPLEMENTADA | repetición rígida SUPERADA |
| ROT-003 | Tramo Trabajo exige turno; Descanso exige `TurnoId=null` y dura un día. | ADR/S-ROT-DESC | dominio + CHECK SQLite | `DescansoProgramadoMigracionTests` | VIGENTE_IMPLEMENTADA | RN-025 |
| ROT-004 | Descanso sin marcas: no falta, tardanza, salida, ordinarias ni HE. Trabajado: todo intervalo reconocido es HE100 desde el primer minuto. | BD01 | dos pasadas de tareo | `DescansoProgramadoAcceptanceTests`; `RotacionTurnosTests` | VIGENTE_IMPLEMENTADA | RN-097/099 |
| ROT-005 | Jornadas con turno reclaman primero; no se reutilizan marcas. Entrada residual en D; salida inequívoca puede caer en D+1 antes de la siguiente jornada. | BD01-D01/D02 | `ServicioCalculoTareo` | `RotacionTurnosTests` | VIGENTE_IMPLEMENTADA | RN-032/097 |
| ROT-006 | Una marca residual o frontera ambigua = cero automático y validación; no se inventa duración. | BD01-D03 | cálculo | `DescansoProgramadoAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-098 |
| ROT-007 | Hoja de HE puede autorizar/evidenciar, pero no define fronteras temporales sin horario explícito. | BD-01 | no modelada como reloj | NO LOCALIZADO | VIGENTE_NO_IMPLEMENTADA | RN-100 |
| ROT-008 | Cuadrilla y planta se resuelven por vigencia histórica. | PeriodoLaboral 3E-B | `ServicioCuadrillas`, contexto laboral | `PeriodoLaboralFase3EBCuadrillasTests` | VIGENTE_IMPLEMENTADA | RN-094 actualizada |

## Feriados

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| FER-001 | Feriado nacional no trabajado conserva remuneración ordinaria y no genera HE por sí solo. | FER-NAC-01 | derecho ordinario explícito en días-persona; cero minutos físicos y cero HE100 | `FER_001_feriado_no_trabajado_conserva_derecho_sin_inventar_minutos` | VIGENTE_IMPLEMENTADA | — |
| FER-002 | Trabajado sin sustitutorio: remuneración ordinaria + base trabajada + sobretasa 100 %; parcial se prorratea por minutos reales. | FER-NAC-02/03 | derecho ordinario separado; `FeriadoTrabajado` desglosa base y recargo sobre N minutos únicos | `FER_002_dia_completo...`; `FER_002_trabajo_parcial...` | VIGENTE_IMPLEMENTADA | RN-102 |
| FER-003 | Con descanso sustitutorio no corresponde adicional de trabajo + 100 %; el dato aún no existe. | FER-NAC-04; BD02-D04 | no modelado | NO LOCALIZADO | VIGENTE_NO_IMPLEMENTADA | gap BD-02 |
| FER-004 | Turno que cruza hacia feriado conserva día de inicio; turno iniciado en feriado se trata como feriado hasta D+1, sin duplicar minutos. | FER-NAC-05/06; BD02-D08/09 | clasificación por fecha de jornada | `FER_NAC_05...`; `FER_NAC_06...` | VIGENTE_IMPLEMENTADA | RN-104 |
| FER-005 | Feriado nacional ≠ día no laborable compensable; este último no está clasificado expresamente. | FER-NAC-07 | no modelado | NO LOCALIZADO | VIGENTE_NO_IMPLEMENTADA | gap BD-02 |
| FER-006 | Feriado ordinario + descanso: sin trabajo es un descanso único; trabajado usa minutos únicos y prevalece `FeriadoTrabajado`. | BD02-D01..05 | `TipoCoincidenciaFeriadoDescanso` | `DescansoProgramadoAcceptanceTests`; `RotacionTurnosTests` | VIGENTE_IMPLEMENTADA | RN-101/102 |
| FER-007 | 1 de mayo + descanso conserva base única pero tratamiento económico especial (cuatro componentes trabajado; descanso+feriado no trabajado). | BD02-D06/07 | dos derechos diarios separados + base y recargo sobre N minutos únicos; sin trabajo conserva dos derechos y cero físico | `FER_007_1_de_mayo_trabajado...`; `FER_007_1_de_mayo_no_trabajado...` | VIGENTE_IMPLEMENTADA | RN-103 |

## Ausencias, permisos y regularización

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| AUS-001 | Solo justificación aprobada modifica tareo; una vigente por trabajador/día. | contrato vigente | servicio + índice filtrado | `PagoDeJustificacionesAlRecalcularTests`; `UnaJustificacionPorTrabajadorYDiaTests` | VIGENTE_IMPLEMENTADA | RN-057/058 |
| AUS-002 | Ausencia/licencia pagada completa acredita 480 min DIU/NOC, sin HE ni nocturnidad. | R-P1-08 | `JustificacionAplicable` | `PagoDelDiaJustificadoTests` | VIGENTE_IMPLEMENTADA | RN-059/060 |
| AUS-003 | Permiso parcial conserva duración/solape real; no recibe 480 ni inventa trabajo. | R-P1-08 | cálculo de solape | `PermisoParcialTests` | VIGENTE_IMPLEMENTADA | RN-062 |
| AUS-004 | Permiso por horas es de un día y fin posterior al inicio. | solicitud vigente | validadores | `ValidadorLimitesSolicitudTests` | VIGENTE_IMPLEMENTADA | RN-071 |
| AUS-005 | Sustento requerido se valida al aprobar; detalle clínico solo en contingencias subsidiadas. | solicitud vigente | servicio/validadores | `SustentoYAprobacionTests`; `ValidadorDetalleClinicoTests` | VIGENTE_IMPLEMENTADA | RN-069 |
| AUS-006 | Solicitudes pendientes/aprobadas consumen límites inclusivos y no deben solaparse. | solicitud vigente | servicio | `ValidadorLimitesSolicitudTests` | VIGENTE_PARCIAL | RN-067/072 |
| AUS-007 | Aprobación debería ser atómica; hoy puede quedar aprobada con días omitidos por choque/periodo. | P2-02 | `ServicioSolicitudes.AprobarAsync` | NO LOCALIZADO directo | VIGENTE_PARCIAL | RN-070 |
| AUS-008 | Anulación debe preservar evidencia; el borrado físico derivado actual es deuda. | política de evidencia; P2-03 | servicio solicitudes | parcial | VIGENTE_PARCIAL | RN-073 |
| AUS-009 | Regularización conserva original, corrección, motivo, actores, fechas, validación y auditoría; si es parcial mantiene incidencia. | RQ-007; R-P1-03 | `Justificacion`, servicio | `RegularizacionDeMarcacionTests`; `FlujoDeRegularizacionTests` | VIGENTE_IMPLEMENTADA | RN-010/063 |

## Marcaciones, atribución e importación

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| MAR-001 | `MarcacionCruda` es inmutable; anular carga excluye sin borrar evidencia y reactivar revierte. | RQ-007 | entidades/importación | `AnulacionImportacionTests`; `CargaAnuladaNoCuentaTests` | VIGENTE_IMPLEMENTADA | RN-010/011 |
| MAR-002 | Dueño = EquipoId + IdMarcador exacto + FechaHora vigente; `0014` ≠ `14`. | atribución confirmada | `AtribucionMarcaciones` | `AtribucionMarcacionesTests`; `ConsultasDeAtribucionEnSqliteTests` | VIGENTE_IMPLEMENTADA | RN-015/016 |
| MAR-003 | Atribución ocurre al recalcular/consultar; ID sin dueño se conserva y no alimenta tareo. | atribución confirmada | cálculo | `AtribucionAlCalcularTests` | VIGENTE_IMPLEMENTADA | RN-017/018 |
| MAR-004 | Primera/última reclamable son extremos; rebotes <2 min se consolidan para paridad; interiores se informan. | cálculo vigente | `CalculadoraTareo` | `CalculadoraTareoTests` | VIGENTE_IMPLEMENTADA | RN-033..035 |
| MAR-005 | Una marca no puede pertenecer a dos jornadas; residual no reclamado queda fuera de ventana/validación. | BD-01 | cálculo | `RotacionTurnosTests` | VIGENTE_IMPLEMENTADA | RN-032/036 |
| MAR-006 | Formato se detecta por contenido, no extensión; TXT/DAT comparten contrato. | importación | detector/lector automático | `DetectorFormatoMarcadorTests`; lectores | VIGENTE_IMPLEMENTADA | RN-001 |
| MAR-007 | Planta/equipo son obligatorios y coherentes; serie del contenido/nombre debe corresponder. | origen | `ServicioImportacion`, `SerieDelArchivo` | `OrigenDeLaCargaTests`; `SerieDelArchivoTests` | VIGENTE_IMPLEMENTADA | RN-002/005 |
| MAR-008 | Duplicado natural = equipo+ID+instante; hash identifica contenido; carga anulada no bloquea corrección. | importación | servicio + índice SQLite | `ColisionDeIdEntreEquiposTests`; `ArchivoCargadoEnOtroEquipoTests` | VIGENTE_IMPLEMENTADA | RN-003/004/009 |
| MAR-009 | Líneas ilegibles se registran; válidas e IDs aún sin dueño se preservan. | importación | lectores + errores | `ImportacionRespaldoRealTests` | VIGENTE_IMPLEMENTADA | RN-007/008 |
| MAR-010 | Importación principal es atómica para cabecera, errores, serie, auditoría y marcas; fallo técnico revierte todo. | R-P1-07 | transacción `ServicioImportacion` | `AtomicidadImportacionAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-012 SUPERADA |
| MAR-011 | Cobertura es por día/aparato esperado; inactivo o futuro no crea falta. | cobertura | `CoberturaDeCarga` | `CoberturaDeCargaTests`; `CoberturaPorAparatoTests` | VIGENTE_IMPLEMENTADA | RN-013/014 |
| MAR-012 | Lector BKtransaction interno es autoridad por defecto; SDK/Comparar son diagnóstico. Eventos de control ignorados no son marcaciones ni rechazados. | P1-BKDAT | lectores + UI | `LectorRespaldoZktecoTests`; `EventosIgnoradosUiTests` | VIGENTE_IMPLEMENTADA | redistribución SDK NO_DETERMINADA |

## PeriodoLaboral, planta y reingreso

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| PER-001 | `PeriodoLaboral` es autoridad histórica de vínculo/empresa; `AsignacionPlanta`, de planta dentro del vínculo. | 3C–3E | `ConsultaContextoLaboral` | `ContextoLaboralHistoricoTests`; suites PL3C | VIGENTE_IMPLEMENTADA | RN-019/020/094 actualizadas |
| PER-002 | Campos planos de `Trabajador` son proyección actual/compatibilidad, no historia. | 3E-E | dual-write de `ServicioTrabajadores` | PL3D/PL3EC | VIGENTE_IMPLEMENTADA | uso histórico plano SUPERADO |
| PER-003 | Máximo un periodo abierto, sin solapamientos y con rango válido; guardas de dominio son obligatorias. | 3A/3D | dominio + índice parcial + servicio | `PeriodoLaboralContratoTests`; PL3D | VIGENTE_IMPLEMENTADA | P1-02 SUPERADO |
| PER-004 | Alta crea periodo abierto y planta inicial; cambio de planta preserva historia y actualiza proyección. | flujo vigente | servicio + `AsignacionPlanta` | `PeriodoLaboralAcceptanceTests`; `ContextoLaboralHistoricoTests` | VIGENTE_IMPLEMENTADA | — |
| PER-005 | Cese cierra exactamente el abierto sin alterar historia cerrada. | R-P1-02; 3D | repositorio selectivo | `BajaTrabajadorTests`; PL3D SQLite | VIGENTE_IMPLEMENTADA | — |
| PER-006 | Reingreso conserva vínculo anterior, crea nuevo periodo/planta, actualiza proyección, es atómico y resiste doble envío. | 3E-C | Application/UI | `PeriodoLaboralFase3ECReingresoTests`; MVC reingreso | VIGENTE_IMPLEMENTADA | — |
| PER-007 | Gap laboral = `SinPeriodoLaboral`/`SinVinculo`; no se inventa empresa/planta. | 3C | contexto laboral | tareo/reportes 3C | VIGENTE_IMPLEMENTADA | — |
| PER-008 | SQLite no protege todos los solapamientos cerrados ni tiene CHECK completo de rango; las guardas siguen necesarias. | deuda 3E-E | validadores + índice parcial | PL3D | VIGENTE_PARCIAL | — |

## PMV

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| PMV-001 | PMV es provisional/aislado: solo escribe `prov_*`, nunca tareo, personal ni asignaciones oficiales. | RQ-008 | servicios PMV | `ImportacionProvisionalTests` | VIGENTE_IMPLEMENTADA | PMV-RN-001 |
| PMV-002 | Reutiliza lectores; importación transaccional; dedupe equipo+ID+instante. | contrato PMV | importador PMV | `ImportacionProvisionalTests` | VIGENTE_IMPLEMENTADA | PMV-RN-002/004/005 |
| PMV-003 | Atribución dinámica equipo+ID+vigencia; sin dueño se conserva “Sin asignar”. | contrato PMV | consulta PMV | `ConsultaMarcacionProvisionalTests` | VIGENTE_IMPLEMENTADA | PMV-RN-006 |
| PMV-004 | No calcula tardanza, HE, nocturnidad, incidencias, feriados ni pago oficial. | RQ-008 | consulta/exportador | pruebas PMV indirectas | VIGENTE_IMPLEMENTADA | PMV-RN-008 |
| PMV-005 | Planta histórica sale de `AsignacionPlanta`, no de proyección actual. | 3E-B | contexto laboral PMV | `PeriodoLaboralFase3EBCuadrillasTests` | VIGENTE_IMPLEMENTADA | PMV-RN-009 actualizada |
| PMV-006 | Alcance por planta aplica a catálogo, historial, importación, consulta y exportación. | R-P0-01 | `IProveedorAlcancePlantas` | `AislamientoPmvAcceptanceTests` | VIGENTE_IMPLEMENTADA | PMV-RN-012 SUPERADA |
| PMV-007 | 1–30 días normal; 31–40 advierte; 41+ consulta permitida, exportación bloqueada. | contrato PMV | controlador/consulta | pruebas consulta/exportador | VIGENTE_IMPLEMENTADA | PMV-RN-010 |

## Reportes, tareo y liquidación

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| REP-001 | Tareo es proyección derivada idempotente por trabajador+fecha; reportar no recalcula. | arquitectura | cálculo/reportes | `ServicioReportesTests` | VIGENTE_IMPLEMENTADA | RN-091/096 |
| REP-002 | Periodo cerrado impide escritura; cierre concurrente invalida guardado. | periodo | `GuardiaDePeriodo` | `CierreDePeriodoConcurrenteTests` | VIGENTE_IMPLEMENTADA | RN-066/092 |
| REP-003 | Resumen/excepciones admiten periodo/mes/día/rango; tareo final exige periodo. | reportes | `ResolutorRangoReporte` | `ResolutorRangoReporteTests` | VIGENTE_IMPLEMENTADA | RN-076 |
| REP-004 | Vínculo/empresa/planta son históricos; área/turno usan vigencias. Otros maestros actuales quedan en P1-06. | 3C/3E; P1-06 | contexto y consultas | reportes 3C; `VigenciaDelAreaEnReportesTests` | VIGENTE_PARCIAL | RN-094 actualizada |
| REP-005 | Vigencia usa la fila relevante más antigua y fuentes consumidas; cambios/borrados marcan desactualizado sin recalcular cerrados. | R-P1-04 | evaluador + auditoría | `VigenciaTareoAcceptanceTests` | VIGENTE_IMPLEMENTADA | RN-088 SUPERADA |
| REP-006 | Reintegros: una fila trabajador/periodo, no negativos, manuales y fuera del cálculo diario. | tareo final | entidad/servicio | `ServicioReintegrosTareoTests` | VIGENTE_IMPLEMENTADA | RN-074/075 |
| REP-007 | DNI/texto Excel se emiten como texto seguro y se neutralizan contra fórmulas. | exportación segura | sanitizador/exportadores | `SanitizadorExcelTests`; `ExportadorTareoFinalTests` | VIGENTE_IMPLEMENTADA | RN-086 |
| REP-008 | Política uniforme de tasas por empresa y congelamiento de maestros residuales no está cerrada. | P1-06/deuda | resoluciones actuales diversas | cobertura parcial | NO_DETERMINADA | RN-089/C-10 |

## Seguridad, arranque y auditoría

| ID | Regla | Fuente | Implementación | Test | Estado | Legado |
|---|---|---|---|---|---|---|
| SEG-001 | Operaciones funcionales respetan rol y alcance de plantas. | política transversal | Identity + alcance | `AislamientoPorPlantaTests`; PMV SEC | VIGENTE_IMPLEMENTADA | RN-093 |
| SEG-002 | Sin usuarios/credenciales bootstrap, primer arranque exige `/setup` de un solo uso; no hay credenciales reales embebidas. | P1-LOCAL-FIRST-RUN | inicializador + setup | `FirstRunTests`; `InicializadorSeguridadTests` | VIGENTE_IMPLEMENTADA | — |
| SEG-003 | `/vivo` prueba proceso; `/estado` valida migraciones, integridad, FK y esquema, responde 503 si no listo y nunca migra. | P1-STARTUP-HEALTH | preparador/verificador | `StartupHealthTests` | VIGENTE_IMPLEMENTADA | health permisivo SUPERADO |
| SEG-004 | En `Starting`/`Failed` se bloquean rutas funcionales salvo `/vivo` y `/estado`; readiness puede recuperarse sin reinicio. | hardening startup | estado/middleware | `StartupHealthTests` | VIGENTE_IMPLEMENTADA | — |
| SEG-005 | Instalación local usa loopback, instancia única y ProgramData persistente; reinstalar/desinstalar preserva DB. | P1-DESKTOP/LOCAL-FIRST-RUN | hosting/installer | ciclo UAC + `FirstRunTests` | VIGENTE_IMPLEMENTADA | — |
| SEG-006 | Credencial expuesta debe rotarse/inutilizarse antes de producción; contención local no cierra exposición histórica. | PHASE 7A | control operativo | manifiesto cuarentena | VIGENTE_NO_IMPLEMENTADA | P0-SEC-01 abierto externamente |
| AUD-001 | Escrituras sensibles conservan actor/fecha/operación; tareo derivado usa log agregado, no auditoría por fila. | arquitectura | `InterceptorAuditoria` + logging | cobertura indirecta | VIGENTE_PARCIAL | RN-095 |
| AUD-002 | Cambios/borrados relevantes deben invalidar vigencia; no anunciar tareo obsoleto como vigente. | R-P1-04 | auditoría + evaluador | `VigenciaTareoAcceptanceTests` | VIGENTE_IMPLEMENTADA | — |

## Reglas históricas no vigentes

| ID | Enunciado antiguo | Estado | Reemplazo |
|---|---|---|---|
| HIST-001 | 480 min desde la entrada es frontera universal de jornada trabajada. | HISTORICA_OBSOLETA | ASIS-001/HE-001; 480 solo rige AUS-002. |
| HIST-002 | Tardanza/salida toleran 5 min en planilla. | SUPERADA | TAR-001/TAR-003. |
| HIST-003 | 14+1+7 se repite universalmente. | SUPERADA | ROT-001/002. |
| HIST-004 | Domingo trabajado es simplemente “como feriado”. | SUPERADA | HE-004. |
| HIST-005 | Campos planos de Trabajador son historia laboral/planta. | SUPERADA | PER-001/002. |
| HIST-006 | Importación principal confirma cabecera y marcas por separado. | SUPERADA | MAR-010. |
| HIST-007 | PMV no tiene aislamiento por planta. | SUPERADA | PMV-006. |
| HIST-008 | Fallo de migración permite `/estado` 200. | SUPERADA | SEG-003/004. |
| HIST-009 | Descanso rotativo requiere turno ficticio. | SUPERADA | ROT-003..006. |

## Brechas vigentes

1. OP-01: instalaciones con versión sembrada 07:00–19:00 / 19:00–07:00 requieren nueva
   `VersionTurno` fechada y recálculo de periodos abiertos; nunca reescribir historia.
2. Feriados: permanecen separados el descanso sustitutorio y el día no laborable compensable;
   FER-001/002/004/007 quedaron cerrados el 2026-10-19.
3. P1-06: otros maestros del reporte aún pueden reflejar estado actual.
4. Solicitudes: aprobación parcial y borrado físico derivado siguen como deuda P2.
5. Nocturnidad/tasas por empresa requieren decisión económica uniforme.
6. P0-SEC-01 no cierra hasta rotar/inutilizar la credencial comprometida.
7. Producción permanece intacta; OP-03 no fue ejecutado.

`ACCEPTANCE_FAILURES.md` queda como evidencia histórica de contratos ya cerrados. Para detalle
usar BD-01/BD-02, `P1_STARTUP_HEALTH.md`, los dossiers PeriodoLaboral 3B–3E,
`P1_BKDAT_REJECTS_ANALYSIS.md` y `P1_BKDAT_IGNORED_EVENTS.md`.
