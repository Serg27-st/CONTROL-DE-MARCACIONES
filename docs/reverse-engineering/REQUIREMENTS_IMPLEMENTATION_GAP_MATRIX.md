# P1-REQUIREMENTS-TRACEABILITY-FINAL

Fecha de corte original: 2026-10-16. Actualización operacional: 2026-10-23.
Matriz autoritativa requisito → implementación → gap → acción.
Fuente funcional: `BUSINESS_RULES_FINAL.md`. Baseline autoritativa: 994/994. Las actualizaciones
P2-AUS-006/007/008, P2-PMV-004 y P2-AUD-001 modificaron contratos e implementación según cada
frente, sin migración ni datos fuente; producción permanece intacta.

## Convenciones

- `EstadoRegla`: `VIGENTE`, `HISTORICO`, `SUPERADO`, `NO_DETERMINADO`.
- `EstadoFinal`: `IMPLEMENTADO`, `RESUELTO`, `PARCIAL`, `NO_IMPLEMENTADO`, `NO_DETERMINADO`.
  `RESUELTO` se reserva para un gap operacional cerrado con evidencia fuera de la suite.
- Los nombres de archivo son relativos al repositorio. `NINGUNO` implica severidad `NINGUNA`.
- `NO EXISTE` se usa cuando no hay implementación o contrato directo localizados.

## MATRIZ_PRINCIPAL

### Horarios, asistencia, tardanza y salida

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-ASIS-001 | ASIS-001 | Horario | DIU 07:00–17:22; NOC 19:00–05:22 D+1; semana 48 h. | VIGENTE | El motor usa la versión fechada del turno. | `CalculoTareo.cs/CalculadoraTareo`; `VersionTurno.cs` | `JornadaOrdinariaYHorasExtraAcceptanceTests.cs` | RIESGO_DATOS | La semilla desplegada 19:00/07:00 no expresa el horario confirmado. | P0 | Ejecutar OP-01: nueva versión fechada y recalcular solo periodos abiertos. | PARCIAL |
| RQ-ASIS-002 | ASIS-002 | Jornada | Jornada atribuida al día de inicio al cruzar medianoche. | VIGENTE | Fecha de jornada conserva el inicio. | `ServicioCalculoTareo.cs`; `CalculoTareo.cs` | `CrucesNocturnosFinDeSemanaAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato nocturno. | IMPLEMENTADO |
| RQ-ASIS-003 | ASIS-003 | Vigencia | Sin vínculo=`SinVinculo`; sin horario=`SinHorario`; cero falta/minutos. | VIGENTE | Contexto laboral y programación resuelven ambos estados. | `ConsultaContextoLaboral.cs`; `ServicioCalculoTareo.cs` | `PeriodoLaboralTareoFase3CTests.cs`; `RotacionTurnosTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos de vigencia. | IMPLEMENTADO |
| RQ-ASIS-004 | ASIS-004 | Cobertura | Sin marcas y sin cobertura=`PendienteValidacion`; con cobertura=`Falta`. | VIGENTE | Cobertura por día y aparato. | `CoberturaDeCarga.cs`; `ServicioCalculoTareo.cs` | `FaltaMasivaPorArchivoSinCargarTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos de cobertura. | IMPLEMENTADO |
| RQ-ASIS-005 | ASIS-005 | Incompleta | Una marca efectiva produce jornada incompleta y cero automático. | VIGENTE | Calculadora no inventa extremo. | `CalculoTareo.cs/CalculadoraTareo` | `CalculadoraTareoTests.cs`; `DescansoProgramadoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-ASIS-006 | ASIS-006 | Ordinario | Llegar antes no genera tiempo pagable. | VIGENTE | Inicio computable limitado por inicio teórico. | `CalculoTareo.cs/CalculadoraTareo` | `JornadaOrdinariaYHorasExtraAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-TAR-001 | TAR-001 | Tardanza | Tolerancia planilla 0; 07:01/19:01=1 min. | VIGENTE | Calcula desde inicio teórico. | `CalculoTareo.cs/CalculadoraTareo` | `TardanzaPlanillaAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener TAR-00..04. | IMPLEMENTADO |
| RQ-TAR-002 | TAR-002 | Tardanza | Campo operativo de tolerancia no perdona planilla. | VIGENTE | Campo persiste, pero no interviene. | `Turno.cs`; `VersionTurno.cs`; `CalculoTareo.cs` | `TardanzaPlanillaAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener compatibilidad hasta limpieza separada. | IMPLEMENTADO |
| RQ-TAR-003 | TAR-003 | Salida | Tolerancia salida 0; 17:21/05:21=1 min. | VIGENTE | Compara con `FinTurno`. | `CalculoTareo.cs/CalculadoraTareo` | `DomingoYSalidaAnticipadaAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener SA-00..04. | IMPLEMENTADO |
| RQ-TAR-004 | TAR-004 | Incidencia | Justificación parcial no oculta otras incidencias. | VIGENTE | Incidencia deriva del resultado postjustificación. | `CalculoTareo.cs`; `ServicioCalculoTareo.cs` | `IncidenciaConJustificacionParcialAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener INC-01..03. | IMPLEMENTADO |

### Horas extra y nocturnidad

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-HE-001 | HE-001 | HE L–V | Umbral 15 min; activado, cuenta desde fin teórico. | VIGENTE | Corte y umbral en calculadora. | `CalculoTareo.cs/CalculadoraTareo` | `ReglasConfirmadasHorasExtraAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos HE-DIU/NOC. | IMPLEMENTADO |
| RQ-HE-002 | HE-002 | HE | Primeros 120 min HE25; resto HE35. | VIGENTE | Reparto diario. | `CalculoTareo.cs/CalculadoraTareo` | `JornadaOrdinariaYHorasExtraAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-HE-003 | HE-003 | Sábado | Todo HE, sin umbral; 120 HE25, resto HE35. | VIGENTE | Rama sábado sin ordinario. | `CalculoTareo.cs/CalculadoraTareo` | `ReglasConfirmadasHorasExtraAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener HE-SAB. | IMPLEMENTADO |
| RQ-HE-004 | HE-004 | Domingo | Todo minuto HE100 desde el primero, sin sustitutorio automático. | VIGENTE | Cálculo y liquidación separan domingo. | `CalculoTareo.cs`; `ServicioCalculoTareo.cs` | `DomingoYSalidaAnticipadaAcceptanceTests.cs`; `LiquidacionPeriodoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener DOM/LIQ. | IMPLEMENTADO |
| RQ-HE-005 | HE-005 | Liquidación | Una base física; primas separadas. | VIGENTE | Liquidación evita doble base. | `ServicioCalculoTareo.cs` | `LiquidacionPeriodoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener LIQ-01..04. | IMPLEMENTADO |
| RQ-NOC-001 | NOC-001 | Nocturnidad | Franja 22:00–06:00. | VIGENTE | Solape temporal calculado. | `CalculoTareo.cs/CalculadoraTareo` | `CalculadoraTareoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato físico. | IMPLEMENTADO |
| RQ-NOC-002 | NOC-002 | Nocturnidad | Es dimensión superpuesta; aporta prima, no segunda base. | VIGENTE | Tareo conserva dimensión; liquidación agrega prima. | `TareoDiario.cs`; `ServicioCalculoTareo.cs` | `LiquidacionPeriodoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato económico de no duplicación. | IMPLEMENTADO |
| RQ-NOC-003 | NOC-003 | Cruce semanal | Vie→sáb regular; sáb→dom segmenta 120/180/322. | VIGENTE | Segmentación a domingo 00:00. | `CalculoTareo.cs/CalculadoraTareo` | `CrucesNocturnosFinDeSemanaAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener NOC-WE. | IMPLEMENTADO |
| RQ-NOC-004 | NOC-004 | Bono nocturno | Debe definirse fórmula normativa final. | NO_DETERMINADO | Existe fórmula operativa actual. | `CalculadoraBonoNocturno.cs` | `CalculadoraBonoNocturnoTests.cs` (conducta actual) | NO_DETERMINADO | Falta confirmar base, prorrateo y alcance normativo. | P2 | Obtener decisión de negocio/legal y después fijar contrato económico. | NO_DETERMINADO |

### Rotaciones y descanso programado

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-ROT-001 | ROT-001 | Rotación | 14+1+7 es referencial; manda programación fechada. | VIGENTE | Programación por tramos. | `ProgramacionCuadrilla.cs` | `RotacionesVariablesAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener programación explícita. | IMPLEMENTADO |
| RQ-ROT-002 | ROT-002 | Rotación | Secuencia finita, sin repetición implícita. | VIGENTE | Resolución se agota. | `ProgramacionCuadrilla.cs` | `RotacionesVariablesAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener ROT-REP-01. | IMPLEMENTADO |
| RQ-ROT-003 | ROT-003 | Modelo | Trabajo exige turno; descanso exige null y un día. | VIGENTE | Dominio + CHECK SQLite. | `ProgramacionCuadrilla.cs`; `ConfiguracionesCatalogos.cs` | `DescansoProgramadoMigracionTests.cs` | NINGUNO | — | NINGUNA | Mantener invariantes. | IMPLEMENTADO |
| RQ-ROT-004 | ROT-004 | Descanso | Sin marcas cero conceptos; trabajado todo HE100. | VIGENTE | Tareo en dos pasadas. | `ServicioCalculoTareo.cs`; `CalculoTareo.cs` | `DescansoProgramadoAcceptanceTests.cs`; `RotacionTurnosTests.cs` | NINGUNO | — | NINGUNA | Mantener ROT-DESC. | IMPLEMENTADO |
| RQ-ROT-005 | ROT-005 | Marcas | Turnos reclaman primero; residual D/D+1 no se reutiliza. | VIGENTE | Selección residual posterior. | `ServicioCalculoTareo.cs` | `RotacionTurnosTests.cs` | NINGUNO | — | NINGUNA | Mantener ROT-MARK. | IMPLEMENTADO |
| RQ-ROT-006 | ROT-006 | Validación | Una marca o frontera ambigua no inventa duración. | VIGENTE | Cero automático y pendiente. | `ServicioCalculoTareo.cs` | `DescansoProgramadoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-ROT-007 | ROT-007 | Autorización HE | Hoja HE no define reloj sin horario explícito. | VIGENTE | No existe fuente horaria de hoja. | NO EXISTE | NO EXISTE | GAP_FUNCIONAL | Falta modelo de programación horaria/autorización si negocio desea usarla. | P2 | Definir formato confiable y crear persistencia/Application/UI antes de consumirla. | NO_IMPLEMENTADO |
| RQ-ROT-008 | ROT-008 | Cuadrillas | Cuadrilla y planta se resuelven históricamente. | VIGENTE | Consulta histórica central. | `ServicioCuadrillas.cs`; `ConsultaContextoLaboral.cs` | `PeriodoLaboralFase3EBCuadrillasTests.cs` | NINGUNO | — | NINGUNA | Mantener PL3EB. | IMPLEMENTADO |

### Feriados

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-FER-001 | FER-001 | Feriado no trabajado | Conserva remuneración ordinaria; no genera HE. | VIGENTE | Derecho diario explícito, separado de minutos; cero trabajo y cero HE100. | `SobretasaDtos.cs`; `ServicioCalculoTareo.cs` | `FER_001_feriado_no_trabajado...` | NINGUNO | — | NINGUNA | Mantener contrato y separación día/minutos. | IMPLEMENTADO |
| RQ-FER-002 | FER-002 | Feriado trabajado | Sin sustitutorio: ordinario + base + 100 %; parcial por minutos. | VIGENTE | Derecho ordinario separado; base y recargo desglosados sobre N minutos físicos únicos. | `SobretasaDtos.cs`; `ServicioCalculoTareo.cs` | `FER_002_dia_completo...`; `FER_002_trabajo_parcial...` | NINGUNO | — | NINGUNA | Mantener contratos; sustitutorio permanece en FER-003. | IMPLEMENTADO |
| RQ-FER-003 | FER-003 | Sustitutorio | Con descanso sustitutorio no corresponde adicional de trabajo+100 %. | VIGENTE | Relación explícita origen↔descanso, alta/anulación auditada, día completo a cero minutos y exclusión económica sin borrar minutos físicos. | `DescansoSustitutorio.cs`; `ServicioDescansosSustitutorios.cs`; `ServicioCalculoTareo.cs`; `DescansosSustitutoriosController.cs` | `DescansoSustitutorioAcceptanceTests`; `DescansoSustitutorioPersistenceTests`; `DescansosSustitutoriosControllerTests` | NINGUNO | — | NINGUNA | Mantener contratos; no inferir historia ni implementar parcialidad sin regla aprobada. | IMPLEMENTADO |
| RQ-FER-004 | FER-004 | Cruce feriado | Cruce hacia feriado conserva inicio; iniciado en feriado mantiene clasificación. | VIGENTE | La jornada usa fecha de inicio y conserva la clasificación hasta D+1. | `CalculoTareo.cs`; `ServicioCalculoTareo.cs` | `FER004_01...`; `FER004_02...` | NINGUNO | — | NINGUNA | Mantener contratos SQLite de ambas fronteras. | IMPLEMENTADO |
| RQ-FER-005 | FER-005 | Día no laborable | Feriado nacional ≠ día no laborable compensable. | VIGENTE | No existe clasificación propia. | NO EXISTE | NO EXISTE | GAP_FUNCIONAL | Requiere catálogo, persistencia y tratamiento separado. | P1 | Modelar tipo de día, carga/calendario y reglas de compensación. | NO_IMPLEMENTADO |
| RQ-FER-006 | FER-006 | Coincidencia | Feriado ordinario+descanso usa una base y prevalece feriado. | VIGENTE | Coincidencia explícita. | `TareoDiario.cs`; `CalculoTareo.cs` | `DescansoProgramadoAcceptanceTests.cs`; `RotacionTurnosTests.cs` | NINGUNO | — | NINGUNA | Mantener ROT-FER-01. | IMPLEMENTADO |
| RQ-FER-007 | FER-007 | 1 de mayo | Coincidencia conserva base única y tratamiento económico especial. | VIGENTE | Dos derechos diarios separados; con trabajo agrega base + recargo 100 % sobre N minutos únicos. | `CalculoTareo.cs`; `SobretasaDtos.cs`; `ServicioCalculoTareo.cs` | `FER_007_1_de_mayo_trabajado...`; `FER_007_1_de_mayo_no_trabajado...` | NINGUNO | — | NINGUNA | Mantener contratos y no convertir derechos diarios en minutos. | IMPLEMENTADO |

### Ausencias, solicitudes, permisos y regularización

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-AUS-001 | AUS-001 | Justificación | Solo aprobada modifica tareo; una vigente trabajador/día. | VIGENTE | Estado + índice filtrado. | `Justificacion.cs`; `ServicioJustificaciones.cs` | `PagoDeJustificacionesAlRecalcularTests.cs`; `UnaJustificacionPorTrabajadorYDiaTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos. | IMPLEMENTADO |
| RQ-AUS-002 | AUS-002 | Licencia pagada | Jornada completa acredita 480, sin HE/nocturnidad. | VIGENTE | Aplicación usa jornada ordinaria configurada. | `CalculoTareo.cs` | `PagoDelDiaJustificadoTests.cs` | NINGUNO | — | NINGUNA | Mantener AUS-PAG. | IMPLEMENTADO |
| RQ-AUS-003 | AUS-003 | Permiso parcial | Usa duración/solape real; no 480 indiscriminado. | VIGENTE | Rama parcial. | `CalculoTareo.cs` | `PermisoParcialTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-AUS-004 | AUS-004 | Permiso horas | Un día; fin posterior a inicio. | VIGENTE | Validadores de solicitud. | `SolicitudAusencia.cs`; `ValidadorLimitesSolicitud.cs` | `ValidadorLimitesSolicitudTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-AUS-005 | AUS-005 | Sustento | Se exige al aprobar; detalle clínico solo subsidiado. | VIGENTE | Servicio + validador. | `ServicioSolicitudes.cs`; `ValidadorDetalleClinico.cs` | `SustentoYAprobacionTests.cs`; `ValidadorDetalleClinicoTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos. | IMPLEMENTADO |
| RQ-AUS-006 | AUS-006 | Límites/solape | Pendiente/aprobada consume límites inclusivos y reserva el rango; rechazada/anulada lo libera. El cruce de extremos también es solape. | VIGENTE | Consulta SQLite por trabajador, estados no terminales y rango inclusivo; límites inclusivos en validador. | `ServicioSolicitudes.cs/GuardarAsync/ValidarLimitesAsync`; `ValidadorLimitesSolicitud.cs` | `ValidadorLimitesSolicitudTests.cs`; `SolapamientoSolicitudesTests.cs` (`AUS006-01/02`) | NINGUNO | — | NINGUNA | Mantener matriz de estados y frontera inclusiva. | IMPLEMENTADO |
| RQ-AUS-007 | AUS-007 | Aprobación | Aprobación debe ser atómica para todos sus días. | VIGENTE | Prevalida todo el rango; cualquier impedimento conserva cabecera pendiente y cero filas nuevas. El éxito guarda cabecera+detalle en un `SaveChanges`, afirma versiones de periodo y conserva el índice único ante carreras. | `ServicioSolicitudes.cs/AprobarAsync`; `GuardiaDePeriodo.cs` | `AprobacionAtomicaSolicitudesTests.cs` (`AUS007-01..03`) | NINGUNO | — | NINGUNA | Mantener prevalidación, guardas de periodo y contratos SQLite. | IMPLEMENTADO |
| RQ-AUS-008 | AUS-008 | Evidencia | Anular debe preservar evidencia derivada. | VIGENTE | Cancela y versiona cada justificación con actor/fecha/motivo; cabecera, detalle y guardas de periodo se confirman juntos. | `ServicioSolicitudes.cs`; `ServicioCalculoTareo.cs`; `GuardiaDePeriodo.cs` | `AUS008-01..06` | NINGUNO | — | NINGUNA | Mantener cancelación lógica, guarda concurrente, rechazo de historia incompleta y prioridad de fila vigente. | IMPLEMENTADO |
| RQ-AUS-009 | AUS-009 | Regularización | Conserva original/corrección/motivo/actores/fechas/validación; parcial mantiene incidencia. | VIGENTE | Flujo separado, versionado y auditado. | `Justificacion.cs`; `ServicioJustificaciones.cs` | `RegularizacionDeMarcacionTests.cs`; `FlujoDeRegularizacionTests.cs` | NINGUNO | — | NINGUNA | Mantener REG-01..04. | IMPLEMENTADO |

### Marcaciones, atribución e importación

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-MAR-001 | MAR-001 | Inmutabilidad | Marca cruda inmutable; anular excluye sin borrar y reactivar revierte. | VIGENTE | Estado de importación gobierna consumo. | `MarcacionCruda.cs`; `ImportacionMarcador.cs` | `AnulacionImportacionTests.cs`; `CargaAnuladaNoCuentaTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-002 | MAR-002 | Atribución | Dueño=equipo+ID exacto+fecha vigente; ceros son significativos. | VIGENTE | Resolutor temporal exacto. | `AtribucionMarcaciones.cs` | `AtribucionMarcacionesTests.cs`; `ConsultasDeAtribucionEnSqliteTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-003 | MAR-003 | Atribución | Se atribuye al calcular/consultar; sin dueño se conserva y no entra a tareo. | VIGENTE | No fija trabajador al importar. | `ServicioCalculoTareo.cs`; `AtribucionMarcaciones.cs` | `AtribucionAlCalcularTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-004 | MAR-004 | Selección | Primera/última son extremos; rebotes <2 min consolidan paridad; interiores se informan. | VIGENTE | Selección de marcas. | `CalculoTareo.cs/CalculadoraTareo` | `CalculadoraTareoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-005 | MAR-005 | Reclamación | Una marca no pertenece a dos jornadas. | VIGENTE | Exclusión de reclamadas. | `ServicioCalculoTareo.cs` | `RotacionTurnosTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-006 | MAR-006 | Formato | Formato por contenido; TXT/DAT comparten contrato. | VIGENTE | Detector + lector automático. | `DetectorFormatoMarcador.cs`; `LectorMarcadorAutomatico.cs` | `DetectorFormatoMarcadorTests.cs`; lectores | NINGUNO | — | NINGUNA | Mantener pruebas de lectores. | IMPLEMENTADO |
| RQ-MAR-007 | MAR-007 | Origen | Planta/equipo coherentes y serie correspondiente. | VIGENTE | Validación de importación. | `ServicioImportacion.cs`; `SerieDelArchivo.cs` | `OrigenDeLaCargaTests.cs`; `SerieDelArchivoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-008 | MAR-008 | Dedupe | Natural=equipo+ID+instante; hash identifica archivo; anulada no bloquea corrección. | VIGENTE | Servicio + índice SQLite. | `ServicioImportacion.cs`; `ConfiguracionesMarcaciones.cs` | `ColisionDeIdEntreEquiposTests.cs`; `ArchivoCargadoEnOtroEquipoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-009 | MAR-009 | Rechazos | Ilegibles se registran; válidas e IDs sin dueño se preservan. | VIGENTE | `ErrorImportacion` separado. | `ImportacionMarcador.cs`; lectores | `ImportacionRespaldoRealTests.cs` | NINGUNO | — | NINGUNA | Mantener clasificación BKDAT. | IMPLEMENTADO |
| RQ-MAR-010 | MAR-010 | Atomicidad | Cabecera, errores, serie, auditoría y marcas confirman o revierten juntas. | VIGENTE | Transacción SQLite tras parsing/dedupe. | `ServicioImportacion.cs` | `AtomicidadImportacionAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener IMP-AT-01..07. | IMPLEMENTADO |
| RQ-MAR-011 | MAR-011 | Cobertura | Cobertura por día/aparato; inactivo/futuro no crea falta. | VIGENTE | Modelo de aparato esperado. | `CoberturaDeCarga.cs` | `CoberturaDeCargaTests.cs`; `CoberturaPorAparatoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-MAR-012 | MAR-012 | BKtransaction | Lector interno es autoridad; SDK diagnóstico; eventos ignorados no son marcas/rechazos. | VIGENTE | Lectores y clasificación UI. | `LectorRespaldoZkteco.cs`; `LectorRespaldoZktecoSdk.cs` | `LectorRespaldoZktecoTests.cs`; `EventosIgnoradosUiTests.cs` | NINGUNO | — | NINGUNA | Mantener lector interno por defecto; no redistribuir SDK sin licencia. | IMPLEMENTADO |

### PeriodoLaboral, planta y reingreso

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-PER-001 | PER-001 | Historia laboral | PeriodoLaboral manda vínculo/empresa; AsignacionPlanta manda planta. | VIGENTE | Contexto histórico central. | `PeriodoLaboral.cs`; `AsignacionPlanta.cs`; `ConsultaContextoLaboral.cs` | `ContextoLaboralHistoricoTests.cs`; suites PL3C | NINGUNO | — | NINGUNA | Mantener autoridad histórica. | IMPLEMENTADO |
| RQ-PER-002 | PER-002 | Legacy | Campos planos son proyección actual/compatibilidad. | VIGENTE | Dual-write controlado. | `Trabajador.cs`; `ServicioTrabajadores.cs` | suites PL3D/PL3EC | NINGUNO | — | NINGUNA | Mantener compatibilidad hasta retirada planificada. | IMPLEMENTADO |
| RQ-PER-003 | PER-003 | Invariantes | Un abierto, sin solapamiento y rango válido. | VIGENTE | Dominio + servicio + índice parcial. | `PeriodoLaboral.cs`; `TrabajadorRepository.cs`; `ConfiguracionesPersonal.cs` | `PeriodoLaboralContratoTests.cs`; PL3D | NINGUNO | — | NINGUNA | Mantener guardas e índice. | IMPLEMENTADO |
| RQ-PER-004 | PER-004 | Alta/cambio | Alta crea periodo/planta; cambio preserva historia y actualiza proyección. | VIGENTE | Servicio de trabajadores. | `ServicioTrabajadores.cs`; `AsignacionPlanta.cs` | `PeriodoLaboralAcceptanceTests.cs`; `ContextoLaboralHistoricoTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos. | IMPLEMENTADO |
| RQ-PER-005 | PER-005 | Cese | Cierra exactamente el abierto sin alterar historia cerrada. | VIGENTE | Persistencia selectiva. | `TrabajadorRepository.cs` | `BajaTrabajadorTests.cs`; `PeriodoLaboralFase3DEscriturasSqliteTests.cs` | NINGUNO | — | NINGUNA | Mantener PL3D. | IMPLEMENTADO |
| RQ-PER-006 | PER-006 | Reingreso | Conserva anterior, crea periodo/planta, actualiza proyección, atómico y sin doble envío. | VIGENTE | Application/UI dedicado. | `ServicioTrabajadores.cs`; `TrabajadoresController.cs` | `PeriodoLaboralFase3ECReingresoTests.cs`; `ReingresoTrabajadorMvcTests.cs` | NINGUNO | — | NINGUNA | Mantener PL3EC. | IMPLEMENTADO |
| RQ-PER-007 | PER-007 | Gap laboral | Gap=`SinPeriodoLaboral`/`SinVinculo`; no inventa contexto. | VIGENTE | Contexto histórico. | `ConsultaContextoLaboral.cs` | `PeriodoLaboralTareoFase3CTests.cs`; reportes 3C | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-PER-008 | PER-008 | Integridad física | Guardas deben cubrir lo que SQLite no impide en periodos cerrados/rango. | VIGENTE | Índice protege abierto; validadores protegen resto en rutas conocidas. | `PeriodoLaboral.cs`; `ConfiguracionesPersonal.cs` | PL3D | DEUDA_MODELO | BD no impide todo solapamiento cerrado ni rango inválido; falta token propio. | P2 | Evaluar trigger/constraint viable y token de concurrencia sin romper SQLite. | PARCIAL |

### PMV

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-PMV-001 | PMV-001 | Frontera | PMV solo escribe `prov_*`; no toca sistema oficial. | VIGENTE | Servicios/tablas separados. | `ServicioImportacionProvisional.cs`; entidades `MarcacionesProvisionales` | `ImportacionProvisionalTests.cs` | NINGUNO | — | NINGUNA | Mantener aislamiento. | IMPLEMENTADO |
| RQ-PMV-002 | PMV-002 | Importación | Lectores compartidos, transacción y dedupe equipo+ID+instante. | VIGENTE | Importador provisional. | `ServicioImportacionProvisional.cs` | `ImportacionProvisionalTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-PMV-003 | PMV-003 | Atribución | Dinámica por equipo+ID+vigencia; sin dueño se conserva. | VIGENTE | Consulta provisional. | `ServicioConsultaMarcacionProvisional.cs` | `ConsultaMarcacionProvisionalTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-PMV-004 | PMV-004 | Límite funcional | PMV no calcula conceptos oficiales ni escribe el modelo oficial. | VIGENTE | Importación solo persiste `prov_*`; consulta genera DTO en memoria y exportación conserva 13 columnas sin conceptos de planilla. | `ServicioImportacionProvisional.cs`; `ServicioConsultaMarcacionProvisional.cs`; `ExportadorMarcacionesProvisionales.cs` | `ImportacionProvisionalTests.PMV004-01` | NINGUNO | — | NINGUNA | Mantener contrato negativo integral PMV→modelo oficial. | IMPLEMENTADO |
| RQ-PMV-005 | PMV-005 | Historia | Planta mostrada viene de AsignacionPlanta histórica. | VIGENTE | Contexto laboral en consulta. | `ServicioConsultaMarcacionProvisional.cs`; `ConsultaContextoLaboral.cs` | `PeriodoLaboralFase3EBCuadrillasTests.cs` | NINGUNO | — | NINGUNA | Mantener PL3EB. | IMPLEMENTADO |
| RQ-PMV-006 | PMV-006 | Seguridad | Alcance por planta en catálogo, historial, importar, consultar y exportar. | VIGENTE | Proveedor de alcance en servicios. | `AlcancePlantasSistema.cs`; servicios PMV | `AislamientoPmvAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener SEC-01..04. | IMPLEMENTADO |
| RQ-PMV-007 | PMV-007 | Rango | 1–30 normal; 31–40 advierte; 41+ consulta sí/exportación no. | VIGENTE | Controlador/consulta/exportador. | `MarcacionesProvisionalesController.cs`; exportador PMV | pruebas consulta/exportador PMV | NINGUNO | — | NINGUNA | Mantener límites. | IMPLEMENTADO |

### Reportes, tareo y liquidación

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-REP-001 | REP-001 | Tareo | Proyección idempotente trabajador+fecha; reportar no recalcula. | VIGENTE | Upsert y lectura persistida. | `ServicioCalculoTareo.cs`; `ServicioReportes.cs` | `ServicioReportesTests.cs` | NINGUNO | — | NINGUNA | Mantener separación cálculo/lectura. | IMPLEMENTADO |
| RQ-REP-002 | REP-002 | Periodo | Cerrado impide escritura; cierre concurrente invalida guardado. | VIGENTE | Guardia/versionado. | `GuardiaDePeriodo.cs`; `ServicioCalculoTareo.cs` | `CierreDePeriodoConcurrenteTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-REP-003 | REP-003 | Alcance reporte | Resumen/excepciones admiten varios rangos; tareo final exige periodo. | VIGENTE | Resolutor de rango. | `RangoReporte.cs/ResolutorRangoReporte` | `ResolutorRangoReporteTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-REP-004 | REP-004 | Historia reporte | Vínculo/empresa/planta históricos; otros maestros deben ser reproducibles al reemitir. | VIGENTE | Tres dimensiones históricas resueltas; maestros residuales actuales. | `ConsultaContextoLaboral.cs`; `ServicioReportes.cs` | `PeriodoLaboralReportesFase3CTests.cs`; `VigenciaDelAreaEnReportesTests.cs` | GAP_FUNCIONAL | P1-06 queda reducido a DNI/puesto/hijos/CECO y otros maestros no congelados. | P1 | Inventariar campos residuales y elegir snapshot o resolución temporal por campo. | PARCIAL |
| RQ-REP-005 | REP-005 | Frescura | Cambios/borrados consumidos marcan tareo desactualizado. | VIGENTE | MIN relevante + fuentes + auditoría. | `ServicioReportes.cs`; `InterceptorAuditoria.cs` | `VigenciaTareoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener VIG. | IMPLEMENTADO |
| RQ-REP-006 | REP-006 | Reintegros | Una fila trabajador/periodo, no negativa, manual y fuera del cálculo diario. | VIGENTE | Entidad/servicio separados. | `ReintegroTareo.cs`; `ServicioReintegrosTareo.cs` | `ServicioReintegrosTareoTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-REP-007 | REP-007 | Excel | DNI/texto como texto seguro, neutralizado contra fórmulas. | VIGENTE | Sanitizador/exportadores. | `ExportadorReportesExcel.cs` | `SanitizadorExcelTests.cs`; `ExportadorTareoFinalTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-REP-008 | REP-008 | Tasas/historia | Política temporal uniforme por empresa y congelamiento residual. | NO_DETERMINADO | Resoluciones actuales difieren por inicio/fin/día. | `ServicioCalculoTareo.cs`; `ServicioReportes.cs` | NO EXISTE para política objetivo | NO_DETERMINADO | Falta decisión sobre empresa y fecha autoritativa por concepto. | P2 | Acordar matriz concepto→empresa→fecha y luego crear contratos. | NO_DETERMINADO |

### Seguridad, arranque y auditoría

| ReqId | ReglaId | Categoria | Requisito | EstadoRegla | ImplementacionActual | Archivo/Clase | Test | GapTipo | GapDetalle | Severidad | AccionRecomendada | EstadoFinal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RQ-SEG-001 | SEG-001 | Autorización | Operaciones respetan rol y alcance de planta. | VIGENTE | Identity + proveedor de alcance. | `AlcancePlantasSistema.cs`; servicios | `AislamientoPorPlantaTests.cs`; `AislamientoPmvAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contratos de autorización. | IMPLEMENTADO |
| RQ-SEG-002 | SEG-002 | First run | Sin usuarios/credenciales, `/setup` de un solo uso; sin secreto real embebido. | VIGENTE | Inicializador + controlador setup. | `InicializadorSeguridad.cs`; `ConfiguracionInicialController.cs` | `FirstRunTests.cs`; `InicializadorSeguridadTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-SEG-003 | SEG-003 | Health | `/vivo` prueba proceso; `/estado` valida DB y nunca migra. | VIGENTE | Verificador read-only. | `IVerificadorEstadoOperativo.cs`; `Program.cs` | `StartupHealthTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-SEG-004 | SEG-004 | Readiness | Starting/Failed bloquea funcionalidad; recuperación externa rehabilita. | VIGENTE | Estado/middleware. | `EstadoPreparacionAplicacion.cs`; `Program.cs` | `StartupHealthTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |
| RQ-SEG-005 | SEG-005 | Instalación local | Loopback, instancia única y ProgramData persistente. | VIGENTE | Hosting/installer local. | `HostingLocalWindows.cs`; `InstanciaLocalWindows.cs` | ciclo UAC; `FirstRunTests.cs` | NINGUNO | — | NINGUNA | Mantener runbook de instalación. | IMPLEMENTADO |
| RQ-SEG-006 | SEG-006 | Credencial expuesta | Credencial comprometida debe rotarse/inutilizarse antes de producción. | VIGENTE | Sustituto creado por flujo Web; cuenta anterior inactiva, bloqueo indefinido y sello rotado. | `UsuariosController.cs`; `ServicioUsuarios.cs` | `P0_SEG006_CREDENTIAL_ROTATION.md`: login nuevo, revocación y prueba antigua fallida | NINGUNO | Exposición histórica contenida; la credencial ya no autentica la instancia activa conocida. | NINGUNA | Mantener cuarentena y no reutilizar snapshots/ZIP históricos. | RESUELTO |
| RQ-AUD-001 | AUD-001 | Auditoría | Escrituras sensibles auditan actor/fecha/operación; tareo derivado deja log agregado. | VIGENTE | Interceptor + logging de recálculo. | `InterceptorAuditoria.cs`; `ServicioCalculoTareo.cs` | `AuditoriaTransversalTests.cs` (`AUD001-01/02`); `IMP_AT_01` | NINGUNO | Contrato SQLite transversal de inclusión, baja lógica, exclusión y log agregado; rollback ya cubierto. | NINGUNA | Mantener contratos transversales y atomicidad. | IMPLEMENTADO |
| RQ-AUD-002 | AUD-002 | Auditoría/frescura | Cambios/borrados relevantes invalidan vigencia. | VIGENTE | Auditoría alimenta evaluador. | `InterceptorAuditoria.cs`; `ServicioReportes.cs` | `VigenciaTareoAcceptanceTests.cs` | NINGUNO | — | NINGUNA | Mantener contrato. | IMPLEMENTADO |

## HISTORICOS_Y_SUPERADOS

No participan en conteos de gaps vigentes.

| Regla anterior | Estado | Regla vigente | Motivo del reemplazo |
|---|---|---|---|
| HIST-001: 480 desde entrada como frontera universal | HISTORICO | ASIS-001/HE-001; 480 solo AUS-002 | R-P0-03.1 y R-P1-08 separaron trabajo y crédito pagado. |
| HIST-002: tolerancia 5 en planilla | SUPERADO | TAR-001/TAR-003 | R-P0-02 confirmó tolerancia cero. |
| HIST-003: 14+1+7 universal e infinito | SUPERADO | ROT-001/002 | Programación explícita y secuencia finita. |
| HIST-004: domingo simplemente “como feriado” | SUPERADO | HE-004 | Descanso semanal trabajado=HE100. |
| HIST-005: campos planos son historia | SUPERADO | PER-001/002 | PeriodoLaboral/AsignacionPlanta son autoridades. |
| HIST-006: importación confirma en dos unidades | SUPERADO | MAR-010 | R-P1-07 agregó atomicidad. |
| HIST-007: PMV cruza plantas | SUPERADO | PMV-006 | R-P0-01 cerró aislamiento. |
| HIST-008: fallo migración permite health OK | SUPERADO | SEG-003/004 | P1-STARTUP-HEALTH separó liveness/readiness. |
| HIST-009: descanso requiere turno ficticio | SUPERADO | ROT-003..006 | R-P1-05 implementó tipo explícito. |

## REQUISITOS_SIN_TEST

Se consideran sin contrato directo del requisito objetivo:

| ID | Motivo |
|---|---|
| ROT-007 | No existe implementación ni test de hoja HE como programación horaria. |
| FER-005 | No existe clasificación de día no laborable compensable. |
| REP-008 | No existe política objetivo ni contrato de tasas por empresa/fecha. |

NOC-004 y REP-004 tienen pruebas parciales o de conducta actual; no se cuentan como “sin
test”, pero sus gaps permanecen. FER-001/002/004/007 quedaron cerrados por P0-FER-002/007.

## GAPS_POR_TIPO

| GapTipo | Cantidad | IDs |
|---|---:|---|
| BUG | 0 | — |
| GAP_FUNCIONAL | 3 | ROT-007, FER-005, REP-004 |
| DEUDA_TECNICA | 0 | — |
| DEUDA_MODELO | 1 | PER-008 |
| DEUDA_UX | 0 | — |
| DEUDA_TEST | 0 | — |
| RIESGO_SEGURIDAD | 0 | — |
| RIESGO_DATOS | 1 | ASIS-001 |
| NO_DETERMINADO | 2 | NOC-004, REP-008 |

Implementados sin gap: **79**. Reglas vigentes/no determinadas con gap: **7**. Total
principal: **86**.

## GAPS_POR_PRIORIDAD

| Prioridad | IDs |
|---|---|
| P0 | ASIS-001 |
| P1 | FER-005, REP-004 |
| P2 | NOC-004, ROT-007, PER-008, REP-008 |
| P3 | — |

## ACCIONES_PRIORIZADAS

1. Ejecutar OP-01 con versión DIU/NOC fechada y recálculo controlado (`ASIS-001`).
2. Modelar día no laborable compensable por separado (`FER-005`).
3. Resolver P1-06 campo por campo con snapshot o vigencia (`REP-004`).
4. Definir política económica de nocturnidad y tasas por empresa/fecha (`NOC-004`, `REP-008`).

## Validación de integridad

- Reglas principales: 86; históricos/superados: 9.
- Cada `ReqId` y `ReglaId` aparece una vez en la matriz principal.
- Todo gap tiene acción y severidad; toda fila sin gap usa `NINGUNA`.
- PeriodoLaboral, BKDAT, first-run y ERD no se reabren.
- PMV permanece separado del sistema oficial y su aislamiento no es gap.

## Validación técnica de la fase

- P0-FER-002/007, 2026-10-19: `dotnet build ControlMarcacion.sln -c Release --no-restore
  -m:1 -p:UseSharedCompilation=false`, correcto, 0 errores y 0 advertencias.
- Regresión dirigida: 15/15 verdes.
- Suite completa: 958/958 verdes, 0 fallos y 0 omitidos.
- La incidencia histórica de conexión entre `vstest.console` y `testhost` quedó superada al
  ejecutar el runner fuera del sandbox de IPC; no fue un fallo funcional.
- P1-FER-001, 2026-10-22: se revalidó RQ-FER-001 ante un contexto de entrada desactualizado.
  La fila conserva `GapTipo=NINGUNO`, severidad `NINGUNA` y estado `IMPLEMENTADO`; no se
  requirió cambio productivo ni contrato adicional.
- P1-FER-004, 2026-10-22: `FER004-01/02` validan sobre SQLite la frontera completa
  catálogo→servicio→tareo. Regresión dirigida 7/7; suite 977/977; build 0/0. Se retiró
  FER-004 de `REQUISITOS_SIN_TEST` sin cambiar lógica productiva.
- P2-AUS-007, 2026-10-23: reproducción roja previa confirmada; aprobación todo-o-nada con
  prevalidación, guardado único, índice único y guardas de periodo. `AUS007-01..03` 3/3,
  regresión inmediata 123/123, suite 980/980 y build 0 errores/0 advertencias.
- P2-AUS-008, 2026-10-23: tres fallos rojos confirmaron el borrado físico; cancelación lógica,
  resolución versionada y guardas de periodo cerraron el riesgo. `AUS008-01..06` 6/6,
  Infrastructure 520/520, suite 986/986 y build 0 errores/0 advertencias.
- P2-AUS-006, 2026-10-23: la implementación existente quedó confirmada sin cambio productivo.
  `AUS006-01/02` cubren sobre SQLite los cuatro estados y la frontera inclusiva; regresión
  dirigida 127/127, suite 991/991 y build 0 errores/0 advertencias.
- P2-PMV-004, 2026-10-23: `PMV004-01` recorre importación, consulta y exportación sobre SQLite;
  confirma escrituras exclusivas en `prov_*`, cero cambios en importación/marcación/tareo/vínculo
  oficiales y ausencia de conceptos de liquidación. Regresión PMV 42/42, suite 992/992 y build 0/0.
- P2-AUD-001, 2026-10-23: `AUD001-01/02` validan sobre SQLite la fila física de una escritura
  sensible, actor, identidad, baja lógica, motivo y cambios; el tareo derivado queda excluido por
  fila y emite un único log agregado 4200. `IMP_AT_01` conserva la evidencia de rollback.
  Regresión dirigida 4/4, suite 994/994 y build 0/0; sin cambio productivo ni migración.
