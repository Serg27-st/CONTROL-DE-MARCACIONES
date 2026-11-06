# FASE 0.1 — Reconciliación de Auditoría de Programación Laboral

> **Fecha:** 2026-11-04
> **Restricción:** Solo lectura. No se modifica código, BD, migraciones, docs ni tests.
> **Objetivo:** Resolver contradicciones técnicas antes de implementar la programación masiva.

---

## 1. Matriz de contradicciones

| # | Afirmación del informe 09 | Realidad en código/tests | Clasificación |
|---|---|---|---|
| C-01 | DIU = 07:00–19:00, NOC = 19:00–07:00 | **Semilla**: sí, ambos así. **Tests de acceptance**: los contratos operan con DIU 07:00–**17:22** y NOC 19:00–**05:22**. **OP-01**: procedimiento piloto para versionar DIU/NOC a 17:22/05:22 con "Rige desde"; aún no ejecutado en producción. | **[DUDA]** — ver V1 |
| C-02 | Jornada ordinaria = 480 min | **Semilla**: `MinutosJornadaOrdinaria = 480` en los 5 turnos. **Motor**: los tests R-P0-03.1 confirman que L–V el ordinario llega hasta `HoraFin` (622 min con turno 17:22), no 480. Los 480 **no intervienen** en días laborables L–V. | **[DUDA]** — ver V1 |
| C-03 | El informe no menciona el umbral de 15 min para HE | **Tests**: `JornadaOrdinariaYHorasExtraAcceptanceTests` confirma que exceso < 15 min sobre el fin teórico = 0 HE; desde 15 min se computa HE completa desde el fin. | **[SÍ]** omisión en el informe |
| C-04 | "La secuencia NO se repite automáticamente" | **Test ROT_REP_01** (`RotacionesVariablesAcceptanceTests:125`): `secuencia.ResolverEn(Ancla.AddDays(2)).Estado.ShouldBe(SinProgramacion)`. **Código**: `ResolverEn` línea 93: `desplazamiento >= DiasDelCiclo → SinProgramacion`. | **[SÍ]** confirmado |
| C-05 | El importador Excel ya existe y es reutilizable | **Código**: `ImportadorProgramacionLaboral` acepta `Stream contenido` — ClosedXML lo abre como `XLWorkbook`. El stream **debe ser un Excel válido** (`.xlsx`). No hay ruta para inyectar filas sin archivo. | **[NO]** parcialmente incorrecto — ver V4 |
| C-06 | "Las cuadrillas no son obligatorias" para programación masiva | **Código**: el importador crea cuadrillas `IMP-{trabajadorId}` por cada bloque. `AsignacionTurno` requiere `TurnoId` XOR `CuadrillaId`. Para secuencias con descanso se necesita `ProgramacionCuadrilla`. Para turno fijo individual **no** se necesita cuadrilla. | **[DUDA]** — ver V2 |
| C-07 | Descanso = exactamente 1 día por tramo | **Código** (`TramoRotacion:211`): `TipoTramoRotacion.Descanso when Dias != 1 → error`. | **[SÍ]** confirmado |

---

## 2. Verificación 1 — Horarios de turnos

### Evidencia

| Fuente | DIU HoraFin | NOC HoraFin | MinutosJornadaOrdinaria |
|---|---|---|---|
| `DatosSemilla.cs:432` | 19:00 | 07:00 | 480 |
| `VersionTurno` semilla (`:497`) | 19:00 | 07:00 | 480 |
| `OP_01_SHIFT_VERSION_UPDATE.md` | **17:22** (piloto) | **05:22** (piloto) | 480 (sin cambio) |
| `JornadaOrdinariaYHorasExtraAcceptanceTests:20` | 17:22 (fixture) | 05:22 (fixture) | 480 |
| `CalculadoraTareoTests:91` | 17:22 (vía `ConFin`) | 05:22 (vía helper) | 480 |
| `ACCEPTANCE_RULES.md:91` VIG-TURNO-01 | 17:22 | 05:22 | — |
| `BUSINESS_RULES.md:90` RN-046 | 17:22 | 05:22 | — |
| BD en producción (no verificada aquí) | **07:00→19:00 o 17:22** según si OP-01 se ejecutó | ídem | 480 |

### Análisis

**Coexisten dos horarios para DIU/NOC:**

1. **Semilla y BD original:** DIU 07:00–19:00, NOC 19:00–07:00 (720 min brutos, 480 ordinarios).
2. **Regla de negocio confirmada (R-P0-03.1):** DIU 07:00–17:22, NOC 19:00–05:22 (622 min brutos, 622 ordinarios L–V).

La diferencia es **operativa, no de código**: el mecanismo `VersionTurno` con "Rige desde" ya permite la transición sin migración. El documento **OP-01** describe el procedimiento exacto — probado sobre una copia, pendiente de ejecutarse en producción.

### Impacto en el motor

El motor **no usa `MinutosJornadaOrdinaria` para decidir cuándo empieza HE** en días L–V (test `DIU_CANONICO_07_00_a_17_22`). Usa `HoraFin` del turno (resuelto por `VersionTurno`). Los 480 min intervienen solo en sábado y circunstancias especiales.

Con la semilla actual (19:00), una jornada completa DIU da 720 min brutos, 480 ordinarios y 240 extras. Con 17:22 daría 622 ordinarios y 0 extras. **Son resultados opuestos.**

### Clasificación: **[DUDA]**

El informe 09 no distingue entre la semilla (19:00/07:00) y el horario confirmado (17:22/05:22). Esto es un **bloqueante para la programación masiva de turnos**: si la auditoría asume un horario y la BD tiene otro, los resultados del tareo no coincidirán con las expectativas.

**Acción requerida antes de implementar:** confirmar si OP-01 ya se ejecutó en la BD de producción. Si no, ejecutar OP-01 primero. El informe 09 debe documentar ambos horarios y su estado de transición.

---

## 3. Verificación 2 — Cuadrillas

### Operación: Asignación masiva de turno fijo

**[SÍ] funciona sin cuadrillas.**

`AsignacionTurno` acepta `TurnoId` directo (sin `CuadrillaId`). `ServicioTrabajadores.ReasignarTurno` ya maneja esto. Una selección masiva solo necesita llamar a `ReasignarTurno` N veces (o un método bulk equivalente).

**Evidencia:** `AsignacionTurno.cs:32` — `TurnoId` es nullable, `CuadrillaId` es nullable, CHECK exige exactamente uno.

### Operación: Programación de una secuencia de turnos (ej. 14D+14N)

**[NO] requiere cuadrilla — por diseño.**

`ProgramacionCuadrilla` es la **única** entidad que modela secuencias finitas con tramos. `AsignacionTurno` solo asigna un turno fijo o una cuadrilla. No hay `AsignacionSecuencia` independiente.

El importador resuelve esto creando cuadrillas `IMP-{trabajadorId}` — una cuadrilla "individual" por trabajador. Es un workaround que funciona pero contamina el catálogo de cuadrillas con entradas artificiales.

**Evidencia:** `ImportadorProgramacionLaboral.cs:161-174` — crea `Cuadrilla` con código `IMP-{trabajadorId}`.

### Operación: Programación de descansos móviles

**[NO] requiere cuadrilla y `ProgramacionCuadrilla`.**

Un descanso programado es un `TramoRotacion` con `Tipo = Descanso` dentro de una `ProgramacionCuadrilla`. No hay otro mecanismo. `AsignacionTurno` no modela descansos.

**Evidencia:** `ProgramacionCuadrilla.cs:103` — `DescansoProgramado` solo sale de un tramo tipo Descanso.

### Operación: Cambio individual dentro de una programación grupal

**[SÍ] funciona — con una operación de reasignación.**

`ReasignarTurno` cierra la asignación vigente a la cuadrilla y abre una nueva (a turno fijo o a otra cuadrilla). La persona sale del grupo para ese rango. El `CHECK` garantiza que no haya estado ambiguo.

**Evidencia:** `ServicioTrabajadores.ReasignarTurno` (5 casos documentados en CLAUDE.md).

### Resumen V2

| Operación | Sin cuadrilla | Con cuadrilla | Notas |
|---|---|---|---|
| Turno fijo masivo | **[SÍ]** | No necesita | N llamadas a `ReasignarTurno` |
| Secuencia (14D+14N) | **[NO]** | Requerida | Solo `ProgramacionCuadrilla` modela secuencias |
| Descanso móvil | **[NO]** | Requerida | `TramoRotacion.Descanso` vive en una programación |
| Cambio individual | **[SÍ]** | Se sale del grupo | `ReasignarTurno` cierra la asignación a cuadrilla |

**Conclusión:** la afirmación D04 del informe ("10 asignaciones individuales") solo es correcta para turno fijo. Secuencias y descansos **necesitan** una representación vía `ProgramacionCuadrilla`, sea explícita o generada (como hace el importador con `IMP-{id}`).

---

## 4. Verificación 3 — Secuencias finitas

### Cuándo termina una secuencia

La secuencia termina en `FechaFin`, que se calcula como `FechaAncla + DiasDelCiclo - 1`.

**Evidencia** (`ProgramacionCuadrilla.cs:76-79`):
```csharp
var finEsperado = FechaAncla.AddDays(DiasDelCiclo - 1);
if (FechaFin is { } fechaFin && fechaFin > finEsperado)
    return error;
```

Y `ServicioCuadrillas.ReemplazarDesdeAsync` la fija:
`FechaFin = desde + sum(tramos) - 1`.

### ¿Se repite automáticamente?

**[NO].** Evidencia triple:

1. **Código** (`ProgramacionCuadrilla.cs:93`): `desplazamiento >= DiasDelCiclo → SinProgramacion`.
2. **Test** (`ROT_REP_01`, línea 129): `ResolverEn(Ancla.AddDays(2)).Estado == SinProgramacion` para un ciclo de 2 días.
3. **Diseño** (docstring línea 28): `"Secuencia finita de trabajo y descansos"`.

No hay operador `%` (módulo) — el desplazamiento se compara linealmente contra `DiasDelCiclo`.

### Qué devuelve cuando termina

`ResultadoProgramacionDiaria` con `Estado = SinProgramacion`, `TurnoId = null`.

En el motor (`ServicioCalculoTareo.cs:1117-1127`), esto produce un `TurnoResuelto` nulo con el mensaje: *"El trabajador integra una cuadrilla que no tenía ciclo programado ese día (secuencia finita). Programe la cuadrilla para poder calcular."*

### Cómo se representa un descanso

`TramoRotacion` con `Tipo = Descanso`, `TurnoId = null`, `Dias = 1` (exactamente, validado en línea 211). Devuelve `EstadoProgramacionDiaria.DescansoProgramado`.

**Evidencia** (`ROT_DESC_07`, línea 110-111): un descanso programado **no es** `SinProgramacion` — son estados distintos. El motor los trata diferente: `DescansoProgramado` genera observación "descanso programado"; `SinProgramacion` genera incidencia.

### Qué sucede cuando un trabajador cambia de turno

`ReasignarTurno` cierra la `AsignacionTurno` vigente (fija `FechaFin`) y abre una nueva. Si la asignación era a cuadrilla, la persona sale del grupo para ese rango. 5 casos en `ReasignarTurno`:

1. Corrección en sitio (fecha ≤ inicio vigente, mismo o distinto turno).
2. Nueva asignación (fecha > inicio vigente).
3. Cuadrilla manda sobre turno fijo (si llegan ambos).
4. Rechazo si borraría la anterior entera.
5. Rechazo si alcanza periodo cerrado.

### Contratos que impiden repetición automática

| Test | Archivo | Qué verifica |
|---|---|---|
| `ROT_REP_01` | `RotacionesVariablesAcceptanceTests:125` | Día 3 de ciclo 2 = `SinProgramacion` |
| `ProgramacionCuadrillaTests` | Varios | Antes del ancla = `SinProgramacion`, fin correcto |
| Validación de `FechaFin` | `ProgramacionCuadrilla:76-79` | `FechaFin` no puede exceder `FechaAncla + DiasDelCiclo - 1` |

---

## 5. Verificación 4 — Reutilización del importador

### ¿Puede recibir propuestas sin Excel físico?

**[NO] directamente.** La firma es `PrevisualizarAsync(Stream contenido, ...)` y el cuerpo hace:

```csharp
using var libro = new XLWorkbook(contenido);  // ClosedXML
```

El stream **debe** ser un `.xlsx` válido. No hay sobrecargas que acepten una lista de filas en memoria.

### ¿Qué lógica puede extraerse?

El método privado `AnalizarAsync` hace el trabajo pesado:

1. Lee las filas del Excel → `List<DiaProgramado>`.
2. Concilia turnos contra la BD.
3. Detecta conflictos, incompatibles, duplicados.
4. Calcula la huella SHA.
5. Devuelve `(ResultadoPrevisualizacionProgramacion, List<DiaProgramado>)`.

**Punto de extracción:** si se separa la lectura del Excel (paso 1) de la conciliación y validación (pasos 2–5), se podría alimentar con filas de cualquier origen. Esto requiere:

- Extraer un tipo `DiaProgramadoEntrada` que represente una fila de cualquier fuente.
- Un método `AnalizarDiasAsync(List<DiaProgramadoEntrada>, ...)` que haga la conciliación.
- La lectura del Excel pasa a ser un adaptador que produce `List<DiaProgramadoEntrada>`.

**Esfuerzo estimado:** 2–3 días de refactoring en Infrastructure, sin tocar Domain.

### Idempotencia

**[SÍ].** `ConfirmarAsync` detecta bloques idempotentes: si todas las filas de un bloque contiguo coinciden con lo existente (`CoincideConExistente`), se saltan. Test `CATTURN_22_23`.

### Concurrencia

**[SÍ].** `SemaphoreSlim Exclusividad = new(1, 1)` serializa las confirmaciones. La huella SHA evita confirmar si los catálogos cambiaron entre previsualización y confirmación.

### Aislamiento por planta

**[SÍ].** `plantasAutorizadas` se recibe del `IProveedorAlcancePlantas` y se compara contra la planta del trabajador.

### Periodos cerrados

**[SÍ].** `ConfirmarAsync` línea 138-144: recorre los periodos que solapan el rango y llama `contexto.AfirmarAbierto(periodo)`, que lanza excepción si está cerrado.

### Rollback

**[SÍ].** `ConfirmarAsync` envuelve todo en `BeginTransactionAsync`. Si falla, se deshace completo. Test `CATTURN_24_28_29`.

### Resumen V4

| Aspecto | Estado | Evidencia |
|---|---|---|
| Acepta stream no-Excel | **[NO]** | `new XLWorkbook(contenido)` |
| Lógica extraíble | **[SÍ]** | `AnalizarAsync` separable en lectura + conciliación |
| Idempotencia | **[SÍ]** | `CoincideConExistente` + test CATTURN_22_23 |
| Concurrencia | **[SÍ]** | `SemaphoreSlim` + huella SHA |
| Aislamiento planta | **[SÍ]** | `plantasAutorizadas` |
| Periodo cerrado | **[SÍ]** | `AfirmarAbierto` |
| Rollback | **[SÍ]** | Transacción explícita + tests |

---

## 6. Verificación 5 — Decisiones D01–D06

### D01: Secuencia finita con renovación explícita

**[SÍ] aprobable.** Es coherente con el diseño actual (ROT_REP_01). "Renovar" = crear nueva `ProgramacionCuadrilla` con los mismos tramos desde el día siguiente al fin. `ReemplazarDesdeAsync` ya hace exactamente esto (sin repetir, cierra la anterior). Solo falta el botón y la alerta.

### D02: Cuadrillas opcionales, calendario como interfaz principal

**[DUDA] parcialmente aprobable.** Cuadrillas son opcionales para turno fijo (V2 lo confirma). **No son opcionales para secuencias ni descansos** — el modelo no tiene otra representación. La propuesta necesita matizar: "cuadrillas opcionales para turno fijo; cuadrillas implícitas (generadas) para secuencias y descansos". El calendario puede ser la interfaz, pero la cuadrilla sigue siendo el modelo interno.

### D03: Copiar período anterior como borrador revisable

**[SÍ] aprobable.** Consultar `AsignacionTurno` + `ProgramacionCuadrilla` del periodo anterior, generar `List<DiaProgramadoEntrada>` y pasar por la misma previsualización. Condición: el periodo anterior debe existir y tener datos. Trabajadores cesados entre ambos periodos se excluyen (verificar por `FechaCese`).

### D04: Asignación individual masiva para turno fijo; secuencias y descansos requieren representación

**[DUDA] necesita reformularse.** La recomendación original dice "10 asignaciones individuales", lo cual es correcto **solo para turno fijo**. Para secuencias y descansos la opción es:

- **a)** Cuadrillas implícitas `IMP-{id}` (como hace el importador). Ventaja: funciona hoy. Desventaja: contamina el catálogo.
- **b)** Cuadrillas explícitas con integrantes. Ventaja: modelo limpio. Desventaja: obliga al coordinador a crear la cuadrilla.
- **c)** Nueva entidad `ProgramacionIndividual` (sin cuadrilla). Ventaja: limpia. Desventaja: duplica lógica de `ProgramacionCuadrilla` y `ResolverTurno` tendría que mirar un tercer nivel.

**Recomendación:** opción (a) con convención de nomenclatura y filtro en el catálogo de cuadrillas para ocultar las `IMP-*`. Es la ruta de menor resistencia — ya funciona, ya está probada, y no requiere cambios en Domain ni en el motor.

### D05: Previsualización y confirmación compartidas

**[SÍ] aprobable con condición.** La previsualización comparte los mismos DTOs (`ResultadoPrevisualizacionProgramacion`, `EstadoFilaProgramacion`). La condición es extraer `AnalizarAsync` del acoplamiento con ClosedXML para que acepte filas de cualquier fuente (ver V4).

### D06: Coordinador y administrador respetando alcance por planta

**[SÍ] aprobable.** `ProgramacionLaboralController` ya tiene `[Authorize(Roles = "COORDINADOR,ADMINISTRADOR")]`. `IProveedorAlcancePlantas` ya filtra por planta. CLAUDE.md documenta que el filtrado por `UsuarioPlanta` aún no está implementado como filtro activo en las consultas, pero el dato se captura y el mecanismo está previsto.

### Resumen D01–D06

| Decisión | Veredicto | Condición |
|---|---|---|
| D01 Finita + renovación | **[SÍ]** | Ninguna |
| D02 Cuadrillas opcionales | **[DUDA]** | Solo para turno fijo; secuencias las necesitan |
| D03 Copiar periodo anterior | **[SÍ]** | Periodo anterior debe existir |
| D04 Asignación masiva individual | **[DUDA]** | Reformular: fijo sin cuadrilla, secuencia con `IMP-*` |
| D05 Previsualización compartida | **[SÍ]** | Extraer `AnalizarAsync` del acoplamiento Excel |
| D06 Roles y alcance planta | **[SÍ]** | `UsuarioPlanta` como filtro es trabajo futuro separado |

---

## 7. Riesgos y bloqueantes

### Bloqueante B-01: Estado de los horarios DIU/NOC en producción

**Severidad: ALTA.** Si la BD de producción todavía tiene DIU 07:00–19:00, todo cálculo de tareo con turno diurno producirá 240 min de HE donde hay 0. Y viceversa: la programación masiva que asuma 17:22 y la BD tenga 19:00 dará resultados incorrectos.

**Acción:** verificar el estado de OP-01. Si no se ejecutó, ejecutarlo antes de cualquier implementación.

### Bloqueante B-02: Cuadrillas `IMP-*` contaminan el catálogo

**Severidad: MEDIA.** El importador ya crea cuadrillas artificiales. Si la programación masiva desde la UI también las crea, el catálogo se llenará de entradas que no representan cuadrillas reales.

**Mitigación:** filtro en la vista de cuadrillas para ocultar/agrupar las `IMP-*`. Convención clara de nomenclatura.

### Riesgo R-01: Descanso de exactamente 1 día

**Severidad: BAJA.** `TramoRotacion` valida `Descanso.Dias != 1 → error`. Si el negocio necesita descansos de 2+ días consecutivos, hay que modelarlos como N tramos de descanso consecutivos. El importador ya lo soporta (puede tener varios tramos descanso en la secuencia).

### Riesgo R-02: El umbral de 15 min para HE no está documentado en el informe

**Severidad: BAJA.** No afecta la programación de turnos (es del motor de cálculo), pero el coordinador debe saberlo para interpretar correctamente los resultados del tareo.

---

## 8. Arquitectura mínima para programación masiva sin Excel

### Capa 1 — Turno fijo masivo (sin cuadrilla)

```
UI: Listado Personal → selección múltiple → modal "Asignar turno"
     ↓ (trabajadorIds[], turnoId, rigeDesde)
Application: nuevo método BulkReasignarTurnoAsync
     ↓ llama ReasignarTurno por cada uno, en transacción
Domain: sin cambios (AsignacionTurno + ReasignarTurno ya existen)
```

**Entidades tocadas:** `AsignacionTurno` (escritura), `Turno` (lectura).
**Cuadrillas:** no se crean.
**Previsualización:** lista de cambios propuestos antes de confirmar.

### Capa 2 — Secuencia con descansos (requiere cuadrilla implícita)

```
UI: Vista de programación → selección de trabajadores → patrón + fechas
     ↓ (trabajadorIds[], tramos[], fechaAncla)
Application: nuevo método ProgramarMasivamenteAsync
     ↓ Para cada trabajador: busca/crea cuadrilla IMP-{id},
       crea ProgramacionCuadrilla, asigna a cuadrilla
Infrastructure: reutiliza lógica de AnalizarAsync (extraída del importador)
Domain: sin cambios
```

**Entidades tocadas:** `Cuadrilla` (creación IMP-*), `ProgramacionCuadrilla` + `TramoRotacion` (creación), `AsignacionTurno` (escritura).

### Capa 3 — Previsualización compartida

```
Application: IAnalizadorProgramacion (nueva interfaz)
     ├─ Método: AnalizarAsync(List<DiaProgramadoEntrada>, plantasAutorizadas)
     │  → ResultadoPrevisualizacionProgramacion (mismos DTOs)
     │
     ├─ Consumidor 1: ImportadorProgramacionLaboral (lee Excel → DiaProgramadoEntrada)
     └─ Consumidor 2: ProgramacionMasivaServicio (recibe del UI → DiaProgramadoEntrada)
```

**Cambio clave:** extraer de `ImportadorProgramacionLaboral.AnalizarAsync` la lógica de conciliación y validación en un servicio puro que acepte `List<DiaProgramadoEntrada>`. El importador Excel pasa a ser un adaptador que lee y delega.

---

## 9. Contratos de aceptación propuestos

### Turno fijo masivo

| ID | Escenario | Resultado esperado |
|---|---|---|
| BULK-FIX-01 | 10 trabajadores, asignar DIU desde 2026-12-01 | 10 `AsignacionTurno` con `TurnoId`, sin `CuadrillaId` |
| BULK-FIX-02 | 1 de 10 tiene periodo cerrado en el rango | Transacción completa se rechaza (todo o nada) |
| BULK-FIX-03 | Turno no habilitado para la planta | Rechazo con mensaje legible |
| BULK-FIX-04 | Reasignar el mismo turno que ya tiene | Idempotente: no crea asignación nueva |
| BULK-FIX-05 | Trabajador en cuadrilla → se le asigna turno fijo | Sale de la cuadrilla (cierra asignación anterior) |

### Secuencia masiva

| ID | Escenario | Resultado esperado |
|---|---|---|
| BULK-SEQ-01 | 5 trabajadores, secuencia 14D+1R+14N desde 2026-12-01 | 5 cuadrillas `IMP-*`, cada una con `ProgramacionCuadrilla` |
| BULK-SEQ-02 | Trabajador ya tiene `IMP-*` anterior | Reutiliza la cuadrilla, crea nueva programación desde fecha |
| BULK-SEQ-03 | Solapamiento con programación existente | Previsualización marca `Conflicto` |
| BULK-SEQ-04 | Descanso de 3 días | Se modela como 3 tramos descanso consecutivos |
| BULK-SEQ-05 | Confirmar con 0 conflictos | `ResultadoConfirmacionProgramacion` con conteos correctos |

### Alerta de agotamiento

| ID | Escenario | Resultado esperado |
|---|---|---|
| ALRT-01 | Cuadrilla se agota en 7 días | Tarjeta de alerta en portada |
| ALRT-02 | Cuadrilla se agota en 45 días | Sin alerta |
| ALRT-03 | Cuadrilla sin programación activa | Alerta "sin programación vigente" |
| ALRT-04 | Renovar cuadrilla agotada | Nueva `ProgramacionCuadrilla` con mismos tramos, desde día siguiente |

### Previsualización compartida

| ID | Escenario | Resultado esperado |
|---|---|---|
| PREV-01 | Filas desde UI y desde Excel producen el mismo resultado de previsualización | `ResultadoPrevisualizacionProgramacion` idéntico para los mismos datos |
| PREV-02 | Huella de concurrencia funciona sin Excel | `Huella` calculada sobre los mismos campos |
| PREV-03 | Conflicto detectado bloquea confirmación | `PuedeConfirmar = false` |

---

## 10. Orden de implementación recomendado

| Orden | Entregable | Dependencia | Bloqueante previo |
|---|---|---|---|
| **0** | **Verificar/ejecutar OP-01** (horarios DIU/NOC en producción) | Ninguna | **B-01** |
| 1 | Alerta de secuencia agotada (portada + cuadrillas) | Ninguna | — |
| 2 | Turno fijo masivo (selección + `BulkReasignarTurnoAsync`) | Ninguna | — |
| 3 | Extraer `AnalizarAsync` del importador (separar lectura Excel de conciliación) | Pre-requisito de 4 y 5 | — |
| 4 | Secuencia masiva desde UI (con cuadrillas `IMP-*`) | Paso 3 | — |
| 5 | Previsualización compartida (UI + Excel) | Paso 3 | — |
| 6 | Vista de cobertura planta × día (solo lectura) | Ninguna | — |
| 7 | Catálogo de plantillas de rotación | Ninguna | — |
| 8 | Copiar del periodo anterior | Paso 3 | — |
| 9 | Filtro en cuadrillas para ocultar `IMP-*` | Paso 4 | **B-02** |
| 10 | Calendario visual editable | Pasos 2, 4, 5 | — |

Los pasos 1, 2 y 6 son independientes entre sí y pueden ejecutarse en paralelo.
