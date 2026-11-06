# Diagnóstico: Normalización de turnos DIU y NOC — Planta 1

**Fecha:** 2026-11-05  
**Estado:** Diagnóstico — pendiente de aprobación para ejecutar  
**Base consultada:** `src/ControlMarcacion.Web/ControlMarcacion.db`

---

## 1. Estado real del catálogo local

### 1.1 Turnos registrados

| Id | Código | Nombre | HoraInicio | HoraFin | Refrigerio | Tolerancia | Gracia salida | Antic. entrada | Antic. salida | Jornada ordinaria | Días | Activo |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | DIU | Diurno | 07:00 | **19:00** | 0 | 5 | 0 | 15 | 5 | 480 | L-S | Sí |
| 2 | NOC | Nocturno | 19:00 | **07:00** | 0 | 5 | 0 | 15 | 5 | 480 | L-S | Sí |
| 3 | SAB | Especial sábado | 07:00 | 15:00 | 0 | 5 | 0 | 15 | 5 | 480 | Sáb | Sí |
| 4 | DOM1 | Dominical 1 | 07:00 | 15:00 | 0 | 5 | 0 | 15 | 5 | 480 | Dom | Sí |
| 5 | DOM2 | Dominical 2 | 07:00 | 19:00 | 0 | 5 | 0 | 15 | 5 | 480 | Dom | Sí |

**Hallazgo:** DIU tiene `HoraFin = 19:00` y NOC tiene `HoraFin = 07:00`. Los horarios
solicitados son **DIU 17:22** y **NOC 05:22**.

### 1.2 Asociaciones turno-planta

| Turno | Planta |
|---|---|
| SAB (id=3) | PLANTA 1 (id=3) |

**DIU y NOC no tienen filas en `turno_planta`.** Esto significa que no están asociados
explícitamente a ninguna planta. El código que usa `turno_planta` es:
- La pantalla de turnos del catálogo (muestra/edita las plantas habilitadas).
- El motor las usa como conjunto de turnos candidatos para `DetectorTurnoDiscrepante`.

**SAB, DOM1 y DOM2:** SAB está asociado a Planta 1. DOM1 y DOM2 no tienen asociación.

### 1.3 Versiones de turno

| Id | Turno | FechaInicio | FechaFin | HoraInicio | HoraFin | Refrigerio | Tolerancia | Jornada | Activo |
|---|---|---|---|---|---|---|---|---|---|
| 1 | DIU (1) | 2000-01-01 | *(abierta)* | 07:00 | **19:00** | 0 | 5 | 480 | Sí |
| 2 | NOC (2) | 2000-01-01 | *(abierta)* | 19:00 | **07:00** | 0 | 5 | 480 | Sí |
| 3 | SAB (3) | 2000-01-01 | *(abierta)* | 07:00 | 15:00 | 0 | 5 | 480 | Sí |
| 4 | DOM1 (4) | 2000-01-01 | *(abierta)* | 07:00 | 15:00 | 0 | 5 | 480 | Sí |
| 5 | DOM2 (5) | 2000-01-01 | *(abierta)* | 07:00 | 19:00 | 0 | 5 | 480 | Sí |

**Solo una versión por turno**, abierta desde 2000-01-01 (la semilla). No hay historial de
cambios.

---

## 2. Dependencias identificadas

### 2.1 Tablas que referencian `turno`

| Tabla | FK | Significado |
|---|---|---|
| `version_turno` | `turno_id` | Historial de horarios del turno |
| `turno_planta` | `turno_id` | Plantas habilitadas para el turno |
| `asignacion_turno` | `turno_id` | Asignación de turno fijo a trabajador |
| `tramo_rotacion` | `turno_id` | Tramo de ciclo de cuadrilla |
| `tareo_diario` | `turno_id` | Turno usado en el cálculo del día |

### 2.2 Estado de las dependencias en la base local

| Tabla | Filas que referencian DIU/NOC | Estado |
|---|---|---|
| `asignacion_turno` | **0** | Sin asignaciones |
| `tramo_rotacion` | **0** | Sin programaciones |
| `tareo_diario` | **0** | Sin tareos calculados |
| `turno_planta` | **0** para DIU/NOC | Sin asociación a plantas |
| `version_turno` | **2** (una por turno) | Versión abierta desde 2000-01-01 |
| `trabajador` | **0** activos | Sin personal cargado |
| `periodo` | **0** | Sin periodos creados |

**La base local está vacía de datos operativos.** No hay trabajadores, asignaciones,
tareos ni periodos. Los únicos datos son las semillas (catálogos).

---

## 3. Análisis del cálculo con horarios 17:22 / 05:22

### 3.1 Cómo el motor usa `HoraFin`

El motor (`CalculoTareo.cs`) reparte minutos en `Repartir()` (línea 1285):

1. **Días L-V (camino legacy):** calcula `FinTurno` = `fecha + HoraFin` (o `fecha+1 + HoraFin`
   si cruza medianoche). Todo minuto antes del corte es **ordinario**; después es **extra**.
   `MinutosJornadaOrdinaria` **no interviene** en este camino.

2. **Rotación programada y no-laborable:** usa `Math.Min(trabajados, turno.MinutosJornadaOrdinaria)`.

### 3.2 Jornada efectiva con los nuevos horarios

**DIU 07:00 → 17:22:**
- Duración: 10 horas 22 minutos = **622 minutos**.
- Refrigerio: 0 (pagado, no se descuenta).
- Minutos ordinarios en L-V: 622 (todo antes de 17:22).
- Jornada ordinaria legal: 480 minutos (8 horas).
- Diferencia: 622 − 480 = 142 minutos = 2h 22min de sobretiempo (120 al 25 % + 22 al 35 %).

**NOC 19:00 → 05:22 del día siguiente:**
- Duración: 10 horas 22 minutos = **622 minutos**.
- Refrigerio: 0 (pagado).
- Minutos ordinarios en L-V: 622 (todo antes de 05:22).
- Diferencia: 622 − 480 = 142 minutos de sobretiempo.

### 3.3 Aritmética del motor con los nuevos horarios

**Con `MinutosJornadaOrdinaria = 480` (valor actual):**

| Escenario | L-V legacy | Rotación / No-laborable |
|---|---|---|
| Jornada completa (622 min) | 622 ordinarios, 0 extras | 480 ordinarios, 142 extras |
| Sale a las 19:22 (DIU) | 622 ordinarios, 120 HE25 | 480 ordinarios, 262 extras |
| Sale a las 21:00 (DIU) | 622 ordinarios, 120 HE25, 38 HE35 | 480 ordinarios, 300 extras |

**El problema:** en L-V legacy, los 622 minutos **completos** se computan como ordinarios.
No se generan horas extras hasta que el trabajador se queda **después** de 17:22. Pero 622
minutos incluyen 142 minutos (2h22m) por encima de la jornada legal de 8 horas.

**Esto NO es un error del motor.** Es la regla de negocio confirmada por GCA (documentada
en CLAUDE.md): «la jornada se computa desde la hora del turno» y el refrigerio es tiempo
pagado. El corte ordinarios/extras está en `HoraFin`, no en `MinutosJornadaOrdinaria`.
Las 2h22m de sobretiempo diario las absorbe el turno: son horas extra que se pagan.

### 3.4 Validación contra tests existentes

Los tests de aceptación ya validan exactamente estos horarios:

| Test | Turno | HoraFin | Resultado esperado |
|---|---|---|---|
| `HE_01_lunes_hasta_17_22_es_integramente_ordinario` | DIU | 17:22 | 622 ordinarios, 0 extras |
| `HE_02_lunes_dos_horas_despues_de_17_22_son_extra_25` | DIU | 17:22 | 622 ordinarios, 120 HE25 |
| `Reparte_la_jornada_en_ordinarias_y_extras_al_25_y_al_35` | DIU | 17:22 | 622 ordinarios, 120 HE25, 38 HE35 |
| `CrucesNocturnosFinDeSemanaAcceptanceTests` | NOC | 05:22 | 622 ordinarios |
| `LiquidacionPeriodoAcceptanceTests` | NOC | 05:22 | 622 HE en un contexto nocturno |

Todos usan `ConFin(TurnoDiurno(), 17, 22)` o equivalente — el turno base con `HoraFin`
modificado. **Los horarios 17:22/05:22 ya están validados por los tests.**

---

## 4. Turnos SAB, DOM1 y DOM2: ¿siguen siendo necesarios?

| Turno | Uso actual | Dependencias | Recomendación |
|---|---|---|---|
| SAB | Habilitado solo para Planta 1 | 0 asignaciones, 0 tramos, 0 tareos | **Pendiente de confirmación por GCA.** El sábado en un turno 12h (DIU/NOC) ya se calcula como sobretiempo. SAB solo tiene sentido si hay operarios que trabajan jornada de 8h en sábado como día ordinario. |
| DOM1 | Sin planta asociada | 0 dependencias | **Pendiente de confirmación.** Cubre el domingo de 8h (07:00–15:00). El domingo en turno rotativo se calcula al 100 %. DOM1 parece redundante si todos los domingos se pagan al 100 %. |
| DOM2 | Sin planta asociada | 0 dependencias | **Pendiente de confirmación.** Cubre el domingo de 12h (07:00–19:00). Misma duda que DOM1. |

**No se recomienda desactivarlos sin confirmación de GCA.** Con 0 dependencias, su
existencia no afecta nada y su ausencia podría afectar un caso de uso que no conocemos.
Es más seguro dejarlos y preguntar.

---

## 5. Propuesta de actualización

### 5.1 Mecanismo: nueva `VersionTurno`

El sistema ya tiene vigencia por fecha en los horarios (`VersionTurno`). La actualización es:

1. **Cerrar** la versión vigente (id=1 para DIU, id=2 para NOC) poniendo `FechaFin = fecha_efectiva - 1 día`.
2. **Crear** una nueva versión con `FechaInicio = fecha_efectiva`, `FechaFin = null` (abierta), y los nuevos horarios.
3. **Actualizar** el `Turno` maestro para que muestre los valores vigentes hoy.

Esto se hace **desde la pantalla de turnos del catálogo** (`/Turnos`), que ya soporta
«Rige desde» (`GuardarTurnoRequest.RigeDesde`). Al editar el horario con una fecha, el
servicio cierra la versión vigente y abre la nueva automáticamente.

**No se necesita migración.** Es una operación de datos, no de esquema.

### 5.2 Valores de la nueva versión

| Campo | DIU nuevo | NOC nuevo |
|---|---|---|
| HoraInicio | 07:00 | 19:00 |
| **HoraFin** | **17:22** | **05:22** |
| MinutosRefrigerio | 0 | 0 |
| MinutosTolerancia | 5 | 5 |
| MinutosGraciaSalida | 0 | 0 |
| MinutosAnticipacionEntrada | 15 | 15 |
| MinutosAnticipacionSalida | 5 | 5 |
| **MinutosJornadaOrdinaria** | **480** | **480** |
| DiasAplicables | LunesASabado | LunesASabado |

`MinutosJornadaOrdinaria` se mantiene en **480** (8 horas legales). No cambia: la jornada
legal no se modificó, lo que cambió es el horario del turno (cuándo termina la presencia
física en planta).

### 5.3 Fecha de vigencia recomendada

**Opción A — Desde hoy (2026-11-05):** apropiado si los horarios operativos ya cambiaron en
planta. Los tareos de noviembre se calcularían con el horario nuevo.

**Opción B — Desde el inicio del periodo actual o del siguiente:** apropiado si se quiere
que el cambio coincida con el corte de planilla.

**Opción C — Corrección en sitio (fecha pasada):** si los horarios 17:22/05:22 son los que
**siempre** se usaron en Planta 1 y los 19:00/07:00 de la semilla eran incorrectos, la
fecha debería ser la del ingreso del primer trabajador con ese turno.

**Recomendación:** depende de la respuesta a la pregunta "¿desde cuándo rige este horario
en Planta 1?". Si siempre fue así, es corrección en sitio. Si es un cambio reciente, la
fecha del cambio.

⚠️ **Si hay periodos cerrados en el rango, la corrección se rechaza.** La pantalla de
turnos valida que la fecha efectiva no alcance un periodo cerrado. Como la base local no
tiene periodos, no hay obstáculo hoy; en producción hay que verificar.

---

## 6. Impacto sobre tareos y programaciones

### 6.1 Base local (desarrollo)

**Impacto: cero.** No hay trabajadores, asignaciones, tareos ni periodos.

### 6.2 Base de producción (hipotético)

| Escenario | Efecto |
|---|---|
| Versión nueva desde una fecha futura | Solo afecta cálculos desde esa fecha. Tareos anteriores no cambian. |
| Corrección en sitio (fecha pasada) | Al recalcular un periodo anterior, usará el horario nuevo. `EstadoCalculoTareo` marcará los periodos como desactualizados. |
| Periodos cerrados | La pantalla rechaza la fecha si cae en un periodo cerrado. Los tareos cerrados **no se recalculan** por diseño. |
| Asignaciones a cuadrillas | Las cuadrillas que usan DIU/NOC en sus tramos de rotación resolverán el turno por la versión vigente a cada fecha. Sin cambio si la programación es posterior a la fecha efectiva. |
| Asignaciones directas | `AsignacionTurno` con `TurnoId = 1 o 2` resolverán el horario por la versión vigente a cada fecha. |

### 6.3 Efecto en el cálculo numérico

Usando como ejemplo un trabajador con turno DIU en un día de lunes:

| | Horario 19:00 (actual) | Horario 17:22 (nuevo) |
|---|---|---|
| Marca entrada 07:00, salida 19:00 | 720 ordinarios, 0 extras | **622 ordinarios, 98 HE25** |
| Marca entrada 07:00, salida 17:22 | 622 ordinarios, 0 extras | 622 ordinarios, 0 extras |
| Marca entrada 07:00, salida 21:00 | 720 ordinarios, 120 HE25 | 622 ordinarios, 120 HE25, **98 HE35** |

**El cambio redistribuye minutos entre ordinarios y extras.** Con HoraFin en 19:00, todo
lo que el trabajador está en planta entre 17:22 y 19:00 se cuenta como ordinario. Con
HoraFin en 17:22, esos 98 minutos pasan a ser HE25.

**Esto es económicamente significativo:** 98 minutos/día × tarifa HE25 × ~22 días/mes ×
N trabajadores. La decisión de la fecha efectiva tiene impacto directo en planilla.

---

## 7. Semillas para nuevas instalaciones

### 7.1 Estado actual

Las semillas en `DatosSemilla.cs` (líneas 431-501) crean los turnos con 19:00/07:00.
`SembrarVersionesDeTurno` replica los mismos horarios en la `VersionTurno` inicial.

### 7.2 Propuesta

**Actualizar las semillas** a los horarios operativos confirmados:

```csharp
// DIU
HoraInicio = new TimeOnly(7, 0), HoraFin = new TimeOnly(17, 22),
// NOC
HoraInicio = new TimeOnly(19, 0), HoraFin = new TimeOnly(5, 22),
```

Esto solo afecta **instalaciones nuevas** (las que se crean con `EnsureCreated` o ejecutan
las migraciones desde cero). Las bases existentes no ejecutan las semillas.

**Riesgo:** los horarios 17:22/05:22 son de Planta 1. Si las otras plantas (Detergentes,
Intracan/FEM, Sulfonación) usan 19:00/07:00, la semilla genérica no puede tener los dos
horarios a la vez. Opciones:

- **A)** Actualizar la semilla a 17:22/05:22 (el caso confirmado) y documentar que las
  otras plantas ajusten desde la pantalla si sus horarios difieren.
- **B)** Mantener la semilla en 19:00/07:00 y que Planta 1 ajuste desde la pantalla.
- **C)** Crear turnos específicos por planta (`DIU-P01`, `NOC-P01`) — **no recomendado**:
  rompe la unicidad del código y complica las programaciones cruzadas.

**Recomendación: opción A**, porque 17:22/05:22 son los horarios operativos confirmados por
GCA. Si otra planta necesita otros, los ajusta con una nueva `VersionTurno`, que es
exactamente para lo que existe el mecanismo de vigencia.

---

## 8. Plan de pruebas

### 8.1 Antes de ejecutar

| # | Verificación | Método |
|---|---|---|
| P-01 | Los tests existentes con `ConFin(TurnoDiurno(), 17, 22)` siguen verdes | `dotnet test --filter "HE_01\|HE_02\|HE_03\|CrucesNocturnos"` |
| P-02 | Editar DIU desde la pantalla con "Rige desde" crea una nueva versión y cierra la anterior | Manual en la app o test de integración |
| P-03 | `EstadoCalculoTareo` marca periodos como desactualizados después del cambio | Manual en la app |

### 8.2 Después de ejecutar (nuevos tests sugeridos)

| # | Test | Verifica |
|---|---|---|
| T-01 | `DIU_con_version_17_22_da_622_ordinarios` | Turno con versión nueva calcula correctamente |
| T-02 | `NOC_con_version_05_22_da_622_ordinarios` | Idem para nocturno |
| T-03 | `Version_anterior_sigue_dando_720_ordinarios` | Recalcular un periodo anterior a la fecha efectiva usa el horario de 19:00 |
| T-04 | `Editar_turno_con_rige_desde_cierra_version` | La pantalla de turnos cierra la versión vigente |
| T-05 | `Semilla_nueva_crea_turnos_con_17_22` | `EnsureCreated` produce los horarios actualizados |

### 8.3 Verificación en producción

| # | Paso | Cuándo |
|---|---|---|
| V-01 | Consultar `version_turno` antes y después del cambio | Antes y después |
| V-02 | Recalcular un periodo abierto y comparar el Excel antes/después | Después del cambio |
| V-03 | Verificar que periodos cerrados no se recalculan | Después del cambio |
| V-04 | Verificar que `EstadoCalculoTareo` avisa correctamente | Después del cambio |

---

## 9. Riesgos

| # | Riesgo | Severidad | Mitigación |
|---|---|---|---|
| R-01 | **Otras plantas usan DIU/NOC con otro horario.** El turno es único por código y compartido. | **Alta** | Confirmar con GCA antes de cambiar. Si difieren, crear turnos específicos o usar la pantalla por planta. |
| R-02 | **Corrección en sitio altera tareos ya entregados.** Si la fecha es muy anterior, tareos ya pagados cambiarán al recalcular. | **Alta** | Elegir la fecha con GCA. Los periodos cerrados están protegidos. |
| R-03 | **Efecto económico en planilla.** Redistribución de ordinarios a HE25 (98 min/día). | **Alta** | Es una decisión de negocio, no técnica. GCA debe entender el impacto antes de fijar la fecha. |
| R-04 | **La semilla difiere de la base de producción.** Si se actualiza la semilla pero la producción ya tiene datos con 19:00/07:00. | **Baja** | La semilla solo afecta instalaciones nuevas. Producción se actualiza desde la pantalla. |
| R-05 | **`MinutosJornadaOrdinaria` sigue en 480.** En rotación, 480 es el corte. En L-V legacy, no interviene. Coherente pero contraintuitivo: el turno tiene 622 minutos y la jornada ordinaria dice 480. | **Baja** | Documentar. Los 480 son la jornada legal (D.S. 007-2002-TR art. 1), no la duración del turno. |
| R-06 | **`turno_planta` vacío para DIU/NOC.** `DetectorTurnoDiscrepante` usa esta tabla para saber qué turnos son candidatos en una planta. Sin filas, no detecta discrepancias en DIU/NOC. | **Media** | Asociar DIU y NOC a las plantas donde se usan, desde la pantalla de turnos. Es una acción separada del cambio de horario. |

---

## 10. Cambios necesarios (pendientes de aprobación)

### 10.1 Datos (desde la pantalla, sin migración)

1. Editar turno DIU: `HoraFin = 17:22`, con "Rige desde = [fecha a confirmar]".
2. Editar turno NOC: `HoraFin = 05:22`, con "Rige desde = [fecha a confirmar]".
3. *(Opcional)* Asociar DIU y NOC a Planta 1 en `turno_planta`.

### 10.2 Código (opcional, con aprobación)

1. Actualizar semillas en `DatosSemilla.cs`: `HoraFin` de DIU y NOC.
2. Actualizar `SembrarVersionesDeTurno`: mismos horarios.
3. *(Si se actualiza la semilla)* Migración de datos para las bases que ya ejecutaron la
   semilla anterior: solo si se necesita que la base de desarrollo refleje el cambio sin
   pasar por la pantalla.

### 10.3 Tests (con aprobación)

1. T-01 a T-05 descritos en la sección 8.2.

---

## 11. Preguntas para GCA antes de ejecutar

1. **¿Los horarios 17:22/05:22 son exclusivos de Planta 1 o aplican a todas las plantas?**
   Si cada planta tiene un horario distinto, hay que crear turnos específicos o usar versiones
   distintas por fecha.

2. **¿Desde cuándo rige este horario?** Si siempre fue así (los 19:00/07:00 de la semilla
   eran incorrectos), la corrección debe ser retroactiva. Si es un cambio reciente, la fecha
   del cambio.

3. **¿SAB, DOM1 y DOM2 se usan en Planta 1?** Si toda la operación es con turnos de 12h
   rotativos, estos turnos de 8h podrían no necesitarse.

4. **¿Los otros turnos (SAB, DOM1, DOM2) también necesitan ajuste de horario?** Si SAB fuera
   07:00–13:22 en vez de 07:00–15:00, por ejemplo.
