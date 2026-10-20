# P1-DATA-PERIODOLABORAL — Fase 3D

## Consolidación de invariantes y escrituras futuras

Estado: **COMPLETADA**  
Fecha de cierre: 2026-10-13  
Alcance: dominio, servicio de Personal, repositorio de trabajador y pruebas dirigidas.  
Fuera de alcance: producción, OP-03, migraciones productivas, reportes, tareo, eliminación de campos legacy y Fase 3E.

## Baseline

La medición inicial se ejecutó una sola vez, antes de modificar esta fase:

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Tests Domain | 405/405 |
| Tests Infrastructure | 472/472 |
| Tests Web | 23/23 |
| Total | 900/900 verdes, 0 fallos, 0 omitidos |

## Inventario de escrituras

| Operación | Archivo / método | ¿Escribe `PeriodoLaboral`? | Riesgo observado | Invariante previa | Gap encontrado |
|---|---|---:|---|---|---|
| Alta | `ServicioTrabajadores.CrearAsync` → `Trabajador.AbrirVinculo` | Sí, crea el primer periodo y su primera planta | Alta sin episodio o con episodio incompleto | Apertura dentro del agregado | Faltaba validar toda la línea temporal antes de persistir |
| Cese | `ServicioTrabajadores.DarDeBajaAsync` → `Trabajador.DarDeBaja` | Sí, cierra periodo y planta abiertos | Elegir arbitrariamente un periodo o continuar sin uno abierto | Rechazo de segundo cese y validación de fecha | `FirstOrDefault` no distinguía cero de múltiples periodos abiertos |
| Reingreso | `Trabajador.Reingresar` | Sí, crea un episodio nuevo | Solapamiento o reapertura del episodio anterior | Rechazo si ya existe un periodo abierto | Existe primitiva de dominio, pero todavía no hay flujo de aplicación/UI dedicado |
| Cambio de empresa | `ServicioTrabajadores.ActualizarAsync` → `Trabajador.CambiarEmpresa` | No durante vínculo activo | Reescritura de historia empresarial | Se rechaza cambiar empresa a un trabajador activo | El cambio efectivo requiere cese y reingreso; no existe aún servicio dedicado de reingreso |
| Cambio de planta | `ServicioTrabajadores.ActualizarAsync` → `Trabajador.CambiarPlanta` → `PeriodoLaboral.CambiarPlanta` | Sí, en `AsignacionPlanta`; no crea otro periodo | Solapamiento o discontinuidad de asignaciones | Validador común de vigencias | Faltaba validar integralmente contención, continuidad y extremos del periodo |
| Edición actual | `ServicioTrabajadores.ActualizarAsync` → `SincronizarVinculoAbierto` | Sí, puede corregir inicio/empresa del abierto | Solapar historia cerrada o reescribirla al guardar el grafo | Sincronización del periodo abierto | El repositorio cargaba solo el abierto y `Update(graph)` marcaba también historia cargada |
| Persistencia | `TrabajadorRepository.ObtenerPorIdAsync`, `Actualizar`, `GuardarCambiosAsync` | Persiste el agregado | Validación incompleta y actualizaciones accidentales | Un `SaveChanges` por operación | Faltaba cargar la historia completa y limitar `Modified` a la raíz seguida |

### Respuestas funcionales confirmadas

- `CambioEmpresa` **no** genera por sí mismo un `PeriodoLaboral`. En un trabajador activo se rechaza. El siguiente episodio se crea mediante reingreso después del cese.
- `CambioPlanta` **no** genera un nuevo `PeriodoLaboral`. Crea/cierra `AsignacionPlanta` dentro del periodo vigente.
- Los gaps entre un periodo cerrado y el siguiente están permitidos y no se rellenan.
- `Trabajador.FechaIngreso`, `FechaCese`, `EmpresaId` y `PlantaId` representan exclusivamente la proyección actual compatible con la UI existente.

## Invariantes y capas de protección

| Invariante | Dominio | Servicio | EF | SQLite | Gap residual |
|---|---|---|---|---|---|
| PL-INV-01: máximo un periodo abierto | `ValidarPeriodosLaborales`, `Reingresar` y rechazo explícito en cese | Valida antes de guardar | Índice único filtrado modelado | `UQ_periodo_laboral_abierto` | Ninguno en flujos normales; SQLite es la última barrera concurrente |
| PL-INV-02: periodos sin solapamiento | `ValidadorVigencias`/`RangoTemporal` y validación del agregado completo | Carga y valida toda la historia | Sin constraint general | Sin constraint de solapamiento | SQL directo podría insertar periodos cerrados solapados; riesgo documentado |
| PL-INV-03: fin no anterior al inicio | `PeriodoLaboral.Validar` y validadores de vigencia | Valida antes de guardar | `AsignacionPlanta` tiene CHECK | CHECK para planta; no para el periodo | SQL directo podría invertir fechas del periodo; candidato futuro, no necesario para esta fase |
| PL-INV-04: trabajador, empresa y planta válidos | IDs positivos y periodo con planta válida/contained | Catálogos y planta asignable | Relaciones y FKs | FKs físicas | La identidad nueva del trabajador se completa al persistir el agregado; la FK es la autoridad física |
| PL-INV-05: reingreso crea episodio nuevo | `Reingresar` construye y agrega un periodo nuevo | Aún no existe caso de uso dedicado | Inserción de entidad nueva | FK e índice de abierto | Falta exponer el flujo de aplicación/UI si el negocio lo solicita |
| PL-INV-06: cese cierra exactamente el abierto | `DarDeBaja` exige exactamente uno y lo cierra | Valida agregado antes de guardar | Tracking del hijo modificado | Escritura atómica en `SaveChanges` | Ninguno en flujo normal |
| PL-INV-07: historia cerrada no se reescribe | Operaciones actúan sobre el abierto/vigente | Repositorio carga historia solo para validar | Solo la raíz se marca `Modified`; EF detecta hijos realmente cambiados | Una transacción por `SaveChanges` | Corrección histórica explícita futura requerirá un caso de uso separado y auditado |

## Gaps encontrados y tratamiento

Se encontraron cuatro gaps relevantes:

1. El cese aceptaba una proyección activa sin periodo abierto y, ante corrupción con varios abiertos, podía elegir el primero. Se corrigió con cardinalidad exacta y errores explícitos.
2. La edición cargaba únicamente el periodo abierto. Una corrección de `FechaIngreso` podía solaparlo con un periodo cerrado invisible para el agregado. Se corrigió cargando la historia completa.
3. Marcar el grafo completo con `Update` podía reescribir periodos cerrados cargados solo para validar. Se corrigió marcando como modificada únicamente la raíz seguida; EF conserva el seguimiento diferencial de hijos realmente alterados.
4. No hay caso de uso de aplicación/UI para reingreso, aunque la primitiva de dominio existe y queda cubierta sobre SQLite. Se documenta para una fase posterior; no bloquea la integridad de los flujos hoy expuestos.

Riesgos restantes:

- `PeriodoLaboral` no tiene `RowVersion` ni otro token de concurrencia. El índice único filtrado resuelve la carrera de dos periodos abiertos, pero SQLite no impide por sí solo todos los solapamientos entre periodos cerrados.
- SQLite no tiene hoy un CHECK propio para `PeriodoLaboral.FechaCese >= FechaIngreso`. Dominio y servicio lo impiden en flujos normales; una escritura SQL directa puede eludirlo.
- No se añadió un constraint complejo de solapamiento: SQLite no ofrece una exclusión temporal limpia y la arquitectura ya centraliza esta regla en el agregado más una única transacción.

## Cambios realizados

- `PeriodoLaboral.Validar` concentra rango, IDs, existencia de planta, contención, ausencia de solapamientos, continuidad y coherencia de extremos.
- `PeriodoLaboral.CorregirFechaIngreso` modifica coordinadamente el inicio del periodo abierto y el inicio de su primera asignación de planta.
- `Trabajador.ValidarPeriodosLaborales` valida cada periodo y la ausencia de solapamiento entre episodios.
- `Trabajador.DarDeBaja` exige exactamente un periodo abierto y deja de seleccionar periodos de forma arbitraria.
- Alta, edición y cese validan el agregado completo antes de guardar.
- La sincronización del periodo abierto rechaza cardinalidad corrupta.
- El repositorio carga todos los periodos con sus plantas para que las escrituras puedan validar contra historia cerrada.
- La actualización del repositorio no marca como modificada toda la historia cargada.
- No se creó migración: la protección necesaria para flujos normales queda en dominio/servicio, el índice físico existente conserva la unicidad del abierto y las FKs preservan referencias.

## Transaccionalidad

Las operaciones compuestas se materializan en un único `SaveChangesAsync`, que EF Core ejecuta atómicamente. El contrato `PL3D_WRITE_04` provoca una violación real de FK después de cerrar un periodo y preparar el reingreso; una lectura posterior confirma que el periodo original sigue abierto, no existe el nuevo y la proyección actual tampoco cambió.

## Campos legacy en escrituras

| Campo | Clasificación | Contrato |
|---|---|---|
| `Trabajador.FechaIngreso` | `PROYECCION_ACTUAL`, `COMPATIBILIDAD`, `CANDIDATO_ELIMINACION_FUTURA` | Alta y reingreso reflejan el inicio del episodio actual; no reconstruye historia |
| `Trabajador.FechaCese` | `PROYECCION_ACTUAL`, `COMPATIBILIDAD`, `CANDIDATO_ELIMINACION_FUTURA` | Cese refleja el cierre actual; reingreso vuelve a `null` |
| `Trabajador.EmpresaId` | `PROYECCION_ACTUAL`, `COMPATIBILIDAD`, `CANDIDATO_ELIMINACION_FUTURA` | Reingreso refleja la empresa del nuevo periodo; la historia se consulta en `PeriodoLaboral` |
| `Trabajador.PlantaId` | `PROYECCION_ACTUAL`, `COMPATIBILIDAD`, `CANDIDATO_ELIMINACION_FUTURA` | Refleja la planta vigente; la historia se consulta en `AsignacionPlanta` |

No se eliminó ni redefinió ninguna propiedad o columna legacy.

## Evidencia ejecutable

Se añadieron 14 contratos:

- 10 `PL3D_INV_*` de dominio: segundo abierto, solapamiento, contigüidad, gap, rango inválido, IDs inválidos, cese sin abierto, cese con múltiples abiertos, reingreso sin alterar historia y sincronización de proyección actual.
- 4 `PL3D_WRITE_*` sobre SQLite real en memoria: secuencia alta–cese–reingreso, edición solapada rechazada, historia cerrada no reescrita y rollback por fallo técnico.

La base temporal ejecuta además:

```text
PRAGMA integrity_check;     → ok
PRAGMA foreign_key_check;   → 0 filas
```

Validación integral final:

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Tests Domain | 415/415 |
| Tests Infrastructure | 476/476 |
| Tests Web | 23/23 |
| Total | 914/914 verdes, 0 fallos, 0 omitidos |

## Conclusión

Los flujos normales ya no pueden regenerar múltiples periodos abiertos, solapamientos por edición, cierres ambiguos ni reescrituras accidentales del historial cerrado. El reingreso crea un episodio independiente y las escrituras compuestas son atómicas.

La Fase 3D queda **COMPLETADA** y el único estado siguiente permitido es **LISTO PARA FASE 3E**. Esto no significa listo para producción.
