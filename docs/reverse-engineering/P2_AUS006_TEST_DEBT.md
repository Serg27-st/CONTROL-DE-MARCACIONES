# P2-AUS-006 — cierre de deuda de test

Fecha: 2026-10-23  
Estado: **CERRADO**  
Producción: **NO TOCADA**

## Definición reconciliada

Para un mismo trabajador, una solicitud existente `Pendiente` o `Aprobada` reserva su rango
completo y bloquea cualquier nueva solicitud cuyas fechas se crucen. Los extremos son
inclusivos: si una termina el mismo día en que comienza la otra, existe solape. Una solicitud
`Rechazada` o `Anulada` no reserva fechas y permite reutilizar el rango.

Pendiente y aprobada también consumen, con conteo inclusivo, los límites del tipo de ausencia.
Rechazada y anulada no consumen esos límites.

## Implementación existente

| Responsabilidad | Archivo | Método |
|---|---|---|
| Solape persistido | `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioSolicitudes.cs` | `GuardarAsync` |
| Acumulación por estado y periodo | mismo archivo | `ValidarLimitesAsync` |
| Regla pura de límites | `src/ControlMarcacion.Domain/Solicitudes/ValidadorLimitesSolicitud.cs` | validadores de evento/año/vacaciones |

`GuardarAsync` filtra por el mismo trabajador, excluye la solicitud editada y los estados
`Anulada`/`Rechazada`, y aplica `FechaInicio <= finNuevo && FechaFin >= inicioNuevo`. La forma
de las comparaciones demuestra que los extremos son inclusivos.

`SolicitudAusencia.SeSolapaCon` no tiene llamadores en la ruta actual y no fue tomado como
fuente de verdad ni modificado en esta fase. La decisión persistida corresponde al servicio.

## Inventario y cobertura previa

| Evidencia | Clasificación | Alcance |
|---|---|---|
| `ValidadorLimitesSolicitudTests` | DIRECTO parcial | Límites y conteo inclusivo; recibe acumulados ya resueltos y no prueba consulta por estados. |
| `AprobacionAtomicaSolicitudesTests` (`AUS007-01..03`) | INDIRECTO | Transición y atomicidad al aprobar; no prueba alta frente a rangos persistidos. |
| `AnulacionPreservaEvidenciaSolicitudesTests` (`AUS008-01..06`) | INDIRECTO | Estado terminal y evidencia cancelada; no prueba la matriz de solape de `GuardarAsync`. |
| Pruebas de tareo, permisos y regularizaciones | NO_RELACIONADO | Operan después de la solicitud o sobre otras reglas. |

Conclusión previa: implementación correcta, sin bug productivo y sin contrato SQLite directo
de solape por estados.

## Contratos agregados

Archivo: `tests/ControlMarcacion.Infrastructure.Tests/Justificaciones/SolapamientoSolicitudesTests.cs`

- `AUS006-01`: matriz `Pendiente/Aprobada` bloquean y `Rechazada/Anulada` liberan el rango.
- `AUS006-02`: el extremo compartido se rechaza; el día siguiente se acepta.

Son dos contratos y cinco casos ejecutados sobre SQLite real en memoria. Reutilizan
`MarcacionDbContext`, datos semilla, `ServicioSolicitudes` y `AlcanceSinRestriccion`, el mismo
patrón de infraestructura usado por AUS-007/008. No se creó un motor alternativo.

## Validación

| Verificación | Resultado |
|---|---:|
| Casos AUS-006 | 5/5 verdes |
| Regresión dirigida AUS-006/007/008 + ausencias/justificaciones | 127/127 verdes |
| Suite completa | 991/991 verdes; 0 fallos; 0 omitidos |
| Build Release | 0 errores; 0 warnings |

La primera ejecución aislada del runner no logró conectar `vstest.console` con `testhost`.
La misma compilación produjo 0 errores/0 warnings y la ejecución fuera del aislamiento de IPC
completó normalmente; no fue un fallo funcional ni de contrato.

## Alcance y cierre

- Código productivo modificado: **NO**.
- Migraciones: **NO**.
- Deuda de FK de `justificacion`: **NO TOCADA**.
- AUS-007 y AUS-008: **NO REABIERTOS**, regresión verde.
- FER-005, ASIS-001 y REP-004: **NO TOCADOS**.
- Producción: **NO TOCADA**.
- OP-03: **NO EJECUTADO**.
- Commit: **NO**.

Resultado: `AUS-006 = CERRADO` como deuda de test pura.
