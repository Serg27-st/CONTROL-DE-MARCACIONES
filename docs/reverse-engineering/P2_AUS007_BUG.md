# P2-AUS-007 — aprobación atómica de solicitudes de ausencia

Fecha: 2026-10-23
Estado: **CERRADO**

## 1. Definición exacta

```text
AUS-007:
Regla: la aprobación de una solicitud debe ser atómica para todos los días de su rango.
Comportamiento esperado: si cualquier día no puede generar o conservar su justificación,
la solicitud permanece Pendiente y no se crea ninguna justificación nueva.
Comportamiento anterior: los días con periodo cerrado, sin periodo o con otra justificación
se omitían; los demás se guardaban y la cabecera quedaba Aprobada.
Bug: un estado final completo ocultaba una ejecución parcial.
Impacto: el tareo y su efecto económico podían reflejar solo parte de la ausencia aprobada.
```

Clasificación: **PERSISTENCIA / ATOMICIDAD**. El defecto no nacía en el cálculo de minutos,
la precedencia de eventos, la vigencia laboral, la UI ni los reportes.

## 2. Inventario mínimo

| Archivo | Clase/método | Responsabilidad |
|---|---|---|
| `ServicioSolicitudes.cs` | `ServicioSolicitudes.AprobarAsync` | Valida la solicitud, materializa una justificación por día y cambia la cabecera. Era la causa raíz. |
| `IServicioSolicitudes.cs` | `AprobarAsync` | Contrato Application de la operación de aprobación. |
| `SolicitudDtos.cs` | `ResultadoAprobacion` | Resultado consumido por Web; un resultado exitoso ya no puede representar omisiones. |
| `ConfiguracionesSolicitudes.cs` | `JustificacionConfiguracion` | Índice único físico trabajador+fecha para justificaciones vigentes. Protege carreras. |
| `GuardiaDePeriodo.cs` | `AfirmarAbierto` | Ata la escritura al estado/versión del periodo dentro del guardado. |
| `SolicitudesController.cs` | `Aprobar` | Presenta el éxito o el rechazo de negocio; ya capturaba `ReglaNegocioException`. |

## 3. Reproducción previa

Contrato `AUS007-01` sobre SQLite real:

```text
solicitud pendiente: 01/07/2026–03/07/2026
justificación manual preexistente: 02/07/2026

esperado:
excepción de negocio
solicitud Pendiente
0 justificaciones nuevas de la solicitud

resultado anterior:
no lanzó excepción
creó 01/07 y 03/07
dejó la solicitud Aprobada
```

Evidencia roja previa al cambio:

```text
AUS007-01: 0 verdes / 1 fallo
Should throw ReglaNegocioException, but did not
```

El primer intento dentro del aislamiento fue anulado porque `vstest.console` no conectó con
`testhost` en 90 segundos. La repetición fuera de ese aislamiento ejecutó el contrato y confirmó
el fallo funcional anterior; no se confundió la incidencia del runner con el bug.

## 4. Causa raíz

`AprobarAsync` recorría las fechas y usaba `continue` ante tres impedimentos:

- periodo cerrado;
- día sin periodo activo;
- justificación preexistente de otro origen.

Durante el mismo recorrido agregaba las filas de los días restantes. Después, sin exigir cobertura
completa, asignaba `EstadoSolicitud.Aprobada` y ejecutaba `SaveChangesAsync`. El guardado era
transaccional para las filas preparadas, pero la decisión preparada ya era funcionalmente parcial.

Además, los periodos se consultaban sin seguimiento, por lo que la aprobación no afirmaba su token
de concurrencia frente a un cierre ocurrido entre lectura y escritura.

## 5. Corrección mínima

La operación ahora se ejecuta en dos etapas dentro del mismo método:

1. prevalidación completa del rango, sin cambiar estado ni agregar justificaciones;
2. solo si no existe ningún impedimento, preparación de todas las filas, afirmación de cada periodo
   abierto y un único `SaveChangesAsync` para cabecera y detalle.

Si hay un impedimento, se lanza una excepción de negocio con las fechas afectadas y no se persiste
nada. Si aparece una carrera al cerrar un periodo o insertar otra justificación, la concurrencia o
el índice único abortan el guardado completo y se devuelve un mensaje operativo legible.

No se modificaron `MarcacionCruda`, `PeriodoLaboral`, `AsignacionPlanta`, reglas de tareo, créditos
de 480 minutos, feriados, descansos ni precedencias. No fue necesaria migración ni cambio físico.

## 6. Contratos

| ID | Caso | Resultado |
|---|---|---|
| AUS007-01 | Choque en el día intermedio de un rango de tres días | Rechaza todo; cabecera Pendiente; cero filas nuevas. |
| AUS007-02 | Un día sin periodo y otro válido | Rechaza todo; cabecera Pendiente; cero filas nuevas. |
| AUS007-03 | Tres días válidos | Aprueba una vez y crea las tres justificaciones. |

Resultados:

```text
Contratos AUS007:              3/3 verdes
Regresión inmediata ausencias: 123/123 verdes
Suite completa:                980/980 verdes
Fallos:                        0
Omitidos:                      0

Build Release:
Errores:                       0
Advertencias:                  0
```

## 7. Cierre

```text
Migración nueva: NO
Datos modificados: NO
Producción: NO TOCADA
OP-03: NO EJECUTADO
Commit: NO
Regresiones: 0

AUS-007 = CERRADO
```

Deudas separadas que no se reabren aquí: `AUS-006` (cobertura integral de solapamientos) y
`AUS-008` (preservación lógica de evidencia al anular).
