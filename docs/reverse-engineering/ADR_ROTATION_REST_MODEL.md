# ADR — Descanso explícito en rotaciones

- **ID:** D-ROT-DESC
- **Fecha:** 2026-10-06
- **Estado:** IMPLEMENTADA por R-P1-05 / ROT-DESC
- **Contrato asociado:** `ROT-DESC-01` verde
- **Alcance:** modelo de rotación, resolución diaria, migración SQLite y contratos ejecutables.

## Contexto y problema

Antes de R-P1-05, la rotación de una cuadrilla se modelaba como una `ProgramacionCuadrilla`
compuesta exclusivamente por `TramoRotacion` con un `TurnoId` obligatorio. Esto permitía ciclos de
duración y secuencia variables, pero obliga a identificar cada posición como un turno de
trabajo. No puede expresar naturalmente esta secuencia:

```text
DIU × 14 → DESCANSO × 1 → NOC × 7
```

El patrón 14+1+7 es solo un caso observado. El mismo modelo debe admitir 10+1+5,
4 NOC+1 descanso+3 DIU u otra secuencia configurada, sin condicionales por código.

El problema no es solamente hacer nullable una FK. La resolución debe distinguir de forma
inequívoca:

1. un descanso que sí fue programado;
2. la ausencia de programación o asignación;
3. una programación existente que no pudo resolverse por datos inválidos.

## Modelo previo reconstruido

| Entidad | Responsabilidad actual | FKs y nulabilidad | Invariantes actuales | Consumidores y significado de `TurnoId` |
|---|---|---|---|---|
| `ProgramacionCuadrilla` | Versiona por fecha el ciclo de una cuadrilla y fija su `FechaAncla`. | `CuadrillaId` obligatorio; contiene tramos. `FechaFin` nullable significa vigencia abierta. | Debe tener tramos con `Dias > 0`; se selecciona solo si está activa y vigente. Una nueva programación cierra o sustituye la anterior. | `ServicioCuadrillas`, consultas de personal y `ServicioCalculoTareo`. No tiene `TurnoId` propio. |
| `TramoRotacion` | Representa una posición ordenada del ciclo durante una cantidad de días corridos. | `ProgramacionId` obligatorio; `TurnoId` **obligatorio** y FK restrictiva a `Turno`. | Índice único `(ProgramacionId, Orden)`; servicio exige turno válido, activo y habilitado en la planta; `Dias` entre 1 y 366. | UI de cuadrillas, resolución del tareo y señal de vigencia. `TurnoId` significa hoy “este tramo siempre es trabajo bajo este turno”. |
| `Turno` | Identidad del turno, valores actuales, ventanas de marcación, días aplicables y plantas. | Es referenciado por tramos, asignaciones, versiones y tareos; sus colecciones no cambian la identidad. | Código único; horarios y parámetros validados por el servicio; puede inactivarse sin perder historia. | Catálogos, resolución de jornadas, tareo y reportes. No representa una ausencia de trabajo. |
| `VersionTurno` | Conserva el horario y reglas del turno que rigieron en cada rango. | `TurnoId` obligatorio; `FechaFin` nullable. | Vigencias activas no solapadas; al recalcular se elige la versión de la fecha. | `HorariosPorFecha` hidrata una copia de `Turno`. Si no encuentra versión, el comportamiento actual cae conservadoramente en los valores actuales de `Turno`. |
| `AsignacionTurno` | Dice si una persona tiene turno fijo o integra una cuadrilla durante un rango. | `TrabajadorId` obligatorio; `TurnoId` nullable; `CuadrillaId` nullable. | La aplicación exige **exactamente uno** entre `TurnoId` y `CuadrillaId`; las vigencias no deben solaparse. | Personal y tareo. En una asignación directa, `TurnoId` es el turno del día; con cuadrilla es nulo y la programación decide el resultado diario. |

## Resolución previa

```text
trabajador + fecha
  ↓
AsignacionTurno vigente (la de FechaInicio más reciente como red de seguridad)
  ├─ TurnoId directo → Turno → VersionTurno vigente
  └─ CuadrillaId
       ↓
     ProgramacionCuadrilla activa y vigente
       ↓
     (fecha - FechaAncla) módulo suma(Dias)
       ↓
     TramoRotacion
       ↓
     TurnoId → Turno → VersionTurno vigente
```

`TramoEn` usa un módulo positivo. Mientras una programación permanezca vigente, su
secuencia se repite indefinidamente hacia adelante; el método aislado también proyecta
hacia atrás, aunque el consumidor normal verifica antes la vigencia. `ProximosCambios`
compara solo `TurnoId`.

Si la asignación es a cuadrilla y no existe programación vigente, `ResolverTurno` devuelve
turno nulo con un motivo específico. Si no hay asignación, falta el turno referenciado o
no se puede hidratarlo, también puede terminar en turno nulo, normalmente sin distinguir
la causa. `CalculadoraTareo` interpreta cualquier turno nulo como `EstadoTareo.SinHorario`:
no genera falta, tardanza ni salida anticipada y deja los minutos en cero. Además, el
orquestador solo selecciona `MarcacionesDeLaJornada` cuando existe un turno; por tanto, un
día sin turno no calcula las marcas que pudiera tener.

### Contrato `ROT-DESC-01`

- **Escenario:** un tramo explícito de descanso de un día debe existir sin turno laboral ni
  turno ficticio.
- **Resultado actual:** reflexión observa que `TramoRotacion.TurnoId` es `long`.
- **Resultado esperado por el contrato:** el tipo subyacente nullable debe ser `long`.
- **Razón exacta del fallo:** `Nullable.GetUnderlyingType(typeof(long))` devuelve nulo; la
  entidad y la FK exigen siempre un turno.

El contrato permanece deliberadamente rojo. La alternativa elegida lo satisfará en una
iteración posterior, pero esta ADR no lo modifica.

## Alternativas evaluadas

### A. `TurnoId` nullable

`TurnoId != null` significaría trabajo y `TurnoId == null`, descanso.

**Ventajas:** cambio conceptual y de UI pequeño; migración simple; las filas existentes no
necesitan cambiar su FK; conserva bien los ciclos laborales actuales.

**Desventajas:** el mismo nulo puede significar descanso intencional, dato incompleto o
referencia no resuelta. Obliga a repartir null checks en dominio, consultas, UI y reportes.
No permite validar en la base la intención del tramo y escala mal si aparece otro tipo.

**Migración estimada:** hacer nullable `TramoRotacion.TurnoId`; en SQLite probablemente
requiere reconstruir la tabla. No hay backfill de datos.

### B. tipo explícito + `TurnoId` nullable

Agregar `TipoTramoRotacion = Trabajo | Descanso` y hacer nullable `TurnoId`.

```text
Trabajo  → TurnoId obligatorio
Descanso → TurnoId nulo
```

**Ventajas:** expresa intención, separa descanso de corrupción, permite un `CHECK` físico,
produce DTO/UI/reportes claros y admite evolución sin depender de códigos mágicos. La
resolución puede ser exhaustiva y obliga a tratar el descanso conscientemente.

**Desventajas:** cambia entidad, DTO, servicio, resolver, consultas y UI; requiere migración
y adaptar los métodos que hoy devuelven siempre `long`.

**Migración estimada:** columna `Tipo` no nula, backfill `Trabajo`, `TurnoId` nullable y
restricción de coherencia. Es reversible mientras no existan descansos; después, volver al
modelo anterior perdería precisamente esa semántica.

### C. turno ficticio de descanso

Crear un turno `DESCANSO`, con horas artificiales, y mantener la FK obligatoria.

**Ventajas:** evita cambiar la nulabilidad de la FK y algunos recorridos siguen recibiendo
un `Turno`.

**Desventajas y riesgos:** aparece en catálogo y asignaciones; necesita una `VersionTurno` y
horarios sin significado; puede producir falta, tardanza, salida, HE, nocturnidad y ventanas
de marcación falsas. Contamina reportes/exportaciones y exige impedir su uso directo. Si el
comportamiento depende de `Turno.Codigo == "DESCANSO"`, introduce una regla global frágil y
no protegida por el modelo.

**Migración estimada:** puede no alterar esquema, pero sí exige inserción/versionado de
datos especiales y condicionales transversales. El menor cambio físico oculta el mayor
riesgo funcional.

### D. entidad de descanso separada

Mantener tramos laborales y agregar, por ejemplo, `DescansoProgramado` por fecha/posición.

**Ventajas:** descanso y turno son conceptos distintos y cada uno puede tener datos propios.

**Desventajas:** divide artificialmente una sola secuencia ordenada; obliga a combinar dos
fuentes para calcular posición, resolver precedencias y mostrar el ciclo. Introduce
solapamientos y huecos nuevos, complica `FechaAncla`, corrimientos e histórico.

**Migración estimada:** tabla nueva, constraints de no solapamiento y rediseño del algoritmo
de orden/posición. Las filas laborales existentes pueden conservarse, pero todas las
consultas deben componer ambas colecciones.

## Matriz de decisión

Escala 1–5, donde 5 es más favorable. En “cambios requeridos”, 5 significa menor amplitud
de cambio seguro, no simplemente menos líneas.

| Criterio | A nullable | B tipo explícito | C turno ficticio | D entidad separada |
|---|---:|---:|---:|---:|
| Fidelidad al dominio | 3 | **5** | 1 | 4 |
| Simplicidad conceptual | 4 | **5** | 2 | 1 |
| Bajo riesgo de bugs | 2 | **5** | 1 | 2 |
| Cambios requeridos | **5** | 3 | 2 | 1 |
| Preservación histórica | 4 | **5** | 3 | 4 |
| Impacto DB favorable | **4** | 3 | 3 | 1 |
| Claridad en UI | 3 | **5** | 1 | 2 |
| Extensibilidad | 2 | **5** | 1 | 4 |
| Facilidad de test | 3 | **5** | 2 | 3 |
| Bajo riesgo para tareo | 2 | **5** | 1 | 3 |
| Bajo riesgo para reportes | 2 | **5** | 1 | 3 |
| Compatibilidad con rotaciones actuales | **5** | **5** | 4 | 4 |
| **Total** | 39 | **56** | 22 | 32 |

## Decisión

**RECOMENDADA: opción B — tipo explícito de tramo + `TurnoId` nullable.**

Es la única alternativa que vuelve imposible confundir por diseño un descanso válido con
un turno faltante, sin convertir el descanso en un turno laboral ni separar elementos de
la misma secuencia. Su migración es mecánica y preserva exactamente todas las filas
existentes al clasificarlas como trabajo.

**DESCARTADAS:** A por ambigüedad estructural; C por contaminar el concepto de turno y el
motor; D por duplicar la línea de tiempo y crear problemas de orden y solapamiento que el
modelo actual ya resolvió dentro de `TramoRotacion`.

## Modelo e invariantes implementados

Nombre conceptual; el nombre definitivo puede seguir las convenciones del proyecto:

```text
TramoRotacion
- Tipo: Trabajo | Descanso
- TurnoId: long?
- Dias: int
- Orden: int
```

Invariantes en dominio, servicio y base:

- `Tipo = Trabajo` exige `TurnoId != null`.
- `Tipo = Descanso` exige `TurnoId == null`.
- Un descanso dura exactamente un día bajo el requisito confirmado actual.
- Todo tramo tiene `Dias > 0`; el límite operativo actual de 366 se conserva.
- `Orden` es positivo, único y contiguo dentro de la programación.
- Una programación contiene al menos un tramo de trabajo.
- Al crear un tramo de trabajo, el turno debe existir, estar activo y estar habilitado para
  la planta. La inactivación posterior no borra su uso histórico.
- La base aplica un `CHECK` equivalente a
  `(Tipo = Trabajo AND TurnoId IS NOT NULL) OR (Tipo = Descanso AND TurnoId IS NULL)`.
- El cambio futuro no edita programaciones que ya rigieron: cierra/crea vigencias mediante
  el mecanismo actual.

## `FechaAncla` y repetición

El descanso consume posición como cualquier otro tramo. Para 14+1+7, con el ancla como
día 1:

| Offset desde `FechaAncla` | Resolución |
|---:|---|
| 0–13 | DIU |
| 14 | descanso programado |
| 15–21 | NOC |

`DiasDelCiclo` vale 22. El descanso no se salta al calcular el módulo ni al correr el
ancla; `ProximosCambios` debe comparar la identidad completa del resultado
(`Trabajo(turno)`/`Descanso`), no solo un `TurnoId`.

S-ROT-DESC confirmó que una secuencia **no se repite implícitamente**. El módulo positivo
actual es comportamiento a retirar: pasada la última posición explícita no puede volver al
primer tramo solo porque la programación siga abierta. `FechaFin` ya puede delimitar una
ejecución única cuando coincide con el último día de la secuencia, pero el flujo actual no
permite declararla al crear la programación y una vigencia más larga no expresa por sí sola
si se autorizó repetir. No se agrega todavía `RepetirCiclo`; la especificación ejecutable
compara ambas alternativas y mantiene la repetición explícita como decisión de modelado
previa a la implementación.

## Contrato de resolución implementado

No devolver `Turno?` como única señal. Separar la resolución de la rotación de la
hidratación del horario:

```text
ResultadoProgramacionDiaria
- Estado:
    Trabajo
    DescansoProgramado
    SinProgramacion
    ErrorConfiguracion
- TurnoId?          // solo Trabajo
- ProgramacionId?   // trabajo/descanso provenientes de cuadrilla
- TramoId?          // trabajo/descanso provenientes de cuadrilla
- Origen            // AsignacionDirecta | Cuadrilla
- CodigoMotivo?     // causa estable para SinProgramacion/ErrorConfiguracion
- Descripcion       // explicación operativa
```

Reglas:

- `Trabajo` exige `TurnoId`; después se resuelve `Turno` y su `VersionTurno` de la fecha.
- `DescansoProgramado` exige programación/tramo y prohíbe `TurnoId`.
- `SinProgramacion` es una ausencia conocida de asignación o de programación vigente; no
  finge descanso.
- `ErrorConfiguracion` representa combinaciones inválidas o una referencia que no puede
  resolverse; no se degrada silenciosamente a descanso ni a ausencia de programación.
- Una asignación directa y una de cuadrilla continúan siendo mutuamente excluyentes. En
  datos corruptos con ambas, el futuro resolver debe devolver error; no aplicar una
  prioridad implícita.
- Un cambio manual futuro crea una nueva `AsignacionTurno` directa y cierra la pertenencia
  a cuadrilla desde la frontera. Antes de ella, la rotación —incluido su descanso— permanece
  intacta; desde ella, manda la asignación directa por ser la única vigente.

## Comportamiento implementado del tareo

### Descanso programado sin marcaciones

- `EstadoTareo.Descanso`.
- No falta, tardanza, salida anticipada ni incidencia de marcación.
- Cero minutos trabajados, ordinarios, HE25, HE35, HE100 y nocturnos.
- Observación explícita “Descanso programado por rotación”, no “sin horario”.

### Descanso programado con marcaciones

S-ROT-DESC confirmó la clasificación: en descanso programado, todo minuto de un intervalo
de trabajo válido es HE100 desde el primero, con cero ordinarias, HE25, HE35, tardanza y
salida anticipada; no aplica el umbral de 15 minutos. Liquidación debe seguir obteniendo la
tasa del concepto persistido, nunca de un literal nuevo.

La atribución trabajador↔marca no cambia y continúa resolviéndose por equipo, número,
fecha/hora y `AsignacionMarcador`. BD01-D01..D03 aprobaron resolver primero todas las
jornadas con turno, preservar `reclamadas` y delimitar el descanso sobre el residuo con las
fronteras adyacentes. Un caso inequívoco usa extremos; uno incompleto o ambiguo requiere
validación auditada. La primera marca válida debe pertenecer a D y la salida puede caer en
D+1 solo antes de la siguiente jornada. No se inyecta un turno ficticio ni se toma
automáticamente el día civil.

### Domingo y feriado

- **Domingo + descanso de rotación:** conservar ambos hechos. El origen programático es
  `DescansoProgramado`; la clasificación económica dominical vigente lleva todo lo
  trabajado al 100 %. No sumar dos conceptos ni convertir domingo en el origen del
  descanso rotativo.
- **Feriado ordinario + descanso de rotación:** BD02-D01..D05 conserva ambos hechos, pero
  clasifica N minutos trabajados únicamente como `FeriadoTrabajado`; no agrega
  `DescansoSemanalTrabajado` sobre la misma base. Sin trabajo hay cero minutos.
- **1 de mayo + descanso de rotación:** BD02-D06/D07 exige tratamiento especial. Trabajado
  conserva cuatro componentes económicos —descanso, feriado, trabajo y 100 %— sobre N
  minutos físicos únicos; sin trabajo conserva los dos componentes remunerativos y cero
  minutos trabajados.

## Impacto implementado por capa

- **Domain:** enum y nulabilidad de `TramoRotacion`; resultado discriminado; adaptación de
  `TurnoEn`/`ProximosCambios`; invariantes agregadas. `Turno` y `VersionTurno` no modelan
  descansos.
- **Application:** DTOs y requests deben transportar `Tipo`; validación exhaustiva y
  etiquetas “DESCANSO”. El contrato del tareo debe recibir la resolución explícita.
- **Infrastructure:** configuración EF nullable + `CHECK`; migración; consultas que hoy
  proyectan `TurnoId` como obligatorio; resolución y señal de vigencia.
- **Web:** cada fila muestra `Tipo: Trabajo/Descanso`, selector de turno habilitado solo
  para Trabajo y `Días = 1` para Descanso. Listado, detalle y próximos cambios muestran
  “DESCANSO”, sin agregarlo al catálogo de turnos.
- **Reportes:** el turno aparece explícitamente en vistas de cuadrilla y en la columna de
  turno del reporte de excepciones; resumen y tareo final consumen minutos agregados. Un
  descanso debe mostrarse como “DESCANSO PROGRAMADO” cuando corresponda, derivado del
  resultado de programación, no de un turno artificial ni de `TurnoId == null` aislado.
  Para auditoría estable, la implementación debe transportar —y evaluar si persiste— el
  origen de programación junto al resultado diario; esto se coordina con P1-06.
- **Tests:** contratos de dominio, EF/SQLite, servicio, tareo, UI/DTO y no regresión.

## Migración propuesta (no ejecutada)

Para la opción B:

1. agregar `Tipo` no nullable con valor temporal/predeterminado `Trabajo`;
2. verificar que todos los tramos actuales tengan un `TurnoId` válido;
3. backfill explícito de todas las filas existentes a `Trabajo`;
4. hacer nullable `TurnoId`, conservando la FK restrictiva;
5. agregar el `CHECK` Trabajo/Descanso y constraints de días/orden que admita SQLite;
6. retirar el default temporal para que toda alta declare intención;
7. verificar conteos, FKs, ciclos y resolución antes/después.

SQLite probablemente reconstruirá `TramoRotacion`. La operación debe tener respaldo,
validación `foreign_key_check`/`integrity_check` y rollback probado. No se crea esa
migración en D-ROT-DESC.

### Estrategia para datos existentes

Todos los `TramoRotacion` existentes se convierten en `Tipo = Trabajo` y conservan Id,
`ProgramacionId`, `Orden`, `TurnoId`, `Dias` y auditoría. Las rotaciones sin descansos deben
resolver exactamente los mismos turnos en todas las fechas. No se infiere ni inserta ningún
descanso histórico: solo puede nacer de una programación explícita nueva/versionada.

## Contratos implementados

- **ROT-DESC-01:** modelo 14 DIU → 1 descanso → 7 NOC; el tramo descanso tiene tipo
  explícito y `TurnoId == null`. El contrato está verde desde R-P1-05.
- **ROT-DESC-02:** descanso programado sin marcas produce `Descanso`, cero minutos y cero
  falta/tardanza/salida anticipada.
- **ROT-DESC-03:** descanso con marcas conserva una sola atribución de cada marca, no usa
  turno ficticio y clasifica el tiempo como descanso trabajado según la política/tasa
  vigente confirmada, sin duplicar base.
- **ROT-DESC-04:** ciclos existentes compuestos solo por trabajo producen exactamente la
  misma secuencia y tareo antes/después de migrar.
- **ROT-DESC-05:** para ancla 2026-07-06, día 14 resuelve DIU, día 15 descanso y día 16 NOC;
  no consulta otra vuelta: D-02 prohíbe la repetición implícita.
- **ROT-DESC-06:** una nueva programación futura cierra la anterior; fechas previas
  conservan su tramo/turno/descanso y las posteriores usan la nueva secuencia.

Agregar además pruebas negativas para Trabajo sin turno, Descanso con turno, descanso de
más de un día, orden duplicado y error de referencia; y una prueba SQLite del `CHECK`.

## Consecuencias

### Positivas

- El descanso se vuelve dato de dominio y no ausencia de dato.
- Los errores de configuración no se pagan ni se ocultan como descansos.
- Se preservan histórico, flexibilidad y compatibilidad.
- UI, reportes y pruebas reciben una semántica estable.

### Costes

- La implementación atraviesa varias capas e incluye una migración SQLite aún no desplegada.
- `ServicioCalculoTareo` y `CalculadoraTareo` tienen una ruta explícita para trabajo sin
  turno teórico.
- BD01-D01..D03 ya cerraron la delimitación: propiedad por la primera marca válida no
  reclamada de D, posible cierre en D+1 antes de la siguiente jornada y validación auditada
  ante ambigüedad. BD02-D01..D09 cerraron la clasificación económica entre feriado y
  descanso rotativo, incluida la excepción del 1 de mayo.

## Decisiones técnicas cerradas

- Usar opción B.
- El descanso pertenece a la misma secuencia ordenada y consume posición/ancla.
- No crear `Turno DESCANSO` ni depender de códigos de turno.
- Resolver con estados discriminados; no usar `Turno?` como único contrato.
- Migrar filas existentes a `Trabajo` sin inferir descansos históricos.
- Mantener cambios futuros mediante vigencias, sin reescribir pasado.
- No repetir una secuencia por módulo sin una autorización explícita de programación.
- Descanso trabajado: todo intervalo válido es HE100 desde el primer minuto, sin umbral,
  tardanza ni salida anticipada.
- La atribución de marcas no cambia; BD01-D01..D03 fijan su delimitación temporal.
- Feriado ordinario coincidente absorbe la clasificación económica de descanso trabajado;
  el 1 de mayo utiliza el contrato especial BD02-D06/D07 sin duplicar minutos físicos.

## Decisiones operativas no bloqueantes

1. Confirmar la etiqueta operativa final: se recomienda “DESCANSO PROGRAMADO” en ciclo,
   detalle y reportes.

BD-01 y BD-02 están cerradas funcionalmente. El registro de descanso sustitutorio y la
clasificación de día no laborable compensable siguen como gaps separados; no bloquean el
modelo base. **ROT-DESC fue implementado y verificado con 803/803 pruebas verdes.**

La migración dedicada `DescansoProgramadoEnRotaciones` rellena todos los tramos previos
como `Trabajo`, vuelve nullable `turno_id` y agrega los `CHECK` aprobados. `Up`, integridad,
constraints y `Down` se validaron sobre SQLite temporal. No se aplicó a la base canónica.
