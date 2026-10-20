# Reglas de aceptación verificables

Fecha de corte: 2026-10-06  
Fase: 6.1 — Reconciliación maestra de requisitos  
Alcance: especificación y pruebas; no contiene correcciones de producción.

Este documento es la **fuente autoritativa de comportamiento esperado**. El código y los tests existentes describen implementación o contratos de su fecha cuando contradicen una confirmación posterior.

## 1. Reglas de negocio confirmadas

Estas reglas prevalecen sobre `CLAUDE.md`, comentarios, semillas y pruebas antiguas cuando exista contradicción:

1. Turnos regulares (R-P0-03.1): **DIU 07:00–17:22** y **NOC 19:00–05:22 del día siguiente**. El ordinario de lunes a viernes llega hasta el **fin teórico del turno** (`FinTurno`, con cruce de medianoche); no se infiere desde 480 minutos contados a partir de la marca de entrada. La tardanza no corre el corte, y llegar antes no genera HE. Una jornada completa (07:00–17:22 o 19:00–05:22) da tardanza 0, salida anticipada 0 y HE 0; el NOC conserva sus minutos nocturnos.
1a. **Umbral HE de lunes a viernes: 15 minutos.** Si el exceso sobre el fin teórico es menor a 15 min, HE = 0. Desde 15 min, HE = exceso completo desde el fin teórico: los 15 minutos no se pierden, solo activan. DIU: 17:36 = 0; 17:37 = 15; 17:38 = 16; 18:22 = 60; 19:22 = 120. NOC: 05:36 = 0; 05:37 = 15; 06:22 = 60. Primeras 120 min de HE al 25 %, resto al 35 %. Los feriados conservan su comportamiento previo; el umbral no se les aplica.
1b. **NOC viernes→sábado:** la jornada se atribuye al viernes y el cruce de calendario no convierte 00:00–05:22 en HE de sábado. Solo el exceso posterior al fin teórico puede ser HE y conserva el umbral regular: 05:22 = 0; 05:37 = 15.
1c. **NOC sábado→domingo:** la jornada se atribuye al sábado, pero su clasificación económica se segmenta a domingo 00:00. El tramo sabatino es HE25/HE35 (primeros 120 min al 25 %, resto al 35 %) y todo minuto desde domingo 00:00 es HE100. El umbral de 15 minutos no aplica a esa frontera: 00:01 ya aporta 1 HE100.
1d. **Licencia/ausencia pagada completa (R-P1-08):** acredita exactamente **480 minutos = 8 horas efectivas sin almuerzo**, en DIU y NOC, aunque el intervalo del turno regular sea 622 minutos. El crédito no genera HE25/HE35/HE100 ni nocturnidad por sí solo. Los permisos parciales conservan su duración/efecto real y no reciben 480 indiscriminadamente.
2. Sábado: **todo** minuto trabajado es sobretiempo, desde el primer minuto y **sin umbral**; primeros 120 minutos al 25 % y el resto al 35 %; cero ordinarios.
3. Domingo: descanso semanal. Si se trabaja, **todo el tiempo son horas extra al 100 %** desde el primer minuto y **sin umbral**, sin descanso sustitutorio automático y sin repartir el mismo tiempo como ordinario, 25 % o 35 %.
4. Patrón operativo conocido: 14 días diurnos + 1 descanso móvil + 7 noches. **No es ciclo universal ni repetición obligatoria**. Pueden existir tramos diurnos/nocturnos, duraciones distintas, programación futura y cambios manuales sin alterar el pasado. El descanso dentro de una rotación es de un día; el modelo actual no lo representa naturalmente sin turno.
5. Franja nocturna: 22:00–06:00. La fórmula económica exacta permanece separada y requiere validar configuración/base normativa.
6. Para planilla, tolerancia de tardanza: **0 minutos**, en DIU y NOC (07:01 = 1; 07:05 = 5; 19:01 = 1; 19:05 = 5). Un campo operativo/configurado de 5 minutos no debe perdonar tardanza de planilla.
7. Para planilla, tolerancia de salida anticipada: **0 minutos**, contra el `FinTurno` teórico real. DIU: 17:22=0; 17:21=1; 17:17=5. NOC: 05:22=0; 05:21=1; 05:17=5. Un campo operativo/configurado no debe perdonar el descuento.
8. `MarcacionCruda` es inmutable. Toda corrección se registra separadamente con original, valor corregido, motivo, usuario, fecha, validación y auditoría; nunca se sobrescribe el hecho biométrico.
9. PMV es provisional y separado: decodifica, consulta, cruza, agrupa, exporta y traza, pero no calcula conceptos oficiales ni modifica marcaciones, tareo, trabajadores o asignaciones oficiales.
10. **Atomicidad de importación principal (R-P1-07):** `ImportacionMarcador`, sus `ErrorImportacion`, una serie aprendida, su auditoría y las `MarcacionCruda` de la operación se confirman juntas o se revierten juntas ante un fallo técnico. Los errores funcionales de línea sí pueden persistir junto con las marcas válidas de una importación exitosa.

### 1.1 Feriado nacional — FER-NAC-01..08

- **FER-NAC-01:** el feriado nacional no trabajado conserva la remuneración ordinaria que
  le corresponde; por sí solo no genera HE ni sobretasa.
- **FER-NAC-02:** trabajado sin descanso sustitutorio: remuneración ordinaria del feriado +
  base del trabajo efectivo + sobretasa de 100 % sobre esa base. En un día completo son
  tres remuneraciones diarias en total y dos adicionales respecto del sueldo ordinario ya
  pagado. “Sobretasa 100 %” no significa pago total 100 % ni pago total doble.
- **FER-NAC-03:** si el trabajo es parcial, base y 100 % se prorratean por los minutos
  efectivamente trabajados; no se completa automáticamente una jornada.
- **FER-NAC-04:** si existe descanso sustitutorio, bajo la regla general no corresponde el
  adicional de base del trabajo + 100 %. El sistema debe distinguir ambas variantes.
- **FER-NAC-05:** una jornada iniciada en día laborable que cruza hacia un feriado conserva
  el tratamiento de su día de inicio; el tramo posterior a medianoche no se reclasifica.
- **FER-NAC-06:** una jornada iniciada en el feriado se evalúa como trabajo en feriado,
  incluso si termina al día siguiente, sin duplicar minutos físicos.
- **FER-NAC-07:** feriado nacional y día no laborable compensable son conceptos distintos;
  el segundo no activa automáticamente base + 100 %.
- **FER-NAC-08:** los minutos de feriado trabajado constituyen una única cantidad física.
  Su valorización aplica base + 100 %; la remuneración ordinaria del feriado es un concepto
  separado y no duplica minutos en `TotalHorasEquivalentes`.

La coincidencia `FeriadoNacional + DescansoProgramado` se cierra mediante BD02-D01..D09:
un feriado ordinario trabajado se clasifica únicamente como `FeriadoTrabajado` sobre una
base física; el 1 de mayo conserva su tratamiento económico especial.

### 1.2 Descanso programado — BD01-D01..D03

`BD_01_REST_WORK_BOUNDARIES.md` confirma por código que las jornadas con turno seleccionan
marcas mediante su ventana, excluyen las anteriores al fin de la jornada previa, mantienen
`reclamadas`, consolidan rebotes y calculan con los extremos brutos. Un descanso no tiene
ventana ni autorización implícita de cruce de medianoche.

La regla aprobada es: reclamar primero todas las jornadas con turno; usar para el descanso
solo el residuo; exigir primera marca válida no reclamada en la fecha del descanso; admitir
salida en D+1 solo antes de la próxima jornada y si no fue reclamada; y enviar todo caso
ambiguo a una confirmación auditada. Una marca produce cero minutos e incidencia. Con dos o
más marcas inequívocas se usan los extremos brutos; las interiores no se emparejan. No se
deduce refrigerio ni se inventa el extremo faltante. La hoja de HE del coordinador es
evidencia/autorización, no una ventana temporal automática.

### 1.3 Coincidencia feriado + descanso — BD02-D01..D09

- Sin trabajo: cero minutos y ningún descanso físico duplicado.
- Feriado ordinario trabajado: N minutos físicos delimitados por BD-01 se clasifican como
  `FeriadoTrabajado`; no se suma `DescansoSemanalTrabajado` sobre N.
- Sin sustitutorio: remuneración ordinaria del feriado + base del trabajo + 100 %, sin
  duplicar minutos.
- Con sustitutorio: no corresponde el adicional general; el dato aún no existe.
- El 1 de mayo coincidente con descanso conserva cuatro componentes económicos si se
  trabaja y dos si no se trabaja, siempre con minutos físicos únicos.
- Los cruces nocturnos conservan la clasificación por fecha de inicio.

Documento detallado: `BD_02_HOLIDAY_REST_COINCIDENCE.md`.

## 2. Matriz de reglas vigentes

| ID | Regla | Tipo de fuente | Requisito vigente | Comportamiento actual | Test | Estado |
|---|---|---|---|---|---|---|
| VIG-HE-01 | Corte ordinario L–V | NEGOCIO CONFIRMADO | Ordinario hasta el fin teórico (DIU 17:22, NOC 05:22 +1); tardanza no corre el corte | `CalculadoraTareo.Repartir` corta en `FinTurno`; `MinutosJornadaOrdinaria` no interviene | HE-01/02, HE-DIU, HE-NOC, canónicos, tardanza-corte | COINCIDE (código) |
| VIG-HE-02 | Sábado | NEGOCIO CONFIRMADO | Todo trabajado es HE desde el primer minuto, sin umbral: 120 min 25 %, resto 35 % | Sábado laborable: 0 ordinarios, todo extra | HE-03/04, HE-SAB | COINCIDE |
| VIG-HE-03 | Umbral HE L–V | NEGOCIO CONFIRMADO | Exceso < 15 min = 0 HE; ≥ 15 min = todo desde el fin teórico | `Jornada` recorta el cómputo al fin teórico si el exceso es < 15 min; no aplica a sábado, domingo ni feriados | HE-DIU-00..07, HE-NOC-00..05 | COINCIDE |
| VIG-DOM-01 | Domingo | NEGOCIO CONFIRMADO | Descanso semanal; todo minuto trabajado es HE al 100 % desde el primer minuto | `DescansoSemanal` con 0 ordinarios/25/35; liquidación excluye cualquier base ordinaria superpuesta y aplica el 100 % una sola vez | HE-05, DOM-01..05, LIQ-04 | COINCIDE |
| VIG-TURNO-01 | Configuración DIU/NOC | NEGOCIO CONFIRMADO | DIU 07:00–17:22; NOC 19:00–05:22 | Semilla `Turno`/`VersionTurno` (vigente desde 2000-01-01): DIU 07:00–19:00, NOC 19:00–07:00 | — | **CONFIGURACIÓN DE DATOS REQUIERE PLAN DE ACTUALIZACIÓN**: nueva versión de turno con «Rige desde»; sin migración ni reescritura histórica |
| VIG-TAR-01 | Tardanza de planilla | NEGOCIO CONFIRMADO | 07:01=1 min; 07:05=5 min; 19:01=1; 19:05=5 | Cuenta desde la entrada teórica; configuración histórica no interviene | TAR-00..04, TAR-NOC | COINCIDE |
| VIG-SAL-01 | Salida anticipada de planilla | NEGOCIO CONFIRMADO | 17:22=0; 17:21=1; 17:17=5; 05:22=0; 05:21=1; 05:17=5 | Cuenta hasta `FinTurno` teórico; configuración histórica no interviene | SA-00..04, SA-NOC | COINCIDE |
| VIG-NOC-01 | Franja nocturna | NEGOCIO CONFIRMADO | 22:00–06:00 | Calcula solape en esa franja | tests nocturnidad existentes | COINCIDE |
| VIG-NOC-02 | Fórmula económica nocturna | REQUISITO PENDIENTE | Separada de la franja; validar base legal/configuración | Liquidación usa la tasa vigente como prima superpuesta, sin repetir la base; bono conserva su fórmula separada | LIQ-03, LIQ-HE-NOC-01 + reportes | SIN DOBLE BASE; fórmula económica definitiva aún REQUIERE NEGOCIO |
| VIG-NOC-WE-01 | NOC viernes→sábado | NEGOCIO CONFIRMADO | Mantiene jornada regular; el sábado calendario no convierte el tramo previo al fin en HE | Se atribuye al viernes; solo el exceso posterior a `FinTurno` puede ser HE y conserva umbral 15 | NOC-WE-01/02 | COINCIDE |
| VIG-NOC-WE-02 | NOC sábado→domingo | NEGOCIO CONFIRMADO | HE25/35 hasta domingo 00:00; desde allí HE100 sin umbral; fecha de jornada sigue sábado | `Repartir` corta el tramo sabatino en domingo 00:00; HE100 es el remanente físico derivado | NOC-WE-03..06 | COINCIDE |
| VIG-ROT-01 | Rotación flexible | NEGOCIO CONFIRMADO | Tramos/duraciones/cambios variables; 14+1+7 es ejemplo | `Trabajo`/`Descanso`, FK nullable y secuencia finita | ROT-01..03, ROT-DESC-01..09, ROT-REP-01 | COINCIDE |
| BD-01 | Delimitación de trabajo en descanso | NEGOCIO CONFIRMADO | Propiedad por primera marca válida de D; jornadas regulares primero; residuo no reclamado; posible cierre D+1 antes de la siguiente jornada; extremos si inequívocos; ambigüedad a validación | Dos pasadas con identidad de marca, frontera de la siguiente jornada y regularización auditada | ROT-MARK-01..06 | COINCIDE |
| BD-02 | Feriado ordinario + descanso programado | NEGOCIO CONFIRMADO | Sin trabajo, cero minutos; trabajado, una base N clasificada como FeriadoTrabajado, sin segundo DescansoSemanalTrabajado | `Feriado` prevalece y conserva una sola cantidad física | ROT-FER-01 | COINCIDE |
| BD-02-1M | 1 de mayo + descanso programado | NEGOCIO CONFIRMADO | Tratamiento especial: cuatro componentes si trabaja, dos si no; minutos físicos únicos | Dos derechos diarios separados; con trabajo, base + 100 % sobre N minutos únicos | FER-007 trabajado/no trabajado + ROT-FER-02 | COINCIDE |
| FER-NAC-01 | Feriado nacional no trabajado | NEGOCIO CONFIRMADO | Remuneración ordinaria separada; cero HE/sobretasa por no trabajar | Derecho diario explícito; cero minutos y cero concepto trabajado | FER-001 directo | COINCIDE |
| FER-NAC-02 | Feriado trabajado sin sustitutorio | NEGOCIO CONFIRMADO | Una cantidad física; adicional = base del trabajo + 100 % | `FeriadoTrabajado` desglosa N de base + N de recargo; derecho ordinario separado | FER-002 completo | COINCIDE; sustitutorio separado en FER-003 |
| FER-NAC-03 | Trabajo parcial en feriado | NEGOCIO CONFIRMADO | Base y 100 % proporcionales a minutos reales | Base y recargo usan los extremos reales, sin completar jornada | FER-002 parcial | COINCIDE |
| FER-NAC-04 | Descanso sustitutorio | NEGOCIO CONFIRMADO | Con sustitutorio no se genera el adicional general por trabajo + 100 % | No existe campo, entidad ni workflow de descanso sustitutorio | Contrato futuro FER-NAC-04 | NO IMPLEMENTADO |
| FER-NAC-05 | Jornada cruza hacia feriado | NEGOCIO CONFIRMADO | Conserva tratamiento de la fecha de inicio | `EsFeriado` se resuelve con `FechaJornada`; un NOC previo no toma el feriado siguiente | FER-NAC-05 directo | COINCIDE |
| FER-NAC-06 | Jornada inicia en feriado | NEGOCIO CONFIRMADO | Se evalúa como feriado aunque termine al día siguiente | `EsFeriado` usa la fecha de inicio y clasifica la jornada completa una sola vez | FER-NAC-06 directo | COINCIDE |
| FER-NAC-07 | Feriado ≠ no laborable compensable | NEGOCIO CONFIRMADO | Clasificación explícita y efectos diferentes | `Feriado` distingue alcance nacional por `PlantaId = null`, pero mezcla bajo el mismo concepto fechas locales/paradas y no modela día compensable | Contrato futuro FER-NAC-07 | GAP DE MODELO |
| FER-NAC-08 | Liquidación de feriado | NEGOCIO CONFIRMADO | Minutos físicos únicos; valorización base + 100 %; ordinario del feriado separado | R-P1-01 excluye ordinarios superpuestos y liquida `FeriadoTrabajado` sobre una sola base | Contrato futuro FER-NAC-08 | COINCIDE EN CÓDIGO; falta contrato directo |
| VIG-LIQ-01 | Categorías superpuestas | DOCUMENTACIÓN VIGENTE + NEGOCIO | Una base no se suma dos veces | Nocturnidad aporta solo prima; ordinarios se excluyen de días no laborables trabajados y HE100/descanso aporta su base una vez | LIQ-03/04, LIQ-HE-NOC-01 | COINCIDE |
| VIG-PL-01 | Un vínculo laboral abierto | CÓDIGO + INVARIANTE | Editar no duplica; cese cierra | R-P1-02: el repositorio de escritura carga solo `PeriodosLaborales` abiertos; edición y baja funcionan entre `DbContext` distintos | PL-01/02 | COINCIDE EN CÓDIGO; datos históricos pendientes |
| VIG-INC-01 | Justificación parcial | NEGOCIO CONFIRMADO | No oculta incidencia aún pendiente | R-P1-03 decide la incidencia sobre el resultado posterior a aplicar la justificación; una parcial coexiste con el problema restante | INC-01..03, INC-PARCIAL-01 | COINCIDE |
| VIG-PMV-01 | Aislamiento por planta | DOCUMENTACIÓN VIGENTE | Admin solo opera plantas asignadas | PMV aplica `IProveedorAlcancePlantas` en listado/historial, importación y consulta/exportación (R-P0-01) | SEC-01..04 | COINCIDE |
| VIG-TAREO-01 | Frescura por alcance | REQUISITO CONFIRMADO | “Actualizado” significa que no existe entrada relevante posterior al cálculo aplicable a periodo + plantas visibles | R-P1-04 compara contra el `CalculadoEn` más antiguo del alcance, consulta las entradas que consume el motor y usa auditoría para borrados | VIG-01..05, VIG-07..09 | COINCIDE; VIG-06 N/A |
| VIG-REG-01 | Regularización inmutable y auditada | NEGOCIO CONFIRMADO | Corrección separada con original, valor, motivo, actor, fecha, validación y auditoría | Marca original, corrección, workflow y bitácora se conservan en los flujos caracterizados | REG-01..04 | COINCIDE |
| VIG-PMV-02 | Frontera funcional PMV | NEGOCIO CONFIRMADO | Sin tareo/conceptos oficiales ni mutación de maestros | Escribe solo `prov_*` y lee padrón para atribuir | suite PMV | COINCIDE |
| VIG-AUS-PAG-01 | Crédito de ausencia pagada completa | NEGOCIO CONFIRMADO | 480 min efectivos sin almuerzo, independiente del intervalo DIU/NOC de 622; sin HE ni nocturnidad automática | `Aplicar` usa `Turno.MinutosJornadaOrdinaria`; DIU/NOC vigentes conservan 480 | AUS-PAG-01..05 | COINCIDE |
| VIG-IMP-AT-01 | Importación principal atómica | INVARIANTE DE INTEGRIDAD | Cabecera, errores, serie aprendida, auditoría y marcas se confirman o revierten juntas; errores de línea válidos no abortan | R-P1-07 abre una transacción SQLite después de parsing/dedupe y conserva los `SaveChanges` necesarios dentro de ella | IMP-AT-01..07 | COINCIDE |

La documentación histórica que defiende 480 minutos como frontera universal de una jornada trabajada, cinco minutos de tolerancia de tardanza/salida, DIU 07:00–19:00, NOC 19:00–07:00, HE desde el primer minuto tras el fin de L–V o domingo meramente “como feriado” queda reemplazada. Los 480 sí permanecen vigentes para licencia/ausencia pagada completa. La repetición universal 14+1+7 queda **OBSOLETA**. El contrato R-P0-03 «07:00–17:23 → 1 HE» queda reemplazado por el umbral: 17:23 → 0 HE.

## 3. Casos ejecutables incorporados

| Regla | Test | Método | Tipo | Ejecutado | Resultado |
|---|---|---|---|---|---|
| RN-046/RN-047 · HE-01 | `ReglasConfirmadasHorasExtraAcceptanceTests` | `HE_01_lunes_hasta_17_22_es_integramente_ordinario` | DIRECTO | Sí | PASA (R-P0-03.1; fixture DIU 07:00–17:22) |
| RN-046/RN-047 · HE-02 | `ReglasConfirmadasHorasExtraAcceptanceTests` | `HE_02_lunes_dos_horas_despues_de_17_22_son_extra_25` | DIRECTO | Sí | PASA: 622 + 120 HE25 |
| RN-046/RN-047 · HE-03 | `ReglasConfirmadasHorasExtraAcceptanceTests` | `HE_03_sabado_de_07_00_a_09_00_es_dos_horas_extra_25` | DIRECTO | Sí | PASA |
| RN-046/RN-047 · HE-04 | `ReglasConfirmadasHorasExtraAcceptanceTests` | `HE_04_sabado_de_07_00_a_12_00_reparte_25_y_35` | DIRECTO | Sí | PASA |
| RQ-016 · AUS-PAG-01/03 | `PagoDelDiaJustificadoTests` | `Una_licencia_pagada_completa_en_Diu_acredita_480_y_no_622` | DIRECTO | Sí | PASA |
| RQ-016 · AUS-PAG-03/04/05 | `PagoDelDiaJustificadoTests` | `Una_licencia_pagada_completa_en_Noc_acredita_480_sin_HE_ni_nocturnidad` | DIRECTO | Sí | PASA |
| RQ-016 · AUS-PAG-01/02 | `PagoDeJustificacionesAlRecalcularTests` | `Una_licencia_con_goce_completa_persiste_480_minutos` y vacaciones | INTEGRACIÓN SQLITE | Sí | PASA |
| RQ-014 · IMP-AT-01..07 | `AtomicidadImportacionAcceptanceTests` | rollback, éxito, error funcional, hash, duplicado de marca, fallo de inserción y contadores | INTEGRACIÓN SQLITE + INTERCEPTORES | Sí | PASAN desde R-P1-07 |
| RN-041/RN-078 · HE-05 | `ReglasConfirmadasHorasExtraAcceptanceTests` | `HE_05_domingo_trabajado_no_se_reparte_como_ordinario_ni_extra` | DIRECTO | Sí | PASA |
| RN-046/RN-047 · HE-DIU-00..07, HE-NOC-00..05, DIU/NOC-CANONICO, tardanza-corte, llegada temprana, TAR-NOC, SA-NOC, HE-SAB, DOM-04/05 | `JornadaOrdinariaYHorasExtraAcceptanceTests` | varios | DIRECTO | Sí | PASAN (R-P0-03.1) |
| RN-029/RN-047 · NOC-WE-01..06 | `CrucesNocturnosFinDeSemanaAcceptanceTests` | viernes→sábado y sábado→domingo | DIRECTO | Sí | PASAN (R-P0-HE.2): viernes conserva jornada regular; sábado corta HE25/35 a las 00:00 y domingo inicia HE100 desde el primer minuto |
| RN-090 · LIQ-01 | `LiquidacionPeriodoAcceptanceTests` | `LIQ_01_solo_base_ordinaria` | DIRECTO | Sí | PASA: 8 h equivalentes |
| RN-090 · LIQ-02 | `LiquidacionPeriodoAcceptanceTests` | `LIQ_02_ordinarias_y_extras_aplican_una_sola_base` | DIRECTO | Sí | PASA: 11.85 h equivalentes |
| RN-079/RN-090 · LIQ-03 | `LiquidacionPeriodoAcceptanceTests` | `LIQ_03_nocturnidad_agrega_solo_la_sobretasa_no_otra_base` | DIRECTO | Sí | PASA desde R-P1-01: 10.80 h equivalentes |
| RN-078/RN-090 · LIQ-04 | `LiquidacionPeriodoAcceptanceTests` | `LIQ_04_descanso_trabajado_no_se_suma_ademas_como_ordinario` | DIRECTO | Sí | PASA desde R-P1-01: 16.00 h equivalentes |
| RN-047/RN-079/RN-090 · LIQ-HE-NOC-01 | `LiquidacionPeriodoAcceptanceTests` | `LIQ_HE_NOC_01_cruce_sabado_domingo_conserva_base_unica_y_prima_nocturna` | DIRECTO | Sí | PASA: 120 HE25 + 180 HE35 + 322 HE100; 442 nocturnos superpuestos; 19.86 h equivalentes |
| RN-019 · PL-01 | `PeriodoLaboralAcceptanceTests` | `PL_01_crear_y_editar_dos_veces_conserva_un_solo_vinculo_abierto` | DIRECTO (subregla) | Sí | PASA desde R-P1-02: una fila total y una abierta entre tres `DbContext` de escritura |
| RN-019 · PL-02 | `PeriodoLaboralAcceptanceTests` | `PL_02_dar_de_baja_cierra_el_vinculo_activo` | DIRECTO (subregla) | Sí | PASA desde R-P1-02: el vínculo queda cerrado con fecha y motivo en un `DbContext` nuevo |
| RN-055 · INC-01 | `IncidenciaConJustificacionParcialAcceptanceTests` | `INC_01_permiso_parcial_no_oculta_una_salida_no_registrada` | DIRECTO (combinación) | Sí | PASA desde R-P1-03: conserva `SalidaNoRegistrada` y requiere validación |
| RN-055 · INC-02 | `IncidenciaConJustificacionParcialAcceptanceTests` | `INC_02_tardanza_justificada_no_oculta_una_salida_no_registrada` | DIRECTO (combinación) | Sí | PASA desde R-P1-03: justificar tardanza no resuelve la salida faltante |
| RN-055 · INC-03 | `IncidenciaConJustificacionParcialAcceptanceTests` | `INC_03_regularizacion_parcial_no_oculta_el_extremo_que_sigue_faltando` | DIRECTO (combinación) | Sí | PASA desde R-P1-03: la regularización incompleta conserva la incidencia restante |
| RN-055 · INC-PARCIAL-01 | `FlujoDeRegularizacionTests` | `INC_PARCIAL_01_regularizacion_incompleta_permanece_en_la_bandeja` | DIRECTO (persistencia/bandeja) | Sí | PASA: persiste `SalidaNoRegistrada` y la consulta real devuelve un pendiente; la regularización completa devuelve cero |
| RN-048 · TAR-00..04 | `TardanzaPlanillaAcceptanceTests` | teoría 06:59/07:00/07:01/07:05/07:17 | DIRECTO | Sí | PASA: 0/0/1/5/17 min |

No quedan contratos deliberados en rojo. Las brechas de la matriz siguiente son coberturas
parciales o decisiones pendientes y no pruebas fallidas ni omitidas.

## 4. Matriz de aceptación pendiente

| Regla | Escenario verificable | Test actual | Tipo | Ejecutado | Resultado / brecha |
|---|---|---|---|---|---|
| RN-055 | Tardanza parcialmente justificada conserva desvío e indica si requiere bandeja | `PermisoParcialTests` cubre aritmética, no bandeja | PARCIAL | Sí | Decisión de negocio pendiente sobre bandeja |
| RN-055/RN-058 | Justificación parcial seguida de regularización del extremo faltante | `UnaJustificacionPorTrabajadorYDiaTests` | INDIRECTO | Sí | El modelo de una justificación por día impide coexistencia; falta contrato de UX |
| RN-063 | Regularización incompleta mantiene pendiente el extremo faltante | `RegularizacionDeMarcacionTests.Una_regularizacion_a_medias_no_inventa_el_otro_extremo` | PARCIAL | Sí | Conserva `Falta`, pero no prueba bandeja ni segunda regularización |
| RN-088 · VIG-01 | Marca posterior al cálculo vuelve obsoleto | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA |
| RN-088 · VIG-02 | Justificación aprobada posterior vuelve obsoleto | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA |
| RN-088 · VIG-03 | Eliminación/anulación de justificación aprobada vuelve obsoleto | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA mediante auditoría persistida |
| RN-088 · VIG-04 | Cambio de `VersionTurno` aplicable vuelve obsoleto | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA |
| RN-088 · VIG-05 | Alta, cambio de fecha/planta o eliminación de feriado relevante vuelve obsoleto | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA; eliminación usa auditoría |
| RN-088 · VIG-06 | Parámetro de tareo posterior vuelve obsoleto | No aplica | N/A | Sí | `ParametroLegal` no es entrada de `ServicioCalculoTareo`; no se inventa invalidación |
| RN-088 · VIG-07 | Cambio de programación/tramo de cuadrilla aplicable vuelve obsoleto | `VigenciaTareoAcceptanceTests` + heredadas | DIRECTO | Sí | PASA también al convertir un tramo aplicable en descanso |
| RN-088 · VIG-08 | Recálculo parcial mantiene A vigente y B obsoleta | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA con periodo + plantas visibles y mínimo calculado |
| RN-088 · VIG-09 | Cambio descriptivo no usado no invalida | `VigenciaTareoAcceptanceTests` | DIRECTO | Sí | PASA; teléfono del trabajador no es entrada del motor |
| PMV-RN-012 | Admin planta A no lista/importa/consulta/exporta equipo B | `AislamientoPmvAcceptanceTests` | DIRECTO | Sí | PASA desde R-P0-01 (SEC-01..04) |
| PMV-RN-006 | Asignación activa, futura, expirada, inexistente y padrón obsoleto | `ConsultaMarcacionProvisionalTests.Trabajador_asignado_por_vigencia`, `Id_marcador_sin_trabajador_asignado` | PARCIAL | Sí | Activa/expirada/sin asignar cubiertas; futura/inexistente/stale no separadas |
| PMV-RN-010 | Rangos 1, 30, 31, 40 y 41 en consulta y exportación | `SyntheticPerformanceBaselineTests.PERF_PMV_consulta_sintetica` + pruebas heredadas | PARCIAL | Sí | Consulta/flags backend pasan en las cinco fronteras; falta acción Web/exportación real |
| PMV-RN-009 | Cuadrilla resuelve turno diario con el mismo motor principal | — | NINGUNO | No | Implementación solo imprime etiqueta de cuadrilla |
| RN-094/PMV-RN-009 | Reemisión histórica tras cambios de planta/área/sección/CECO | `VigenciaDelAreaEnReportesTests` | PARCIAL | Sí | Área parcialmente histórica; población/planta/CECO actuales |
### Ejecución Fase 6.2 de contratos antes pendientes

| Contrato | Ejecutado | Resultado |
|---|---|---|
| DOM-01 · domingo 07:00–09:00 | Sí | PASA desde R-P0-03: 120 al 100 %, cero ordinarios/25/35 |
| DOM-02 · domingo 07:00–17:22 | Sí | PASA desde R-P0-03: 622 al 100 % |
| DOM-03 · domingo nocturno | Sí | PASA desde R-P0-03: 480 al 100 % y 480 nocturnos |
| SA-00..04 · salida 17:23/17:22/17:21/17:17/17:00 | Sí | PASAN: 0/0/1/5/22 minutos |
| TAR-SA-NOC-01 · cruce nocturno | Sí | PASA: 1 minuto de tardanza y 1 de salida anticipada |
| REG-01/02 | Sí | PASAN: ingreso/salida biométricos permanecen intactos y la corrección es separada |
| REG-03 | Sí | PASA: motivo, actores, fechas, resolución y auditoría están disponibles |
| REG-04 | Sí | PASA: corrección posterior no sobrescribe la marca ni el intento previo |
| ROT-01 · 7/7 | Sí | PASA |
| ROT-02 · 10/5 | Sí | PASA |
| ROT-03 · programaciones sucesivas | Sí | PASA: futuro distinto no altera resolución histórica |
| ROT-DESC-01..09 | Sí | PASAN desde R-P1-05 |
| ROT-MARK-01..06 | Sí | PASAN desde R-P1-05 |
| ROT-FER-01/02 | Sí | PASAN; P0-FER-007 conserva clasificación y añade la fórmula económica aprobada sin duplicar minutos |
| ROT-REP-01 | Sí | PASA: la secuencia termina sin módulo implícito |
| SEC-01 · listado de equipos PMV | Sí | PASA desde R-P0-01 |
| SEC-02 · consulta/exportación PMV | Sí | PASA desde R-P0-01 |
| SEC-03 · importación PMV | Sí | PASA desde R-P0-01 |

## 5. Reconciliación de cobertura 79/18/11 versus 83/14/11

La discrepancia era aritmética, no cuatro tests nuevos. Al buscar el texto `PARCIAL` en la fila completa también se contaron como cobertura parcial cuatro reglas cuya columna **Config.** era `Parcial`, aunque su columna **Cobertura** era `TESTEADA`: RN-005, RN-008, RN-047 y RN-061. Leyendo exclusivamente la sexta columna, el corte de Fase 4 es **83 DIRECTO / 14 PARCIAL / 11 NINGUNO**.

Fase 5 dejó **82/16/10** y Fase 6.1 quedó en **77/21/10** al ampliar contratos. Fase 6.2 añadió cobertura directa para RN-010, RN-025, RN-041, RN-047, RN-049, RN-063 y PMV-RN-012. R-P1-04 convierte RN-088 de parcial a directo: **85 DIRECTO / 15 PARCIAL / 8 NINGUNO = 108 reglas**. BD01/BD02 agregan ocho reglas confirmadas todavía sin pruebas: **85 DIRECTO / 15 PARCIAL / 16 NINGUNO = 116 reglas**.

- DIRECTO (85): incluye RN-088 mediante VIG-01..05 y VIG-07..09, además de los contratos rojos y los REG/ROT de Fase 6.2.
- PARCIAL (15): RN-019, RN-028, RN-030, RN-052, RN-055, RN-067, RN-068, RN-073, RN-077, RN-079, RN-094, RN-096, PMV-RN-001, PMV-RN-009, PMV-RN-010.
- NINGUNO (16): RN-012, RN-027, RN-031, RN-050, RN-070, RN-089, RN-095,
  RN-097, RN-098, RN-099, RN-100, RN-101, RN-102, RN-103, RN-104 y PMV-RN-008.

`DIRECTO` expresa que existe un test que apunta a la regla; no garantiza por sí mismo que el resultado sea verde ni que la implementación coincida con negocio. Los resultados concretos quedan registrados en la tabla.

## 6.2. Estado de suite tras completar contratos

- Domain: 309 totales; 293 pasan; 16 fallos deliberados; 0 omitidos; 768 ms reportados.
- Infrastructure: 409 totales; 402 pasan; 7 fallos deliberados; 0 omitidos; 30 s reportados.
- Global: **718 totales; 695 pasan; 23 fallos deliberados; 0 inesperados; 0 omitidos**.
- Duración de pared de `dotnet test --no-restore`: 35.3 s.

La matriz completa de los veintitrés contratos rojos está en `ACCEPTANCE_FAILURES.md`.

### Corte tras R-P1-02

- Domain: 354 totales; 350 pasan; 4 fallos deliberados; 0 omitidos.
- Infrastructure: 411 totales; 411 pasan; 0 fallos; 0 omitidos.
- Global: **765 totales; 761 pasan; 4 fallos deliberados; 0 inesperados; 0 omitidos**.
- PL-01/02 pasan sin cambiar expectativas y usando un `DbContext` distinto por operación.

### Corte actual tras R-P1-05 / ROT-DESC

- Domain: 368 totales; 368 pasan; 0 fallos; 0 omitidos.
- Infrastructure: 435 totales; 435 pasan; 0 fallos; 0 omitidos.
- Global: **803 totales; 803 pasan; 0 deliberados; 0 inesperados; 0 omitidos**.
- VIG-01..05 y VIG-07..09 pasan; VIG-06 queda no aplicable por consumidor real.

## 6. Criterio de salida de remediación

Una corrección solo puede considerarse aceptada cuando:

- los 17 contratos de Fase 5 pasan sin alterar sus expectativas y se agregan los contratos Fase 6.1 aplicables;
- domingo demuestra 100 % para todo minuto trabajado y salida anticipada demuestra 0/1/5 en las tres fronteras;
- regularización demuestra inmutabilidad y trazabilidad de creación, validación, cancelación/rechazo;
- la suite heredada sigue verde;
- VIG-01..05 y VIG-07..09 permanecen verdes; VIG-06 solo se exige si un parámetro entra realmente al cálculo;
- PMV demuestra aislamiento de planta en listado, consulta y exportación desde una prueba Web/integración;
- no quedan pruebas omitidas ni se cambia una expectativa para acomodar la implementación sin nueva decisión de negocio.
