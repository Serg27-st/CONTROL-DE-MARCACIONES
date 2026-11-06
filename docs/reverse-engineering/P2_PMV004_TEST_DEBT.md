# P2-PMV-004 — cierre de deuda de test del módulo provisional

Fecha: 2026-10-23  
Estado: **CERRADO**  
Producción: **NO TOCADA**

## Definición reconciliada

PMV es un ramal provisional y aislado. Puede decodificar archivos, persistir su propia carga,
errores y marcaciones, consultar maestros de forma histórica, agrupar la primera y última marca
y exportar sus 13 columnas con trazabilidad.

No puede crear o modificar importaciones oficiales, `MarcacionCruda`, `TareoDiario` ni
`PeriodoLaboral`; tampoco puede producir tardanza, salida anticipada, HE25/35/100,
nocturnidad, incidencias, clasificación de feriado, remuneración o pago oficial.

## Implementación existente

| Responsabilidad | Archivo | Método |
|---|---|---|
| Importación provisional | `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ServicioImportacionProvisional.cs` | `ImportarAsync` |
| Consulta provisional | `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ServicioConsultaMarcacionProvisional.cs` | `ConsultarAsync` |
| Exportación provisional | `src/ControlMarcacion.Infrastructure/MarcacionesProvisionales/ExportadorMarcacionesProvisionales.cs` | `Exportar` |

El importador agrega exclusivamente `ImportacionProvisional`, `MarcacionProvisional` y
`ErrorImportacionProvisional` dentro de una transacción. La consulta usa `AsNoTracking` para
las lecturas, agrupa en DTOs y no guarda. El exportador recibe esos DTOs y produce un archivo
en memoria; no consume el servicio de tareo ni la liquidación.

## Cobertura previa clasificada

| Evidencia | Clasificación | Alcance |
|---|---|---|
| `ImportacionProvisionalTests` | DIRECTA parcial | Carga, errores, historial y duplicados en tablas provisionales. |
| `ConsultaMarcacionProvisionalTests.La_consulta_no_modifica_ninguna_tabla` | DIRECTA parcial | Conserva provisionales, trabajadores y asignaciones durante una consulta. |
| `ExportadorMarcacionesProvisionalesTests` | DIRECTA parcial | Dos hojas, 13 columnas, trazabilidad y neutralización de fórmulas. |
| `AislamientoPmvAcceptanceTests` | INDIRECTA | Alcance por planta en catálogo, historial, importación y consulta. |
| Contrato integral PMV→modelo oficial | AUSENTE | No existía una operación completa con observación simultánea de `prov_*` y tablas oficiales. |

Duplicados, rangos 30/31/40/41, archivos con líneas inválidas e identificadores sin asignación
ya tienen contratos propios. No definen PMV-004 y no se duplicaron.

## Contrato agregado

Archivo:
`tests/ControlMarcacion.Infrastructure.Tests/MarcacionesProvisionales/ImportacionProvisionalTests.cs`

ID:
`PMV004-01_el_flujo_provisional_no_escribe_ni_expone_conceptos_del_motor_oficial`

El contrato usa SQLite real y atraviesa:

1. importación válida de dos marcas;
2. persistencia de cabecera y marcas `prov_*`;
3. consulta provisional y agrupación en una jornada;
4. exportación real del libro de 13 columnas;
5. comparación antes/después de importación y errores oficiales, `MarcacionCruda`,
   `TareoDiario` y `PeriodoLaboral`;
6. exclusión explícita de conceptos de asistencia y liquidación en el Excel.

No se mockearon SQLite, el importador, la consulta ni el exportador. Solo se reutilizó la fuente
de marcaciones sustituida que ya emplea el fixture existente para aislar el formato del archivo.

## Validación

| Verificación | Resultado |
|---|---:|
| PMV004-01 | 1/1 verde |
| Regresión PMV: importación, consulta, exportación y aislamiento | 42/42 verdes |
| Suite completa | 992/992 verdes; 0 fallos; 0 omitidos |
| Build Release | 0 errores; 0 warnings |

## Alcance y cierre

- Código productivo modificado: **NO**.
- Migraciones y esquema `prov_*`: **NO**.
- Conexión entre PMV y motor oficial: **NO CREADA**.
- FER-005, ASIS-001 y REP-004: **NO TOCADOS**.
- AUS-006/007/008: **NO REABIERTOS**.
- BKtransaction, Identity e instalador: **NO TOCADOS**.
- Producción: **NO TOCADA**.
- OP-03: **NO EJECUTADO**.
- Commit: **NO**.

Resultado: `PMV-004 = CERRADO` como deuda de test pura.
