# Módulo Turnos y rotaciones

## Propósito

Resolver el horario aplicable por fecha mediante turno, versiones horarias, cuadrilla y programación de rotación.

## Responsabilidades

- Mantener turnos y versiones vigentes.
- Asociar turnos a plantas.
- Mantener cuadrillas y miembros.
- Programar tramos de rotación por fecha.
- Resolver el turno efectivo del trabajador.
- Detectar discrepancias entre turno previsto y marcas.

## Entidades

`Turno`, `VersionTurno`, `TurnoPlanta`, `Cuadrilla`, `ProgramacionCuadrilla` y sus tramos de rotación.

## Servicios

`ServicioTurnos`, `ServicioCuadrillas`, consultas de catálogo y resolución dentro de `ServicioCalculoTareo`.

## Reglas

- La versión de turno se resuelve por vigencia.
- La programación de cuadrilla puede cambiar por fecha.
- Duraciones y secuencias deben ser variables.
- 14+1+7 es un patrón observado, no una regla universal o infinita.
- El descanso móvil debe poder existir sin inventar un turno laboral.

## Versionado del horario («Rige desde»), verificado en OP-01

`ServicioTurnos.GuardarAsync` → `AjustarVigenciaDelHorarioAsync`:
- Horario idéntico al de la versión abierta (`MismoHorarioQue`): **no hace nada**, aunque se indique otro «Rige desde», y no informa error.
- «Rige desde» posterior al inicio de la versión abierta: la cierra el día previo y abre otra.
- «Rige desde» igual o anterior al inicio de la versión abierta: **corrección en sitio** de la versión abierta (copia horas, no mueve fechas).
- Vacío = hoy. Se rechaza si algún periodo cerrado alcanza la fecha.
- Las versiones cerradas nunca se modifican desde la aplicación.

Consecuencias operativas, ver [OP_01_SHIFT_VERSION_UPDATE](../OP_01_SHIFT_VERSION_UPDATE.md):
- La frontera puede atrasarse (deshacer y reaplicar), pero no adelantarse.
- En la primera edición de un turno sembrado (versión abierta desde 2000-01-01), un «Rige desde» ≤ 2000-01-01 reescribe la historia.
- Crear una versión no recalcula el tareo.

Estado de los datos: la semilla y la base canónica tienen DIU 07:00–19:00 y NOC 19:00–07:00. El cambio a 07:00–17:22 / 19:00–05:22 está validado en copia y pendiente de FechaRigeDesde ([procedimiento](../OP_01_PRODUCTION_PROCEDURE.md)).

## Señal de vigencia del tareo (R-P1-04)

Una `VersionTurno` que solapa el rango y pertenece a un turno realmente asignado, una
`ProgramacionCuadrilla` aplicable o un cambio de su `TramoRotacion` invalidan el tareo de
las plantas alcanzadas. Un turno/programación sin trabajadores aplicables no invalida.
Este detector incluye los cambios de tramos de descanso sin rediseñar la resolución ni la
regla de vigencia.

## Descanso explícito — diseño D-ROT-DESC

`TramoRotacion` admite ahora un `TurnoId` nullable gobernado por un tipo explícito. Esto
representa tramos de descanso sin turno ficticio; 14+1+7 sigue siendo solo un ejemplo.

La decisión de diseño está en [ADR_ROTATION_REST_MODEL](../ADR_ROTATION_REST_MODEL.md):

- modelo implementado: `Tipo = Trabajo | Descanso` y `TurnoId` nullable;
- invariante: Trabajo exige turno y Descanso lo prohíbe;
- el descanso confirmado dura un día y consume una posición completa de `FechaAncla`;
- el resolver distingue Trabajo, DescansoProgramado, SinProgramacion y
  ErrorConfiguracion;
- no se creará un turno ficticio ni se inferirán descansos históricos;
- la migración hace backfill de todos los tramos existentes a Trabajo sin cambiar su resultado.

Estado: **IMPLEMENTADO / RESUELTO** por R-P1-05. `ROT-DESC-01` está verde y existe la
migración dedicada `DescansoProgramadoEnRotaciones`, validada solo en SQLite temporal.

`ResolverEn` recorre la suma acumulada una sola vez. Después del último día devuelve
`SinProgramacion`; no existe modo repetitivo implícito.

## Flujo

Trabajador/fecha → asignación o cuadrilla → programación vigente → tramo aplicable → turno → versión horaria → jornada.

Véase [D10](../diagrams/D10_SHIFT_ROTATION.md).

## Fuentes de verdad

- Programación vigente de cuadrilla para la fecha.
- Asignación individual cuando corresponde.
- Versión de turno vigente para las horas teóricas.
- Regla confirmada de flexibilidad en `ACCEPTANCE_RULES.md`.

## Archivos críticos

- `src/ControlMarcacion.Domain/Catalogos/Turno.cs`
- `src/ControlMarcacion.Domain/Catalogos/VersionTurno.cs`
- `src/ControlMarcacion.Domain/Catalogos/ProgramacionCuadrilla.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCuadrillas.cs`
- `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs`

## Pruebas

`ProgramacionCuadrillaTests`, `ProgramacionDeCuadrillasTests`, `RotacionTurnosTests`, `VigenciaDelHorarioDeTurnoTests` y `DetectorTurnoDiscrepanteTests`.

## Problemas conocidos

- P1-05: descanso móvil — resuelto por R-P1-05; mantener contratos ROT-DESC/MARK/FER/REP.
- PENDIENTE NEGOCIO: repetición explícita; atribución de marcas en descanso sin turno
  teórico; precedencia económica feriado + descanso rotativo.
- RIESGO/P1: tareo no congela versión/programación usada.
- DEUDA: programación abierta depende parcialmente de guardias de servicio.
