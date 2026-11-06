# CAT-DYN-001 — Importación inteligente de unidades, equipos y marcadores

Fecha de intervención: 2026-10-28.

## Decisión arquitectónica

`Unidad` es el nombre funcional de la entidad técnica existente `Planta`. Se conservan la
clase, tabla `planta`, propiedades `PlantaId`, claves foráneas, historia de asignación,
`UsuarioPlanta`, `IProveedorAlcancePlantas`, rutas y migraciones. No se creó una entidad
paralela ni se modificó el esquema.

La terminología visible pasó a `Unidad/Unidades` en catálogos, personal, equipos,
marcadores, cuadrillas, feriados, turnos, filtros y reportes. Los identificadores internos
siguen denominándose `Planta` para preservar compatibilidad.

## Plantilla

La carga reconoce `unidad` y conserva `planta` como alias. Si ambas columnas aparecen,
sus valores normalizados deben coincidir. Una contradicción rechaza la fila.

Columnas opcionales agregadas:

- `codigo_unidad`: requerido únicamente para proponer una unidad inexistente.
- `equipo`: selector por código, nombre o serie de un equipo existente.
- `codigo_equipo` y `nombre_equipo`: requeridos para proponer un equipo inexistente.
- `serie_equipo`: identidad física opcional conforme al modelo vigente; nunca se inventa.

Las plantillas anteriores siguen siendo válidas: si no declaran marcador ni datos de
equipo, no se exige ninguna columna nueva.

## Conciliación y orden de resolución

La resolución sigue el orden `Empresa → Unidad → Equipo → Marcador → Trabajador →
Asignación`. Los estados expuestos son `Existente`, `NuevoPropuesto`, `Ambiguo`,
`Invalido`, `NoAplica` y `PendienteDatos`.

La comparación recorta y colapsa espacios, ignora mayúsculas/minúsculas y conserva tildes
y caracteres significativos. La unidad se resuelve dentro de su empresa; el equipo dentro
de la unidad; el marcador dentro del equipo. Ninguna coincidencia ambigua se elige en
silencio.

La previsualización ejecuta exactamente el flujo de escritura dentro de una transacción
aislada y la revierte al terminar. Devuelve un resumen de unidades, equipos, marcadores,
puestos y acciones por fila. Su huella conjunta se conserva en el token temporal. Al
confirmar, el lote se recalcula y se rechaza si la huella cambió.

## Persistencia e integridad

Cada fila tiene un savepoint anterior a cualquier alta. Una fila rechazada revierte su
unidad, equipo, marcador, puesto, trabajador y asignaciones; no deja catálogos huérfanos.
Las filas válidas del mismo archivo se conservan, de acuerdo con la política histórica de
importación parcial.

Las unidades se crean por `IServicioCatalogos`, los equipos por `IServicioEquipos`, los
marcadores por `IServicioMarcadores` y los trabajadores/asignaciones por
`ServicioTrabajadores`. Así se preservan validaciones, auditoría y vigencias existentes.
El bloqueo de creación del lote serializa la conciliación local y las restricciones físicas
de SQLite siguen siendo la última defensa frente a duplicados.

## Seguridad e historia laboral

La importación no agrega filas `UsuarioPlanta` ni amplía alcances. El alta final del
trabajador atraviesa `ServicioTrabajadores` y su `IProveedorAlcancePlantas`; si el usuario
no puede operar sobre la unidad recién propuesta, la fila completa se revierte.

El alta y la actualización continúan usando el flujo canónico de personal, por lo que
`PeriodoLaboral` y `AsignacionPlanta` mantienen la autoridad histórica. No se reescriben
periodos anteriores.

## Auditoría dirigida del modelo

- `Planta`: código único en el ámbito físico vigente y pertenencia obligatoria a empresa.
- `EquipoMarcador`: código global único, nombre y unidad obligatorios; serie opcional y
  única cuando existe.
- `Marcador`: código único por equipo; los ceros iniciales se conservan.
- `AsignacionMarcador`: vínculo temporal por `TrabajadorId`, `EquipoId`, `IdMarcador`,
  `FechaInicio` y `FechaFin`.
- `ServicioTrabajadores`: valida alcance, coherencia unidad/equipo, solapamientos y genera
  las proyecciones e historias laborales.

## Pruebas

Se agregaron treinta contratos `CATDYN-01..30` contra SQLite relacional con interceptor de
auditoría. Cubren reutilización y creación, normalización, alias, preview/cancelación,
rollback, equipos, marcadores, vigencias, idempotencia, seguridad, auditoría, historia,
puestos y aislamiento respecto de marcaciones/tareo.

La evidencia numérica final de ejecución se registra en `PROJECT_STATE.md` al cerrar la
suite. Producción, OP-03, bases operativas, importadores ZKTeco y motor de tareo permanecen
fuera del alcance.

## Limitaciones explícitas

- La carga no inventa códigos de unidad, códigos/nombres de equipo ni series.
- No concede acceso a una nueva unidad; esa asignación requiere el flujo autorizado de
  administración de usuarios.
- No resuelve por aproximación ortográfica ni elimina tildes.
- No modifica equipos existentes ni reactiva catálogos inactivos.
- La serie del equipo sigue siendo opcional porque así lo define el modelo físico actual.

