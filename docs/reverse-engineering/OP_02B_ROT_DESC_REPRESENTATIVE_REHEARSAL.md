# OP-02B — Ensayo representativo de ROT-DESC

Fecha: 2026-10-07  
Resultado: **VALIDADO**  
Evidencia histórica: **CONTROLADA REPRESENTATIVA**  
Base canónica local: **RECUPERADA LÓGICAMENTE; SHA FÍSICO CAMBIÓ DURANTE R-WEB**  
Migración productiva: **NO PERSISTENTE; OP-03 NO EJECUTADO**

## 1. Alcance y decisión

OP-02B cerró dos de los tres bloqueantes operativos de OP-02:

- migró sin regresión un dataset pre-migración representativo, con varias cuadrillas,
  secuencias, turnos y vigencias;
- restauró realmente un backup sobre una base marcada como no confiable y demostró
  identidad SHA-256, integridad y recuperación del estado previo;
- ejecutó el smoke Web autenticado contra la copia migrada, pero este **no pasó**.

La migración y el restore son conformes. Este documento registró inicialmente dos fallos
reproducibles del flujo autenticado. La intervención posterior R-WEB-ROT-DESC corrigió ambos,
añadió cobertura MVC real y repitió UI-ROT-01..07 con resultado verde. Por el criterio
conjunto de salida, OP-02B queda **VALIDADO**. OP-03 puede prepararse, pero no fue ejecutado.

No se cambió código funcional ni reglas de negocio durante este ensayo.

## 2. Inventario de bases disponibles

Se inventariaron 15 archivos `.db`, `.sqlite`, `.sqlite3` o copias candidatas dentro del
repositorio y sus artefactos. Para cada uno se registraron ruta, tamaño, SHA-256, fecha,
`integrity_check`, `foreign_key_check`, historial de migraciones y los conteos solicitados.
El detalle está en:

- `artifacts/op02b-rot-desc-20261007/database_inventory.csv`;
- `artifacts/op02b-rot-desc-20261007/database_inventory.json`.

Resultado de la búsqueda:

- los backups más antiguos preceden a `RotacionPorCuadrilla` y no tienen las tablas o
  columnas necesarias;
- la base canónica y `ControlMarcacion_backup_antes_pmv.db` tienen cero programaciones y
  cero tramos;
- las únicas copias con programaciones pertenecen a OP-02 y son fixtures controlados;
- ninguna base disponible contiene rotaciones históricas reales representativas.

No se presenta ninguna de estas copias como evidencia productiva real.

## 3. Fuente controlada representativa

Se creó `ControlMarcacion_OP02B_SOURCE.db` como copia exacta de la base canónica y se cargó,
antes de la migración, un dataset mínimo basado exclusivamente en el esquema real:

| Programa | Secuencia | Vigencia |
|---|---|---|
| A | DIU ×14 → NOC ×7 | cerrada |
| B | DIU ×10 → NOC ×5 | cerrada |
| C | DIU ×5 → NOC ×5 | cerrada |
| D | DIU ×10 | cerrada |
| E-1 | DIU ×5 → NOC ×5 | histórica cerrada |
| E-2 | NOC ×5 → DIU ×5 | histórica cerrada |
| E-3 | DIU ×5 → NOC ×5 | futura |

Conteos pre-migración:

| Dato | Resultado |
|---|---:|
| Programaciones | 7 |
| Tramos | 13 |
| Cuadrillas | 5 |
| Asignaciones relacionadas | 5 |
| Marcaciones controladas | 2 |
| Tareo relacionado | 1 |

Todos los tramos eran `Trabajo` por construcción y tenían `TurnoId` obligatorio. La fuente,
el backup previo y el archivo restaurado comparten el SHA-256:

`3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B`.

Esta evidencia se clasifica como **CONTROLADA REPRESENTATIVA**, no como real histórica.

## 4. Snapshot y resolución pre-migración

El snapshot determinista preservó `ProgramacionId`, `CuadrillaId`, `FechaAncla`,
`FechaInicio`, `FechaFin`, `TramoId`, `Orden`, `TurnoId` y `Dias`.

- archivo: `pre_migration_snapshot.csv`;
- SHA-256: `2FE173142900D3ED716C99520EAC2B4CE95D85C7498C98BE1CB7F69872BEC48B`;
- `integrity_check`: `ok`;
- `foreign_key_check`: 0 filas;
- resoluciones baseline: 34.

Para cada programación se resolvieron inicio, interior, último día del primer tramo, primer
día del tramo siguiente cuando existía y último día de secuencia. Esto incluyó las vigencias
cerradas y la futura de la cuadrilla E.

## 5. Migración de la copia

Solo se migró:

`artifacts/op02b-rot-desc-20261007/ControlMarcacion_OP02B_SOURCE_ROT_DESC_OP02B.db`.

La ejecución de
`20261007033130_DescansoProgramadoEnRotaciones` terminó con salida 0 en 5,752 ms. EF emitió
textualmente la advertencia:

```text
The migration operation 'PRAGMA foreign_keys = 0;' cannot be executed in a transaction.
If the app is terminated or the process fails while this operation is being executed then
the migration will be left in a partially applied state and would need to be reverted
manually before it can be applied again.
```

Clasificación: **riesgo operativo conocido**. Obliga a detener escrituras, disponer de un
backup restaurable y validar inmediatamente después; el `Down` no es la única salvaguarda.

## 6. Resultado post-migración

- `integrity_check = ok`;
- `foreign_key_check = 0 filas`;
- la migración objetivo aparece exactamente una vez;
- los 13 tramos previos quedaron `Tipo = Trabajo`;
- cero tramos previos quedaron `Descanso`;
- `Id`, `ProgramacionId`, `Orden`, `TurnoId`, `Dias` y los campos de vigencia permanecieron
  idénticos;
- el snapshot posterior tiene el mismo SHA-256 del previo.

Las 34 resoluciones pre/post coincidieron y no hubo diferencias. Una programación futura no
cambió la interpretación de las vigencias cerradas, y cada programa cerrado siguió
resolviendo correctamente dentro de sus fechas.

## 7. Nueva secuencia ROT-DESC

Sobre la copia ya migrada se añadió una programación controlada nueva:

`DIU ×2 → Descanso ×1 → NOC ×2`.

| Día | Resultado |
|---|---|
| D1 | DIU |
| D2 | DIU |
| D3 | DescansoProgramado |
| D4 | NOC |
| D5 | NOC |
| D6 | SinProgramacion |

La comprobación final de la copia conservó `integrity_check = ok`, cero violaciones FK y
una sola aplicación de la migración.

## 8. Backup y restore drill

Antes del ensayo se detuvieron las escrituras y se copió la base pre-migración a:

`artifacts/op02b-rot-desc-20261007/backup-pre-migration/ControlMarcacion_OP02B_SOURCE.db`.

No existían `.db-wal` ni `.db-shm` que formaran parte del estado consistente del backup.
SHA-256 del backup:

`3C38C81BCEBBB58C161D55E0CA65A5137EDD3B142C8FE5C1A825EB99A485BB2B`.

Para evitar una corrupción deliberada insegura, se usó una copia post-migración modificada
como base no confiable a descartar. Su SHA-256 fue:

`8A2619F9A174A9ACA854994DC99320FCA333386DA02555E4D47AF2CB8F4B0C46`.

Después se reemplazó realmente por el backup. Resultado:

- SHA-256 restaurado idéntico al backup;
- `integrity_check = ok`;
- `foreign_key_check = 0 filas`;
- la migración ROT-DESC volvió a estar ausente;
- 7 programaciones, 13 tramos y 1 tareo recuperados;
- la marca de estado no confiable quedó ausente;
- duración desde inicio de restauración hasta validación: aproximadamente 57.667 ms.

La duración es evidencia del ensayo local, no un SLA.

### Procedimiento exacto de rollback

1. Detener la aplicación y todo proceso que pueda abrir la base.
2. Bloquear escrituras durante toda la recuperación.
3. Identificar el backup completo y verificar sus SHA-256 registrados.
4. Reemplazar la base no confiable por el `.db` del backup.
5. Restaurar `.db-wal` y `.db-shm` únicamente si pertenecen al mismo backup consistente;
   si no existían, retirar sidecars residuales de la base descartada.
6. Ejecutar `PRAGMA integrity_check` y exigir `ok`.
7. Ejecutar `PRAGMA foreign_key_check` y exigir cero filas.
8. Validar `__EFMigrationsHistory` y los conteos de programaciones, tramos y tareo contra
   la captura previa.
9. Arrancar la aplicación apuntando a la base restaurada.
10. Ejecutar login y smoke funcional de consulta antes de reabrir escrituras.

## 9. Smoke UI autenticado y repetición R-WEB

Método: navegador IAB contra una instancia ASP.NET Core real enlazada únicamente a la copia
migrada. Usuario de prueba existente, rol `COORDINADOR`, con acceso confirmado a programación
de cuadrillas, detalle y reportes. No se modificaron credenciales reales.

| Caso | Resultado | Evidencia |
|---|---|---|
| UI-ROT-01 | **PASA** | Trabajo muestra Turno habilitado y Días editable. |
| UI-ROT-02 | **PASA** | Descanso limpia/deshabilita Turno, fija Días=1 y lo deja readonly. |
| UI-ROT-03 | **PASA** | MVC autenticado rechaza Descanso + TurnoId y no persiste. |
| UI-ROT-04 | **PASA** | MVC autenticado rechaza Descanso + Días=2 y no persiste. |
| UI-ROT-05 | **PASA** | POST autenticado `DIU ×2, DESC ×1, NOC ×2` responde 302 y persiste tres tramos exactos. |
| UI-ROT-06 | **PASA** | GET de Detalle responde 200 y carga la secuencia exacta desde SQLite. |
| UI-ROT-07 | **PASA** | Reportes/Excepciones muestra `DESCANSO PROGRAMADO`. |

R-WEB confirmó las causas: CSP bloqueaba el script inline y MVC dejaba `Unvalidated` los
elementos del `IReadOnlyList` del DTO de Application. La vista carga ahora un script externo
compatible con `script-src 'self'`; un ViewModel Web mutable valida las condiciones y se
mapea explícitamente al DTO después de `ModelState`. La consola final tuvo cero errores CSP
y JavaScript. Detalle en `R_WEB_ROT_DESC_FORM_FIX.md`.

El detalle estructurado está en
`artifacts/op02b-rot-desc-20261007/ui_smoke_results.json`; los logs reales de la instancia
están en `web_stdout.log` y `web_stderr.log`.

## 10. Suite completa

Comando ejecutado:

```powershell
dotnet test ControlMarcacion.sln --nologo --no-restore --no-build
```

Resultado:

- Domain: 368/368;
- Infrastructure: 435/435;
- Web: 4/4;
- total: **807/807**;
- fallos: 0;
- omitidos: 0.

## 11. Base original y corrección de trazabilidad

La afirmación anterior de identidad física dejó de ser válida durante R-WEB: un arranque de
diagnóstico aplicó accidentalmente la migración a la base canónica local. Se ejecutó el
`Down` y el estado lógico final quedó conforme —14 migraciones, objetivo ausente, cero
programaciones/tramos, integridad `ok`, FK 0—, pero el rebuild de SQLite cambió el SHA a
`642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928`.

Otro arranque abrió por error la ruta de `C:\ProgramData`; los cambios quedaron solo en WAL.
Tras autorización expresa, se respaldaron y retiraron los sidecars. El archivo principal
conservó SHA `0E91899A86D8534A1D3770056AC5D7B8BED5C8A6A242CCDB80E3BC552F3A0734`,
8 migraciones, integridad `ok`, FK 0 y cero sidecars. No quedó despliegue productivo
persistente. Ver la cronología y hashes de respaldo en `R_WEB_ROT_DESC_FORM_FIX.md`.

## 12. Evidencias

Directorio: `artifacts/op02b-rot-desc-20261007`.

- inventario: `database_inventory.csv` y `database_inventory.json`;
- fuente controlada: `ControlMarcacion_OP02B_SOURCE.db`;
- copia migrada: `ControlMarcacion_OP02B_SOURCE_ROT_DESC_OP02B.db`;
- snapshots: `pre_migration.json`, `pre_migration_snapshot.csv`, `post_migration.json` y
  `post_migration_snapshot.csv`;
- backup y recuperación: `backup-pre-migration/`, `restore-drill/` y `restore_drill.json`;
- comprobación final: `final_probe.json`;
- salida operacional de migración y suite: `operation_results.json`;
- smoke autenticado: `ui_smoke_results.json`, `web_stdout.log` y `web_stderr.log`;
- scripts reproducibles `op02b_*.py`.

## 13. Estado y siguiente paso

**OP-02B = VALIDADO.** Representatividad controlada, migración, restore, 34/34 resoluciones,
UI-ROT-01..07 y 807/807 pruebas están verdes. OP-03 queda listo para preparación, no
ejecutado. La futura ventana debe conservar parada de escrituras, backup restaurable y
verificación inmediata; no debe tratar el `Down` como única salvaguarda.
