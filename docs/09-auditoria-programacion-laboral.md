# FASE 0 — Auditoría de Programación Laboral

> **Fecha:** 2026-11-04
> **Restricción:** Solo lectura. Este documento no modifica código.
> **Objetivo:** Evaluar el módulo de programación de turnos para rediseñar la experiencia
> hacia un flujo más simple, intuitivo y escalable a cientos de trabajadores.

---

## 1. Inventario de componentes

### Domain

| Archivo | Responsabilidad |
|---|---|
| `Catalogos/Turno.cs` | Entidad POCO. Horario, tolerancia, ventanas, gracia de salida, `DiasSemana` (flags), `CruzaMedianoche`, `DuracionBrutaMinutos`. No tiene EF Core. |
| `Catalogos/VersionTurno.cs` | Vigencia por fecha del horario de un turno. `AplicarA` devuelve instancia nueva (nunca muta). `MismoHorarioQue` compara 9 campos. |
| `Catalogos/Cuadrilla.cs` | Identidad: código, nombre, planta. |
| `Catalogos/ProgramacionCuadrilla.cs` | Vigencia + ancla + tramos. `ResolverEn(fecha)` calcula `(fecha − ancla) mod ciclo`. Secuencia **finita** (no cíclica): pasado el último tramo → `SinProgramacion`. `ProximosCambios` para la UI. |
| `Catalogos/TramoRotacion.cs` | `Tipo` (Trabajo / Descanso), `TurnoId` (nullable, solo en Trabajo), `Dias` (≥1, Descanso exactamente 1). |
| `Personal/AsignacionTurno.cs` | `TurnoId` **o** `CuadrillaId` (exactamente uno, CHECK en BD). Vigencia por fecha con `IVigenciaPorFecha`. |

### Application

| Archivo | Responsabilidad |
|---|---|
| `Catalogos/IServicioCuadrillas.cs` | `ListarAsync`, `ProgramarAsync`, `CorrerRotacionAsync`, `SumarIntegrantesAsync`, etc. |
| `Catalogos/IServicioTurnos.cs` | CRUD de turnos con vigencia de horario. |
| `Catalogos/Dto/CuadrillaDtos.cs` | `ProgramarCuadrillaRequest` (tramos + fecha + ancla), `CorrerRotacionRequest` (±60 días). |
| `Personal/ServicioTrabajadores.cs` | `ReasignarTurno` (5 casos: corrección, frontera, cuadrilla manda). `AsignarCuadrillaAsync` (todo o nada). |
| `Personal/Dto/CuadrillaPersonalDtos.cs` | `AsignarCuadrillaRequest` (lista de IDs de trabajador). |
| **`Programacion/IImportadorProgramacionLaboral.cs`** | **Importación masiva por Excel.** `GenerarPlantillaExcel`, `PrevisualizarAsync`, `ConfirmarAsync`. |
| `Programacion/Dto/ImportacionProgramacionDtos.cs` | `FilaProgramacionPrevisualizada`, `ResultadoPrevisualizacionProgramacion` (con `Huella`, `PuedeConfirmar`), `ResultadoConfirmacionProgramacion`. |

### Infrastructure

| Archivo | Responsabilidad |
|---|---|
| `Persistencia/Servicios/ServicioCuadrillas.cs` (591 lín.) | Implementación completa. `ReemplazarDesdeAsync` (nunca borra: cierra vigencias y da baja lógica). `GarantizarPeriodosAbiertosAsync`. |
| `Persistencia/Servicios/ServicioCalculoTareo.cs` | `ResolverTurno`: cadena de 3 niveles — asignación → programación de cuadrilla → versión de turno. |
| `Programacion/ImportadorProgramacionLaboral.cs` | Lee Excel, concilia turnos, previsualiza y confirma. Crea cuadrillas bajo el capó. Token de huella para concurrencia. |

### Web

| Archivo | Responsabilidad |
|---|---|
| `Controllers/CuadrillasController.cs` (344 lín.) | CRUD + Programar + CorrerRotacion + SumarIntegrantes. |
| `Controllers/TurnosController.cs` (136 lín.) | CRUD de turnos. Valores por defecto: 07:00–19:00, tolerancia 5, anticipación 15/5, jornada 480. |
| `Controllers/ProgramacionLaboralController.cs` (119 lín.) | Importación Excel con flujo previsualizar → confirmar/cancelar. Token en `IMemoryCache` (20 min). |
| `Views/Cuadrillas/Detalle.cshtml` | Ciclo, rotación, integrantes, candidatos, historial. |
| `Views/Cuadrillas/Programar.cshtml` | Formulario de tramos (tipo, turno, días) + "Rige desde". |
| `Views/Turnos/Editar.cshtml` (247 lín.) | Horario, ventanas, gracia, rige desde, días, plantas. |

### Tests (selección relevante)

| Archivo | Cobertura |
|---|---|
| `ProgramacionCuadrillaTests.cs` | Ciclo 14D+14N, antes del ancla, próximos cambios, tramo cero rechazado, baja lógica. |
| `RotacionesVariablesAcceptanceTests.cs` | ROT_01 (7+7), ROT_02 (10+5), ROT_DESC (descanso), **ROT_REP_01 (la secuencia NO se repite)**. |
| `ImportadorProgramacionLaboralTests.cs` (682 lín.) | Plantilla, preview, incompatible, duplicados, solapamiento, periodo cerrado, token, idempotencia, rollback, formatos de fecha. **30+ escenarios.** |
| `VigenciaDelHorarioDeTurnoTests.cs` | Recálculo con horario versionado, julio no cambia al editar hoy. |
| `CambioDeTurnoTests.cs`, `RotacionTurnosTests.cs` | Cambio individual, cuadrilla, corrección en sitio, periodo cerrado. |

---

## 2. Flujo actual del coordinador

### A. Programación individual (ficha por ficha)

```
1. Ir a Personal → buscar trabajador → abrir panel
2. Elegir turno del desplegable (o cuadrilla + turno)
3. Opcionalmente poner "Turno rige desde"
4. Guardar → ReasignarTurno resuelve los 5 casos
5. Repetir para cada trabajador
```

**Esfuerzo:** O(n) operaciones manuales, una por trabajador. Con 200 obreros que rotan,
son 200 ediciones para una sola rotación quincenal.

### B. Programación por cuadrilla

```
1. Ir a Catálogos → Cuadrillas → crear la cuadrilla
2. Sumar integrantes (selección múltiple, todo o nada)
3. Programar: definir tramos (turno + días) y fecha ancla
4. Si la rotación se corre: "Correr rotación" (±60 días)
5. Para reprogramar: nueva programación "desde fecha"
```

**Esfuerzo:** O(1) por grupo, pero requiere:
- Crear y mantener la estructura de cuadrillas
- Acordarse de reprogramar cuando la secuencia finita se agota
- Sumar integrantes uno por uno al principio

### C. Importación masiva por Excel (módulo ya existente)

```
1. Ir a Programación Laboral → descargar plantilla Excel
2. Llenar: documento, fecha (o rango), tipo día (trabajo/descanso), turno
3. Subir → Previsualizar (concilia turnos, detecta conflictos)
4. Si no hay conflictos → Confirmar
5. El sistema crea cuadrillas internamente
```

**Esfuerzo:** O(1) global, pero exige preparar el Excel fuera del sistema.

### Resumen del estado actual

| Camino | Existe | Escala | Fricción principal |
|---|---|---|---|
| Ficha individual | Sí | No escala | 200 ediciones por rotación |
| Cuadrilla | Sí | Escala por grupo | Hay que crear/mantener cuadrillas, y la secuencia finita se agota |
| Excel masivo | **Sí** | Escala globalmente | Requiere archivo externo; el coordinador tiene que armarlo |

---

## 3. Análisis de dependencias

### 3.1 Cadena de resolución del turno (motor de cálculo)

```
ResolverTurno(trabajadorId, fecha)
  │
  ├─ 1. AsignacionTurno vigente en fecha
  │     ├─ TurnoId → turno fijo
  │     └─ CuadrillaId → 2.
  │
  ├─ 2. ProgramacionCuadrilla.ResolverEn(fecha)
  │     ├─ TramoTrabajo → TurnoId del tramo
  │     ├─ DescansoProgram. → null (día de descanso)
  │     └─ SinProgramacion → null (secuencia agotada)
  │
  └─ 3. VersionTurno vigente en fecha → AplicarA(turno)
        → instancia nueva con el horario de esa fecha
```

**Dependencia crítica:** cualquier rediseño de la programación debe producir datos que
esta cadena consuma sin cambios. El motor no se toca.

### 3.2 Entidades que dependen directamente

| Entidad | Usa turno/cuadrilla para… |
|---|---|
| `CalculadoraTareo` | Emparejar marcaciones, calcular tardanza, sobretiempo, nocturnidad |
| `DetectorTurnoDiscrepante` | Detectar si las marcas contradicen el turno asignado |
| `ImportadorProgramacionLaboral` | Crear asignaciones y cuadrillas al confirmar |
| `EstadoCalculoTareo` | Detectar si un cambio de turno invalida el tareo |
| Pantalla de Tareo | Mostrar turno del día en la observación |
| Reportes (Resumen/Excepciones) | Turno en la fila del trabajador |
| Portada (panel de inicio) | Estado del cálculo, incidencias |

### 3.3 Guardianes transversales

- **Periodo cerrado:** `GarantizarPeriodosAbiertosAsync` bloquea todo cambio que alcance
  un periodo cerrado. El rediseño debe respetar esta barrera.
- **AsignacionTurno CHECK:** exactamente `TurnoId` o `CuadrillaId`, nunca ambos, nunca
  ninguno. El constraint está en la base.
- **Índice único filtrado:** una sola asignación abierta por trabajador (`fecha_fin IS NULL`).

---

## 4. Matriz de funcionalidades propuestas

| # | Funcionalidad | Existe | Impacto | Complejidad | Prioridad |
|---|---|---|---|---|---|
| F01 | Selección masiva de trabajadores | Parcial (cuadrilla: todo o nada) | Alto | Baja | P0 |
| F02 | Patrones de rotación guardados | No (los tramos son por cuadrilla) | Alto | Media | P1 |
| F03 | Borrador / previsualización antes de confirmar | **Sí** (importador Excel) | Alto | Ya existe | P0 |
| F04 | Copiar programación del periodo anterior | No | Alto | Media | P1 |
| F05 | Calendario visual editable | No | Muy alto | Alta | P2 |
| F06 | Detección automática de conflictos | **Sí** (importador: `Conflicto`, `Incompleto`) | Alto | Ya existe | P0 |
| F07 | Asignación por arrastrar y soltar | No | Medio | Muy alta | P3 |
| F08 | Importación Excel opcional | **Sí** (módulo `ProgramacionLaboral`) | Alto | Ya existe | P0 |
| F09 | Vista resumen de cobertura de turnos | No | Alto | Media | P1 |
| F10 | Undo / deshacer última programación | Parcial (anular = reprogramar desde) | Medio | Media | P2 |
| F11 | Programación individual rápida (sin abrir ficha) | No | Medio | Baja | P1 |
| F12 | Alertas de secuencia agotada | No | Alto | Baja | P0 |
| F13 | Rotación automática continua (ciclo infinito) | No (diseño deliberado: finita) | Alto | Baja en Domain, media en UX | P1 |
| F14 | Validación cruzada con marcaciones existentes | Parcial (`DetectorTurnoDiscrepante`) | Medio | Media | P2 |

---

## 5. Problemas de usabilidad identificados

### U01. Escalar la programación individual es inviable

Con 200+ trabajadores, programar uno por uno es impracticable. Cada rotación quincenal
exige abrir 200 fichas. Este es el problema #1 que el rediseño debe resolver.

### U02. La cuadrilla es poderosa pero pesada de arrancar

Crear la cuadrilla, sumar integrantes, programar los tramos y mantener la estructura
requiere que el coordinador entienda un modelo abstracto (ancla, tramos, ciclo). Es
correcto para operaciones pero la curva de entrada es empinada.

### U03. La secuencia finita se agota en silencio

`ProgramacionCuadrilla` es finita (test ROT_REP_01). Cuando se agota, `ResolverEn`
devuelve `SinProgramacion` y el motor no puede calcular el turno. **No hay ningún aviso
proactivo** de que una cuadrilla está por quedarse sin programación. El coordinador lo
descubre cuando el tareo saca incidencias.

### U04. El importador Excel ya existe pero no es visible

`ImportadorProgramacionLaboral` con su flujo previsualizar/confirmar, conciliación de
turnos, detección de conflictos, huella de concurrencia e idempotencia ya está
construido y cubierto por 30+ tests. Pero si el coordinador no sabe que existe o no
tiene un Excel preparado, no lo usa.

### U05. No hay vista consolidada de "quién tiene qué turno esta semana"

El coordinador no puede ver de un vistazo la programación de toda una planta para un
rango de fechas. Tiene que entrar ficha por ficha o a cada cuadrilla.

### U06. El "Rige desde" de la ficha tiene 5 casos que el coordinador no distingue

`ReasignarTurno` resuelve corrección en sitio, frontera, cuadrilla manda, etc. La UI
ofrece un solo campo de fecha sin explicar qué pasará. Un error de fecha puede mover
la frontera de la asignación anterior de forma inesperada.

### U07. Correr la rotación un múltiplo del ciclo se rechaza sin contexto

El sistema rechaza correctamente un desplazamiento que no cambiaría nada, pero el
mensaje puede confundir al coordinador que no entiende la aritmética modular.

---

## 6. Riesgos técnicos

### R01. La secuencia finita sin alerta es el riesgo más caro

Si nadie reprograma a tiempo, la cuadrilla entera se queda sin turno. El motor no
puede calcular → todos los días salen `PendienteValidacion` → la bandeja de
incidencias se llena de golpe → el coordinador se entera tarde.

**Mitigación propuesta:** alerta en la portada y en la pantalla de cuadrillas cuando
una programación se agote en los próximos N días.

### R02. `AplicarA` devuelve instancia nueva — cualquier caché debe entenderlo

`VersionTurno.AplicarA` es pura y no muta. Si una futura optimización cachea turnos
por ID sin considerar la fecha, el horario aplicado sería incorrecto.

### R03. La importación Excel crea cuadrillas internamente

`ImportadorProgramacionLaboral` puede crear cuadrillas como efecto colateral de la
confirmación. Si el coordinador también gestiona cuadrillas manualmente, las dos
fuentes pueden colisionar.

### R04. El CHECK de `AsignacionTurno` es inflexible por diseño

No se puede tener turno fijo y cuadrilla al mismo tiempo. Cualquier propuesta de
"turno por defecto + cuadrilla opcional" choca contra este constraint.

### R05. El token de confirmación vive en `IMemoryCache` (20 min)

En un escenario de múltiples instancias (hoy no aplica: una sola PC), el token
no se compartiría. No es un riesgo actual, pero sí una limitación para escalar.

---

## 7. Viabilidad de las 14 funcionalidades propuestas

### F01 — Selección masiva de trabajadores: **VIABLE, complejidad BAJA**

`AsignarCuadrillaAsync` ya acepta una lista de IDs y opera todo-o-nada. Se puede
exponer un selector con checkboxes en el listado de Personal y reutilizar la misma
lógica. No requiere cambios en Domain.

### F02 — Patrones de rotación guardados: **VIABLE, complejidad MEDIA**

Hoy los tramos viven dentro de `ProgramacionCuadrilla`. Se necesitaría una entidad
`PlantillaRotacion` (nombre, tramos) en Domain, y una pantalla de catálogo. Al
programar, se elige una plantilla en vez de definir tramos a mano. No rompe nada
existente.

### F03 — Borrador / previsualización: **YA EXISTE**

`ImportadorProgramacionLaboral.PrevisualizarAsync` devuelve `FilaProgramacionPrevisualizada`
con estado (`Existente`, `NuevoPropuesto`, `Descanso`, `Conflicto`, `Incompleto`) y
`PuedeConfirmar` (solo si 0 conflictos y 0 incompletos). El flujo
previsualizar → confirmar/cancelar ya está en producción con token de huella.

### F04 — Copiar del periodo anterior: **VIABLE, complejidad MEDIA**

Consultar `AsignacionTurno` + `ProgramacionCuadrilla` del periodo anterior y generar
un borrador para el siguiente. Requiere definir qué es "copiar" cuando hay cuadrillas
(¿copiar la cuadrilla o desagrupar?). El patrón de previsualizar/confirmar ya existe.

### F05 — Calendario visual editable: **VIABLE, complejidad ALTA**

Una rejilla personas × días con celdas coloreadas por turno. Cada celda es editable
(click → selector de turno). Es la funcionalidad más impactante y la más costosa en
frontend. El backend ya tiene todo: `ResolverTurno` da el turno de cualquier día, y
`ReasignarTurno` / `ProgramarAsync` escriben los cambios. La complejidad está en la
UX: drag-select, undo, confirmar en lote, rendimiento con 200×31 celdas.

### F06 — Detección automática de conflictos: **YA EXISTE**

El importador detecta: turno incompatible con la planta, trabajador no encontrado,
duplicados, solapamiento con programación existente, alcance fuera del periodo. Los
estados `Conflicto` e `Incompleto` bloquean la confirmación.

### F07 — Asignación por arrastrar y soltar: **VIABLE, complejidad MUY ALTA**

Técnicamente posible pero requiere un framework JS de rejilla (o desarrollo custom
sobre el calendario visual). No aporta sobre F05 lo suficiente para justificar el costo
adicional en esta fase.

### F08 — Importación Excel opcional: **YA EXISTE**

`ProgramacionLaboralController` con plantilla descargable, previsualización y
confirmación. Acepta rangos de fechas, concilia turnos, detecta nocturnos, crea
cuadrillas, es idempotente. Cubierto por 30+ tests incluyendo formatos de fecha.

### F09 — Vista resumen de cobertura de turnos: **VIABLE, complejidad MEDIA**

Una consulta que cruce `AsignacionTurno` + `ProgramacionCuadrilla` para un rango de
fechas y muestre una tabla planta × turno × conteo. `ResolverTurno` ya sabe calcular
el turno de cualquier persona en cualquier fecha; el reto es hacerlo eficientemente
para toda una planta (200+ personas × 31 días).

### F10 — Undo / deshacer: **VIABLE CON LIMITACIONES, complejidad MEDIA**

`ReemplazarDesdeAsync` cierra vigencias y da baja lógica (nunca borra). Se podría
"deshacer" reactivando la programación anterior y dando baja a la nueva. Pero si el
tareo ya se recalculó con la nueva, deshacer cambia resultados. El periodo cerrado ya
bloquea cambios retroactivos.

### F11 — Programación individual rápida: **VIABLE, complejidad BAJA**

Un endpoint que reciba (trabajadorId, turnoId, rigeDesde) sin pasar por la ficha
completa. `ReasignarTurno` ya encapsula toda la lógica. Solo falta la UI (botón en
el listado o en la vista de tareo).

### F12 — Alertas de secuencia agotada: **VIABLE, complejidad BAJA**

`ProgramacionCuadrilla` calcula `FechaFin` como `FechaAncla + sum(tramos) - 1`.
Comparar contra hoy + N días es trivial. Agregar una tarjeta a la portada con
`IServicioPanelInicio` (que ya orquesta sin consultar la base directamente).

### F13 — Rotación continua (ciclo infinito): **VIABLE CON ADR, complejidad BAJA en Domain**

`ProgramacionCuadrilla.ResolverEn` ya usa `(fecha − ancla) mod ciclo`. Hoy el
`FechaFin` corta la secuencia. Quitar ese corte (o poner `FechaFin = null` como
"infinito") haría la rotación perpetua. Pero es una **decisión de diseño deliberada**
(ROT_REP_01): la secuencia finita obliga a reprogramar, lo que deja constancia. Un
ciclo infinito no tiene auditoría de "quién decidió que la rotación sigue".

**Requiere ADR** para decidir: ¿ciclo infinito por defecto con opción de cortar, o
finito con botón de "renovar"?

### F14 — Validación cruzada con marcaciones: **VIABLE, complejidad MEDIA**

`DetectorTurnoDiscrepante` ya existe y el motor lo consulta. Se puede exponer en la
previsualización: "estos 5 trabajadores tienen marcaciones nocturnas pero les estás
asignando turno diurno". Requiere leer `marcacion_cruda` del rango.

---

## 8. Propuesta de flujo simplificado

### Flujo objetivo: "Programar el mes en 5 minutos"

```
COORDINADOR
  │
  ├─ OPCIÓN A: Desde la pantalla "Programación Laboral" (nueva)
  │     1. Elegir planta y rango de fechas
  │     2. El sistema muestra la rejilla persona × día con la
  │        programación vigente (incluyendo cuadrillas resueltas)
  │     3. Acciones:
  │        a) "Copiar del periodo anterior" → prellenar
  │        b) "Aplicar patrón" → elegir plantilla de rotación
  │           y rango de trabajadores (checkboxes)
  │        c) Edición puntual: click en celda → cambiar turno
  │        d) Importar Excel → flujo existente de previsualización
  │     4. Vista previa con conflictos resaltados (reutilizar
  │        lógica de EstadoFilaProgramacion)
  │     5. Confirmar → el sistema crea/modifica asignaciones
  │
  ├─ OPCIÓN B: Desde la cuadrilla (como hoy, para quienes la usan)
  │     Sin cambios. Es la ruta de quien ya domina el modelo.
  │
  └─ OPCIÓN C: Excel (como hoy, para migrar datos externos)
        Sin cambios. Flujo previsualizar/confirmar ya probado.
```

### Principios del rediseño

1. **Las cuadrillas no son obligatorias.** Son una herramienta, no un requisito. La
   programación individual y la masiva por Excel deben funcionar sin crear cuadrillas.

2. **El Excel es una vía, no la vía.** Quien trabaja cómodo en Excel sigue usándolo.
   Quien no, programa desde la pantalla.

3. **Todo pasa por previsualización.** Ningún cambio masivo se aplica sin mostrar
   antes qué va a pasar. El patrón ya existe en el importador.

4. **El sistema avisa antes de que el turno se agote.** Alerta proactiva en la portada
   y en la vista de cuadrillas.

5. **El motor de cálculo no se toca.** `ResolverTurno` sigue consumiendo
   `AsignacionTurno` → `ProgramacionCuadrilla` → `VersionTurno`. Cualquier nueva UI
   escribe a esas mismas entidades.

---

## 9. Evaluación del modelo de datos conceptual

### ¿El modelo actual soporta el rediseño?

**Sí, con extensiones menores.**

| Concepto | Existe | Cambio necesario |
|---|---|---|
| Turno con vigencia de horario | ✅ `VersionTurno` | Ninguno |
| Asignación por fecha | ✅ `AsignacionTurno` | Ninguno |
| Cuadrilla con ciclo | ✅ `ProgramacionCuadrilla` + `TramoRotacion` | Ninguno |
| CHECK de TurnoId XOR CuadrillaId | ✅ | Ninguno |
| Periodo cerrado como barrera | ✅ `GarantizarPeriodosAbiertosAsync` | Ninguno |
| Plantilla de rotación guardada | ❌ | Nueva entidad `PlantillaRotacion` |
| Ciclo infinito opcional | ❌ | `FechaFin = null` en `ProgramacionCuadrilla` (requiere ADR) |
| Historial de programaciones masivas | ❌ | Nueva entidad o reutilizar la importación como log |

### Invariantes que el rediseño no puede romper

1. `AsignacionTurno` lleva exactamente `TurnoId` o `CuadrillaId` (CHECK).
2. Una sola asignación abierta por trabajador (índice único filtrado).
3. `ReemplazarDesdeAsync` nunca borra: cierra vigencias y da baja lógica.
4. Periodo cerrado = nada se escribe.
5. `VersionTurno.AplicarA` devuelve instancia nueva.
6. `ProgramacionCuadrilla.ResolverEn` es pura y determinista.

---

## 10. Integración con el motor de tareo

### Punto de contacto

`ServicioCalculoTareo.ResolverTurno` es el **único** punto donde el motor obtiene el
turno de un trabajador para una fecha. Cualquier forma de programar — ficha, cuadrilla,
Excel, calendario visual — termina escribiendo en `AsignacionTurno` y/o
`ProgramacionCuadrilla`, que es lo que `ResolverTurno` lee.

### Flujo completo

```
Programación (escritura)          Motor (lectura)
─────────────────────            ──────────────────
AsignacionTurno ───┐
                   ├──→ ResolverTurno ──→ CalculadoraTareo
ProgramacionCuadr.─┘         │
                             ▼
                    VersionTurno.AplicarA
                             │
                             ▼
                     Turno hidratado
                     con horario de
                     esa fecha
```

### ¿Qué pasa si el turno cambia después de calcular?

`EstadoCalculoTareo` compara `TareoDiario.CalculadoEn` contra la última modificación de
turnos/asignaciones/programaciones. Si algo cambió, la pantalla y el Excel avisan
"Tareo desactualizado — recalcule". **El rediseño no necesita cambiar este mecanismo**
siempre que los cambios de programación actualicen `ModificadoEn` en las entidades
correspondientes (que ya lo hacen por el interceptor de auditoría).

### Riesgo de sobre-aviso

`EstadoCalculoTareo` mira `Turno.ModificadoEn`, no la vigencia de la versión. Esto
causa sobre-aviso (documentado en CLAUDE.md): editar un turno hoy marca como
desactualizado un julio ya cerrado. El rediseño no agrava este problema porque no
cambia cómo se escribe `Turno`.

---

## 11. Cambios mínimos necesarios

### Nivel 0 — Sin tocar código existente

| Cambio | Archivo(s) | Esfuerzo |
|---|---|---|
| Alerta de secuencia agotada en portada | `IServicioPanelInicio` + vista | 1–2 días |
| Enlace visible a Programación Laboral en el menú | Layout + `_Menu.cshtml` | Horas |
| Tooltip/ayuda en "Rige desde" explicando los 5 casos | `_PanelTrabajador.cshtml` | Horas |

### Nivel 1 — Extensiones sobre lo existente

| Cambio | Archivo(s) | Esfuerzo |
|---|---|---|
| Selección masiva en listado de Personal → asignar turno | `TrabajadoresController` + JS | 2–3 días |
| Vista de cobertura planta × turno × fecha | Nuevo controlador + vista | 3–5 días |
| Programación individual rápida (sin abrir ficha completa) | Nuevo endpoint + modal | 1–2 días |

### Nivel 2 — Funcionalidades nuevas

| Cambio | Archivo(s) | Esfuerzo |
|---|---|---|
| Plantillas de rotación (catálogo) | Nueva entidad + CRUD | 3–5 días |
| Copiar programación del periodo anterior | Servicio + UI | 3–5 días |
| Calendario visual (rejilla persona × día) | Controlador + JS pesado | 2–3 semanas |
| Ciclo infinito opcional | ADR + cambio en Domain + migración | 3–5 días (requiere decisión) |

---

## 12. Plan de implementación por fases

### Fase 1 — Visibilidad y alertas (1 semana)

**Objetivo:** que el coordinador sepa qué tiene y qué le falta, sin cambiar cómo programa.

1. Alerta de secuencia agotada en la portada (tarjeta nueva en `IServicioPanelInicio`).
2. Alerta en la pantalla de Cuadrillas (programaciones que se agotan en <30 días).
3. Enlace a Programación Laboral (importador Excel) en el menú principal.
4. Tooltip de ayuda en "Rige desde" y "Turno rige desde".

**Riesgo:** bajo. Solo lectura + cambios de UI.

### Fase 2 — Programación masiva sin Excel (2–3 semanas)

**Objetivo:** programar grupos de trabajadores desde la pantalla, sin preparar un archivo.

1. Selección múltiple en listado de Personal (checkboxes).
2. Acción "Asignar turno a selección" → modal con turno + rige desde + previsualización.
3. Vista de cobertura: rejilla planta × día con turno de cada persona (solo lectura).
4. Programación individual rápida: botón en la fila del listado.

**Riesgo:** medio. Reutiliza `ReasignarTurno` (ya probado) pero la selección masiva
necesita UI nueva y tests de concurrencia.

### Fase 3 — Patrones y copiar (2 semanas)

**Objetivo:** reducir la entrada de datos repetitiva.

1. Catálogo de plantillas de rotación (`PlantillaRotacion` en Domain).
2. "Aplicar patrón" desde la vista de cobertura → seleccionar trabajadores + plantilla + fecha.
3. "Copiar del periodo anterior" → borrador editable antes de confirmar.

**Riesgo:** medio. La plantilla es una entidad nueva; "copiar" tiene casos borde
(trabajadores que ya no están, turnos que cambiaron).

### Fase 4 — Calendario visual interactivo (3–4 semanas)

**Objetivo:** la experiencia definitiva — ver y editar la programación como una rejilla.

1. Rejilla persona × día editable (click en celda → cambiar turno).
2. Drag-select para rangos.
3. Coloreo por turno, conflictos resaltados.
4. Confirmación en lote con previsualización.
5. ADR sobre ciclo infinito (si se decide implementar).

**Riesgo:** alto. Es la fase más costosa en frontend. Requiere decisiones de UX
(rendimiento con 200×31 celdas, mobile, accesibilidad).

### Fase 5 — Refinamiento (continuo)

- Validación cruzada con marcaciones en la previsualización.
- Undo de la última programación masiva.
- Arrastrar y soltar (si el calendario visual lo justifica).

---

## 13. Tests de aceptación

Cada test describe un escenario verificable. El prefijo indica la fase.

### Fase 1 — Alertas

| ID | Escenario | Resultado esperado |
|---|---|---|
| F1_ALRT_01 | Cuadrilla cuya programación se agota en 7 días | Tarjeta de alerta en la portada con "N cuadrillas por agotar programación" |
| F1_ALRT_02 | Cuadrilla con programación para 60+ días | Sin alerta |
| F1_ALRT_03 | Cuadrilla sin programación activa | Alerta "sin programación vigente" |
| F1_MENU_01 | Menú principal con rol COORDINADOR | Enlace "Programación Laboral" visible |
| F1_MENU_02 | Menú principal con rol GERENCIA | Sin enlace a Programación Laboral |

### Fase 2 — Masiva sin Excel

| ID | Escenario | Resultado esperado |
|---|---|---|
| F2_SEL_01 | Seleccionar 10 trabajadores → asignar turno NOC desde 2026-12-01 | Previsualización muestra 10 cambios; al confirmar, 10 `AsignacionTurno` creadas |
| F2_SEL_02 | Seleccionar trabajador con periodo cerrado en el rango | Rechazo con mensaje accionable |
| F2_SEL_03 | Asignar turno no habilitado para la planta del trabajador | Rechazo: "turno X no está habilitado para planta Y" |
| F2_COB_01 | Ver cobertura de planta Detergentes, noviembre 2026 | Rejilla con 200 filas × 30 columnas, turno de cada día coloreado |
| F2_COB_02 | Día sin turno asignado en la rejilla | Celda marcada en rojo/naranja |
| F2_RAP_01 | Cambiar turno desde la fila del listado sin abrir ficha | Modal con turno + fecha, sin navegar |

### Fase 3 — Patrones

| ID | Escenario | Resultado esperado |
|---|---|---|
| F3_PAT_01 | Crear plantilla "14D+14N" (DIU 14 días, NOC 14 días) | Plantilla guardada y visible en catálogo |
| F3_PAT_02 | Aplicar plantilla a 5 trabajadores desde 2027-01-01 | Previsualización muestra 5 × 28 = 140 días programados |
| F3_COP_01 | Copiar programación de octubre a noviembre | Borrador con los mismos turnos del mes anterior |
| F3_COP_02 | Copiar cuando un trabajador cesó entre los dos periodos | Trabajador cesado excluido del borrador, con aviso |

### Fase 4 — Calendario visual

| ID | Escenario | Resultado esperado |
|---|---|---|
| F4_CAL_01 | Abrir calendario de planta Detergentes, noviembre | Rejilla cargada en <3 segundos con turnos coloreados |
| F4_CAL_02 | Click en celda → cambiar de DIU a NOC | Celda cambia de color; cambio pendiente (no guardado aún) |
| F4_CAL_03 | Confirmar 50 cambios | Previsualización → "50 asignaciones se crearán/modificarán" → confirmar |
| F4_CAL_04 | Conflicto con periodo cerrado al confirmar | Los días en periodo cerrado se marcan como error, el resto se puede confirmar por separado |
| F4_CAL_05 | 200 personas × 31 días rendimiento | La rejilla es usable (scroll fluido, sin congelamiento) |

---

## 14. Decisiones pendientes

### D01. ¿Ciclo infinito o finito con renovación?

**Contexto:** la secuencia finita es deliberada (ROT_REP_01). Obliga a reprogramar, lo
que deja rastro auditable.

**Opciones:**
- A) Finito con alerta proactiva y botón "Renovar" (extiende la misma programación).
- B) Infinito por defecto (`FechaFin = null`), con opción de cortar.
- C) Configurable por cuadrilla (finito o infinito).

**Recomendación:** opción A. Mantiene la auditoría, resuelve el problema con la alerta,
y "Renovar" es un click en vez de reprogramar desde cero. Requiere ADR.

### D02. ¿La rejilla visual reemplaza o complementa las cuadrillas?

**Contexto:** si el calendario visual permite programar a cualquier persona cualquier
día, las cuadrillas podrían volverse innecesarias.

**Recomendación:** complementa. Las cuadrillas siguen siendo la abstracción correcta
para grupos que rotan juntos. La rejilla es la vista; la cuadrilla, el modelo.

### D03. ¿Qué rango cubre "copiar del periodo anterior"?

**Opciones:**
- A) El periodo inmediatamente anterior (si existe y está cerrado).
- B) Cualquier periodo pasado elegido por el coordinador.
- C) Los últimos N días (independiente de periodos).

**Recomendación:** opción A para simplicidad. Copiar de un periodo abierto es ambiguo
(los datos pueden cambiar).

### D04. ¿La selección masiva en Personal crea cuadrillas internamente?

**Contexto:** "seleccionar 10 personas y asignarles turno NOC" puede implementarse como
10 `AsignacionTurno` individuales o como una cuadrilla implícita.

**Recomendación:** 10 asignaciones individuales. Si el coordinador quiere que roten
juntas, crea una cuadrilla explícitamente. Mezclar las dos semánticas confunde.

### D05. ¿La importación Excel y la rejilla visual comparten previsualización?

**Contexto:** ambas necesitan mostrar qué va a cambiar antes de confirmar.

**Recomendación:** sí. Reutilizar `ResultadoPrevisualizacionProgramacion` y los estados
`Existente`, `NuevoPropuesto`, `Conflicto`, `Incompleto` para ambas vías. Así el
coordinador ve el mismo formato venga de donde venga el cambio.

### D06. ¿Quién puede programar turnos?

**Contexto:** hoy `COORDINADOR` y `ADMINISTRADOR` acceden a Programación Laboral y a
Cuadrillas. El `ADMINISTRADOR` puede editar la ficha del trabajador (y por tanto su
turno), pero ¿debería poder hacer programación masiva?

**Recomendación:** mantener ambos roles. La programación masiva tiene el mismo riesgo
que la individual, solo cambia la escala. El `[Authorize]` del controlador ya cubre
los dos.

---

## Apéndice A — Datos de referencia (semilla)

### Turnos sembrados

| Código | Horario | Cruza medianoche | Refrigerio | Días |
|---|---|---|---|---|
| DIU | 07:00–19:00 | No | 0 min | L–S |
| NOC | 19:00–07:00 | Sí | 0 min | L–S |
| SAB | 07:00–15:00 | No | 0 min | Solo sábado |
| DOM1 | 07:00–15:00 | No | 0 min | Solo domingo |
| DOM2 | 07:00–19:00 | No | 0 min | Solo domingo |

Todos con `MinutosRefrigerio = 0` (confirmado por GCA: el refrigerio es tiempo pagado).
`VersionTurno` abierta desde `VigenciaOriginal` (2000-01-01).

### Regla de negocio confirmada

- Jornada diurna: 07:00–19:00 (12h, 480 ordinarias + 240 extras).
- Jornada nocturna: 19:00–07:00 (cruza medianoche, misma duración).
- Rotación común observada: 14 día + descanso + 7/14 noche.
- Franja nocturna legal: 22:00–06:00.
- Domingo: descanso por defecto, sobretasa 100% si se trabaja.
- `ProgramacionCuadrilla`: secuencia **finita**, no cíclica.

---

## Apéndice B — Hallazgo clave: el importador ya existe

`ImportadorProgramacionLaboral` + `ProgramacionLaboralController` ya implementan:

- Generación de plantilla Excel descargable.
- Lectura de documento, fecha/rango, tipo de día, código de turno.
- Conciliación de turnos contra la base (detecta turnos nuevos, incompatibles, nocturnos).
- Previsualización completa con estados por fila (`Existente`, `NuevoPropuesto`,
  `Descanso`, `Conflicto`, `Incompleto`).
- Bloqueo de confirmación si hay conflictos o incompletos.
- Token de huella (SHA) para concurrencia (no confirma si el archivo cambió).
- Idempotencia: reimportar el mismo Excel no duplica.
- Rollback: si falla una fila, se deshace todo.
- Creación de cuadrillas internas.
- Reevaluación de preparación para tareo.
- 30+ tests cubriendo plantilla, preview, conflictos, duplicados, solapamiento,
  periodo cerrado, token, idempotencia, rollback, formatos de fecha.

**Este módulo es la base sobre la que construir las fases 2–4**, no algo que haya que
reescribir.
