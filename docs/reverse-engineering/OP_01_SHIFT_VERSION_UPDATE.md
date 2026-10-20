# OP-01 — Actualización operativa de versiones DIU/NOC y recálculo piloto

Fecha de ejecución: 2026-10-05 (piloto); 2026-10-06 (rollback y procedimiento de producción)  
Tipo: operativa y de validación de datos. **Sin cambios de código, semillas, migraciones ni base canónica.**  
Resultado: el mecanismo «Rige desde» lleva DIU/NOC al horario vigente **sin reescribir la historia**, y el recálculo piloto cumple todos los contratos. Falta definir la fecha de producción.

## 1. Situación inicial

### Base canónica (protegida, no modificada)

| Dato | Valor |
|---|---|
| Ruta | `src/ControlMarcacion.Web/ControlMarcacion.db` |
| SHA-256 antes | `d4bcdab88d74668b74de123346b941f17fb86d103bb07ce1a81fb119b9a3effa` (igual al de Fase 3B) |
| SHA-256 después | `d4bcdab88d74668b74de123346b941f17fb86d103bb07ce1a81fb119b9a3effa` (sin cambios) |
| `-wal` / `-shm` al copiar | ausentes |

### Copia descartable

| Dato | Valor |
|---|---|
| Ruta | `<scratchpad de sesión>/op01/op01_copia.db` (fuera del repositorio; efímera) |
| Creada | 2026-10-05 15:47 -05:00 |
| SHA-256 al crear | idéntico al canónico |
| `PRAGMA integrity_check` | `ok` |

### Turnos y versiones antes (copia = producción)

| Turno | Id | Inicio | Fin | Cruza | `MinutosJornadaOrdinaria` | Versión abierta |
|---|---:|---|---|---|---:|---|
| DIU | 1 | 07:00 | 19:00 | no | 480 | Id 1 |
| NOC | 2 | 19:00 | 07:00 | sí | 480 | Id 2 |

| Turno | Versión | Desde | Hasta | Inicio | Fin |
|---|---:|---|---|---|---|
| DIU | 1 | 2000-01-01 | — | 07:00 | 19:00 |
| NOC | 2 | 2000-01-01 | — | 19:00 | 07:00 |
| SAB / DOM1 / DOM2 | 3 / 4 / 5 | 2000-01-01 | — | sin cambios | sin cambios |

Otros datos: periodos 2026-07 y 2026-09, ambos **Abiertos** (ninguno cerrado); 0 `asignacion_turno`; 0 `tareo_diario`; 0 `marcacion_cruda`; 170 `asignacion_marcador`.

## 2. Procedimiento

1. Copiar la base canónica y verificar hash e integridad.
2. Preparar datos piloto **en la copia**:
   - asignaciones de turno desde 2026-09-01: trabajador 51 → DIU, trabajador 52 → NOC (los dos activos, con número vigente en el equipo 2, planta 3). Se insertaron directamente porque es una inyección controlada de datos de prueba;
   - marcaciones por el camino real (`IServicioImportacion.ImportarAsync`) con un TXT sintético en formato G3: 28 leídas, 28 insertadas, 0 rechazadas, origen `NoVerificado`.
3. Recalcular 2026-09 con las versiones antiguas (`IServicioCalculoTareo.RecalcularPeriodoAsync`) y guardar la foto «antes».
4. Versionar con **el mismo servicio que usa `/Turnos`** (`IServicioTurnos.GuardarAsync`). El turno se precarga como en la pantalla de edición y solo cambian `HoraFin` y `RigeDesde`:
   - DIU: 07:00–17:22, rige desde la fecha piloto;
   - NOC: 19:00–05:22, rige desde la fecha piloto.
5. Sin recalcular, releer el tareo (verificación de «no hay recálculo automático»).
6. Recalcular 2026-09 y comparar.

La operación no usó SQL para escribir turnos ni versiones. El SQL solo se usó para leer, sobre la copia.

Ejecutor: un arnés de consola fuera del repositorio que registra la infraestructura real (`AgregarInfraestructura`) contra la copia, con usuario de auditoría por omisión. No hubo login ni se ingresaron credenciales.

**Fecha piloto de RigeDesde: 2026-09-15 (martes).** Es solo para la copia; no es la fecha de producción.

## 3. Versiones después

| Turno | Versión | Desde | Hasta | Inicio | Fin | `MinutosJornadaOrdinaria` |
|---|---:|---|---|---|---|---:|
| DIU | 1 | 2000-01-01 | **2026-09-14** | 07:00 | 19:00 | 480 |
| DIU | 6 | **2026-09-15** | — | 07:00 | 17:22 | 480 |
| NOC | 2 | 2000-01-01 | **2026-09-14** | 19:00 | 07:00 | 480 |
| NOC | 7 | **2026-09-15** | — | 19:00 | 05:22 | 480 |

- Las versiones anteriores permanecen con su horario original y terminan el día previo a «Rige desde».
- Sin solapamientos; cada turno conserva una sola versión abierta. SAB, DOM1 y DOM2 no se tocaron.
- `Turno` quedó con los valores vigentes (DIU 17:22, NOC 05:22) y `modificado_por` registrado.
- Auditoría: `turno` 2 modificaciones; `version_turno` 2 inserciones y 2 modificaciones (los cierres).
- `MinutosJornadaOrdinaria` quedó en **480**: la validación del turno lo admite (480 ≤ 622) y no hay requisito vigente que fije otro valor. El motor ya no lo usa para el corte L–V. Sigue relacionado con el P1-08 (ausencia pagada), que no se mezcla aquí.

## 4. Resultados del recálculo piloto (2026-09-14 a 2026-09-23)

`ord` = ordinarios; `SA` = salida anticipada; `noct` = minutos nocturnos.

### DIU (trabajador 51)

| Fecha | Marcas | Versión | Antes (versión antigua) ord / HE / tard / SA | Después ord / HE25 / tard / SA |
|---|---|---|---|---|
| 14 (lun) | 07:00–17:22 | antigua 07–19 | 622 / 0 / 0 / 98 | **622 / 0 / 0 / 98** (historia conservada) |
| 15 | 07:00–17:22 | nueva | 622 / 0 / 0 / 98 | **622 / 0 / 0 / 0** — DIU canónico |
| 16 | 07:00–17:36 | nueva | 636 / 0 / 0 / 84 | **622 / 0 / 0 / 0** — bajo el umbral, declarado en la observación |
| 17 | 07:00–17:37 | nueva | 637 / 0 / 0 / 83 | **622 / 15 / 0 / 0** |
| 18 | 07:00–17:38 | nueva | 638 / 0 / 0 / 82 | **622 / 16 / 0 / 0** |
| 21 | 07:10–17:37 | nueva | 627 / 0 / 10 / 83 | **612 / 15 / 10 / 0** — la tardanza no corre el corte |
| 22 | 07:01–17:21 | nueva | 620 / 0 / 1 / 99 | **620 / 0 / 1 / 1** |

### NOC (trabajador 52; la jornada se imputa al día de inicio)

| Fecha | Marcas | Versión | Antes ord / HE / tard / SA / noct | Después ord / HE25 / tard / SA / noct |
|---|---|---|---|---|
| 14 | 19:00–05:22 | antigua 19–07 | 622 / 0 / 0 / 98 / 442 | **622 / 0 / 0 / 98 / 442** (historia conservada) |
| 15 | 19:00–05:22 | nueva | 622 / 0 / 0 / 98 / 442 | **622 / 0 / 0 / 0 / 442** — NOC canónico |
| 16 | 19:00–05:36 | nueva | 636 / 0 / 0 / 84 / 456 | **622 / 0 / 0 / 0 / 442** |
| 17 | 19:00–05:37 | nueva | 637 / 0 / 0 / 83 / 457 | **622 / 15 / 0 / 0 / 457** |
| 18 (vie→sáb) | 19:00–05:38 | nueva | 638 / 0 / 0 / 82 / 458 | **622 / 16 / 0 / 0 / 458** |
| 21 | 19:01–05:21 | nueva | 620 / 0 / 1 / 99 / 441 | **620 / 0 / 1 / 1 / 441** |
| 22 | 19:10–05:37 | nueva | 627 / 0 / 10 / 83 / 457 | **612 / 15 / 10 / 0 / 457** |

Todos los contratos de ACCEPTANCE_RULES (VIG-HE-01/03, VIG-TAR-01, VIG-SAL-01, nocturnidad) se cumplen con datos persistidos. Los 15 minutos extra del NOC (05:22–05:37) caen dentro de la franja 22:00–06:00 y suman nocturnidad: son dimensiones independientes.

## 5. ¿Crear la versión recalcula el tareo?

**No.** Después de `GuardarAsync`, las filas de `tareo_diario` quedaron idénticas, con el mismo `calculado_en`. Hace falta un recálculo explícito. Como `turno.modificado_en` es posterior al último `calculado_en` y el turno está asignado en el rango, la alerta de vigencia (`EstadoCalculoTareo`, fuente «edición de un turno que rige en esas fechas») marca el periodo como desactualizado. Ojo: esa alerta tiene limitaciones conocidas (P1-04).

## 6. Impacto estimado en producción (base canónica, solo lectura)

| Concepto | Valor |
|---|---|
| Periodos abiertos | 2026-07 (01–31) y 2026-09 (01–30) |
| Trabajadores con DIU o NOC (asignación directa o por cuadrilla) | **0**: `asignacion_turno` vacía; sin cuadrillas |
| Plantas afectadas | ninguna hoy |
| Filas `TareoDiario` potencialmente afectadas | **0**: `tareo_diario` vacía |

Cuando existan asignaciones, se estima así:

```sql
-- filas afectadas = días ≥ FechaRigeDesde de periodos abiertos × trabajadores con DIU/NOC vigente ese día
SELECT a.turno_id, COUNT(DISTINCT a.trabajador_id) AS trabajadores
FROM asignacion_turno a
WHERE a.activo = 1 AND a.turno_id IN (1, 2)
  AND (a.fecha_fin IS NULL OR a.fecha_fin >= :FechaRigeDesde)
GROUP BY a.turno_id;
-- + integrantes de cuadrillas cuyos tramos usan los turnos 1/2 en el rango.
```

## 7. Fecha de producción

No existe evidencia documental inequívoca de desde qué fecha rige 07:00–17:22 / 19:00–05:22.

**PENDIENTE OPERATIVO: definir FechaRigeDesde para producción.**

Restricciones verificadas en el código para esa fecha:
- Debe ser **posterior al 2000-01-01**. Una fecha igual o anterior al inicio de la versión vigente se toma como **corrección en sitio** y reescribiría la historia.
- **Vacío = hoy.**
- Se rechaza si algún periodo **cerrado** alcanza la fecha.

## 8. Procedimiento futuro para producción

Está en un documento separado: **[OP_01_PRODUCTION_PROCEDURE.md](OP_01_PRODUCTION_PROCEDURE.md)**, con precondiciones, los pasos 1–8 y el rollback. No se ejecutó en producción.

## 8a. Rollback: mecanismo verificado

Escenarios R1–R5 sobre una segunda copia en estado post-piloto (`op01_rollback.db`); R6 y R7 sobre una tercera copia limpia de la canónica (`op01_riesgo.db`). Todos con el mismo servicio de `/Turnos`.

| Caso | Acción | Resultado observado |
|---|---|---|
| R1 | DIU, **mismo** horario 17:22, «Rige desde» anterior (09-10) | **Sin cambios**, y el servicio no informa error: la frontera no se puede adelantar |
| R2 | DIU, horario antiguo 19:00, misma fecha 09-15 | v6 corregida en sitio a 07:00–19:00; v1 intacta; nada borrado |
| R3 | DIU, 17:22, «Rige desde» posterior (09-22) | v6 cerrada el 09-21; v8 abierta desde el 09-22 |
| R4 | NOC equivocado (05:30 desde 09-20) y luego correctivo 05:22 con la misma fecha | v9 creada y corregida en sitio; v7 intacta |
| R5 | NOC 05:00 con «Rige desde» 2000-01-01, con versión abierta posterior | Solo se corrige la versión **abierta** (v9) y sin mover su fecha; las versiones cerradas nunca se tocan |
| R6 | **Primera** edición DIU (abierta = versión 2000) con «Rige desde» 2000-01-01 | **La versión histórica se reescribe en sitio** (v1 desde 2000 pasa a 17:22). Solo la auditoría conserva el valor previo |
| R7 | NOC sin «Rige desde» | Frontera = hoy (2026-10-06); versión antigua cerrada el 2026-10-05 |

Conclusiones:
- Las versiones cerradas son inmutables desde la aplicación; la corrección en sitio afecta solo a la abierta y no mueve fechas.
- La frontera puede **atrasarse** (deshacer y reaplicar) pero **no adelantarse** desde la aplicación.
- Riesgo operativo crítico: en la primera edición en producción, un «Rige desde» ≤ 2000-01-01 reescribe toda la historia.

## 8b. Riesgos

| Riesgo | Severidad | Mitigación |
|---|---|---|
| «Rige desde» ≤ 2000-01-01 en la primera edición reescribe la historia (R6) | Alta | Precondición explícita; doble verificación; backup; restauración en sitio con el horario antiguo |
| La frontera no se puede adelantar después (R1) | Media | Elegir la fecha con negocio antes; backup |
| Mismo horario con otra fecha: no hace nada pero la pantalla informa éxito (R1) | Media | Verificar siempre `version_turno` (Paso 5); candidato a mejora de UX |
| «Rige desde» vacío = hoy (R7) | Media | Campo obligatorio en el procedimiento |
| Crear la versión no recalcula; el tareo queda viejo | Media | Paso 6 obligatorio; alerta de vigencia con limitaciones (P1-04) |
| Administrador recalcula solo sus plantas | Media | Recalcula el COORDINADOR |
| La app desplegada no incluye R-P0-03.1 | Alta | Precondición 2 del procedimiento |
| `MinutosJornadaOrdinaria` = 480 sigue influyendo en la ausencia pagada | Media | P1-08, fuera de OP-01 |
| Nuevas instalaciones con horarios obsoletos | Media | DEPLOY-01 |

## 9. Semillas y nuevas instalaciones

La semilla (`DatosSemilla`) sigue creando DIU 07:00–19:00 y NOC 19:00–07:00. No se modificó: `HasData` exige migración y alteraría la versión histórica sembrada. Se registra el ticket **DEPLOY-01 (DATA/DEPLOYMENT): las nuevas instalaciones no deberían arrancar con horarios obsoletos**. Requiere decidir entre migración con versión nueva y paso de instalación documentado. No se resolvió en OP-01.

## 10. Tests y código

- Código del repositorio: sin cambios en OP-01. El arnés vive fuera del repositorio.
- Línea base vigente: 758 totales, 750 verdes, 8 rojos deliberados, 0 inesperados. No se re-ejecutó la suite porque no hubo cambios de código; el piloto valida con datos persistidos lo mismo que cubren los contratos HE/TAR/SA/DOM.
- No se tocaron liquidación, `TotalHorasEquivalentes`, ausencia pagada, P1 ni rotaciones.

## 11. Conclusión

| Criterio | Resultado |
|---|---|
| «Rige desde» crea versión nueva sin reescribir la anterior | Cumple |
| Fecha anterior resuelve el horario antiguo; desde «Rige desde», el nuevo | Cumple |
| DIU/NOC canónicos: 0 tardanza, 0 salida anticipada, 0 HE | Cumple |
| Umbral 15 min DIU/NOC (0 / 15 / 16) | Cumple |
| La tardanza no corre el corte | Cumple |
| Nocturnidad conservada (442 en el NOC regular) | Cumple |
| Recálculo automático al versionar | No ocurre (recálculo explícito) |
| Base canónica intacta | Cumple (hash idéntico; también tras las pruebas de rollback del 2026-10-06) |
| Rollback sin borrar historia | Verificado (R2–R4); límites y riesgo R6 documentados |

**OP-01: VALIDADO EN COPIA.** Para ejecutarlo en producción falta: FechaRigeDesde (negocio), respaldo, edición desde `/Turnos` y recálculo de los periodos abiertos.
