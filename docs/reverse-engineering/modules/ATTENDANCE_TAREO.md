# Módulo Asistencia y tareo

## Propósito

Transformar marcas atribuidas y reglas temporales en un resultado diario persistido, con minutos ordinarios, extras, nocturnos, tardanza, faltas e incidencias.

## Responsabilidades

- Construir la jornada calculada.
- Seleccionar primera/última marca y consolidar rebotes.
- Resolver turno/rotación, feriado, descanso y justificaciones.
- Calcular conceptos de asistencia.
- Crear o actualizar `TareoDiario`.
- Detectar incidencias y permitir recálculo controlado.

## Entidades

`CalculoTareo`, `TareoDiario`, `TipoIncidencia` y `DeteccionTurnoDiscrepante`.

## Servicios

`ServicioCalculoTareo`, `GuardiaDePeriodo`, el contrato `IServicioCalculoTareo` y los controladores de asistencia/incidencias.

## Reglas vigentes críticas

- Turnos regulares: DIU 07:00–17:22; NOC 19:00–05:22 del día siguiente.
- L–V: ordinario hasta el fin teórico. Un exceso menor a 15 min no es HE; desde 15 min, HE completa desde el fin teórico (17:37 = 15; 05:37 = 15). Primeras 2 h al 25 %, resto al 35 %.
- Sábado: todo trabajado es extra desde el primer minuto, sin umbral; primeras 2 h 25 %, resto 35 %.
- Domingo: descanso semanal; todo minuto trabajado va al 100 %, sin umbral: cero ordinarios, cero 25 % y cero 35 %.
- NOC viernes→sábado: el tramo del sábado hasta 05:22 sigue dentro de la jornada regular; solo el exceso posterior al fin puede ser HE y conserva el umbral de 15 min.
- NOC sábado→domingo: HE25/35 hasta las 00:00 y HE100 desde el primer minuto del domingo, sin cambiar la fecha de jornada ni partir el `TareoDiario`.
- Planilla: tardanza con tolerancia 0 (07:01 = 1; 19:01 = 1). El campo de cinco minutos no interviene.
- Planilla: salida anticipada con tolerancia 0 contra `FinTurno` (17:21 = 1; 05:21 = 1).
- La franja nocturna es 22:00–06:00; coexiste con 0 HE (el NOC regular tiene 442 min nocturnos).

## Reparto ordinario/extra (R-P0-03 + R-P0-03.1)

`CalculadoraTareo.Repartir` clasifica los minutos trabajados según el día:

| Día | Ordinarios | Sobretiempo | `TipoDiaNoLaborable` |
|---|---|---|---|
| Domingo trabajado (cualquier turno, incluidos DOM1/DOM2 y la jornada regularizada) | 0 | 0 en 25/35; todo es 100 % = `MinutosTrabajados`; sin umbral | `DescansoSemanal` (o `Feriado` si coincide, por precedencia) |
| Sábado laborable | 0 | hasta domingo 00:00, sin umbral: 120 al 25 %, resto al 35 %; el remanente dominical es HE100 | `Ninguno` |
| L–V laborable (cualquier turno) | minutos computados antes de `FinTurno` (reloj, con cruce de medianoche) | lo posterior a `FinTurno`, si el exceso alcanzó el umbral de 15 min | `Ninguno` |
| Feriado L–S, descanso de otro día | comportamiento vigente: `min(trabajados, MinutosJornadaOrdinaria)`; sin umbral | resto | según el caso |

- Umbral (`Jornada`, constante `UmbralSobretiempoMinutos = 15`): solo en L–V laborable. Si el exceso truncado sobre `FinTurno` es menor a 15 min, el cómputo se cierra en `FinTurno` (esos minutos no cuentan como trabajados, igual que la gracia de salida) y la observación lo declara. Se evalúa después de la gracia de salida configurada, que sigue en 0.
- La tardanza no corre el corte: 07:10–17:37 da 10 de tardanza, 612 ordinarios y 15 HE.
- La llegada anticipada no genera HE: el cómputo empieza en la hora del turno, como antes.
- El refrigerio se sigue descontando de los trabajados y sale de la cola (sobretiempo), igual que antes. No se tomó ninguna decisión nueva.
- La franja nocturna se calcula sobre el intervalo computado; un exceso bajo el umbral tampoco suma nocturnidad.
- La jornada conserva `FechaJornada` de inicio. Solo la frontera sábado→domingo segmenta la clasificación económica; viernes→sábado no lo hace. `MinutosExtra100` se deriva del remanente físico, por lo que no requiere otra fila ni cambio de esquema.
- `MinutosJornadaOrdinaria` ya no decide el corte de días laborables L–V; sobrevive en feriado/descanso no dominical y en el crédito de ausencia pagada.

Tardanza y salida anticipada se calculan minuto a minuto contra los extremos teóricos del turno; los campos configurables de tolerancia y anticipación siguen almacenados pero no afectan planilla.

## Consumo en liquidación (R-P1-01)

El resultado diario no cambió. Liquidación distingue categorías físicas de dimensiones superpuestas:

- ordinario, HE25, HE35 y HE100/descanso aportan tiempo físico una sola vez;
- nocturnidad conserva sus minutos, pero aporta únicamente la prima de la tasa vigente;
- una fila no laborable trabajada no vuelve a sumar `MinutosOrdinarios` históricos; la ausencia pagada completa conserva su crédito confirmado de 480 minutos;
- el HE100 del cruce sábado→domingo se deriva del remanente de `MinutosTrabajados` que no está en ordinario/HE25/HE35.

## Configuración DIU/NOC (VIG-TURNO-01)

La semilla `Turno` y `VersionTurno` (vigente desde 2000-01-01, abierta) define **DIU 07:00–19:00 y NOC 19:00–07:00**. Contradice el requisito y no se cambió:

- la aplicación arranca con `MigrateAsync`, así que cambiar `HasData` exige migración;
- esa migración reescribiría la única versión, vigente desde 2000, y alteraría el pasado.

**CONFIGURACIÓN DE DATOS REQUIERE PLAN DE ACTUALIZACIÓN:** editar DIU (fin 17:22) y NOC (fin 05:22) en `/Turnos` con «Rige desde» = fecha acordada. Eso abre una `VersionTurno` nueva y conserva la historia; después hay que recalcular los periodos abiertos. Mientras no se haga, el motor corta el ordinario en 19:00/07:00 y la salida anticipada se mide contra esas horas.

**OP-01 (validado en copia, [OP_01_SHIFT_VERSION_UPDATE](../OP_01_SHIFT_VERSION_UPDATE.md)):** con «Rige desde» 2026-09-15, las versiones antiguas se cerraron el 14 con su horario intacto. El 14 sigue calculándose con el horario antiguo; desde el 15 se cumplen los contratos canónicos, el umbral, la tardanza que no corre el corte y la nocturnidad. Crear la versión no recalcula el tareo. Falta la FechaRigeDesde de producción.

## Ambigüedades abiertas tras R-P0-03.1

- **Ausencia pagada (cerrado R-P1-08):** acredita `MinutosJornadaOrdinaria` = 480, porque son 8 horas efectivas sin almuerzo; un día trabajado completo puede dar 622 ordinarios por su intervalo de reloj. No es una contradicción.
- **Domingo con DOM1/DOM2 sin marcas:** conserva `Falta`, porque el turno lo declara aplicable.
- **Sábado de descanso por turno y feriado en día hábil:** conservan el reparto vigente con 480 y sin umbral.
- **Domingo-feriado:** queda tipo `Feriado` con cero ordinarios. Ambos tipos van al 100 % en el tareo final.
- **Gracia de salida configurada:** se evalúa antes del umbral. Con gracia ≥ 15 min, la gracia (inclusiva) absorbería un exceso de exactamente su valor. Hoy vale 0 en todos los turnos.
- **NOC que empieza en sábado:** se imputa al sábado, con HE25/35 hasta domingo 00:00 y HE100 desde allí. **NOC que empieza en domingo:** conserva el tratamiento dominical íntegro.

## Flujo

Marcas atribuidas + horario + calendario + justificaciones → jornada → `CalculoTareo` → validaciones/incidencias → `TareoDiario`.

Para `DescansoProgramado`, el orquestador hace dos pasadas: primero todas las jornadas
con turno reclaman marcas por identidad; después el descanso evalúa solo el residuo. La
primera marca debe caer en D y un cierre D+1 solo entra antes de la ventana laboral
siguiente. Cero marcas produce descanso; una marca o ambigüedad produce cero minutos y
validación; dos o más extremos inequívocos producen HE100 íntegra sin refrigerio, umbral,
tardanza ni salida anticipada. Feriado prevalece sobre descanso sin duplicar minutos.

Véanse [D08](../diagrams/D08_MARKING_TO_WORKDAY.md) y [D09](../diagrams/D09_WORKDAY_TO_TAREO.md).

## Fuentes de verdad

- Contrato funcional: `ACCEPTANCE_RULES.md`.
- Algoritmo actual: `CalculoTareo.cs`.
- Orquestación/persistencia: `ServicioCalculoTareo.cs`.
- Resultado operativo: `TareoDiario` persistido.

## Archivos críticos

- `src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs`
- `src/ControlMarcacion.Domain/Asistencia/TareoDiario.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/GuardiaDePeriodo.cs`
- `tests/ControlMarcacion.Domain.Tests/Asistencia/ReglasConfirmadasHorasExtraAcceptanceTests.cs`
- `tests/ControlMarcacion.Domain.Tests/Asistencia/TardanzaPlanillaAcceptanceTests.cs`

## Pruebas

HE-01..05, DOM-01..03, NOC-WE-01..06 y los contratos de `JornadaOrdinariaYHorasExtraAcceptanceTests` (HE-DIU-00..07, HE-NOC-00..05, DIU/NOC-CANONICO, tardanza-corte, llegada temprana, TAR-NOC, SA-NOC, HE-SAB, DOM-04/05) están verdes. TAR-00..04, SA-00..04 y TAR-SA-NOC-01 están verdes. La suite también cubre incidencias, permisos, regularización, bono nocturno, discrepancia de turno, cierre concurrente y falta por archivo ausente.

INC-01..03 e INC-PARCIAL-01 están verdes desde R-P1-03. `ConIncidencia` trabaja
sobre el resultado posterior a aplicar la justificación: una anomalía independiente
permanece visible y una regularización completa deja de requerir validación.

## Vigencia del resultado (R-P1-04)

`ServicioReportes.EvaluarVigenciaDelCalculoAsync` no recalcula ni modifica
`TareoDiario`. Responde si el resultado persistido sigue vigente:

- unidad: rango de fechas + plantas visibles; un filtro explícito de planta estrecha ese alcance;
- referencia: `MIN(TareoDiario.CalculadoEn)` del alcance, porque una sola fila antigua impide declarar el conjunto al día;
- fuentes: marcaciones/cargas, justificaciones aprobadas, asignaciones de marcador y turno, turno/versiones, programación/tramos, feriados, altas/vigencia laboral y comportamiento de tipos de ausencia cuando se usan;
- borrados: la bitácora existente aporta la fecha y los valores originales de justificaciones y feriados eliminados;
- no fuentes: `ParametroLegal` y datos descriptivos como teléfono o equipo no consumidos por el motor.

No hay marca persistida nueva, migración ni recálculo automático. Si una entrada cambia
un periodo cerrado, el detector lo declara desactualizado; la política de reliquidación
continúa pendiente de negocio.

## Problemas conocidos

- P0-HE: **RESUELTO EN CÓDIGO** (R-P0-03 + R-P0-03.1). Antes de usar datos reales: reconfigurar DIU/NOC (VIG-TURNO-01) y recalcular los periodos abiertos.
- P0-TARD/P0-SA: **RESUELTOS**. Con la semilla vigente (fin 19:00/07:00) la salida anticipada real se mide contra esas horas hasta la reconfiguración.
- DEUDA TÉCNICA: el umbral de 15 min es una constante del motor; `MinutosJornadaOrdinaria` sigue vivo y editable para feriado/descanso no dominical y para el crédito de ausencia. En este último uso, 480 es requisito confirmado.
- P1-01: doble conteo de base en liquidación — **RESUELTO**; LIQ-01..04 y LIQ-HE-NOC-01 verdes.
- P1-03: incidencias incompletas suprimidas — **RESUELTO**; bandeja y resolución completa cubiertas.
- P1-04: frescura/invalidation incompleta — **RESUELTO**; VIG aplicables verdes.
- P1-05 / ROT-DESC: **RESUELTO**; 18 contratos nominales y migración SQLite validados.
- RIESGO: resultado no congela todos los insumos.
- DEUDA: `MinutosTolerancia` y `MinutosAnticipacionSalida` permanecen en modelo, versión, DTO, UI y semillas sin efecto en el cálculo de planilla.
