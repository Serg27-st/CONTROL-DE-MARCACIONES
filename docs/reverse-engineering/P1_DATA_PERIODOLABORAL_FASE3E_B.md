# P1-DATA-PERIODOLABORAL — Fase 3E-B

## Corrección de usos históricos incorrectos detectados en 3E-A

Estado: **COMPLETADA**  
Fecha: 2026-10-14  
Alcance: exclusivamente los cuatro consumidores clasificados como
`USO_HISTORICO_INCORRECTO` en 3E-A.

## 1. Baseline inicial

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 415/415 |
| Infrastructure | 476/476 |
| Web | 23/23 |
| Total | 914/914 verdes; 0 fallos; 0 omitidos |

## 2. Inventario mínimo

| Consumidor | Archivo | Clase/método | DTO/salida | Tests previos relacionados |
|---|---|---|---|---|
| Asignación fechada de cuadrilla | `Application/Personal/ServicioTrabajadores.cs` | `ServicioTrabajadores.AsignarCuadrillaAsync` | `AsignarCuadrillaRequest` | `CambioDeTurnoTests` |
| Integrantes históricos | `Infrastructure/Persistencia/Consultas/ConsultaTrabajadores.cs` | `IntegrantesDeCuadrillaAsync` | `IntegranteCuadrillaDto` | `AislamientoPorPlantaTests` |
| Candidatos históricos | mismo | `CandidatosParaCuadrillaAsync` | `CandidatoCuadrillaDto` | filtros y rotaciones de Personal |
| Planta de jornada PMV | `Infrastructure/MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs` | `ConsultarAsync` | `JornadaProvisionalDto.Planta` | `ConsultaMarcacionProvisionalTests`, aislamiento PMV |

Dependencia compartida reutilizada:

- `ConsultaContextoLaboral.CargarRangoAsync` precarga `PeriodoLaboral` y `AsignacionPlanta`.
- `HistoriaLaboralTrabajador.Resolver` resuelve una fecha sin fallback legacy.
- `ResultadoResolucionContextoLaboral.ObtenerOError` conserva el error explícito ante historia
  ambigua.

No se creó un segundo resolvedor temporal. `ContextosLaboralesEnFechaAsync` es solo una fachada
de aplicación en lote sobre el resolvedor de 3C, necesaria porque `ServicioTrabajadores` no puede
depender directamente de Infrastructure.

## 3. Cambios por consumidor

### 3.1 `AsignarCuadrillaAsync`

Antes:

- comprobaba acceso y planta mediante `Trabajador.PlantaId` actual;
- interpretaba `FechaCese` actual como ausencia de vínculo;
- comparaba la fecha efectiva contra `Trabajador.FechaIngreso`;
- la mutación buscaba únicamente la asignación de turno abierta actual.

Ahora:

1. reúne todos los trabajadores solicitados;
2. precarga en una sola consulta su contexto laboral para la fecha efectiva;
3. exige un `PeriodoLaboral` aplicable y una `AsignacionPlanta` aplicable;
4. aplica el alcance contra esa planta histórica;
5. valida la cuadrilla contra esa planta;
6. modifica únicamente el tramo de turno/cuadrilla que contiene la fecha efectiva.

La escritura histórica conserva el final del tramo reemplazado o el cese del vínculo. Un reingreso
posterior y su asignación abierta permanecen intactos. El rango afectado sigue pasando por la
protección de periodos de planilla cerrados.

Contratos: `PL3EB_CUA_01` a `PL3EB_CUA_04`.

### 3.2 `IntegrantesDeCuadrillaAsync`

Antes filtraba el alcance con `a.Trabajador.PlantaId`, aunque la consulta recibía una fecha.

Ahora:

- obtiene los integrantes vigentes en esa fecha;
- precarga sus historias en lote;
- conserva solo quienes tienen vínculo y planta canónicos en la fecha;
- aplica `PlantasVisibles` contra esa planta histórica.

Contratos: `PL3EB_INT_01` y `PL3EB_INT_02`.

### 3.3 `CandidatosParaCuadrillaAsync`

Antes seleccionaba por `Trabajador.PlantaId` y `Trabajador.FechaCese == null`.

Ahora la población sale de las historias que contienen la fecha solicitada. Un candidato debe:

- tener periodo laboral en la fecha;
- tener la planta solicitada vigente en esa fecha;
- no integrar ya la cuadrilla en esa fecha.

Un cesado hoy puede ser candidato en una fecha donde trabajaba; una persona activa hoy no aparece
antes de su ingreso; un traslado posterior no reescribe su planta pasada.

Contratos: `PL3EB_CAN_01` a `PL3EB_CAN_03`.

### 3.4 PMV histórico

PMV sigue provisional, aislado y sin escritura en tareo oficial. Solo cambió la fuente de la
columna `Planta`.

Fecha de referencia exacta:

```text
DateOnly.FromDateTime(MarcacionProvisional.FechaHora)
```

La consulta agrupa por ese día funcional y por ID de marcador. Para todos los trabajadores
atribuidos en el rango se precarga una vez `PeriodoLaboral`/`AsignacionPlanta`; cada jornada resuelve
la planta con su propia fecha.

Si falta contexto histórico:

- conserva la atribución de identidad ya resuelta por `AsignacionMarcador`;
- deja `JornadaProvisionalDto.Planta = null`;
- agrega la observación `Sin contexto laboral histórico para esta fecha`;
- nunca usa `Trabajador.PlantaId` como fallback.

Contratos: `PL3EB_PMV_01` y `PL3EB_PMV_02`.

## 4. Eficiencia y N+1

| Ruta | Carga histórica |
|---|---|
| Asignación masiva | una carga para todos los ids de la solicitud |
| Integrantes | una carga para todos los ids encontrados |
| Candidatos | una carga del único día y una consulta de fichas candidatas |
| PMV | una carga para todos los trabajadores atribuidos y todo el rango |

No se añadió ninguna consulta `PeriodoLaboral`/`AsignacionPlanta` dentro de bucles por trabajador o
por marcación. Las resoluciones diarias ocurren en memoria sobre historias precargadas.

## 5. Contratos añadidos

| Familia | Cantidad | Cobertura |
|---|---:|---|
| `PL3EB_CUA_*` | 4 | planta histórica, preingreso, postcese y reingreso posterior |
| `PL3EB_INT_*` | 2 | alcance antes y después de un traslado |
| `PL3EB_CAN_*` | 3 | cesado histórico, preingreso y traslado posterior |
| `PL3EB_PMV_*` | 2 | planta histórica y ausencia de fallback actual |
| **Total** | **11** | cuatro consumidores corregidos |

Ejecución dirigida durante implementación:

- `CambioDeTurnoTests`: 25/25 verdes.
- cuadrillas históricas + consulta PMV: 20/20 verdes.
- comprobación adicional de aislamiento: 29/29 verdes antes del ajuste final de escritura; el
  ajuste no modificó consultas ni política de alcance.

## 6. Archivos modificados

- `Application/Personal/Dto/CuadrillaPersonalDtos.cs`.
- `Application/Personal/IConsultaTrabajadores.cs`.
- `Application/Personal/ServicioTrabajadores.cs`.
- `Infrastructure/Persistencia/Consultas/ConsultaTrabajadores.cs`.
- `Infrastructure/MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs`.
- tests dirigidos de dominio, consultas históricas y PMV.

No se modificaron reportes generales, tareo, `CalculoTareo`, marcaciones oficiales, seguridad
global, esquema SQLite ni migraciones.

## 7. Resultado

| Criterio | Estado |
|---|---|
| Cuadrillas usan vínculo/planta de la fecha efectiva | CUMPLIDO |
| PMV usa la planta del día de la marcación | CUMPLIDO |
| Fallback silencioso a `Trabajador.PlantaId` | ELIMINADO en los cuatro consumidores |
| N+1 histórico nuevo | NO |
| Campos legacy eliminados | NO; permanecen para estado actual/compatibilidad |
| Flujo Application/UI de reingreso | NO IMPLEMENTADO; fuera de alcance |
| Producción | NO TOCADA |
| Migración productiva | NO |
| OP-03 | NO |
| Commit | NO |

## 8. Validación integral final

| Verificación | Resultado |
|---|---:|
| Build Release | 0 errores, 0 warnings |
| Domain | 419/419 |
| Infrastructure | 483/483 |
| Web | 23/23 |
| **Total** | **925/925 verdes; 0 fallos; 0 omitidos** |

El runner de pruebas no pudo establecer el canal con `testhost` dentro del sandbox de la sesión
porque el runtime recibió acceso denegado a servicios de Windows. La suite integral se repitió sin
esa restricción, sobre los mismos binarios Release y sin cambios de código, y finalizó correctamente.

## 9. Conclusión

Los cuatro usos históricos incorrectos inventariados en 3E-A fueron corregidos sin ampliar la
política global de seguridad ni introducir una segunda lógica de resolución temporal.

Estado siguiente permitido: **LISTO PARA FASE 3E-C**. Esto no autoriza ejecutar 3E-C ni implica
preparación para producción.
