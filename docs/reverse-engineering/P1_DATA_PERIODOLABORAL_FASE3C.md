# P1-DATA-PERIODOLABORAL — Fase 3C

Fecha: 2026-10-13  
Estado: **COMPLETADA**  
Siguiente estado máximo: **LISTO PARA FASE 3D**

## Alcance y baseline

Se migraron progresivamente los lectores históricos, sin eliminar los campos planos de
`Trabajador`, sin migración destructiva y sin tocar la base productiva. La baseline real fue:

```text
Build Release: 0 errores / 0 warnings
Tests: 874 total / 874 verdes / 0 fallos / 0 omitidos
```

El inventario previo está en `P1_DATA_PERIODOLABORAL_FASE3C_INVENTARIO.md` y la situación final
de cada consumidor en `P1_DATA_PERIODOLABORAL_FASE3C_MATRIZ.md`.

## Decisiones implementadas

1. `Trabajador.ObtenerPeriodoVigente(fecha)` es la definición canónica de vigencia cargada en
   dominio. Cero periodos significa `SinPeriodo`; dos o más significa error de configuración.
2. `ConsultaContextoLaboral` precarga periodos y asignaciones de planta por rango y resuelve en
   memoria. Expone `PeriodoLaboral`, `SinPeriodo`, `LegacyFallback` y `ErrorConfiguracion`;
   ningún consumidor migrado usa el fallback.
3. La empresa histórica sale de `PeriodoLaboral.EmpresaId`; la planta, de la asignación vigente
   dentro del periodo. Nunca se inventan con la ficha actual.
4. Los resúmenes que cruzan una transición representan los valores en orden como `A → B`; las
   filas diarias y filtros resuelven el contexto del propio día.
5. El recálculo obtiene la población de periodos que solapan el mes. Preingreso, postcese y gaps
   quedan `SinVinculo`, sin falta, tardanza, salida anticipada ni minutos pagados.
6. Una justificación fuera del vínculo se conserva, pero no se aplica al tareo de ese día.
7. El alcance por planta en recálculo, incidencias, listado, resumen y liquidación usa la planta
   histórica del tareo. Trasladar hoy a alguien no altera la autorización de jornadas pasadas.
8. Una jornada nocturna sigue perteneciendo a su fecha de inicio. Si el cese es D, la jornada
   iniciada en D conserva su salida D+1; D+1 se evalúa separadamente. No cambiaron rotaciones,
   FER-NAC, liquidación ni atribución de marcas.

## Bloques y evidencia

| Bloque | Cambio principal | Resultado al cerrar el bloque |
|---|---|---|
| 3C-1 | resolución única de vigencia, sin selección arbitraria | build 0/0; 881/881 |
| 3C-2 | empresa/planta históricas y escritor de cambio de planta | build 0/0; 888/888 |
| 3C-3 | población, filtros, filas y frescura de reportes | build 0/0; 894/894 |
| 3C-4 | recálculo, feriados, candidatos, seguridad y liquidación por historia | build 0/0; 900/900 |

Familias dirigidas añadidas:

```text
PL3C-VIG   7
PL3C-ORG   7
PL3C-REP   6
PL3C-TAREO 6
Total      26
```

La suite final mantiene importación, marcaciones, rotaciones, descanso programado, feriados,
reportes, PMV, aislamiento por planta, hosting local y Web en verde.

## SQLite temporal y comparación

Origen de evidencia 3B, solo lectura durante el ensayo:

```text
C:\Users\stafur\AppData\Local\Temp\P1-DATA-PERIODOLABORAL-3B-20261012\working\ControlMarcacion.db
SHA-256: AAE25C1DED583E921A6BB9CA51DBEC3350B7645C0D4972E283E3D99194CE1865
```

Se creó una copia de trabajo 3C; antes del arranque su tamaño y hash coincidían exactamente con
el origen. El arranque solo creó y retiró el lock normal de migraciones en la copia; no aplicó
migraciones y dejó cero locks.

Comparación de 25 trabajadores × 31 días de julio de 2026:

```text
Pares comparados:       775
Deltas de vigencia:       0
Deltas de empresa:        0
Deltas de planta:         0
Resoluciones ambiguas:    0
```

Clasificación: 0 esperados, 0 bugs observados en esa copia, 0 regresiones y 0 no determinados.
Los bugs previos que la copia alineada no puede exhibir quedan cubiertos por los 26 contratos
dirigidos, en especial historia distinta de la proyección actual, ceses, reingresos y gaps.

Integridad final de la copia:

```text
PRAGMA integrity_check: ok
PRAGMA foreign_key_check: 0 filas
__EFMigrationsLock: 0 filas
Trabajadores: 352
Periodos laborales: 352
Asignaciones de planta: 352
```

## Smoke HTTP

La aplicación se levantó en loopback contra la copia, en puerto aislado y sin navegador:

```text
GET /vivo           200 ControlMarcaciones-OK
GET /estado         200 ControlMarcaciones-OK;LISTO
GET /Cuenta/Ingresar 200, formulario presente
GET /Reportes        302 hacia /Cuenta/Ingresar
```

No se usó ni inspeccionó una credencial real. Por ello el smoke confirma disponibilidad del
login y protección del recurso, no una sesión autenticada manual. Reportes y tareo quedaron
validados por sus suites de integración/end-to-end; la copia 3B no contenía tareos y no se
inventaron datos operativos para forzar una consulta protegida.

## Rendimiento, riesgos y pendientes

- No existe una consulta de `PeriodoLaboral` por trabajador/día: hay precarga por rango.
- La autorización SQL de tareos está centralizada en `AplicarAlcancePlantas`.
- No se creó un índice ni una migración: los índices existentes son suficientes para el patrón
  observado; medir sobre volumen productivo sigue siendo una actividad futura.
- Los campos legacy continúan presentes y sincronizados como proyección actual. Eso no es
  fallback histórico y su eliminación queda fuera de 3C.
- No quedaron consumidores históricos críticos en `NO_MIGRADO`, `FALLBACK_DOCUMENTADO` o
  `BLOQUEADO`.

## Límites operativos

```text
Base productiva tocada: NO
Saneamiento productivo: NO
Migración productiva: NO
OP-03: NO
Commit: NO
Fase 3D ejecutada: NO
```

