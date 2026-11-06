# Fase 2 — Programación masiva de turnos fijos

**Fecha:** 2026-11-05  
**Estado:** Diseño — pendiente de aprobación  
**Prerequisito:** Fase 1.1 cerrada (1 221 tests, 0 regresiones, ROLL03 aprobado)

---

## 1. Análisis: ¿por qué NO reutilizar `IConciliadorProgramacion` para turnos fijos?

### 1.1 Qué hace el conciliador hoy

`ConciliadorProgramacion.ConfirmarAsync` (líneas 92-146) toma una lista de
`EntradaProgramacion` (trabajador × fecha × turno/descanso) y para cada bloque contiguo
crea:

| Estructura | Cantidad | Propósito |
|---|---|---|
| `Cuadrilla` con código `IMP-{trabajadorId}` | 1 por trabajador | Contenedor artificial |
| `ProgramacionCuadrilla` | 1 por bloque contiguo | Ciclo con ancla y rango |
| `TramoRotacion` con `Dias = 1` | 1 **por día** | Secuencia día a día |
| `AsignacionTurno` con `CuadrillaId` | 1 por bloque | Apunta a la cuadrilla, no al turno |

Para 200 trabajadores × 30 días = **6 000 tramos** de rotación, 200 cuadrillas y 200
programaciones, todo para decir "estos 200 van en turno DIU".

### 1.2 Qué necesita un turno fijo masivo

Un turno fijo es: **desde esta fecha, este turno, para estos trabajadores**. El motor lo
resuelve por la rama rápida de `ResolverTurno` (línea 1101 de `ServicioCalculoTareo`):

```csharp
if (vigente.TurnoId is { } turnoFijo)
{
    var turnoResuelto = horarios.En(turnoFijo, fecha);
    // ... resuelto, sin cuadrilla, sin programación, sin tramos
}
```

Una sola `AsignacionTurno` con `TurnoId` (sin `CuadrillaId`) y `FechaInicio` basta. Es el
mismo mecanismo que la ficha de Personal ya usa (`ReasignarTurno`, `ServicioTrabajadores`
línea 1081), pero aplicado a muchos trabajadores a la vez.

### 1.3 Decisión

**No reutilizar `IConciliadorProgramacion`** para turnos fijos. Crear un servicio nuevo
(`IServicioProgramacionMasiva`) que:

- Reutilice la **validación de periodos cerrados** que el conciliador ya tiene (consulta
  los periodos del rango y llama `contexto.AfirmarAbierto`).
- Reutilice la **lógica de reasignación** de `ServicioTrabajadores` (el patrón
  `ReasignarTurno` / `Reasignar` que maneja corrección en sitio, cierre de vigente,
  validación de línea de tiempo).
- **No cree cuadrillas `IMP-*`**, programaciones ni tramos.
- Opere con `AsignacionTurno.TurnoId` directo.

El conciliador sigue intacto para su caso de uso: programación por Excel con mezcla de
turnos, descansos y secuencias arbitrarias (Fase 1). No se modifica.

---

## 2. Diseño de pantalla

### 2.1 Flujo

```
┌─────────────────────────────────────────────────┐
│ Programación masiva de turnos fijos              │
│                                                  │
│  ┌──────────┐  ┌──────────┐  ┌─────────────┐   │
│  │ 1. Alcance│→│ 2. Selección│→│ 3. Previsual│  │
│  │   Planta  │  │ Trabajadores│ │  y confirmar│  │
│  └──────────┘  └──────────┘  └─────────────┘   │
└─────────────────────────────────────────────────┘
```

### 2.2 Paso 1 — Alcance

- **Planta** (obligatorio, desplegable filtrado por `IProveedorAlcancePlantas`).
- **Turno destino** (obligatorio, desplegable de turnos activos de esa planta).
- **Fecha efectiva** (`DateOnly`, obligatorio, valor por omisión: mañana).
  - No puede caer dentro de un periodo cerrado.
  - No puede ser anterior al ingreso de ningún trabajador seleccionado (se valida en paso 3).

### 2.3 Paso 2 — Selección de trabajadores

Tabla paginada (50 por página) del personal activo de la planta elegida, con filtros:

| Filtro | Tipo | Nota |
|---|---|---|
| Búsqueda | texto | Documento, apellidos, nombres |
| Turno actual | desplegable | Incluye "Sin turno", "Cuadrilla (cualquiera)" |
| Tipo planilla | desplegable | Obrero / Empleado |
| Área | desplegable | Filtrada por planta |

Columnas: checkbox, documento, apellidos y nombres, turno actual (o cuadrilla), área.

- **Seleccionar todo** marca la página visible (no las que no se ven).
- **Seleccionar todos los del filtro** marca todos los que cumplen el filtro activo,
  con confirmación ("Seleccionar los N trabajadores del filtro").
- El conteo de seleccionados se muestra como pastilla: "42 trabajadores seleccionados".
- El estado de selección se mantiene en un `Set<long>` del lado del servidor (en sesión o
  en un campo oculto con los IDs) para sobrevivir el cambio de página.

### 2.4 Paso 3 — Previsualización y confirmación

Tabla de solo lectura con todos los seleccionados y el efecto de la operación:

| Columna | Contenido |
|---|---|
| Documento | |
| Apellidos y nombres | |
| Turno actual | Lo que tiene hoy (turno fijo o cuadrilla + turno del día) |
| Turno nuevo | El seleccionado en paso 1 |
| Desde | Fecha efectiva |
| Estado | `Cambio` / `Sin cambio` (ya tiene ese turno) / `Advertencia` |
| Observación | p. ej. "Ya tiene turno DIU desde 01/10/2026" o "Corta asignación a cuadrilla A" |

**Advertencias** (no bloquean, informan):
- El trabajador está asignado a una cuadrilla → el turno fijo la reemplaza.
- El trabajador no tiene turno → primera asignación.
- La fecha es anterior al inicio de la vigente → corrección en sitio.

**Fingerprint estable:** hash SHA256 de:
```
{PlantaId}|{TurnoId}|{FechaEfectiva}|{TrabajadorId1,TrabajadorId2,...ordenados}
```
Se calcula en la previsualización, viaja como campo oculto al confirmar, y el servicio lo
recalcula para garantizar que nadie modificó la selección entre los dos pasos.

**Botones:**
- **Volver** → regresa a paso 2 con la selección intacta.
- **Confirmar** → ejecuta la operación.
- Sin los `N sin cambio`: "Confirmar: asignar turno DIU a 38 trabajadores desde 15/11/2026".

---

## 3. Contratos del servicio

### 3.1 Interfaz

```csharp
// Application/Programacion/IServicioProgramacionMasiva.cs

public interface IServicioProgramacionMasiva
{
    Task<PrevisualizacionMasiva> PrevisualizarAsync(
        SolicitudProgramacionMasiva solicitud,
        CancellationToken cancelacion = default);

    Task<ResultadoProgramacionMasiva> ConfirmarAsync(
        SolicitudProgramacionMasiva solicitud,
        string hashPrevisualizado,
        CancellationToken cancelacion = default);
}
```

### 3.2 DTOs

```csharp
// Application/Programacion/Dto/ProgramacionMasivaDtos.cs

public sealed record SolicitudProgramacionMasiva(
    long PlantaId,
    long TurnoId,
    DateOnly FechaEfectiva,
    IReadOnlyList<long> TrabajadorIds);

public sealed record PrevisualizacionMasiva(
    string Hash,
    int TotalSeleccionados,
    int Cambios,
    int SinCambio,
    int Advertencias,
    IReadOnlyList<FilaPrevisualizacion> Filas);

public sealed record FilaPrevisualizacion(
    long TrabajadorId,
    string Documento,
    string ApellidosNombres,
    string? TurnoActual,
    string? CuadrillaActual,
    string TurnoNuevo,
    DateOnly FechaEfectiva,
    EstadoCambio Estado,
    string? Observacion);

public enum EstadoCambio { Cambio, SinCambio, Advertencia }

public sealed record ResultadoProgramacionMasiva(
    int Procesados,
    int SinCambio,
    int Errores,
    IReadOnlyList<ErrorProgramacion> Detalle);

public sealed record ErrorProgramacion(
    long TrabajadorId,
    string ApellidosNombres,
    string Motivo);
```

### 3.3 Contratos de protección de datos (Bloque D)

| # | Contrato | Implementación |
|---|---|---|
| D-01 | **Alcance autorizado**: el servicio recibe `IProveedorAlcancePlantas` y valida que la planta solicitada esté dentro del alcance del usuario | `PuedeVerPlantaAsync(plantaId)` antes de cualquier consulta |
| D-02 | **Alcance vacío = denegar**: si `PlantasVisiblesAsync` devuelve `[]`, toda operación falla sin revelar datos | El servicio lanza `AccesoDenegadoException` |
| D-03 | **Validación de alcance en preview Y en confirm**: ambos métodos llaman `PuedeVerPlantaAsync`; no se confía en que preview ya lo validó | Cada método es independiente |
| D-04 | **Fingerprint estable**: el hash es determinista y no depende del orden en que el usuario seleccionó | IDs ordenados `OrderBy(id)` antes de hashear |
| D-05 | **Selección paginada**: la selección no está limitada a lo visible en una página del listado | El campo `TrabajadorIds` acepta la lista completa |
| D-06 | **Periodos cerrados**: la fecha efectiva no puede caer en un periodo cerrado | `AfirmarAbierto` sobre todos los periodos que contienen la fecha |
| D-07 | **Idempotencia**: confirmar dos veces la misma solicitud no duplica asignaciones | `ReasignarTurno` detecta "ya tiene ese turno" y lo marca `SinCambio` |
| D-08 | **Confirmaciones concurrentes**: dos confirmar simultáneos no se pisan | `SemaphoreSlim(1, 1)` serializa los confirms, igual que el conciliador |
| D-09 | **Todo o nada**: si uno falla, ninguno se aplica | Transacción explícita con `CreateExecutionStrategy().ExecuteAsync` |
| D-10 | **Sin sobrescritura sin preview**: confirm exige el hash del preview; si no coincide, rechaza | `hashPrevisualizado != hashCalculado` → error con mensaje legible |
| D-11 | **Verificación de pertenencia**: cada trabajador de la lista pertenece a la planta solicitada | Se valida contra la base; un ID de otra planta se rechaza |
| D-12 | **Sin efecto en cesados**: un trabajador cesado antes de la fecha efectiva se excluye con aviso | `FechaCese < FechaEfectiva` → `SinCambio` con observación "Cesado" |

---

## 4. Archivos a crear y modificar

### 4.1 Archivos nuevos

| Archivo | Capa | Contenido |
|---|---|---|
| `Application/Programacion/IServicioProgramacionMasiva.cs` | Application | Interfaz del servicio |
| `Application/Programacion/Dto/ProgramacionMasivaDtos.cs` | Application | DTOs del servicio |
| `Infrastructure/Programacion/ServicioProgramacionMasiva.cs` | Infrastructure | Implementación con EF Core |
| `Web/Controllers/ProgramacionMasivaController.cs` | Web | Controlador MVC, 3 acciones |
| `Web/Views/ProgramacionMasiva/Index.cshtml` | Web | Paso 1: alcance |
| `Web/Views/ProgramacionMasiva/Seleccion.cshtml` | Web | Paso 2: tabla paginada con checkboxes |
| `Web/Views/ProgramacionMasiva/Previsualizar.cshtml` | Web | Paso 3: preview y botón confirmar |
| `Web/Views/ProgramacionMasiva/Resultado.cshtml` | Web | Resultado de la operación |
| `Tests/Infrastructure.Tests/Programacion/ProgramacionMasivaTests.cs` | Tests | Tests de integración con SQLite |

### 4.2 Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Infrastructure/InyeccionDependencias.cs` | Registrar `IServicioProgramacionMasiva` como Scoped |
| `Web/Views/Shared/_Layout.cshtml` | Enlace al menú (bajo Catálogos o Asistencia) |
| `Web/Views/Shared/_MenuItems.cshtml` | Ítem de menú para roles COORDINADOR/ADMINISTRADOR |

### 4.3 Archivos que NO se tocan

| Archivo | Razón |
|---|---|
| `ConciliadorProgramacion.cs` | Caso de uso distinto, intacto |
| `ImportadorProgramacionLaboral.cs` | Lectura de Excel, no aplica |
| `ServicioCalculoTareo.cs` | El motor ya soporta `TurnoId` directo |
| `CalculadoraTareo.cs` (Domain) | Prohibido por restricciones |
| Migraciones | No se necesitan nuevas tablas ni columnas |

---

## 5. Tests de aceptación

### 5.1 Previsualización

| Test | Verifica |
|---|---|
| `PREV_01_preview_muestra_cambios_y_sin_cambio` | Filas con turno distinto → `Cambio`; filas con el mismo → `SinCambio` |
| `PREV_02_preview_advierte_si_corta_cuadrilla` | Trabajador en cuadrilla → `Advertencia` con observación |
| `PREV_03_preview_rechaza_planta_fuera_de_alcance` | `PlantasVisiblesAsync` no incluye la planta → excepción |
| `PREV_04_preview_rechaza_periodo_cerrado` | Fecha en periodo cerrado → excepción con mensaje |
| `PREV_05_preview_excluye_cesados` | Cesado antes de la fecha → `SinCambio` con "Cesado" |
| `PREV_06_preview_hash_es_estable` | Dos llamadas con los mismos datos → mismo hash |

### 5.2 Confirmación

| Test | Verifica |
|---|---|
| `CONF_01_confirmar_crea_asignaciones_directas` | `AsignacionTurno.TurnoId` no nulo, `CuadrillaId` nulo |
| `CONF_02_confirmar_cierra_asignacion_vigente` | La anterior tiene `FechaFin = FechaEfectiva - 1` |
| `CONF_03_confirmar_rechaza_hash_distinto` | Hash del preview ≠ hash recalculado → error |
| `CONF_04_confirmar_es_idempotente` | Confirmar dos veces → segunda vez 0 cambios |
| `CONF_05_confirmar_es_todo_o_nada` | Inyectar fallo → 0 asignaciones escritas |
| `CONF_06_confirmar_valida_alcance` | Planta fuera de alcance → excepción |
| `CONF_07_confirmar_no_pisa_periodo_cerrado` | Fecha en periodo cerrado → excepción |
| `CONF_08_confirmar_serializa_concurrencia` | Dos confirms paralelos → ambos completan sin corrupción |

### 5.3 Integración con el motor

| Test | Verifica |
|---|---|
| `MOT_01_turno_asignado_masivo_se_usa_en_calculo` | Recalcular un periodo después de la asignación masiva usa el turno correcto |
| `MOT_02_no_genera_cuadrillas_IMP` | Después de confirmar, no existe ninguna `Cuadrilla` con código `IMP-*` |

---

## 6. Riesgos pendientes

| # | Riesgo | Mitigación | Severidad |
|---|---|---|---|
| R-01 | **Trabajador con turno de cuadrilla real**: asignar turno fijo masivo corta la asignación a la cuadrilla. Si la cuadrilla rota la semana siguiente, esa persona no rota | Preview lo advierte; el usuario decide si proceder. La observación incluye el nombre de la cuadrilla | Media |
| R-02 | **Línea de tiempo rota**: un trabajador con asignaciones que ya tienen FechaFin podría quedar con un hueco | `ValidadorVigencias.GarantizarLineaDeTiempoSana` se ejecuta después de cada reasignación dentro de la transacción | Baja |
| R-03 | **Selección masiva con muchos IDs**: 500 trabajadores en un POST podrían exceder el tamaño del request | Transmitir como JSON en el body, no como campos de formulario. Límite configurable (default 500) | Baja |
| R-04 | **Corrección en sitio masiva**: fecha anterior al inicio de la vigente para 200 personas a la vez sin que el usuario entienda el efecto | Preview muestra "corrección en sitio" por fila. La fecha por defecto es mañana, no una fecha pasada | Baja |
| R-05 | **`COORDINADOR` global opera sobre cualquier planta**: documentado y deliberado en `ProveedorAlcancePlantasHttp` (línea 82). No es un defecto; es un riesgo aceptado | Auditoría registra quién y cuándo. Propuesto test de integración en Web.Tests (ver Bloque B) | Aceptado |
| R-06 | **Concurrencia entre ficha individual y masiva**: un administrador cambia el turno de Juan desde la ficha mientras otro corre la masiva | El semáforo serializa los confirms masivos pero no las fichas individuales. Mitigación: el hash falla si la asignación cambió entre preview y confirm. Para fase futura: evaluar un semáforo compartido | Media |

---

## 7. Recomendación de implementación

### Orden sugerido

1. **DTOs y contrato** (`Application/Programacion/`): interfaz + records, sin dependencias.
2. **Servicio** (`Infrastructure/Programacion/ServicioProgramacionMasiva.cs`):
   implementación con el patrón `ReasignarTurno` adaptado a lote.
3. **Tests de integración** (`ProgramacionMasivaTests.cs`): sobre SQLite real, no InMemory
   (por los índices filtrados de una sola asignación abierta).
4. **Controlador + vistas**: tres pasos, formulario → selección → preview → resultado.
5. **Registro DI y menú**.

### Estimación

- Capa Application (DTOs + interfaz): 1 archivo, ~80 líneas.
- Capa Infrastructure (servicio): 1 archivo, ~250 líneas.
- Capa Web (controlador + 4 vistas): 5 archivos, ~400 líneas.
- Tests: 1 archivo, ~500 líneas (14 tests).
- Modificaciones menores: 3 archivos, ~10 líneas cada uno.

**Total estimado: ~1 300 líneas en 11 archivos.**

No se crean migraciones. No se modifica el motor de cálculo. No se modifica el
conciliador. No se crean patrones de rotación.

---

## 8. Resumen de entregables completados

| # | Entregable | Estado |
|---|---|---|
| 1 | Test de rollback (ROLL03) | Aprobado — 24/24 tests verdes |
| 2 | Diagnóstico de autorización (`ProveedorAlcancePlantasHttp`) | Completado — 5 escenarios, COORDINADOR global es deliberado |
| 3 | Diseño de UI Fase 2 | Este documento, sección 2 |
| 4 | Diseño de servicio y contratos | Este documento, secciones 3 y 3.3 |
| 5 | Archivos a modificar | Este documento, sección 4 |
| 6 | Tests de aceptación | Este documento, sección 5 (14 tests) |
| 7 | Riesgos pendientes | Este documento, sección 6 (6 riesgos) |
| 8 | Recomendación de implementación | Este documento, sección 7 |
