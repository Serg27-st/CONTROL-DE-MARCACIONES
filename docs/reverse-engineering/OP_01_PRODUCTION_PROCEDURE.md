# OP-01 PRODUCCIÓN — Procedimiento para actualizar DIU/NOC

> **NO EJECUTADO.** Validado en copia descartable (ver [OP_01_SHIFT_VERSION_UPDATE.md](OP_01_SHIFT_VERSION_UPDATE.md)). Requiere FechaRigeDesde aprobada por negocio.

Objetivo: dejar vigentes **DIU 07:00–17:22** y **NOC 19:00–05:22 (+1)** desde FechaRigeDesde, sin reescribir las versiones anteriores (DIU 07:00–19:00, NOC 19:00–07:00).

## Precondiciones

1. **FechaRigeDesde aprobada por escrito por negocio.** Debe cumplir:
   - ser **posterior al 2000-01-01**. Una fecha igual o anterior corrige en sitio la versión histórica y reescribe toda la historia (comprobado en copia, caso R6);
   - no estar alcanzada por ningún periodo **cerrado** (el sistema lo rechaza);
   - **no puede adelantarse después** desde la aplicación (ver Rollback): conviene elegirla con margen.
2. La aplicación desplegada incluye R-P0-02/R-P0-03.1 (tardanza y salida anticipada con tolerancia 0, corte en `FinTurno`, umbral HE de 15 min). Sin ese código, la nueva versión no produce los resultados validados.
3. Ventana de mantenimiento: nadie editando turnos, asignaciones ni marcaciones; opera un usuario **COORDINADOR** (recalcula todas las plantas; un ADMINISTRADOR solo las suyas).
4. Ninguna otra versión de DIU/NOC creada desde la inspección previa (la versión abierta de cada uno sigue siendo la de 2000-01-01, salvo decisión documentada).

## Paso 1 — Crear backup

- Detener la aplicación, o asegurar que no haya escrituras.
- Copiar `ControlMarcacion.db` **junto con** `-wal` y `-shm` si existen. Nunca borrar el `-wal` antes de copiar.
- Abrir el respaldo y comprobar `PRAGMA integrity_check = ok`, `SELECT COUNT(*) FROM version_turno` y el hash SHA-256.
- Exportar desde Reportes, antes del cambio, el resumen y el tareo final de los periodos abiertos afectados (sirven de base para el Paso 8).
- Guardar la foto previa:

```sql
SELECT id, turno_id, fecha_inicio, fecha_fin, hora_inicio, hora_fin FROM version_turno
WHERE turno_id IN (1, 2) ORDER BY turno_id, fecha_inicio;
SELECT id, codigo, fecha_inicio, fecha_fin, estado FROM periodo WHERE activo = 1;
```

## Paso 2 — Definir fecha RigeDesde

Registrar en el acta la FechaRigeDesde aprobada y verificar las precondiciones de la fecha (posterior al 2000-01-01; sin periodo cerrado que la alcance). Identificar los periodos abiertos que contienen días ≥ FechaRigeDesde.

## Paso 3 — Actualizar DIU mediante /Turnos

`/Turnos` → **Editar DIU**:
- Hora fin: **17:22** (inicio 07:00 sin cambio).
- **Rige desde: FechaRigeDesde.** Nunca vacío (vacío = hoy) y nunca ≤ 2000-01-01.
- Los demás campos se dejan como están, incluido «Jornada ordinaria» = 480: la validación lo admite y el motor ya no lo usa para el corte L–V.
- Guardar.

## Paso 4 — Actualizar NOC mediante /Turnos

Igual, con **Editar NOC** → Hora fin **05:22**, mismo «Rige desde».

## Paso 5 — Verificar VersionTurno

```sql
SELECT id, turno_id, fecha_inicio, fecha_fin, hora_inicio, hora_fin, activo FROM version_turno
WHERE turno_id IN (1, 2) ORDER BY turno_id, fecha_inicio;
```

Resultado esperado, por turno:

| Versión | Desde | Hasta | Horario |
|---|---|---|---|
| original (Id 1 o 2) | 2000-01-01 | FechaRigeDesde − 1 | DIU 07:00–19:00 / NOC 19:00–07:00 (**sin cambios**) |
| nueva | FechaRigeDesde | — | DIU 07:00–17:22 / NOC 19:00–05:22 |

Además: una sola versión abierta por turno; `turno.hora_fin` = 17:22 / 05:22; registros en `registro_auditoria` (`turno` Modificación, `version_turno` Inserción y Modificación). Si algo difiere, ir a **Rollback** antes de recalcular.

## Paso 6 — Recalcular rango afectado

Crear la versión **no** recalcula nada (verificado). Desde `/Asistencia`, como COORDINADOR, recalcular **cada periodo abierto** que contenga días ≥ FechaRigeDesde. Los periodos cerrados no se recalculan sin decisión formal de reliquidación.

## Paso 7 — Validar muestra

Para un trabajador DIU y uno NOC con marcas:
- un día **anterior** a FechaRigeDesde no debe cambiar respecto del export del Paso 1;
- días **posteriores**:
  - jornada completa (07:00–17:22 / 19:00–05:22): tardanza 0, salida anticipada 0, HE 0, 622 ordinarios, 442 nocturnos en el NOC;
  - salida hasta 14 min después del fin: 0 HE;
  - desde 15 min: HE completa desde el fin;
  - entrada 1 min tarde: 1 min de tardanza;
  - salida 1 min antes: 1 min de salida anticipada.

```sql
SELECT trabajador_id, fecha, estado, minutos_ordinarios, minutos_extra25, minutos_extra35,
       minutos_nocturnos, minutos_tardanza, minutos_salida_anticipada
FROM tareo_diario WHERE trabajador_id IN (:diu, :noc) AND fecha BETWEEN :antes AND :despues;
```

## Paso 8 — Comparar reportes

Volver a exportar el resumen y el tareo final de los periodos recalculados y compararlos con los del Paso 1:
- las diferencias deben concentrarse en días ≥ FechaRigeDesde de trabajadores DIU/NOC;
- se esperan menos salidas anticipadas espurias, ordinarios hasta 17:22/05:22 y HE solo desde 15 min;
- el estado del cálculo debe figurar «al día». Ojo: la alerta tiene limitaciones conocidas (P1-04), así que se verifica la fecha de cálculo de las filas muestreadas.

## Rollback (verificado en copia; casos R1–R7)

El sistema **no borra versiones**: las versiones cerradas son intocables desde la aplicación y solo la versión **abierta** puede corregirse en sitio. Preferir siempre una edición correctiva por `/Turnos` a la restauración del backup.

| Situación | Acción | Efecto verificado |
|---|---|---|
| Horario nuevo mal tecleado, fecha correcta | Editar el turno con el horario correcto y **el mismo** «Rige desde» | Corrección en sitio de la versión nueva; la histórica queda intacta (R4) |
| Deshacer el cambio | Editar con el horario antiguo (19:00 / 07:00) y **el mismo** «Rige desde» | La versión nueva vuelve al horario antiguo; quedan dos versiones consecutivas con horario idéntico y no se borra nada (R2). Recalcular |
| Fecha demasiado temprana (debía ser posterior, Y > X) | Deshacer (fila anterior) y volver a editar con el horario nuevo y «Rige desde» Y | Se cierra la versión el Y − 1 y se abre la nueva desde Y (R2 + R3). Recalcular |
| Fecha demasiado tardía (debía ser anterior, W < X) | **No se puede desde la aplicación.** Mismo horario + fecha anterior: no hace nada en silencio, aunque la pantalla informe éxito (R1). Horario distinto: corrige en sitio sin mover la fecha (R5) | Restaurar el backup del Paso 1 (si no hubo otras escrituras) o plan de intervención de datos aparte, auditable y sobre copia |
| «Rige desde» ≤ 2000-01-01 en la primera edición | La versión histórica quedó reescrita (R6). Editar de nuevo con el horario **antiguo** y «Rige desde» 2000-01-01 (restaura en sitio; la auditoría muestra los valores previos) y repetir con la fecha correcta | O restaurar el backup |
| «Rige desde» vacío (quedó = hoy) | Tratar como fecha equivocada (filas anteriores) | R7: la frontera queda en la fecha de edición |
| Recálculo ya ejecutado con versión equivocada | Corregir la versión y **recalcular** de nuevo los periodos abiertos | El tareo es reconstruible e idempotente |

Último recurso: restaurar el backup completo (`.db` + `-wal` + `-shm`). Solo si no hubo otras escrituras desde el Paso 1; si las hubo, se perderían.
