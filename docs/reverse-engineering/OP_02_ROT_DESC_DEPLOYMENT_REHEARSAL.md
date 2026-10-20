# OP-02 — Ensayo de despliegue de ROT-DESC sobre copia

Fecha: 2026-10-07  
Resultado: **NO VALIDADO para despliegue productivo**  
Base canónica modificada: **NO**

## 1. Alcance y decisión

Se ensayó la migración `20261007033130_DescansoProgramadoEnRotaciones` únicamente sobre
copias de `src/ControlMarcacion.Web/ControlMarcacion.db`. La migración, los `CHECK`, el
backfill, el cálculo funcional, el recálculo acotado y el `Down` funcionaron. La base
canónica conservó exactamente su tamaño, fecha de modificación y SHA-256.

OP-02 no se declara validado para producción por dos evidencias que el criterio de salida
exige y que esta copia no permitió completar:

1. la base canónica contiene cero programaciones y cero tramos de rotación reales; por
   tanto, el backfill y la no regresión de una rotación previa solo pudieron probarse con
   un fixture controlado creado en la copia antes de migrar;
2. la instancia Web arrancó contra la copia y el código de la vista contiene el
   comportamiento esperado, pero los navegadores disponibles para automatización no
   pudieron alcanzar `localhost`; no hubo una comprobación UI interactiva completa.

Además, EF Core advierte que `PRAGMA foreign_keys = 0` de la migración no puede ejecutarse
dentro de una transacción. Una terminación en ese punto puede dejar el esquema parcialmente
aplicado. Esta advertencia no produjo corrupción en el ensayo, pero exige parada de
escrituras, respaldo recuperable y verificación inmediata en el despliegue futuro.

## 2. Base canónica y copias

| Dato | Valor |
|---|---|
| Base canónica | `src/ControlMarcacion.Web/ControlMarcacion.db` |
| Captura inicial | 2026-10-07 09:35:46 -05:00 |
| Tamaño inicial/final | 3,739,648 bytes |
| Última modificación inicial/final (UTC) | 2026-10-06T19:55:45.7309247Z |
| SHA-256 inicial | `1B6E95CB2EDE734A7638E22D33DE6D0CB4DEA82230D372F0A6257421C3361FD1` |
| SHA-256 final | `1B6E95CB2EDE734A7638E22D33DE6D0CB4DEA82230D372F0A6257421C3361FD1` |
| Sidecars iniciales/finales | no existían `.db-wal` ni `.db-shm` |
| Copia piloto | `artifacts/op02-rot-desc-20261007/ControlMarcacion_ROT_DESC_PILOT.db` |
| SHA-256 inicial de la copia | `1B6E95CB2EDE734A7638E22D33DE6D0CB4DEA82230D372F0A6257421C3361FD1` |
| Copia secundaria para Down | `artifacts/op02-rot-desc-20261007/ControlMarcacion_ROT_DESC_DOWN.db` |

Toda escritura del ensayo se dirigió a las dos copias anteriores.

## 3. Inventario previo

La copia idéntica inicial tenía:

| Conteo | Resultado |
|---|---:|
| `tramo_rotacion` | 0 |
| tramos con turno | 0 |
| `programacion_cuadrilla` | 0 |
| cuadrillas | 0 |
| asignaciones de cuadrilla | 0 |
| migraciones aplicadas | 14 |
| migración ROT-DESC aplicada | 0 |
| `PRAGMA integrity_check` | `ok` |
| `PRAGMA foreign_key_check` | 0 filas |

El esquema previo tenía `turno_id NOT NULL`, sin columna `tipo`, las FK a
`programacion_cuadrilla` y `turno`, el índice no único por `turno_id` y el índice único
por `(programacion_id, orden)`. No tenía los cuatro `CHECK` de ROT-DESC.

Como no existía una rotación real que permitiera verificar el backfill, se creó en la
copia, antes de migrar, una programación controlada y finita:

- tramo 1: DIU, dos días;
- tramo 2: NOC, dos días.

El snapshot lógico previo contiene dos filas y se conservó en
`pre_migration_with_fixture_tramos.csv`. Este fixture no sustituye una validación sobre
datos reales representativos; únicamente hace observable la transformación.

## 4. Aplicación de la migración

Comando ejecutado:

```powershell
dotnet ef database update 20261007033130_DescansoProgramadoEnRotaciones `
  --connection "Data Source=<copia piloto>" `
  --project src/ControlMarcacion.Infrastructure `
  --startup-project src/ControlMarcacion.Infrastructure `
  --no-build
```

Resultado: salida 0, `Done`, duración aproximada 6,438 ms.

Advertencia operativa emitida por EF Core:

```text
PRAGMA foreign_keys = 0 no puede ejecutarse en una transacción; una terminación en ese
punto puede dejar la migración parcialmente aplicada.
```

La advertencia también apareció al ejecutar el `Down` y debe formar parte del runbook.

## 5. Resultado posterior y backfill

- la migración aparece exactamente una vez en `__EFMigrationsHistory`;
- `integrity_check = ok`;
- `foreign_key_check = 0 filas`;
- `tipo` existe y es `NOT NULL`;
- `turno_id` es nullable;
- se preservaron ambas FK y ambos índices;
- existen `CK_tramo_rotacion_tipo_turno`, `CK_tramo_rotacion_descanso_un_dia`,
  `CK_tramo_rotacion_dias_positivos` y `CK_tramo_rotacion_orden_positivo`;
- las dos filas previas quedaron `Trabajo`; ninguna quedó `Descanso`;
- `Id`, `ProgramacionId`, `Orden`, `TurnoId` y `Dias` coinciden byte a byte en los
  snapshots previo y posterior;
- `FechaAncla`, `FechaInicio`, `FechaFin` y `CuadrillaId` no cambiaron.

SHA-256 de ambos snapshots CSV:
`9BC53EDB876C27E5E881042D513DD928DC69EAD7323C5E4C742320190E625F46`.

## 6. Restricciones físicas

Las inserciones se ejecutaron dentro de transacciones descartadas:

| Caso | Resultado |
|---|---|
| `Descanso`, turno nulo, un día, orden positivo | aceptado |
| `Descanso` con turno | rechazado por `CK_tramo_rotacion_tipo_turno` |
| `Descanso` de dos días | rechazado por `CK_tramo_rotacion_descanso_un_dia` |
| `Trabajo` sin turno | rechazado por `CK_tramo_rotacion_tipo_turno` |

## 7. Ensayo funcional y recálculo

El piloto se ejecutó con servicios reales del proyecto y SQLite contra la copia. Se usó
la planta 2, que no tenía trabajadores en la copia inicial, y ocho trabajadores de prueba.
El archivo `functional_results.json` conserva los resultados detallados.

Rotación previa controlada: DIU, DIU, NOC, NOC, sin cambios después de migrar.

Nueva rotación: DIU × 2, Descanso × 1, NOC × 2:

| Día | Resolución |
|---|---|
| D1 | Trabajo DIU |
| D2 | Trabajo DIU |
| D3 | DescansoProgramado |
| D4 | Trabajo NOC |
| D5 | Trabajo NOC |
| D6 | SinProgramacion |

Resultados BD-01:

- sin marcas: `Descanso`, cero ordinarias/HE/tardanza/salida anticipada, sin validación;
- 07:00–17:00: 600 minutos físicos, cero ordinarias/HE25/HE35, clasificados íntegramente
  como HE100 por la capa económica;
- una marca 07:00: `Incompleto`, cero minutos y `RequiereValidacion = true`;
- salida 05:22 de NOC anterior: no fue reutilizada por el descanso;
- 20:00–02:00 D+1: 360 minutos físicos, sin duplicación.

Superposiciones:

- domingo + descanso + 07:00–17:00: 600 minutos únicos, una sola clasificación HE100;
- feriado ordinario + descanso: 600 minutos únicos, `FeriadoTrabajado`, sin segunda base
  de descanso semanal;
- 1 de mayo + descanso: 600 minutos únicos y clasificación estructural especial; la
  liquidación se bloqueó con el mensaje esperado porque su composición económica todavía
  no está modelada.

Vigencia y recálculo:

- antes del cambio: tareo vigente;
- después de cambiar programación: desactualizado por cambio de programación de cuadrilla;
- después de recalcular: vigente;
- filas del periodo piloto de septiembre: 0 → 240;
- segunda ejecución: 240 → 240, con 0 creadas y 240 actualizadas;
- trabajadores procesados: 8; errores: 0;
- el reporte mostró `DESCANSO PROGRAMADO`, no `SIN TURNO`.

## 8. UI

La aplicación Web compiló sin errores y arrancó con la cadena de conexión dirigida a la
copia piloto. En el arranque, EF confirmó que la base estaba actualizada y no aplicó otra
migración. El entorno aislado produjo una advertencia de permisos al escribir la llave de
Data Protection, sin detener el proceso.

La vista `Views/Cuadrillas/Programar.cshtml` fue inspeccionada y contiene:

- Trabajo: selector de turno habilitado y días editables;
- Descanso: selector de turno deshabilitado, valor de turno limpiado, `Dias = 1` y campo
  de días de solo lectura.

No se contabiliza esto como smoke UI interactivo: el navegador interno agotó el tiempo de
conexión a `127.0.0.1:5199` y Chrome/Edge no estaban disponibles para la herramienta de
automatización. La comprobación UI sobre una instancia conectada a copia queda pendiente.

## 9. Down y reversión

En la copia secundaria, que contenía únicamente los dos tramos `Trabajo`, se ejecutó:

1. `Up` a ROT-DESC: salida 0, 6,014 ms;
2. `Down` a `20261001212252_MarcacionesProvisionales`: salida 0, 6,205 ms.

Después del `Down`:

- la migración ROT-DESC no aparece en history;
- `tipo` ya no existe;
- `turno_id` vuelve a ser `NOT NULL`;
- las dos filas y sus turnos permanecen;
- `integrity_check = ok` y `foreign_key_check = 0 filas`.

El `Down` no debe usarse si ya existen tramos `Descanso`, porque el esquema anterior no
puede representarlos. El rollback productivo recomendado es restaurar el backup completo.

## 10. Procedimiento productivo futuro

No ejecutado en esta intervención:

1. detener todas las escrituras de la aplicación;
2. copiar `.db`, `.db-wal` y `.db-shm` si existen, manteniendo el conjunto coherente;
3. calcular y registrar SHA-256, tamaño y fecha;
4. verificar que el respaldo se puede abrir y restaurar;
5. aplicar la migración con el mecanismo EF usado en este ensayo;
6. no interrumpir el proceso durante el tramo no transaccional advertido por EF;
7. comprobar migration history, `integrity_check`, `foreign_key_check`, esquema y backfill;
8. iniciar la aplicación y ejecutar smoke UI autenticado;
9. recalcular solo periodos abiertos y plantas afectadas;
10. ante cualquier inconsistencia, detener escrituras y restaurar el backup.

Antes de programar el despliegue hace falta repetir OP-02 con una copia que tenga
rotaciones reales representativas y completar el smoke UI.

## 11. Suite y evidencias

Suite final del producto:

- Domain: 368/368;
- Infrastructure: 435/435;
- total: **803/803**, 0 fallos, 0 omitidos.

Evidencias conservadas en `artifacts/op02-rot-desc-20261007`:

- `pre_migration.json` y `pre_migration_tramos.csv`;
- `pre_migration_with_fixture.json` y su snapshot CSV;
- `post_migration.json` y su snapshot CSV;
- `final_pilot.json` y `functional_results.json`;
- `down_after.json` y su snapshot CSV;
- scripts de inventario, fixture y comprobación de constraints;
- copias piloto y secundaria.

## 12. Estado

La migración **no fue aplicada a producción**. La base original está intacta.

**OP-02: NO VALIDADO.** Los resultados técnicos sobre copia son conformes, pero faltan la
no regresión sobre rotaciones reales representativas y la comprobación UI interactiva. Por
ello, **ROT-DESC no está autorizado todavía para despliegue productivo**.
