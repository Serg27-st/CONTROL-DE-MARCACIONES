# ADR 0002 — El turno de quien rota sale del ciclo de su cuadrilla

- **Estado:** aceptada
- **Fecha:** 23/09/2026
- **Migración:** `RotacionPorCuadrilla`

## Contexto

Los obreros rotan entre diurno y nocturno, y no rotan de a uno: en el attlog de julio de
2026, el lunes 20, tras el descanso del domingo, cinco personas pasaron de noche a día y
otras cinco de día a noche, todas a la vez. Nadie más cambió en el mes.

Hasta esta decisión, el turno era solo una asignación por trabajador con vigencia por fecha
(`AsignacionTurno.TurnoId`). Cada rotación eran decenas de fichas editadas, y tenían que
editarse el día exacto: el cambio regía desde el día siguiente a guardarlo. Olvidar una sola
ficha no daba error. En quien pasa a noche, el motor emparejaba la salida de una noche con
la entrada de la siguiente y **fabricaba una jornada diurna completa que nunca ocurrió**:
puntual, con sobretiempo y sin minutos nocturnos.

El paso 1 de este trabajo agregó «Turno rige desde», que permite anotar la rotación con la
fecha en que ocurrió. Eso resuelve la fecha, pero no el volumen: siguen siendo decenas de
fichas por rotación.

## Decisión

1. **Cuadrilla** (`Cuadrilla`): el grupo de una planta que rota junto.
2. **Programación con vigencia por fecha** (`ProgramacionCuadrilla`): un ciclo de tramos
   `(turno, días)` —por ejemplo, 14 días de diurno y 14 de nocturno— con dos fechas
   distintas:
   - `FechaInicio`/`FechaFin`: desde cuándo rige la programación.
   - `FechaAncla`: el día en que empieza el primer tramo del ciclo.

   El turno de una fecha es `((fecha − ancla) mod ciclo)` recorrido por los tramos
   (`ProgramacionCuadrilla.TurnoEn`, puro, en `Domain`).
3. **La asignación del trabajador apunta a un turno fijo o a una cuadrilla**, nunca a los
   dos (`AsignacionTurno.TurnoId` o `CuadrillaId`, con una restricción `CHECK` en la base).
   Van en la misma línea de tiempo, así que entrar a una cuadrilla, salir de ella o cambiar
   de cuadrilla es un cambio de asignación como cualquier otro, con «Turno rige desde».
4. El turno de un día **se calcula**, no se genera. El motor resuelve primero la asignación
   vigente del trabajador en esa fecha y, si es una cuadrilla, la programación vigente de
   la cuadrilla en esa misma fecha.
5. Una programación no se edita, se reemplaza: «desde esta fecha, este ciclo». La que
   cubría el día previo se cierra; las que empezaban en la fecha o después pasan a
   **baja lógica** (`Activo = false`) y dejan de contar. «Correr la rotación» es lo mismo
   con el ciclo vigente y el ancla movida unos días.
6. Todo cambio que alcance días de un periodo cerrado se rechaza, igual que el cambio de
   turno con fecha en la ficha.

## Alternativa descartada: generar las asignaciones

Programar la cuadrilla podría haber generado una fila de `AsignacionTurno` por integrante y
por rotación, sin tocar el motor. Se descartó por tres motivos:

- **Nadie extiende el horizonte.** El sistema corre en una sola PC, sin tareas programadas.
  Alguien tendría que acordarse de «generar el mes siguiente», que es justo el tipo de paso
  manual que esta decisión elimina.
- **Corregir obliga a borrar.** Correr una rotación un día reescribe todas las filas futuras
  de todos los integrantes. Con la regla de que nada se borra físicamente, harían falta
  bajas lógicas en `asignacion_turno`, y todas las consultas que hoy leen esa tabla
  tendrían que filtrarlas.
- **Volumen sin información.** Son cientos de filas al año que no dicen nada que la
  programación no diga ya.

## Invariantes que se mantienen

- **Invariante 6** («el turno se resuelve por la asignación vigente a la fecha»): se cumple
  un nivel más adentro. La asignación vigente a la fecha dice la cuadrilla, y la programación
  vigente a la fecha dice el turno. Recalcular un mes antiguo usa el ciclo de entonces.
- **Invariante 2** (tareo reconstruible e idempotente): el turno sale de datos con
  vigencia, sin reloj. Lo fija `El_integrante_de_una_cuadrilla_se_calcula_con_el_turno_de_su_ciclo`,
  que recalcula dos veces y compara.
- **Invariante 7** (nada se borra): una programación reemplazada queda de baja, con autor,
  en el historial de la cuadrilla.
- **Invariante 8** (cada resultado se explica): la observación del día empieza por
  «Cuadrilla A: turno NOC.», porque la ficha de la persona ya no dice qué turno tuvo ese día.

## Consecuencias

- El turno de cada día queda en `TareoDiario.TurnoId`, como antes, así que reportes y
  tareo final no cambian.
- Tres consumidores tuvieron que aprender a mirar la cuadrilla:
  - El filtro «turno» de Personal resuelve en memoria qué cuadrillas están hoy en ese turno.
  - El aviso de «recalcular» de los reportes vigila las programaciones.
  - Dar de baja un turno se bloquea si forma parte del ciclo vigente de alguna cuadrilla.
- Los días de descanso siguen saliendo de `Turno.DiasAplicables`; el ciclo no los modela.
  Si apareciera una cuadrilla con una semana de descanso dentro del ciclo, habría que
  agregar tramos de descanso.
- El ciclo es una hipótesis, y en planta puede no cumplirse. Detectar cuándo las marcas
  contradicen el ciclo es el paso siguiente, y usará la programación como punto de partida.
