# P0-ASIS-001 / OP-01 — normalización controlada de DIU/NOC

Fecha de revisión: 2026-10-22  
Resultado: **BLOQUEADO POR FECHA DE VIGENCIA NO APROBADA**  
Producción: **no tocada**

> Actualización 2026-10-22: la fase documental
> [`BD_ASIS001_FECHA_RIGE_DESDE.md`](BD_ASIS001_FECHA_RIGE_DESDE.md) no localizó evidencia
> A/B ni respuesta autorizada para DIU, NOC, ámbito, transición o excepciones. La decisión
> formal es `PENDIENTE_NEGOCIO`; OP-01 permanece `BLOQUEADO` y no se ejecutó de nuevo.

## Definición exacta de ASIS-001

La regla vigente exige:

```text
DIU = 07:00–17:22
NOC = 19:00–05:22 D+1
```

El código resuelve el horario mediante `VersionTurno`, pero la base canónica conserva la
semilla histórica:

```text
DIU = 07:00–19:00
NOC = 19:00–07:00 D+1
vigentes desde 2000-01-01 sin fecha fin
```

Por ello el defecto no está en el algoritmo actual sino en configuración de datos: mientras
no exista una versión fechada, cualquier recálculo real usa 19:00/07:00 para ordinarios,
salida anticipada y frontera de HE. Editar esos registros en sitio reescribiría la
interpretación histórica.

Fuentes: `REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md` RQ-ASIS-001,
`BUSINESS_RULES_FINAL.md` ASIS-001, `REMEDIATION_PLAN.md` OP-01,
`PROJECT_STATE.md` §§6, 11 y 17, y `CONTEXT_REGISTER.md`.

## OP-01 localizado

El procedimiento ya existía y fue validado en copia el 2026-10-05/06:

- `OP_01_SHIFT_VERSION_UPDATE.md`: piloto con fecha sintética 2026-09-15, casos DIU/NOC,
  no recálculo automático y rollback R1–R7;
- `OP_01_PRODUCTION_PROCEDURE.md`: respaldo, edición por `/Turnos`, verificación,
  recálculo explícito y rollback.

Tablas implicadas por la operación:

- `turno`: refleja el horario actualmente abierto;
- `version_turno`: cierra la versión anterior e inserta la nueva;
- `registro_auditoria`: registra modificaciones/inserciones;
- `periodo` y `tareo_diario`: solo participan después, mediante recálculo explícito de
  periodos abiertos.

No deben cambiar `marcacion_cruda`, `PeriodoLaboral`, `AsignacionPlanta` ni tareos históricos
anteriores al corte.

## Copia controlada actual

Origen leído, no modificado:

`C:\Users\stafur\Documents\CONTROL DE MARCACIONES\src\ControlMarcacion.Web\ControlMarcacion.db`

La base estaba en modo WAL; se copiaron también `-wal` y `-shm`. Destino:

`C:\Users\stafur\Documents\CONTROL DE MARCACIONES\artifacts\op01-20261022`

El manifiesto completo está en `artifacts/op01-20261022/MANIFEST.md`. Los tres archivos
coincidieron byte a byte antes de abrir la copia. Hashes iniciales:

| Archivo | Tamaño | SHA-256 |
|---|---:|---|
| `ControlMarcacion.db` | 4,096 | `52A371445CE0812CA930AEA418E7D7E9D6459F1778A6E14CB56592F08F5A08AF` |
| `ControlMarcacion.db-wal` | 1,606,832 | `869C77C41F2CD82F9A4EFAB2584EA9CDEDF81194AC0AF5B1172F6BE9C3B26E60` |
| `ControlMarcacion.db-shm` | 32,768 | `89512234BE0B760E18D33646B8280F0DC0875138BFCADFB3C537069E11C5834F` |

Precheck en la copia:

```text
PRAGMA integrity_check = ok
PRAGMA foreign_key_check = 0 filas
journal_mode = wal
```

La apertura de solo lectura regeneró metadatos del `-shm` de la copia. Los archivos `.db`
y `.db-wal` de copia y origen mantuvieron hashes iguales; los tres archivos de origen
conservaron los hashes previos.

## Inventario actual

### Turnos

| Id | Código | Nombre | Inicio | Fin | Cruza D+1 | Jornada | Tolerancia almacenada | Estado |
|---:|---|---|---|---|---|---:|---:|---|
| 1 | DIU | Diurno | 07:00 | 19:00 | no | 480 | 5 | activo |
| 2 | NOC | Nocturno | 19:00 | 07:00 | sí | 480 | 5 | activo |
| 3 | SAB | Especial sábado | 07:00 | 15:00 | no | 480 | 5 | activo |
| 4 | DOM1 | Dominical 1 | 07:00 | 15:00 | no | 480 | 5 | activo |
| 5 | DOM2 | Dominical 2 | 07:00 | 19:00 | no | 480 | 5 | activo |

La tolerancia almacenada de 5 minutos no interviene en tardanza/salida de planilla; los
contratos vigentes aplican tolerancia cero. OP-01 no debe modificarla.

### Versiones

| Id | TurnoId | Desde | Hasta | Inicio | Fin | Estado |
|---:|---:|---|---|---|---|---|
| 1 | 1 (DIU) | 2000-01-01 | abierta | 07:00 | 19:00 | activa |
| 2 | 2 (NOC) | 2000-01-01 | abierta | 19:00 | 07:00 | activa |
| 3 | 3 (SAB) | 2000-01-01 | abierta | 07:00 | 15:00 | activa |
| 4 | 4 (DOM1) | 2000-01-01 | abierta | 07:00 | 15:00 | activa |
| 5 | 5 (DOM2) | 2000-01-01 | abierta | 07:00 | 19:00 | activa |

Resultados estructurales:

```text
versiones con Desde null = 0
versiones con múltiples abiertas = 0
solapamientos = 0
duplicadas = 0
huecos = 0
```

El riesgo DIU/NOC es semántico: sus dos versiones son inequívocas, pero contienen el
horario obsoleto.

### Datos consumidores

```text
AsignacionTurno = 0
ProgramacionCuadrilla = 0
TramoRotacion = 0
TareoDiario = 0
MarcacionCruda = 0
Periodo = 1 (2026-07, abierto)
```

No existen asignaciones históricas que puedan resolver `SIN_VERSION` o
`MULTIPLES_VERSIONES`; ambos conteos son cero por conjunto vacío. Tampoco hay muestra real
actual para comparar tareo antes/después.

## Identificación de DIU/NOC

No se identificaron solo por nombre. Coinciden simultáneamente:

- Id/Código 1/DIU y 2/NOC;
- horarios de turno y de su única versión abierta;
- referencias históricas de `DatosSemilla` y migración de vigencia;
- consumidores documentados por OP-01;
- reglas y contratos DIU/NOC de asistencia.

## Fecha de corte

No existe evidencia documental inequívoca de la fecha real en que GCA adoptó
07:00–17:22 / 19:00–05:22. La fecha 2026-09-15 pertenece exclusivamente al piloto anterior
y no autoriza producción ni un nuevo saneamiento de datos.

Restricciones demostradas:

- debe ser posterior a 2000-01-01; de lo contrario la primera versión se corrige en sitio;
- no puede estar dentro de un periodo cerrado;
- vacío significa la fecha de edición;
- una frontera creada demasiado tarde no puede adelantarse después desde la aplicación;
- el recálculo se limita a periodos abiertos, salvo una decisión formal distinta.

Estado: **1 ambigüedad bloqueante — `FechaRigeDesde` no aprobada**.

## Dry-run condicionado

Si negocio aprueba una fecha válida, las filas objetivo actuales son deterministas:

| Operación | Cantidad | IDs/detalle |
|---|---:|---|
| Actualizar proyección `turno` | 2 | Turno 1 DIU: fin 17:22; Turno 2 NOC: fin 05:22 |
| Cerrar versión abierta | 2 | VersionTurno 1 y 2, en `FechaRigeDesde - 1` |
| Insertar versión | 2 | una DIU y una NOC desde `FechaRigeDesde` |
| Ambigüedades | 1 | falta `FechaRigeDesde` aprobada |

No se aplicó el dry-run. Sin la fecha, ni el valor de `fecha_fin` ni el de `fecha_inicio` de
las nuevas filas puede escribirse sin inventar historia.

## Ejecución y postchecks

```text
OP-01 ejecutado en esta copia = NO
motivo = FechaRigeDesde no demostrada
filas insertadas = 0
filas actualizadas = 0
filas cerradas = 0
```

Al no existir escritura, el estado posterior coincide con el previo:

```text
solapamientos posteriores = 0
asignaciones sin versión = 0 (sin asignaciones)
asignaciones con múltiples versiones = 0 (sin asignaciones)
integrity_check = ok
foreign_key_check = 0 filas
```

## Evidencia funcional existente

El piloto anterior sí ejecutó OP-01 sobre una copia con datos sintéticos controlados. Cubrió
14 jornadas (7 DIU y 7 NOC):

- 2 fechas anteriores al corte conservaron exactamente el horario histórico;
- 12 fechas desde el corte reflejaron el cambio esperado;
- DIU 07:00–17:22 y NOC 19:00–05:22 quedaron sin tardanza, salida anticipada ni HE;
- 17:36/05:36 produjeron 0 HE;
- 17:37/05:37 produjeron 15 minutos desde la frontera;
- la tardanza no desplazó el corte y el cruce nocturno conservó el día de inicio;
- regresiones = 0; casos no determinados = 0.

Esta evidencia valida el mecanismo y el rollback, pero no resuelve la fecha de negocio ni
cambia la base actual.

## Rollback

La copia actual no requiere rollback porque no se modificó. El procedimiento futuro conserva:

1. backup conjunto `.db + -wal + -shm` con la aplicación detenida;
2. corrección por `/Turnos` con la misma frontera para errores de horario;
3. recálculo explícito de periodos abiertos;
4. restauración completa solo como último recurso y únicamente sin escrituras concurrentes.

Los límites R1–R7 permanecen documentados en `OP_01_PRODUCTION_PROCEDURE.md`.

## Build y regresión automatizada

No se modificó código productivo ni se añadieron contratos. El arnés auxiliar de solo
lectura vive bajo `artifacts/op01-20261022/harness` y no forma parte de la solución.

Validación final única:

```text
dotnet build -c Release
0 errores / 0 warnings

dotnet test -c Release --no-build
Domain:         428 / 428
Infrastructure: 509 / 509
Web:             38 / 38
Total:           975 / 975
Fallos:          0
Omitidos:        0
```

## Riesgo residual y estado

- La base actual continúa con DIU/NOC obsoletos.
- La semilla para nuevas instalaciones continúa obsoleta (`DEPLOY-01`).
- La fecha productiva continúa pendiente de negocio.
- La copia actual no contiene datos para una regresión representativa nueva.
- Producción y `ProgramData` no fueron leídos, copiados ni modificados.
- FER-005 no fue tocado.

`OP-01 = LOCALIZADO Y MECANISMO VALIDADO PREVIAMENTE EN COPIA`.

`ASIS-001 = BLOQUEADO POR FECHA DE CORTE NO APROBADA`.

Siguiente paso: negocio debe aprobar por escrito `FechaRigeDesde`. Después debe repetirse el
dry-run sobre una copia fresca de la base que realmente se pretenda intervenir; solo con cero
ambigüedades puede ejecutarse OP-01 en esa copia y solicitarse, en una fase posterior, una
ventana productiva.
