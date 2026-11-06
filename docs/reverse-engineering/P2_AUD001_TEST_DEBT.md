# P2-AUD-001 — cierre de deuda de test transversal de auditoría SQLite

Fecha: 2026-10-23  
Estado: **CERRADO**  
Producción: **NO TOCADA**

## Definición reconciliada

AUD-001 exige que toda escritura sensible incluida deje, después de un guardado exitoso,
actor, fecha/hora, operación, tabla, identificador y detalle de los cambios. Una baja lógica se
distingue de una modificación ordinaria y los valores protegidos se enmascaran.

La regla excluye deliberadamente la propia bitácora, marcas crudas, errores de importación,
tareo derivado y entidades provisionales. El recálculo de tareo deja un único evento agregado
`4200` con periodo, actor y conteos; no una fila de auditoría por cada `TareoDiario`.

## Inventario de implementación

| Responsabilidad | Archivo | Elemento |
|---|---|---|
| Entidad y operaciones | `src/ControlMarcacion.Domain/Auditoria/RegistroAuditoria.cs` | `RegistroAuditoria`; `OperacionAuditada` |
| Mapeo EF | `src/ControlMarcacion.Infrastructure/Persistencia/Configuraciones/ConfiguracionesAuditoria.cs` | `RegistroAuditoriaConfiguracion` |
| Persistencia | `src/ControlMarcacion.Infrastructure/Persistencia/MarcacionDbContext.cs` | `RegistrosAuditoria` |
| Captura transversal | `src/ControlMarcacion.Infrastructure/Persistencia/Auditoria/InterceptorAuditoria.cs` | `SavingChanges*`; `SavedChanges*`; `SaveChangesFailed*` |
| Actor de la sesión | `src/ControlMarcacion.Application/Comun/IProveedorUsuarioActual.cs` | `IProveedorUsuarioActual` |
| Registro en DI | `src/ControlMarcacion.Infrastructure/InyeccionDependencias.cs` | interceptor agregado al `DbContext` |
| Consulta/consumo | `src/ControlMarcacion.Infrastructure/Persistencia/Consultas/ConsultaAuditoria.cs` | listado y consulta de bitácora |
| Vigencia de reportes | `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioReportes.cs` | consumo de eventos auditados |
| Resumen de tareo | `src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs` | `Recalculado`, EventId `4200` |

### Esquema físico

Tabla SQLite: `registro_auditoria`.

| Columna | Tipo físico | Obligatoria | Semántica |
|---|---|---:|---|
| `id` | `INTEGER` autoincremental | sí | PK |
| `tabla` | `TEXT`, máximo lógico 80 | sí | entidad física afectada |
| `clave_registro` | `TEXT`, máximo lógico 120 | sí | PK simple o compuesta serializada |
| `operacion` | `TEXT`, máximo lógico 20 | sí | inserción, modificación, baja lógica o eliminación |
| `usuario` | `TEXT`, máximo lógico 150 | sí | actor resuelto por el proveedor actual |
| `fecha_hora` | `TEXT` | sí | momento de materialización de la auditoría |
| `detalle` | `TEXT` | sí | JSON de campo/anterior/nuevo |

No tiene claves foráneas. Índices:

- `ix_registro_auditoria_fecha_hora`, descendente;
- `ix_registro_auditoria_tabla_clave_registro_fecha_hora`;
- `ix_registro_auditoria_usuario_fecha_hora`.

El modelo vigente deja `detalle` como texto sin límite lógico. La migración inicial y el snapshot
histórico conservan `maxLength: 200`, pero en SQLite ambos producen la misma columna física
`TEXT` y no alteran el contrato probado; esta fase no cambia esquema ni crea migración.

## Cobertura previa clasificada

| Evidencia | Clasificación | Alcance |
|---|---|---|
| `FlujoDeRegularizacionTests.REG_03` | DIRECTA parcial | Actor, motivo, fechas y filas de auditoría para regularización. |
| `AtomicidadImportacionAcceptanceTests.IMP_AT_01` | DIRECTA | Un fallo revierte cabecera, errores, marcas y auditoría; no queda éxito falso. |
| `VigenciaTareoAcceptanceTests` | INDIRECTA | Consume auditoría para determinar frescura del tareo. |
| Pruebas MVC de reingreso | INDIRECTA | Ejercitan el interceptor desde una operación Web real. |
| Inclusión/exclusión transversal + log agregado | AUSENTE | Era la deuda exacta de AUD-001. |

No se encontró un bug productivo: infraestructura y operaciones auditadas ya existían. La
ausencia era exclusivamente contractual.

## Contratos agregados

Archivo:
`tests/ControlMarcacion.Infrastructure.Tests/Quality/AuditoriaTransversalTests.cs`

| ID | Contrato SQLite |
|---|---|
| `AUD001-01` | Otorgar y anular un descanso sustitutorio confirma filas físicas relacionadas con tabla/ID, actor exacto, timestamp coherente, inserción, baja lógica, estado anterior/nuevo y motivo exacto. |
| `AUD001-02` | Recalcular un periodo crea tareo, no crea auditoría `tareo_diario` por fila y emite exactamente un evento agregado `4200` con periodo, actor y conteos. |

La consulta se realiza contra `RegistrosAuditoria` con SQLite real y `AsNoTracking`; no hay mock
de persistencia. El timestamp se valida dentro de una ventana, sin igualdad frágil de
milisegundos.

No se agregó `AUD001-03`: `IMP_AT_01` ya cubre exactamente la frontera negativa requerida. Si
la operación principal falla dentro de su transacción, no persiste una auditoría falsa de éxito.

## Validación

| Verificación | Resultado |
|---|---:|
| `AUD001-01/02` | 2/2 verdes |
| Regularización e importación atómica representativas | 2/2 verdes |
| Regresión dirigida total | 4/4 verdes |
| Suite completa | 994/994 verdes; 0 fallos; 0 omitidos |
| Build Release | 0 errores; 0 warnings |

## Alcance y cierre

- Código productivo modificado: **NO**.
- Migraciones o esquema: **NO**.
- FER-005, ASIS-001 y REP-004: **NO TOCADOS**.
- AUS-006/007/008 y PMV: **NO REABIERTOS**.
- BKtransaction, Identity e instalador: **NO TOCADOS**.
- Producción: **NO TOCADA**.
- OP-03: **NO EJECUTADO**.
- Commit: **NO**.

Resultado: `AUD-001 = CERRADO` como deuda de test pura.
