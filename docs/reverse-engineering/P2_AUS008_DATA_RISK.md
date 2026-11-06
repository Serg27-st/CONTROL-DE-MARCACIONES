# P2-AUS-008 — preservación de evidencia al anular solicitudes

Fecha: 2026-10-23  
Estado: **CERRADO EN CÓDIGO Y BASE CONTROLADA**

## 1. Definición exacta

```text
AUS-008:
Regla: anular una solicitud aprobada debe retirar su efecto del tareo sin borrar la
evidencia derivada.
Dato afectado: filas Justificacion originadas por la SolicitudAusencia, incluido su vínculo,
fecha, trabajador, tipo, autor, resolución, motivo y versión.
Condición de activación: ServicioSolicitudes.AnularAsync sobre una solicitud Aprobada cuyos
periodos siguen abiertos.
Comportamiento anterior: RemoveRange eliminaba físicamente todas las justificaciones.
Impacto: se conservaba la cabecera Anulada, pero desaparecía el detalle que probaba qué días
habían estado justificados.
Mitigación: conservar cada fila, pasarla a Cancelada con actor/fecha/motivo, incrementar Version
y guardar detalle, cabecera y afirmaciones de periodo en una sola operación.
```

Clasificación: **PÉRDIDA DE EVIDENCIA / RIESGO DE DATOS**. No es un problema de solapamiento,
vigencia laboral, cálculo de minutos, `MarcacionCruda`, `PeriodoLaboral` ni `AsignacionPlanta`.

## 2. Inventario físico y funcional

| Elemento | Estructura relevante | Garantía/uso |
|---|---|---|
| `solicitud_ausencia` / `SolicitudAusencia` | `estado`, `motivo_anulacion`, rango, trabajador y tipo | La cabecera se conserva y pasa de `Aprobada` a `Anulada`. |
| `justificacion` / `Justificacion` | FK `solicitud_ausencia_id`; `estado`, `resuelta_por_usuario`, `resuelta_en`, `motivo_resolucion`, `version` | El modelo ya soportaba cancelación lógica y concurrencia; no requirió columnas nuevas. |
| `periodo` / `Periodo` | `estado`, `version` | Impide que una anulación se cuele durante un cierre concurrente. |
| `registro_auditoria` | modificación de entidad y detalle de campos | El interceptor registra la transición; ya no recibe una eliminación física. |
| Índice parcial `ix_justificacion_trabajador_id_fecha` | único cuando `estado NOT IN ('Rechazada','Cancelada')` | Conserva historia terminal y permite una nueva justificación vigente en el mismo día. |
| Índices de `justificacion` | `periodo_id`, `solicitud_ausencia_id`, `estado` | Soportan anulación, cálculo, listado y auditoría. |
| `ServicioSolicitudes.AprobarAsync` | lectura/escritura | Ignora evidencia rechazada/cancelada al validar un reemplazo. |
| `ServicioSolicitudes.AnularAsync` | escritura | Era la causa raíz; ahora cancela y versiona sin `DELETE`. |
| `ServicioCalculoTareo` | lectura | Solo aplica `Aprobada`; la bandeja prioriza una vigente sobre historia terminal. |
| `ServicioReportes` | lectura | Vigencia y snapshot consideran cambios de aprobación; no se alteró su regla económica. |

La tabla física `justificacion` no declara FK hacia trabajador, periodo ni tipo de ausencia;
esa debilidad general requiere migración y evaluación de datos propia. AUS-008 no la ocultó ni
la amplió: la anulación ahora rechaza una referencia de periodo inexistente antes de modificar.

## 3. Potencial frente a datos reales

```text
A. El modelo/flujo permitía pérdida: SÍ.
   Reproducción previa: 3/4 contratos AUS008 fallaron porque las filas desaparecieron.

B. Filas incoherentes ya existentes en la base canónica del workspace: NO DETECTADAS.
   La copia auditada contiene 0 solicitudes y 0 justificaciones.

Producción: NO AUDITADA y NO TOCADA.
```

Que la base controlada esté vacía no convierte el bug en preventivo: el test rojo ejecutó el
camino real sobre SQLite y confirmó el borrado físico. Sí impide afirmar que haya solicitudes
históricas concretas que sanear en esa base o en producción.

## 4. Auditoría SQLite controlada

Fuente, solo lectura lógica:

```text
C:\Users\stafur\Documents\CONTROL DE MARCACIONES\src\ControlMarcacion.Web\ControlMarcacion.db
journal_mode = wal
```

Se copió el conjunto completo antes de consultar:

```text
artifacts\P2_AUS008_AUDIT_20261023\ControlMarcacion.db
artifacts\P2_AUS008_AUDIT_20261023\ControlMarcacion.db-wal
artifacts\P2_AUS008_AUDIT_20261023\ControlMarcacion.db-shm
```

| Archivo fuente | Tamaño | SHA-256 antes y después | Copia inicial |
|---|---:|---|---|
| `ControlMarcacion.db` | 4,096 | `52A371445CE0812CA930AEA418E7D7E9D6459F1778A6E14CB56592F08F5A08AF` | coincide |
| `ControlMarcacion.db-wal` | 1,606,832 | `869C77C41F2CD82F9A4EFAB2584EA9CDEDF81194AC0AF5B1172F6BE9C3B26E60` | coincide |
| `ControlMarcacion.db-shm` | 32,768 | `89512234BE0B760E18D33646B8280F0DC0875138BFCADFB3C537069E11C5834F` | coincide |

Precheck sobre la copia:

```text
PRAGMA integrity_check  = ok
PRAGMA foreign_key_check = 0 filas
trabajadores             = 0
periodos                 = 1
solicitudes              = 0
justificaciones          = 0
registros de auditoría   = 0
```

## 5. Consultas de detección

Anulaciones sin evidencia, anulaciones que todavía surten efecto y aprobaciones incompletas:

```sql
WITH evidencia AS (
    SELECT s.id,
           s.trabajador_id,
           s.fecha_inicio,
           s.fecha_fin,
           s.estado,
           CAST(julianday(s.fecha_fin) - julianday(s.fecha_inicio) + 1 AS INTEGER)
               AS dias_esperados,
           SUM(CASE WHEN j.estado = 'Cancelada' THEN 1 ELSE 0 END) AS canceladas,
           SUM(CASE WHEN j.estado NOT IN ('Rechazada', 'Cancelada') THEN 1 ELSE 0 END)
               AS vigentes,
           COUNT(j.id) AS filas_evidencia
      FROM solicitud_ausencia s
 LEFT JOIN justificacion j ON j.solicitud_ausencia_id = s.id
  GROUP BY s.id
)
SELECT id,
       trabajador_id,
       fecha_inicio,
       fecha_fin,
       estado,
       CASE
         WHEN estado = 'Anulada' AND canceladas < dias_esperados
           THEN 'INCONSISTENTE: evidencia cancelada incompleta'
         WHEN estado = 'Anulada' AND vigentes > 0
           THEN 'INCONSISTENTE: conserva efecto vigente'
         WHEN estado = 'Aprobada' AND vigentes < dias_esperados
           THEN 'SOSPECHOSA: efecto aprobado incompleto'
         ELSE 'VALIDA'
       END AS causa
  FROM evidencia
 WHERE (estado = 'Anulada' AND (canceladas < dias_esperados OR vigentes > 0))
    OR (estado = 'Aprobada' AND vigentes < dias_esperados);
```

Detalle derivado desalineado y periodo inexistente:

```sql
SELECT j.id,
       j.trabajador_id,
       j.fecha,
       j.estado,
       CASE
         WHEN j.origen <> 'Solicitud' THEN 'INCONSISTENTE: origen incorrecto'
         WHEN j.trabajador_id <> s.trabajador_id THEN 'INCONSISTENTE: trabajador distinto'
         WHEN j.tipo_ausencia_id <> s.tipo_ausencia_id THEN 'INCONSISTENTE: tipo distinto'
         WHEN j.fecha < s.fecha_inicio OR j.fecha > s.fecha_fin
           THEN 'INCONSISTENTE: fecha fuera del rango'
         WHEN p.id IS NULL THEN 'INCONSISTENTE: periodo inexistente'
         ELSE 'VALIDA'
       END AS causa
  FROM justificacion j
  JOIN solicitud_ausencia s ON s.id = j.solicitud_ausencia_id
  LEFT JOIN periodo p ON p.id = j.periodo_id
 WHERE j.origen <> 'Solicitud'
    OR j.trabajador_id <> s.trabajador_id
    OR j.tipo_ausencia_id <> s.tipo_ausencia_id
    OR j.fecha < s.fecha_inicio
    OR j.fecha > s.fecha_fin
    OR p.id IS NULL;
```

Resultado sobre la copia: **0 filas** en todas las categorías. Clasificación agregada:

| Clasificación | Cantidad | Motivo |
|---|---:|---|
| `VALIDA` | 0 | No existen solicitudes que clasificar. |
| `SOSPECHOSA` | 0 | Sin aprobaciones incompletas. |
| `INCONSISTENTE` | 0 | Sin anulaciones ni detalle desalineado. |
| `NO_DETERMINADA` | 0 | La base no contiene candidatos. |

No se ejecutó saneamiento. Una eventual solicitud anulada sin detalle no debe reconstruirse
automáticamente: el `DELETE` perdió IDs, marcas de tiempo y autoría que no pueden inferirse con
certeza. Ese caso exigiría inventario y decisión separados sobre una copia contemporánea.

## 6. Corrección

`AnularAsync` ahora:

1. carga las justificaciones y sus periodos con seguimiento;
2. rechaza periodos cerrados o referencias inexistentes;
3. cambia cada fila a `Cancelada`, registra actor, fecha y motivo, y aumenta `Version`;
4. afirma la versión de todos los periodos afectados;
5. cambia la cabecera a `Anulada`;
6. confirma todo mediante un único `SaveChangesAsync`.

Un cierre concurrente o una modificación concurrente abortan todo el guardado y producen un
mensaje de negocio. Un segundo POST observa la cabecera ya anulada y no vuelve a mutar evidencia.
Una solicitud correctiva puede usar el mismo día porque aprobación e índice parcial excluyen
`Rechazada` y `Cancelada`.

No hubo migración ni cambio de esquema. `MarcacionCruda`, `PeriodoLaboral` y `AsignacionPlanta`
permanecieron intactas.

## 7. Contratos y validación

| ID | Caso | Resultado |
|---|---|---|
| AUS008-01 | Anulación de dos días | Conserva IDs y datos origen; ambas filas quedan canceladas, motivadas y versionadas. |
| AUS008-02 | Nueva solicitud sobre día previamente cancelado | Conserva historia y crea una única justificación vigente. |
| AUS008-03 | Periodo cerrado | Rechaza toda la anulación; cabecera y detalle siguen aprobados. |
| AUS008-04 | Doble POST secuencial | El segundo se rechaza y no vuelve a mutar la evidencia. |
| AUS008-05 | Bandeja con cancelada + vigente | Muestra la vigente y su estado aprobado. |
| AUS008-06 | Solicitud aprobada con evidencia histórica incompleta | Rechaza sin normalizar ni modificar las filas sobrevivientes. |

Evidencia:

```text
Primera tanda antes del cambio: 1/4 verdes; 3 fallos por filas eliminadas.
AUS008-06 antes de su guarda: 0/1; la historia incompleta se normalizaba en silencio.
Contratos AUS008 finales: 6/6 verdes.
Suite Infrastructure: 520/520 verdes.
Suite completa: 986/986 verdes (428 Domain + 520 Infrastructure + 38 Web).
Omitidos: 0.

Build Release:
Errores: 0
Advertencias: 0
```

## 8. Cierre

```text
Migración nueva: NO
Saneamiento: NO
Datos fuente modificados: NO
Producción: NO TOCADA
OP-03: NO EJECUTADO
Commit: NO

AUS-008 = CERRADO
P2-03 = RESUELTO EN CÓDIGO
```

El faltante de FK físicos adicionales de `justificacion` permanece como deuda de modelo separada;
no autoriza una migración ni un backfill dentro de AUS-008.
