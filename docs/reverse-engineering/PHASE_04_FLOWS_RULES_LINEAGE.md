# Fase 4 — Flujos, linaje y catálogo maestro de reglas

Fecha de corte: 2026-10-05  
Comprensión inicial: **83 %**  
Comprensión final estimada: **92 %**  
Método: lectura dirigida de cálculo, orquestación, DTO, servicios, exportadores y pruebas; sin modificar código ni base de datos y sin ejecutar una campaña exhaustiva de tests.

## 1. Resultado ejecutivo

El sistema principal es una cadena recalculable y auditable en intención:

```text
archivo ZKTeco
→ importación y marcación cruda inmutable
→ atribución histórica por equipo + ID + fecha
→ trabajador y vínculo
→ turno fijo o cuadrilla + programación + tramo + versión de horario
→ ventana y marcas de jornada
→ feriado + cobertura + justificación aprobada
→ CalculadoraTareo
→ TareoDiario persistido
→ agregados, anexos, reintegros y parámetros
→ Excel
```

La exactitud diaria está concentrada en `CalculadoraTareo`; la selección de insumos históricos está en `ServicioCalculoTareo`; y el resultado entregado a planilla se vuelve a componer en `ServicioReportes`. Esta separación es razonable, pero introduce tres fronteras de riesgo:

1. el reporte no es una fotografía pura del tareo: añade padrón, parámetros, solicitudes y reintegros;
2. la advertencia de desactualización no vigila todos esos insumos y usa el cálculo **más reciente**, no la antigüedad/completitud de todas las filas;
3. varias decisiones sensibles siguen codificadas o ambiguas: margen de seis horas, rebote de dos minutos, primer/último evento, refrigerio completo, franja nocturna fija y una sola justificación/incidencia por día.

PMV permanece separado. Comparte parser y padrón, pero no produce tareo ni debe usarse como sustituto del flujo principal.

## 2. Evidencia y límites

Se profundizó solo donde la fase lo exigía:

- `CalculoTareo.cs`, `TareoDiario.cs`, incidencias y bono nocturno;
- `ServicioCalculoTareo.cs`, atribución, cobertura, turnos/versiones y rotación;
- importación ZKTeco principal y lectores;
- solicitudes, justificaciones, regularizaciones y reintegros;
- DTO, composición y exportación de resumen, excepciones y tareo final;
- pruebas declaradas de Asistencia, Marcaciones, Reportes, Solicitudes/Justificaciones y PMV.

No se releyó el repositorio completo, no se abrió SQLite en esta fase, no se ejecutaron pruebas y no se alteró comportamiento productivo.

## 3. Flujo maestro extremo a extremo

| Paso | Clase / método principal | Entrada | Regla decisiva | Salida | Persistencia |
|---:|---|---|---|---|---|
| 1 | `MarcacionesController.Importar` | archivo, planta, equipo, usuario | límite HTTP, autorización y antiforgery | stream al caso de uso | No |
| 2 | `ServicioImportacion.ImportarAsync` | bytes y metadatos | planta/equipo coherentes | equipo validado | Lee catálogos |
| 3 | `CalcularHashAsync` | bytes completos | SHA-256 | hash hexadecimal | No |
| 4 | `LectorMarcadorAutomatico.Leer` | stream | detección por contenido | lector TXT o binario | No |
| 5 | lectores ZKTeco | líneas/registros | parseo, fecha plausible, ID no vacío | marcas válidas + rechazos | No |
| 6 | `SerieDelArchivo` / verificación de origen | serie y nombre | conflicto de serie o coincidencia cruzada bloquea | origen verificado | Puede actualizar serie de equipo |
| 7 | deduplicación | marcas leídas | lote `(ID, instante)` y BD `(equipo, ID, instante)` | nuevas/duplicadas | No |
| 8 | `DetectarSinAsignarAsync` | marcas + padrón | aviso, no rechazo | errores informativos | `error_importacion` |
| 9 | dos guardados de importación | cabecera/errores, luego marcas | no hay transacción envolvente | carga principal | `importacion_marcador`, `marcacion_cruda`, `error_importacion` |
| 10 | `RecalcularPeriodoAsync` | periodo abierto | población activa con vínculo solapado y alcance de planta | trabajadores a procesar | Lee padrón |
| 11 | `AtribucionMarcaciones.Resolver` | equipo, ID, instante | vigencia inclusiva a fecha de marca | trabajador opcional | No |
| 12 | `ResolverTurno` | trabajador/día | asignación fija o cuadrilla vigente | turno lógico/origen | No |
| 13 | `ProgramacionCuadrilla.TurnoEn` | ancla, tramos, fecha | módulo positivo del ciclo | `TurnoId` del día | No |
| 14 | `HorariosPorFecha.En` | turno y fecha | versión histórica; fallback al turno actual | turno hidratado | No |
| 15 | `MarcacionesDeLaJornada` | marcas, turno, fin previo | anticipación, fin +6 h, no reutilizar | marcas reclamadas | No |
| 16 | orquestador | día/persona | vínculo, feriado por planta, cobertura por aparato, justificación aprobada | `EntradaCalculo` | No |
| 17 | `CalculadoraTareo.CalcularSinJustificar` | entrada pura | estados base y aritmética | `ResultadoCalculo` base | No |
| 18 | `Aplicar` / `ConIncidencia` | resultado base | justificación y prioridad de incidencia | resultado final | No |
| 19 | `Volcar` | resultado | reemplazo completo de proyección diaria | entidad tareo | `tareo_diario` upsert |
| 20 | `ServicioReportes` | filtro/rango | población, organización, agregados, parámetros y anexos | DTO de reporte | Solo lectura |
| 21 | `ArmarFilasTareoAsync` | tareos + padrón + reintegros | separación laborable/100 %, movilidad, familia | 57 campos | Solo lectura |
| 22 | `ExportadorReportesExcel` | DTO + metadatos | posiciones fijas, neutralización de texto | XLSX | Archivo en memoria |

### Corte transaccional

- El recálculo construye el lote y hace un único `SaveChanges`; EF lo trata como transacción, y la versión del periodo impide colarse después de un cierre concurrente.
- La importación principal tiene dos `SaveChanges` y puede dejar la cabecera/errores sin marcas si falla el segundo.
- PMV sí envuelve sus dos guardados en transacción explícita.

## 4. De marcación a jornada

### 4.1 Atribución

`MarcacionCruda` no guarda trabajador. La relación se reconstruye cada vez:

```text
MarcacionCruda(EquipoId, IdMarcador, FechaHora)
   + AsignacionMarcador[Desde <= fecha <= Hasta]
   = TrabajadorId opcional
```

El equipo forma parte de la identidad y los ceros a la izquierda son significativos. Una corrección del padrón cambia el dueño histórico al siguiente recálculo, sin tocar el hecho original.

### 4.2 Turno y rotación

La resolución tiene tres niveles temporales:

1. `AsignacionTurno` vigente: turno fijo o cuadrilla;
2. si hay cuadrilla, `ProgramacionCuadrilla` vigente y `TramoRotacion` según ancla/ciclo;
3. `VersionTurno` vigente, que aporta horario y parámetros de cálculo.

`DiasAplicables` pertenece al turno. Por eso una rotación no contiene un tramo “descanso”: asigna un turno, y ese turno decide si el día de semana aplica. Si falta programación, el motor entrega `SinHorario`; no infiere el turno por las marcas.

### 4.3 Ventana y pareo

- Inicio: hora teórica menos anticipación configurada.
- Fin: hora teórica final más **6 horas fijas**.
- Corte inferior adicional: debe ser posterior al fin de la jornada anterior.
- Con ≥2 eventos efectivos, primera y última marca crudas son los extremos.
- Marcas intermedias se ignoran para horas y quedan descritas.
- Rebotes de menos de dos minutos se consolidan para contar eventos, pero no cambian los extremos crudos.
- Una marca aislada se decide como entrada o salida por cercanía al inicio/fin teórico.

## 5. Aritmética de `CalculadoraTareo`

### 5.1 Fórmula principal

Para una jornada con dos extremos coherentes:

```text
inicio_computable = max(entrada_real, inicio_teórico)
fin_computable    = fin_teórico, si salida cae dentro de gracia
                    salida_real, en otro caso
minutos_brutos    = truncar(fin_computable - inicio_computable)
trabajados        = max(0, minutos_brutos - refrigerio)
ordinarios        = min(trabajados, jornada_ordinaria_del_turno)
extras            = trabajados - ordinarios
extra_25          = min(extras, 120)
extra_35          = max(0, extras - 120)
```

La entrada anticipada se conserva como evidencia, pero no genera pago. La gracia de salida tiene comportamiento de umbral: dentro se ignora todo; al excederla cuenta todo el tiempo posterior al fin.

### 5.2 Tardanza y salida anticipada

- Si entrada ≤ inicio + tolerancia: tardanza 0.
- Si la supera: tardanza completa desde el inicio teórico.
- Si salida ≥ fin − anticipación aceptada: salida anticipada 0.
- Si no: minutos completos hasta el fin teórico.
- La salida anticipada no cambia el estado puntual/tardanza por sí sola.

### 5.3 Descanso, feriado y domingo

El motor no usa “domingo” como sinónimo fijo. Revisa `DiasAplicables` del turno. Si además la fecha es feriado, el feriado prevalece sobre descanso semanal. Sin marcas produce `Descanso` o `Feriado`; con marcas calcula jornada y conserva `TipoDiaNoLaborable` para el tramo 100 % del reporte.

### 5.4 Nocturnidad

La franja es 22:00–06:00. El cálculo solapa el intervalo computable con ventanas nocturnas sucesivas. Los extras se modelan como la cola de la jornada: primero 25 %, después 35 %. `MinutosNocturnos` incluye ordinario y extra; los dos campos extra-nocturno permiten derivar el ordinario nocturno.

Brecha: el refrigerio se descuenta de trabajados, pero no de la franja nocturna. Según cuándo caiga el descanso, `MinutosNocturnos` puede superar el tiempo nocturno efectivamente pagable.

### 5.5 Incidencias

La implementación conserva solo una incidencia. La precedencia observada es:

1. día justificado: ninguna;
2. falta de archivo del aparato: `SinCoberturaDeCarga`;
3. falta/incompleto con marcas sueltas: `FueraDeTurno`;
4. jornada impar: `IngresoNoRegistrado` o `SalidaNoRegistrada`;
5. marcas que encajan mucho mejor en otro turno: `TurnoDiscrepante`.

Un día con jornada completa y marcas sobrantes declara el conteo, pero puede no requerir validación. Dos problemas simultáneos no quedan modelados como dos incidencias.

## 6. Solicitud → justificación → recálculo

Una solicitud empieza pendiente. Al aprobarla, el servicio recorre cada fecha y crea una justificación si encuentra periodo abierto y la fecha está libre. El tipo por horas genera `PermisoParcial`; el resto, `Ausencia`. El motor solo lee justificaciones aprobadas.

Efectos:

| Tipo | Efecto sobre resultado |
|---|---|
| Ausencia / marcación incompleta | `AusenciaJustificada`; puede acreditar jornada ordinaria según tipo de ausencia; nunca extras |
| Tardanza | resta minutos justificados; puede dejar el día puntual |
| Salida anticipada | resta minutos justificados |
| Permiso parcial | perdona solape exacto al inicio/final de jornada; no agrega minutos trabajados |
| Marcación regularizada | combina hora verificada con marca real, recalcula toda la jornada y conserva los extremos crudos como `EntradaReal/SalidaReal` |

La regularización tiene ciclo solicitado/aprobado/rechazado/cancelado y concurrencia optimista. La justificación congela `EstadoAnterior`, pero este dato no se copia a `TareoDiario`.

Brechas:

- solo una justificación vigente por día;
- una solicitud queda aprobada aun cuando parte del rango no generó justificación;
- anular una solicitud aprobada elimina sus justificaciones de periodos abiertos;
- aprobar o modificar no dispara recálculo automático.

## 7. Linaje de todos los campos de `TareoDiario`

| Campo | Origen | Transformación / regla | Naturaleza |
|---|---|---|---|
| `Id` | BD | se conserva en recálculo; no se reinserta si ya existe | Persistido |
| `PeriodoId` | periodo solicitado | asignado al crear; clave del lote | Persistido |
| `TrabajadorId` | población del periodo | trabajador procesado | Persistido |
| `Fecha` | bucle inclusivo del periodo | fecha de inicio de jornada | Persistido |
| `TurnoId` | asignación→programación→tramo→turno | nulo sin turno; se guarda ID lógico, no versión | Persistido |
| `EntradaReal` | primera marca cruda de ventana | en jornada impar puede ser el único extremo; regularización no la reemplaza | Persistido |
| `SalidaReal` | última marca cruda de ventana | mismo criterio; puede caer al día siguiente | Persistido |
| `MinutosTrabajados` | extremos + turno | bruto computable menos refrigerio; o jornada acreditada por ausencia pagada | Calculado/persistido |
| `MinutosOrdinarios` | trabajados + turno | mínimo con jornada ordinaria; ausencia pagada acredita jornada ordinaria | Calculado/persistido |
| `MinutosExtra25` | exceso sobre ordinario | primeras 120 min | Calculado/persistido |
| `MinutosExtra35` | exceso restante | después de 120 min | Calculado/persistido |
| `MinutosNocturnos` | intervalo computable | solape 22:00–06:00; sin descuento específico de refrigerio | Calculado/persistido |
| `MinutosExtra25Nocturnos` | cola extra 25 | solape con franja nocturna | Calculado/persistido |
| `MinutosExtra35Nocturnos` | cola extra 35 | solape con franja nocturna | Calculado/persistido |
| `MinutosOrdinariosNocturnos` | tres campos nocturnos | `max(0,total-extra25-extra35)` | Derivado, no persistido |
| `MinutosTardanza` | entrada + inicio/tolerancia | cero dentro de tolerancia; total desde inicio fuera; justificación puede restar | Calculado/persistido |
| `MinutosSalidaAnticipada` | salida + fin/umbral | cero dentro de umbral; total hasta fin fuera; justificación puede restar | Calculado/persistido |
| `TipoDiaNoLaborable` | feriado activo/planta + `DiasAplicables` | feriado tiene precedencia sobre descanso | Calculado/persistido |
| `EsDiaNoLaborable` | campo anterior | distinto de `Ninguno` | Derivado, no persistido |
| `Estado` | vínculo, turno, cobertura, marcas y justificación | enum final; no codifica toda incidencia | Calculado/persistido |
| `TipoIncidencia` | cobertura, paridad, fuera de ventana, detector | una incidencia priorizada | Calculado/persistido |
| `RequiereValidacion` | incidencia/resultado | bandera independiente de `Estado` | Calculado/persistido |
| `MarcacionesFueraVentana` | marcas naturales no reclamadas | conteo, no detalle estructurado | Calculado/persistido |
| `JustificacionId` | justificación aprobada del día | nulo sin aplicación | Persistido |
| `Observacion` | calculadora | explicación concatenada: turno, marcas, tolerancias, extras, incidencia y justificación | Calculado/persistido |
| `CalculadoEn` | reloj servidor | `DateTime.Now` en cada volcado | Persistido |
| `CreadoEn` | interceptor/contexto | alta inicial | Auditoría base |
| `CreadoPor` | usuario del recálculo | solo al crear | Auditoría base |
| `ModificadoEn` | interceptor/contexto | actualización | Auditoría base |
| `ModificadoPor` | usuario del recálculo | en cada volcado | Auditoría base |
| `Activo` | base de entidad | valor inicial; el flujo no lo usa para invalidar tareos | Persistido |

No se persisten: versión exacta de turno usada, lista de IDs de marcas consumidas, detalle de marcas fuera de ventana, cobertura evaluada, parámetros usados ni `EstadoAnterior` de la justificación. La reproducibilidad depende de que los insumos históricos sigan disponibles y de que el código no cambie.

## 8. Linaje de reportes y campos Excel

### 8.1 Clasificación de fuente

- **HISTÓRICO**: se resuelve por vigencia/solapamiento del rango.
- **ACTUAL**: se lee de la ficha/catálogo al momento de generar.
- **CALCULADO**: deriva de tareo, solicitudes, parámetros o fórmula.
- **MANUAL**: fue capturado por una persona y se copia sin cálculo.
- **VACÍO**: columna contractual presente sin fuente implementada.

### 8.2 Resumen de tareo — hoja `Resumen de tareo`

| Campo Excel | Linaje | Clase |
|---|---|---|
| Trabajador | nombre de `Trabajador` | ACTUAL |
| ID marcador · equipo | asignaciones solapadas concatenadas | HISTÓRICO |
| Empresa / Planta | ficha y catálogos | ACTUAL |
| Centro | CECO actual; fallback código de área histórica | MIXTO |
| Centro de costo | CECO actual; fallback sección actual | MIXTO |
| Puesto | ficha/catálogo | ACTUAL |
| Días puntual/tardanza/falta/ausencia/incompleto | conteo por `Estado` | CALCULADO |
| Días no laborable trabajado | descanso + feriado con minutos >0 | CALCULADO |
| Horas ordinarias/25/35/nocturnas/tardanza | suma de minutos ÷60, redondeo a 2 | CALCULADO |
| Bono nocturno | RMV+tasa+sueldo vigentes por cada día nocturno | CALCULADO |
| Movilidad | asistencias reales × tarifa vigente al fin del rango | CALCULADO |

### 8.3 Excepciones — hoja `Excepciones`

| Campo Excel | Linaje | Clase |
|---|---|---|
| Trabajador | ficha | ACTUAL |
| ID marcador · equipo | asignaciones solapadas | HISTÓRICO |
| Fecha | `TareoDiario.Fecha` | CALCULADO |
| Turno | `TareoDiario.TurnoId`→código actual de turno | MIXTO |
| Entrada / Salida | extremos crudos persistidos | HISTÓRICO |
| Estado | `TareoDiario.Estado` | CALCULADO |
| Minutos tardanza / salida anticipada | tareo | CALCULADO |
| Observación | explicación persistida | CALCULADO |

### 8.4 Tareo final — hoja `Tareo`, 57 columnas A:BE

| Col. | Campo | Linaje | Clase |
|---|---|---|---|
| A | N° | secuencia de filas | CALCULADO |
| B | DNI | `Trabajador.NumeroDocumento` | ACTUAL |
| C | NOMBRE | nombres de ficha | ACTUAL |
| D | PLANTA | planta de ficha | ACTUAL |
| E | CENTRO | `Centro.Codigo` del CECO asignado | ACTUAL |
| F | CENTRO DE COSTO | `CentroCosto.Codigo` | ACTUAL |
| G | DESCRIP. CECO | `CentroCosto.Descripcion` | ACTUAL |
| H | AREA | asignaciones de área solapadas; ficha como fallback | HISTÓRICO/MIXTO |
| I | FECHA INGRESO | campo plano de trabajador | ACTUAL |
| J | FECHA CESE | campo plano de trabajador | ACTUAL |
| K | OBS | `CESE` si hay fecha de cese | CALCULADO |
| L | FALTAS | conteo `Estado=Falta` | CALCULADO |
| M | SUSPENSION | sin fuente | VACÍO |
| N | FALTAS JUS. | conteo `AusenciaJustificada` | CALCULADO |
| O | TOTAL FALTAS | L+N | CALCULADO |
| P | DM | código de ausencia `DM` en justificación aplicada | CALCULADO |
| Q | VACACI. | código `VAC` | CALCULADO |
| R | LIC. S/ GOC | código `LSG` | CALCULADO |
| S | LIC. C/ GOC | código `LCG` | CALCULADO |
| T | LIC. PAT. | código `LPAT` | CALCULADO |
| U | SUB. MAT | código `LMAT` | CALCULADO |
| V | SUB. ENF | código `SUBE` | CALCULADO |
| W | PERMISOS | código `PER` | CALCULADO |
| X | TARDANZA | suma minutos/60 | CALCULADO |
| Y | Ausencias | sin fuente | VACÍO |
| Z | Tardanza | sin fuente | VACÍO |
| AA | Dias Efectivas | sin fuente | VACÍO |
| AB | Dias Pendientes | sin fuente | VACÍO |
| AC | Dias Trabajados | sin fuente | VACÍO |
| AD | HRS NORMALES | ordinarios solo de días laborables | CALCULADO |
| AE | 25 % | extra 25 solo laborables | CALCULADO |
| AF | 35 % | extra 35 solo laborables | CALCULADO |
| AG | 100 % | todos los minutos trabajados en descanso/feriado | CALCULADO |
| AH | MOVIL S/. | días asistidos × tarifa | CALCULADO |
| AI | B.NOCT. HRS | nocturnos de todos los días | CALCULADO |
| AJ | B.NOCT. 25 % | extra25 nocturno laborable | CALCULADO |
| AK | B.NOCT. 35 % | extra35 nocturno laborable | CALCULADO |
| AL | B.NOCT. 100 % | nocturno en día no laborable | CALCULADO |
| AM | ASIG. FAMILIAR | RMV×porcentaje si `TieneHijos` | CALCULADO sobre dato ACTUAL |
| AN | REINT. HRS NORMALES | `ReintegroTareo.HorasNormales` | MANUAL |
| AO | REINT. 25 % | `HorasExtra25` | MANUAL |
| AP | REINT. 35 % | `HorasExtra35` | MANUAL |
| AQ | REINT. 100 % | `HorasNoLaborable` | MANUAL |
| AR | REINT. MOVIL | `MontoMovilidad` | MANUAL |
| AS | REINT. B.NOCT. HRS | `HorasNocturnas` | MANUAL |
| AT | REINT. B.NOCT. 25 % | `HorasExtra25Nocturnas` | MANUAL |
| AU | REINT. B.NOCT. 35 % | `HorasExtra35Nocturnas` | MANUAL |
| AV | REINT. B.NOCT. 100 % | `HorasNoLaborableNocturnas` | MANUAL |
| AW | VIATICO | `Viatico` | MANUAL |
| AX | D.M. (DIAS) | `DiasDescansoMedico` | MANUAL |
| AY | SUB. MATERNIDAD (DIAS) | `DiasSubsidioMaternidad` | MANUAL |
| AZ | SUB. X ACCIDENTE (DIAS) | `DiasSubsidioAccidente` | MANUAL |
| BA | LIC. PATER. (DIAS) | `DiasLicenciaPaternidad` | MANUAL |
| BB | LICENCIA CON GOCE | `LicenciaConGoce` | MANUAL |
| BC | REINT. ASIG. FAMILI | `ReintegroAsignacionFamiliar` | MANUAL |
| BD | REINT. AFECTO | `ReintegroAfecto` | MANUAL |
| BE | DESC. PAGO INDEBIDO | `DescuentoPagoIndebido` | MANUAL |

### 8.5 Anexos y trazabilidad

| Hoja | Campos | Fuente y clase |
|---|---|---|
| `Descanso Medico` | mes, razón social, empresa, DNI, nombre | periodo/ficha: ACTUAL |
| `Descanso Medico` | inicio, fin, días en mes, contingencia, diagnóstico, centro atención, observación | solicitud aprobada solapada: MANUAL/CALCULADO |
| `Vacaciones-Licencias` / vacaciones | N°, DNI, nombre, días, inicio, término, reparto mensual, retorno | solicitud aprobada; reparto/retorno calculados |
| `Vacaciones-Licencias` / licencias | mes, DNI, nombre, inicio, fin, días, tipo, sustento | solicitud aprobada: MANUAL/CALCULADO |
| `Trazabilidad` | razón social, alcance, fechas, periodo/estado, filtros, cantidad, generado por/en | filtro, periodo, usuario y reloj: ACTUAL |
| `Trazabilidad` | estado del cálculo | máximo `CalculadoEn` vs último de siete tipos de cambio | CALCULADO, incompleto |

## 9. Fortaleza histórica real

| Dato | Fortaleza | Observación |
|---|---|---|
| Marca, archivo y línea original | Alta | hecho inmutable; anulación reversible |
| Asignación de marcador | Alta | vigencias por fecha y tests directos |
| Asignación de turno/cuadrilla | Alta | vigencias y resolución por día |
| Horario de turno | Media/alta | versión histórica; fallback actual si hay hueco |
| Área en reportes | Media/alta | vigencia solapada; fallback actual |
| Planta, empresa, puesto, DNI, hijos, CECO | Baja para historia | se leen de ficha actual al exportar |
| Vínculo laboral | Baja/media | campos planos; `PeriodoLaboral` no participa |
| Feriados y parámetros usados | Media | vigencias/estado actual; no se congelan en tareo |
| Justificación | Alta en autor/estado | solo una por día; anulación de solicitud puede borrarla |
| Tareo | Media | proyección persistida, pero sin IDs de marcas/versión/parámetros usados |
| Reintegros | Media | fila manual auditable, pero no incluida en vigencia del reporte |
| Excel | Media | tiene trazabilidad, pero mezcla datos históricos y actuales |

## 10. Fuentes de verdad y conflictos

| Concepto | Fuente que manda hoy | Conflicto relevante |
|---|---|---|
| Hecho biométrico | `marcacion_cruda` | la carga anulada existe pero no cuenta |
| Dueño histórico | `asignacion_marcador` | PMV coincide; comentarios antiguos no siempre |
| Vínculo | campos planos de `trabajador` | `periodo_laboral` queda fuera |
| Turno diario | asignación→programación→tramo | PMV no resuelve el tramo de cuadrilla |
| Horario | `version_turno`; fallback `turno` | el tareo solo guarda `TurnoId` |
| Descanso | `turno.dias_aplicables` | no hay tramo de descanso en rotación |
| Feriado | feriado activo nacional/planta | cambios no entran en alerta de vigencia |
| Pago de ausencia | `tipo_ausencia` actual al recalcular | tareo no congela comportamiento aplicado |
| Sobretasas | `tasa_sobretasa` vigente | consultas no distinguen empresa |
| Resultado diario | `tareo_diario` | derivado, sin auditoría fila a fila |
| Resultado Excel | composición de reporte | no es copia pura del tareo |

## 11. Deuda, riesgos y brechas priorizadas

### Críticos

1. PMV permite a Administrador operar equipos fuera de su alcance de planta.
2. Las tasas con `EmpresaId` no se filtran por empresa en liquidación/bono nocturno.
3. `PeriodoLaboral` no gobierna cálculo/reportes pese a existir y contener duplicados de negocio.

### Altos

1. La alerta de tareo vigente puede dar falso “al día”: usa el máximo `CalculadoEn`, no controla completitud y omite insumos de reporte.
2. El Excel de un periodo cerrado puede cambiar por padrón/CECO/parámetros/reintegros actuales.
3. La importación principal no es atómica entre cabecera y marcas.
4. Una solicitud aprobada puede ser parcial sin reflejarlo en su estado.
5. No hay lista de marcas consumidas ni versión/parámetros aplicados en cada tareo.
6. Una sola justificación y una sola incidencia por día pierden simultaneidad de hechos.
7. Refrigerio nocturno y categorías diurno/nocturno requieren decisión de negocio.

### Medios

1. Nombres `AO–BF` sobreviven en entidad/DTO aunque el formato vigente es `AN–BE`.
2. Contratos hablan de tres hojas y el libro entrega cuatro.
3. Margen de salida, rebote y detector de turno tienen umbrales codificados.
4. Cinco columnas del formato y suspensión permanecen vacías por diseño.

## 12. Trazabilidad hacia pruebas

Las reglas con pruebas directas se apoyan principalmente en:

- `CalculadoraTareoTests`, `IncidenciasDelDiaTests`, `DetectorTurnoDiscrepanteTests`;
- `PagoDelDiaJustificadoTests`, `PermisoParcialTests`, `RegularizacionDeMarcacionTests`;
- `RotacionTurnosTests`, `FaltaMasivaPorArchivoSinCargarTests`, `PagoDeJustificacionesAlRecalcularTests`;
- suites de lectores/origen/atribución/cobertura/anulación de Marcaciones;
- `ServicioReportesTests`, `AlcanceYVigenciaReportesTests`, `ExportadorTareoFinalTests` y end-to-end de generación;
- `FlujoDeRegularizacionTests`, `ModeloDeRegularizacionesTests`, unicidad y sustento;
- 35 pruebas declaradas de PMV.

Clasificación mecánica del catálogo: **79 reglas con prueba directa declarada, 18 con cobertura parcial o indirecta y 11 sin prueba directa localizada**. La suite no fue ejecutada en esta fase.

No se localizaron pruebas directas suficientes para: atomicidad de importación principal, margen +6 h como regla aislada, fallback sin `VersionTurno`, salida anticipada y estado, aprobación parcial de solicitudes, empresa en tasas, auditoría de tareo, completitud global de la alerta de vigencia y autorización PMV por planta.

## 13. PMV frente al flujo principal

| Aspecto | Principal | PMV |
|---|---|---|
| Persistencia | tablas productivas | tres tablas `prov_*` |
| Importación | dos guardados sin transacción envolvente | transacción explícita |
| Atribución | histórica al recalcular | histórica al consultar |
| Jornada | ventana de turno, cruza medianoche | fecha calendario, primera/última |
| Cálculo | estados, minutos, incidencia, justificación | ninguno |
| Organización | mezcla historia/actual | actual |
| Cuadrilla | resuelve ciclo y tramo | muestra cuadrilla, no turno del día |
| Salida | tareo/reportes | vista y Excel de 13 columnas |
| Alcance de planta | aplicado | ausente |

No se actualizó D14 porque la lectura de Fase 4 no añadió comportamiento PMV distinto del ya documentado; solo lo relacionó con reglas y riesgos.

## 14. Preguntas abiertas para negocio

1. ¿La jornada ordinaria contractual puede ser de 12 horas o debe limitarse a 8 y tratar el resto siempre como sobretiempo?
2. ¿El refrigerio se descuenta en jornadas cortas y cómo se imputa cuando cae dentro de 22:00–06:00?
3. ¿La tolerancia perdona todos los minutos dentro del umbral y luego cobra desde la hora teórica, como hoy?
4. ¿El margen de salida de seis horas es universal por planta/turno?
5. ¿Primera/última marca es válida ante salidas a refrigerio, trabajos partidos y reingresos?
6. ¿Nocturnidad debe ser dimensión superpuesta o tramo excluyente en el Excel GCA?
7. ¿Cualquier minuto nocturno habilita el bono diario completo?
8. ¿Una solicitud con días omitidos puede llamarse aprobada o necesita estado parcial/rechazo total?
9. ¿Se requieren tardanza y salida anticipada justificadas el mismo día?
10. ¿Qué tasa aplica cuando existen valores por empresa y uno global?
11. ¿Qué datos del trabajador deben congelarse al cerrar periodo?
12. ¿La exportación debe bloquearse si el tareo está desactualizado?
13. ¿Cómo se resuelven reingresos y los duplicados abiertos de `PeriodoLaboral`?
14. ¿PMV debe respetar alcance de planta y resolver la rotación real de cuadrilla?
15. ¿Qué acción corresponde a las 165 jornadas PMV sin dueño observadas en Fase 3B?

## 15. Artefactos de esta fase

- `BUSINESS_RULES.md`: 96 reglas principales y 12 PMV con evidencia, configuración, fuente, cobertura y riesgo.
- `diagrams/D07_ZKTECO_IMPORT_FLOW.md`
- `diagrams/D08_MARKING_TO_WORKDAY.md`
- `diagrams/D09_WORKDAY_TO_TAREO.md`
- `diagrams/D10_SHIFT_ROTATION.md`
- `diagrams/D11_REQUEST_JUSTIFICATION_REINTEGRO.md`
- `diagrams/D12_REPORT_GENERATION.md`
- actualización de `CONTEXT_REGISTER.md`.

## 16. Cierre de fase

La cadena técnica ya está reconstruida con suficiente precisión para discutirla con negocio y convertir reglas en casos de aceptación. El siguiente aumento de certeza no vendrá de leer más código de forma general, sino de resolver las preguntas anteriores, obtener datos representativos del flujo principal y ejecutar escenarios dirigidos por regla.
