# P1-DATA-PERIODOLABORAL — Fase 3A

Fecha: 2026-10-12  
Estado: **FASE 3A COMPLETADA**  
Alcance: contratos ejecutables, dominio y representación persistente; sin migrar consumidores ni datos reales

## 1. Gate aprobado

El usuario aprobó expresamente las cinco decisiones PL-PN01..PL-PN05. El documento
`P1_DATA_PERIODOLABORAL_DECISIONES.md` quedó registrado con fecha 2026-10-12, resultado 5/5
aprobadas, 0 pendientes y estado `[DECISION GATE APROBADO]`.

El contrato vinculante para esta fase es:

- `PeriodoLaboral` es autoridad temporal de vigencia y empresa.
- `AsignacionPlanta` es la autoridad temporal de planta y pertenece al vínculo laboral.
- el reingreso conserva el mismo `Trabajador` y crea periodo y asignación nuevos;
- los campos planos de `Trabajador` sobreviven solo como proyección de compatibilidad;
- no se inventa ni sanea historia ambigua durante esta fase.

## 2. Contrato implementado

Se implementó el núcleo ejecutable del contrato, sin cambiar tareo ni reportes:

1. `Trabajador.TieneVinculoEn(fecha)` consulta exclusivamente sus periodos canónicos.
2. `Trabajador.EmpresaEn(fecha)` resuelve la empresa del periodo que contiene la fecha.
3. `Trabajador.PlantaEn(fecha)` resuelve la asignación contenida en ese periodo.
4. `Trabajador.Reingresar(...)` conserva identidad e historia y abre un periodo nuevo.
5. `Trabajador.CambiarPlanta(...)` cierra la asignación anterior el día previo y abre la nueva
   en la fecha efectiva.
6. Una edición administrativa de una persona cesada ya no materializa otro periodo cerrado ni
   se interpreta como reingreso.

Los rangos son inclusivos y el fin nulo significa vigencia abierta.

## 3. Invariantes

| ID | Regla | Protección | Test asociado |
|---|---|---|---|
| INV-01 | Máximo un `PeriodoLaboral` abierto | Domain (`Reingresar`, validación) + DB (índice único filtrado) | PL-008, `PL_04_sqlite...` |
| INV-02 | Periodos de un trabajador no se solapan | Domain; no se fuerza SQL complejo | PL-009..PL-012 |
| INV-03 | Cese no anterior al ingreso | Domain; deliberadamente sin `CHECK` sobre tabla existente para evitar rebuild SQLite | PL-007 |
| INV-04 | Reingreso crea periodo nuevo | Domain | PL-015, PL-017 |
| INV-05 | Reingreso no sobrescribe historia | Domain | PL-014, PL-016 |
| INV-06 | Empresa histórica pertenece al periodo | Domain + FK EF a `Empresa` | PL-018 |
| INV-07 | Un trabajador admite múltiples periodos | Domain + relación EF 1:N | PL-013, PL-017 |
| INV-08 | Vigencia por fecha depende del periodo | Domain | PL-002..PL-006, PL-020 |
| INV-09 | `Trabajador.Activo` no reescribe vigencia histórica | Domain | PL-019 |
| INV-10 | Campos planos no son autoridad histórica | Domain (`EmpresaEn`, `PlantaEn`, `TieneVinculoEn`) | AP-008, PL-018 |
| AP-CONT | Asignación de planta queda contenida en su periodo | Domain + FK estructural | AP-001, AP-007 |
| AP-UNIQ | No hay dos plantas vigentes para la misma fecha | Domain + único filtrado para abiertas | AP-002, AP-009 |
| AP-FECHAS | Fin de asignación no precede al inicio | Domain + `CHECK` en tabla nueva | pruebas de dominio y modelo SQLite |

No todas las invariantes se trasladaron a SQL. Los solapamientos arbitrarios y la contención
entre dos filas/tablas permanecen en dominio/aplicación porque SQLite no ofrece una restricción
declarativa fiable para esos cruces.

## 4. Semántica temporal

`RangoTemporal` centraliza la semántica aprobada:

```text
inicio <= fecha
AND
(fin IS NULL OR fecha <= fin)
```

También centraliza validez (`fin >= inicio`) y solapamiento inclusivo. La usan
`PeriodoLaboral`, `AsignacionPlanta` y las protecciones del agregado `Trabajador`.

Casos cubiertos: anterior al ingreso, día de ingreso, fecha intermedia, día de cese, posterior
al cese, periodo abierto, periodo futuro y fecha situada en un hueco.

## 5. Cambios en Domain

- Se añadió `Comun/RangoTemporal.cs`.
- Se añadió `Personal/AsignacionPlanta.cs` como vigencia temporal.
- `PeriodoLaboral` ahora:
  - declara explícitamente su autoridad temporal;
  - contiene `AsignacionesPlanta`;
  - resuelve planta por fecha;
  - crea/cambia asignaciones con validación de contención y solapamiento.
- `Trabajador` ahora:
  - resuelve vínculo, empresa y planta desde el modelo canónico;
  - ofrece reingreso explícito;
  - valida toda su línea de periodos;
  - mantiene las proyecciones planas sincronizadas en las operaciones nuevas;
  - cierra también la planta abierta al registrar el cese.

## 6. Cambios en Application

El único cambio funcional en Application se limitó a `ServicioTrabajadores`:

- `SincronizarVinculoAbierto` ya no abre un periodo cuando se guarda la ficha de una persona
  que tiene `FechaCese`; guardar teléfono u otro dato administrativo no es un reingreso.
- Se conserva la materialización de un vínculo faltante únicamente para una ficha legacy vigente.

No se añadió endpoint, DTO, pantalla ni flujo completo de reingreso/traslado. Esos consumidores
pertenecen a una etapa posterior.

## 7. Cambios en Infrastructure

- `MarcacionDbContext` expone `AsignacionesPlanta`.
- `TrabajadorRepository` carga las asignaciones de planta del periodo abierto en escrituras.
- `PeriodoLaboralConfiguracion` agrega:
  - índice temporal existente;
  - índice único filtrado `uq_periodo_laboral_abierto`;
  - relación 1:N hacia `AsignacionPlanta`.
- `AsignacionPlantaConfiguracion` define tabla, FKs, índices, único filtrado y `CHECK` de fechas.
- El snapshot EF quedó alineado con la migración nueva.

No se añadió un `CHECK` a `periodo_laboral`: SQLite habría reconstruido una tabla existente solo
para agregarlo. La fecha inválida queda rechazada por el dominio y la decisión evita una operación
estructural innecesaria sobre datos que aún requieren saneamiento.

## 8. AsignacionPlanta

Se eligió la alternativa B del análisis: pertenencia directa a `PeriodoLaboral`.

```text
PeriodoLaboral 1 ─── N AsignacionPlanta N ─── 1 Planta
```

Campos persistentes propios:

- `PeriodoLaboralId`;
- `PlantaId`;
- `FechaInicio`;
- `FechaFin` nullable;
- identidad, auditoría y baja lógica heredadas de `EntidadBase`.

La asociación indirecta con el trabajador evita duplicar `TrabajadorId` y hace estructural la
regla “la planta pertenece a un vínculo laboral”. Un traslado conserva el periodo; un reingreso
crea una línea de planta nueva bajo el periodo nuevo.

## 9. Tratamiento de Trabajador.Activo

`Activo` no se eliminó ni se convirtió en criterio histórico. El inventario actual queda así:

| Uso | Clasificación | Estado en 3A |
|---|---|---|
| `ServicioCalculoTareo`, selección de población | `[AUTORIDAD HISTÓRICA] [LEGACY]` | No migrado; riesgo registrado |
| `ServicioReportes`, población/novedades por rango | `[AUTORIDAD HISTÓRICA] [LEGACY]` | No migrado; riesgo registrado |
| Desplegables de justificaciones, solicitudes y reintegros | `[FILTRO OPERATIVO]` | Se conserva para operaciones actuales |
| Listado/ficha administrativa | `[ESTADO ADMINISTRATIVO ACTUAL] [FILTRO UI]` | Se conserva |
| `Trabajador.TieneVinculoEn` | contrato de dominio histórico | Ignora `Activo`; usa periodos |

PL-019 demuestra que `Activo=false` no borra ni invalida retrospectivamente un vínculo histórico.
La migración de los dos usos legacy históricos se excluyó expresamente de esta fase.

## 10. Tratamiento de campos legacy

Se conservaron `Trabajador.FechaIngreso`, `FechaCese`, `EmpresaId` y `PlantaId`. Sus comentarios
de dominio los clasifican como **proyección vigente de compatibilidad**.

Escritores identificados:

- alta/edición en `ServicioTrabajadores`;
- baja en `Trabajador.DarDeBaja`;
- reingreso y cambio de planta en las nuevas operaciones del agregado;
- sincronización transitoria del periodo abierto desde la ficha actual.

Lectores legacy continúan en Personal, seguridad por planta, tareo, reportes, solicitudes,
justificaciones y filtros. No se sustituyeron masivamente. Los métodos históricos nuevos nunca
usan los planos como autoridad.

## 11. Migración creada

Se creó:

`20261012151429_PeriodoLaboralCanonicoYAsignacionPlanta`

`Up` fue inspeccionado manualmente y contiene únicamente:

1. creación de `asignacion_planta`;
2. FKs cascade a periodo y restrict a planta;
3. `CHECK` de fechas en la tabla nueva;
4. índices temporal, de FK y únicos filtrados;
5. índice único filtrado para un periodo abierto por trabajador.

`Down` elimina la tabla nueva y el índice del periodo. No hay `UPDATE`, `DELETE`, backfill,
reconstrucción de `periodo_laboral` ni modificación de datos.

## 12. Validación de migración

Se validó en SQLite efímero de prueba mediante `IMigrator`, equivalente a `database update`:

1. migración hasta `20261007033130_DescansoProgramadoEnRotaciones`;
2. actualización al esquema nuevo;
3. verificación de tabla e índices;
4. confirmación de cero filas inventadas en `asignacion_planta`;
5. `PRAGMA integrity_check = ok`;
6. downgrade a la migración anterior;
7. confirmación de tabla/índice retirados y `periodo_laboral` preservada;
8. segundo `PRAGMA integrity_check = ok`.

Pruebas: MIG-PL-01 y MIG-PL-02. No se utilizó ni abrió una base productiva.

## 13. Tests agregados

Se agregaron **35 tests**:

- 20 contratos PL-001..PL-020;
- 10 contratos AP-001..AP-010;
- 3 contratos de persistencia SQLite (`PL_03`, `PL_04`, `PL_05`);
- 2 contratos de migración (`MIG_PL_01`, `MIG_PL_02`).

Cubren alta, límites inclusivos, periodo abierto/futuro, fechas inválidas, abierto único,
solapamientos completos/parciales/contenidos, huecos, reingreso, empresa histórica, independencia
de `Activo`, traslados de planta, consulta histórica, contención, ambigüedad, persistencia, FK,
índice único, Up, Down e integridad SQLite.

## 14. Regresión completa

```text
Baseline anterior: 813 / 813
Tests nuevos:        35

Domain:             398 / 398
Infrastructure:     441 / 441
Web:                  9 / 9
TOTAL:              848 / 848

Fallidos:             0
Omitidos:             0

Build Release:
Errores:              0
Warnings:             0
```

La ejecución de testhost se realizó fuera del sandbox porque su canal local con `vstest` agotó
el timeout dentro del aislamiento. No fue un fallo del código ni requirió acceso a producción.

## 15. Riesgos pendientes

1. **Datos reales sin sanear.** El índice único de periodo abierto fallará al aplicarse sobre una
   copia que conserve duplicados abiertos; es una protección deliberada, no un backfill.
2. **Planta histórica desconocida.** La migración deja la tabla nueva vacía; poblarla sin evidencia
   inventaría historia. Queda `[REQUIERE SANEAMIENTO / BACKFILL HUMANO]`.
3. **Consumidores legacy.** Tareo, reportes y alcance por planta aún leen proyecciones planas.
4. **Solapamientos cerrados.** Se protegen en dominio/aplicación; el SQL solo cubre dos abiertos.
5. **Bypass por escritura directa.** Setters legacy y SQL manual pueden eludir parte del dominio;
   deben restringirse cuando termine la transición.
6. **Corrección de periodos cerrados.** Sigue sin implementarse el flujo excepcional versionado.
7. **Snapshot de reportes cerrados.** PL-PN05 está aprobado, pero fuera del alcance 3A.

## 16. Exclusiones explícitas

No se realizó ninguno de estos trabajos:

- migrar `CalculoTareo`;
- migrar reportes, exportaciones o filtros globales;
- implementar pantallas administrativas completas de reingreso/traslado;
- sanear o backfillear datos reales;
- eliminar campos legacy;
- aplicar la migración a producción;
- tocar OP-03;
- modificar una base productiva;
- hacer commit.

Las cuatro eliminaciones staged de Fase 7A permanecen intactas. Los tres artefactos físicos
ignorados `ControlMarcacion.db`, `-wal` y `-shm` ya existían antes de esta fase (creados a las
08:54); esta fase no creó SQLite/WAL/SHM nuevos en el workspace.

Auditoría de autoría de esta fase:

- Nuevos de 3A: `RangoTemporal.cs`, `AsignacionPlanta.cs`,
  `PeriodoLaboralContratoTests.cs`, `PeriodoLaboralMigracionTests.cs`, los dos archivos de la
  migración y este documento.
- Actualizados de forma localizada por 3A: documento de decisiones, `PeriodoLaboral.cs`,
  `Trabajador.cs`, `ServicioTrabajadores.cs`, `MarcacionDbContext.cs`, configuración personal,
  repositorio, snapshot EF y `PeriodoLaboralAcceptanceTests.cs`.
- El repositorio ya tenía antes de 3A numerosos archivos modificados y no rastreados de fases
  anteriores. No se atribuyen a esta fase ni se revirtieron. Algunos archivos de la segunda lista
  ya estaban dirty; únicamente se añadieron los cambios aquí descritos.

## 17. Próximo paso

Antes de aplicar esta migración a cualquier copia representativa deben ejecutarse las etapas de
perfilado y saneamiento aprobadas en PL-PN04:

1. detectar periodos abiertos duplicados, solapes y fechas imposibles;
2. deduplicar automáticamente solo copias exactas bajo precondiciones verificables;
3. enviar diferencias materiales y planta histórica incierta a revisión humana;
4. construir backfill reversible de `AsignacionPlanta` con evidencia;
5. validar la migración sobre una copia representativa ya saneada;
6. recién después migrar consumidores mediante comparación dual.

La migración creada está lista para pruebas controladas, **no para producción ni para datos
ambiguos sin sanear**.

