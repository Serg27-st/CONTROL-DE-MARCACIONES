# P1-DATA-PERIODOLABORAL — Fase 3B

Fecha: 2026-10-12  
Estado: **LISTO PARA FASE 3C**  
Alcance: saneamiento, backfill y validación exclusivamente sobre copia no productiva

## 1. Objetivo

La fase determinó qué historia de `PeriodoLaboral` y planta podía corregirse sin inventar datos,
ejecutó primero una simulación, aplicó solo casos automáticos sobre una copia y validó allí la
migración `20261012151429_PeriodoLaboralCanonicoYAsignacionPlanta`.

No se tocó producción, no se ejecutó OP-03, no se modificó la fuente de cuarentena, no se
migraron consumidores, no se eliminaron campos legacy y no se hizo commit.

## 2. Base y copia utilizadas

La fuente seleccionada fue la última base local auditada y contenida en Fase 7A. Es evidencia
histórica no productiva; no se la trató como base canónica ni se abrió para escritura.

| Concepto | Valor |
|---|---|
| Origen | `C:\Users\stafur\Documents\ControlMarcacion-Private-Quarantine\phase7a-20261009\data\src\ControlMarcacion.Web\ControlMarcacion.db` |
| Tipo | Base local auditada, no productiva, en cuarentena privada |
| Tamaño origen | 3,751,936 bytes |
| Creación de copia | 2026-10-12 10:40:24 -05:00 |
| Copia inicial inmutable | `C:\Users\stafur\AppData\Local\Temp\P1-DATA-PERIODOLABORAL-3B-20261012\initial\ControlMarcacion.db` |
| Copia de trabajo | `C:\Users\stafur\AppData\Local\Temp\P1-DATA-PERIODOLABORAL-3B-20261012\working\ControlMarcacion.db` |
| Clon de health | `C:\Users\stafur\AppData\Local\Temp\P1-DATA-PERIODOLABORAL-3B-20261012\health\ControlMarcacion.db` |

También se copiaron y verificaron los auxiliares `-shm` y `-wal`; este último tenía tamaño cero.
Todas las transformaciones ocurrieron en `working`; la prueba Web se hizo en `health`.

## 3. Hashes y cadena de custodia

| Elemento | SHA-256 inicial | Resultado |
|---|---|---|
| Origen DB | `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928` | Conservado al cierre |
| Copia inicial DB | `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928` | Igual al origen |
| Copia de trabajo antes del dry-run | `642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928` | Igual al origen |
| Origen/copia `-shm` | `FD4C9FDA9CD3F9AE7C962B0DDF37232294D55580E1AA165AA06129B8549389EB` | Igualdad verificada |
| Origen/copia `-wal` | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` | Igualdad verificada; 0 bytes |

El dry-run no alteró la copia: su hash continuó siendo el inicial. El hash final esperado de la
copia transformada es
`AAE25C1DED583E921A6BB9CA51DBEC3350B7645C0D4972E283E3D99194CE1865`.

## 4. Inventario inicial

| Métrica | Resultado |
|---|---:|
| Trabajadores | 352 |
| Periodos | 913 |
| Periodos abiertos | 913 |
| Trabajadores sin periodo | 0 |
| Trabajadores con exactamente un periodo | 45 |
| Trabajadores con más de un periodo | 307 |
| Trabajadores con más de un periodo abierto | 307 |
| Grupos duplicados exactos | 307 |
| Filas dentro de grupos duplicados | 868 |
| Filas redundantes | 561 |
| Pares solapados iniciales | 900 |
| Fechas `FechaCese < FechaIngreso` | 0 |
| Huecos entre periodos | 0 |
| Trabajadores sin planta actual válida | 0 |
| Plantas actuales distintas / catálogo | 2 / 4 |
| `AsignacionPlanta` existentes | 0; la tabla aún no existía |
| Periodos que requerían backfill | 352 tras deduplicar |
| Violaciones FK / `integrity_check` | 0 / `ok` |

La consulta reproducible está en
`sql/P1_DATA_PERIODOLABORAL_FASE3B_INVENTARIO.sql`.

## 5. Reglas de saneamiento

La transformación se dividió estrictamente en `AUTO_FIX` y `MANUAL_REVIEW`.

Un grupo solo fue automático cuando:

1. coincidían trabajador, empresa, ingreso, cese, motivo, observación normalizada con
   `TRIM`/vacío y estado activo;
2. retirar una fila no cambiaba la semántica temporal;
3. no existía ninguna FK operacional hacia `periodo_laboral`;
4. cada fila redundante conservaba exactamente una auditoría textual de inserción;
5. después de elegir supervivientes no quedaba ningún solapamiento material;
6. no existía una fecha imposible.

La fila superviviente es la original según menor `creado_en`; `id` es únicamente el desempate
estable. Nunca se cerró un periodo por orden de ID.

## 6. Duplicados exactos

Los 307 grupos cumplieron todas las precondiciones. Distribución:

| Tamaño del grupo | Grupos |
|---:|---:|
| 2 | 138 |
| 3 | 84 |
| 4 | 85 |

El manifiesto exhaustivo `P1_DATA_PERIODOLABORAL_FASE3B_AUTO_FIX.csv` asigna
`SAN-001..SAN-307`, trabajador, IDs, superviviente, redundantes, regla, evidencia, acción y
confianza. Se retiraron 561 filas y se conservaron 352. Las 913 auditorías históricas de
`periodo_laboral` permanecen; son evidencia textual sin FK y 561 continúan describiendo los IDs
técnicos retirados.

## 7. Periodos abiertos múltiples

Los 307 trabajadores con múltiples abiertos pertenecían íntegramente a la clase A: duplicado
técnico inequívoco. No hubo evidencia de reingreso, cambio de empresa, fechas diferentes o
historia insuficiente. Después de `AUTO_FIX` quedan 0 trabajadores con más de un periodo abierto.

## 8. Solapamientos

Los 900 pares iniciales eran combinaciones internas de los mismos grupos exactos. Después de
proyectar un superviviente por grupo, antes de escribir, el dry-run obtuvo 0 solapamientos
materiales. La guarda transaccional habría abortado si quedaba uno. Resultado final: 0.

| Causa | Pares/casos remanentes | Acción humana |
|---|---:|---|
| Duplicado | 0 tras deduplicar | No |
| Cambio de empresa | 0 | No |
| Reingreso | 0 | No |
| Error de fecha | 0 | No |
| Desconocido | 0 | No |

## 9. Backfill histórico de planta

El algoritmo no usó `Trabajador.PlantaId` para elegir una planta. Nivel A exige una única planta
proveniente de una fuente temporal explícita que cubra todo el periodo:

- `AsignacionArea -> Area.PlantaId`: 352 periodos;
- `AsignacionMarcador -> EquipoMarcador.PlantaId`: evidencia adicional en 241;
- conflictos entre fuentes: 0;
- coincidencia posterior con el plano actual: 352/352.

| Nivel | Periodos | Tratamiento |
|---|---:|---|
| A — determinístico | 352 | Automatizado |
| B — fuerte | 0 | No aplicado |
| C — plausible | 0 | No aplicado |
| D — desconocido/conflictivo | 0 | No aplicado |

La asignación creada toma exactamente `FechaIngreso` y `FechaCese` del periodo. Así queda
contenida por construcción. El script aborta si falta evidencia A o aparece más de una planta.

## 10. Campos legacy y trabajadores sin periodo

No se modificó ningún campo plano. Comparación de los 352 trabajadores:

| Campo | COINCIDE | LEGACY DESACTUALIZADO | HISTÓRICO INCOMPLETO | AMBIGUO |
|---|---:|---:|---:|---:|
| `FechaIngreso` | 352 | 0 | 0 | 0 |
| `FechaCese` | 352 | 0 | 0 | 0 |
| `EmpresaId` | 352 | 0 | 0 | 0 |
| `PlantaId` | 352 | 0 | 0 | 0 |
| `Activo` | 352 | 0 | 0 | 0 |

No había trabajadores sin periodo; por tanto no se creó ningún vínculo desde campos planos.

## 11. Dry-run

Resultado: **OK**.

| Concepto | Resultado |
|---|---:|
| Registros inspeccionados | 913 periodos / 352 trabajadores |
| Grupos duplicados detectados | 307 |
| Grupos autocorregibles | 307 |
| Filas que se retirarían | 561 |
| Múltiples abiertos iniciales | 307 trabajadores |
| Pares solapados iniciales | 900 |
| Solapamientos materiales tras proyección | 0 |
| Asignaciones determinísticas | 352 |
| Casos humanos | 0 |

El hash antes y después del dry-run fue idéntico al origen.

## 12. Transformaciones aplicadas en copia

1. La transacción de saneamiento validó fechas, auditoría, FKs y solapamientos materiales.
2. Eliminó 561 filas redundantes exactas.
3. Conservó 352 periodos y toda la tabla de auditoría.
4. Una segunda ejecución eliminó 0 filas y no cambió el hash: idempotencia confirmada.
5. Después de la migración se insertaron 352 `AsignacionPlanta` Nivel A.
6. Una segunda ejecución del backfill insertó 0 filas: idempotencia confirmada.

Scripts:

- `sql/P1_DATA_PERIODOLABORAL_FASE3B_SANEAMIENTO.sql`;
- `sql/P1_DATA_PERIODOLABORAL_FASE3B_BACKFILL_PLANTA.sql`.

Ningún caso humano fue procesado.

## 13. Migración aplicada en copia

EF aplicó, en orden, las dos migraciones que faltaban en la copia:

1. `20261007033130_DescansoProgramadoEnRotaciones`;
2. `20261012151429_PeriodoLaboralCanonicoYAsignacionPlanta`.

La segunda creó la tabla e índices previstos, incluido
`uq_periodo_laboral_abierto` y `uq_asignacion_planta_abierta`. `dotnet ef migrations list`
mostró toda la cadena local sin marcas de pendiente. La advertencia operativa de EF sobre el
`PRAGMA foreign_keys` no transaccional pertenece a la migración ROT-DESC anterior; la aplicación
concluyó y las verificaciones posteriores fueron correctas.

## 14. Integridad post-transformación

| Invariante | Resultado |
|---|---:|
| Trabajadores | 352 |
| Periodos / abiertos | 352 / 352 |
| `AsignacionPlanta` | 352 |
| Duplicados exactos | 0 |
| Más de un abierto | 0 |
| Solapamientos | 0 |
| Fechas inválidas | 0 |
| Asignaciones fuera del periodo | 0 |
| Periodos con plantas ambiguas | 0 |
| Planta canónica distinta de plano actual | 0 |
| Divergencias legacy | 0 |
| Violaciones FK | 0 |
| `PRAGMA integrity_check` | `ok` |

## 15. Readiness de la aplicación

La aplicación se arrancó en Development contra un clon adicional de la copia final, por
loopback y sin abrir navegador. No se aplicaron migraciones nuevas durante ese arranque.

| Endpoint | HTTP | Respuesta |
|---|---:|---|
| `/vivo` | 200 | `ControlMarcaciones-OK` |
| `/estado` | 200 | `ControlMarcaciones-OK;LISTO` |

La instancia temporal fue detenida limpiamente después de la comprobación.

## 16. Regresión

```text
Build Release:
Errores:      0
Advertencias: 0

Domain:          398 / 398
Infrastructure:  441 / 441
Web:               9 / 9
TOTAL:            848 / 848
Fallidos:           0
Omitidos:           0
```

El primer build dentro del sandbox mostró el fallo espurio de MSBuild ya observado (mensaje de
error con 0 errores/0 advertencias y recorrido incompleto). La repetición fuera del aislamiento
compiló toda la solución correctamente; `vstest` también se ejecutó fuera para disponer de su
canal local.

## 17. Casos humanos

Resultado: **0**. El manifiesto específico es
`P1_DATA_PERIODOLABORAL_REVISION_HUMANA.md`. Su ausencia de filas individuales es un resultado
verificado: no se descartaron ni resolvieron automáticamente casos B–E.

## 18. Riesgos residuales

1. La copia representa la última base local auditada, no certifica por sí sola el estado actual
   de una base productiva que no fue inspeccionada ni tocada.
2. Tareo, reportes y otros consumidores siguen leyendo campos legacy hasta Fase 3C.
3. SQLite no expresa por constraint todos los solapamientos ni la contención entre tablas; las
   guardas y el dominio continúan siendo necesarias.
4. La auditoría textual conserva 561 IDs retirados. Es intencional para trazabilidad, pero debe
   interpretarse junto con el manifiesto de saneamiento.
5. Esta copia no contenía `MarcacionCruda` ni `TareoDiario`; la evidencia determinística provino
   de asignaciones temporales de área y marcador.
6. Una base diferente debe repetir dry-run y guardas; no puede reutilizarse el conteo 352 como
   supuesto.
7. No se autoriza despliegue, migración productiva ni OP-03 mediante este documento.

## 19. Gate para Fase 3C

Gate de esta copia: **APROBADO**.

- saneamiento automático reproducible: sí;
- 0 ambigüedades remanentes: sí;
- backfill Nivel A completo: sí;
- migración 3A validada: sí;
- integridad y readiness: sí;
- baseline 848/848: sí;
- casos humanos pendientes: 0.

`[LISTO PARA FASE 3C]` significa que puede iniciarse la migración controlada de consumidores y
comparación dual prevista; no significa `[LISTO PARA PRODUCCIÓN]`.

## 20. Recomendación

Avanzar a Fase 3C sobre código y copias controladas, conservando los campos planos como proyección
de compatibilidad durante la comparación dual. Antes de cualquier ventana productiva se debe
repetir este inventario sobre una copia contemporánea, exigir hash/custodia, detenerse ante un
caso humano y mantener separada la decisión de despliegue.

Las cuatro eliminaciones staged de Fase 7A permanecen exactamente iguales:

```text
D src/ControlMarcacion.Web/ControlMarcacion.db
D src/ControlMarcacion.Web/ControlMarcacion.db-shm
D src/ControlMarcacion.Web/ControlMarcacion.db-wal
D src/ControlMarcacion.Web/ControlMarcacion_pre_limpieza_20260826_085933.db.bak
```

