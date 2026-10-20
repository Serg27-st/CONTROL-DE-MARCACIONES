# Fase 6.1 — Reconciliación maestra de requisitos

Fecha: 2026-10-05  
Alcance: reconciliación documental; no se modificaron producción, datos ni tests.

## 1. Autoridad aplicada

1. Requisito actual confirmado por negocio.
2. `ACCEPTANCE_RULES.md` actualizado por esta fase.
3. `BUSINESS_RULES.md` actualizado.
4. Código como descripción de comportamiento.
5. Tests actuales.
6. Dossier previo.
7. `CLAUDE.md`.
8. Comentarios/documentación antigua.
9. Suposiciones.

## 2. Inventario maestro de requisitos explícitos

| ID | Requisito | Fuente | Estado actual |
|---|---|---|---|
| RQ-001 | L–V 07:00–17:22 salvo excepción; posterior puede generar HE | Negocio F6.1 | VIGENTE |
| RQ-002 | Sábado íntegramente HE: 2 h 25 %, resto 35 % | Negocio F6.1 | VIGENTE |
| RQ-003 | Domingo descanso semanal; trabajado íntegramente HE 100 %, sin descanso sustitutorio automático | Negocio F6.1 | VIGENTE |
| RQ-004 | Rotación variable; descanso interno de 1 día; cambios futuros sin alterar pasado | Negocio F6.1 | VIGENTE |
| RQ-005 | Tardanza minuto a minuto con tolerancia 0 | Negocio F6.1 | VIGENTE |
| RQ-006 | Salida anticipada minuto a minuto con tolerancia 0 | Negocio F6.1 | VIGENTE |
| RQ-007 | Marca cruda inmutable; regularización separada, validada y auditada | Negocio F6.1 | VIGENTE |
| RQ-008 | PMV provisional, separado y sin efectos sobre tareo/maestros oficiales | Negocio F6.1 | VIGENTE |
| HIST-001 | 480 minutos como jornada ordinaria universal | Código/comentarios históricos | REEMPLAZADO |
| HIST-002 | Sábado primero ordinario y luego extra | Comportamiento actual | REEMPLAZADO |
| HIST-003 | Domingo trabajado simplemente “como feriado/no laborable” | Aceptación/dossier F5–F6 | REEMPLAZADO |
| HIST-004 | Cinco minutos de tolerancia para tardanza de planilla | Semilla/comportamiento actual | REEMPLAZADO |
| HIST-005 | Cinco minutos de tolerancia para salida anticipada de planilla | Semilla/comportamiento actual | REEMPLAZADO |
| HIST-006 | Repetición universal/infinita 14+1+7 | Interpretación histórica | OBSOLETO |
| PAT-001 | 14 días, 1 descanso y 7 noches como ejemplo real | Observación operativa | PATRÓN OPERATIVO |
| PEND-001 | Descuento/prorrateo de refrigerio y relación con nocturnidad | Fases 4–6 | PENDIENTE DE NEGOCIO |
| PEND-002 | Tasas por empresa y política temporal única | Fases 4–6 | PENDIENTE DE NEGOCIO |
| PEND-003 | Semántica transaccional de aprobación parcial | Fases 4–6 | PENDIENTE DE NEGOCIO |
| PEND-004 | Política de reliquidación de periodos cerrados | Fases 5–6 | PENDIENTE DE NEGOCIO |
| PEND-005 | Snapshot/resolución histórica para reemisión | Fases 4–6 | PENDIENTE DE NEGOCIO |
| PEND-006 | Composición de permiso, tardanza, salida anticipada y regularización en un mismo día | Fases 4–6 | PENDIENTE DE NEGOCIO |

Resumen: 21 inventariados; 8 vigentes, 5 reemplazados, 1 obsoleto, 1 patrón operativo y 6 pendientes.

## 3. Reconciliación de las 108 reglas

Cada identificador de `BUSINESS_RULES.md` fue clasificado exactamente una vez:

- **SIGUE VIGENTE (58):** RN-001, RN-002, RN-003, RN-004, RN-005, RN-007, RN-008, RN-011, RN-013, RN-014, RN-015, RN-016, RN-018, RN-019, RN-020, RN-022, RN-023, RN-026, RN-028, RN-029, RN-030, RN-033, RN-037, RN-038, RN-039, RN-040, RN-042, RN-043, RN-051, RN-054, RN-056, RN-057, RN-058, RN-059, RN-060, RN-062, RN-064, RN-065, RN-066, RN-067, RN-068, RN-069, RN-071, RN-072, RN-074, RN-075, RN-076, RN-080, RN-081, RN-083, RN-084, RN-085, RN-086, RN-087, RN-092, RN-093, PMV-RN-002 y PMV-RN-006.
- **REQUIERE ACTUALIZACIÓN (15):** RN-010, RN-024, RN-025, RN-041, RN-046, RN-047, RN-048, RN-049, RN-061, RN-063, RN-078, RN-090, PMV-RN-001, PMV-RN-008 y PMV-RN-012.
- **COMPORTAMIENTO ACTUAL, NO REQUISITO (35):** RN-006, RN-009, RN-012, RN-017, RN-021, RN-027, RN-031, RN-032, RN-034, RN-035, RN-036, RN-044, RN-045, RN-050, RN-052, RN-053, RN-055, RN-070, RN-073, RN-077, RN-079, RN-082, RN-088, RN-089, RN-091, RN-094, RN-095, RN-096, PMV-RN-003, PMV-RN-004, PMV-RN-005, PMV-RN-007, PMV-RN-009, PMV-RN-010 y PMV-RN-011.
- **QUEDÓ OBSOLETA:** ninguna fila completa. Los fragmentos obsoletos se reemplazaron dentro de las 15 reglas actualizadas; el inventario explícito registra HIST-001..006.

Total: 58 + 15 + 35 = 108.

## 4. Cambios de interpretación

### Domingo

La frase “equivalente a feriado” era insuficiente y queda reemplazada. La regla autoritativa es: domingo = descanso semanal; si se trabaja, todos los minutos son horas extra al 100 %. No se agrega descanso sustitutorio automático. HE-05 detecta reparto indebido, pero falta un contrato directo de que todo el intervalo vaya al 100 %; LIQ-04 cubre parcialmente la liquidación.

### Salida anticipada

El comportamiento actual perdona `MinutosAnticipacionSalida`, sembrado y predeterminado en 5. RQ-006 exige 0 y cálculo minuto a minuto. Es BUG confirmado por contradicción y se clasifica P0 por efecto directo en planilla. Faltan SA-01..03.

### Regularización

El sistema cumple la base: `MarcacionCruda` no se modifica; la corrección vive en una justificación/regularización, conserva extremos crudos, tiene estados, versión y auditoría general. Cumple parcialmente el contrato ampliado: una sola justificación por día impide composiciones, existen borrados físicos de justificaciones, una ruta de retiro descarta el usuario para logging y no hay prueba extremo a extremo de todos los campos/auditoría. Se clasifica GAP FUNCIONAL P1, con deuda de trazabilidad asociada.

### PMV

Cumple RQ-008: decodifica, consulta, atribuye, agrupa, exporta y traza en `prov_*`, sin escribir tareo ni maestros. El P0 de alcance por planta permanece separado: aislamiento funcional no equivale a aislamiento de seguridad.

## 5. Documentation drift definitivo

### VIGENTE

- `ACCEPTANCE_RULES.md`, `BUSINESS_RULES.md`, `REQUIREMENTS_TRACEABILITY.md`.
- `MASTER_DOSSIER.md`, `PROJECT_STATE.md`, `AI_CONTEXT_MAP.md` después de Fase 6.1.
- `REMEDIATION_PLAN.md`, `TECHNICAL_DEBT.md` y `CONTEXT_REGISTER.md` actualizados.
- `DATABASE.md` para estructura física.

### HISTÓRICA

- `PHASE_01_*` a `PHASE_06_*` anteriores a este documento.
- `INDEPENDENT_ARCHITECTURE_REVIEW.md` y `PHASE_04_INDEPENDENT_REVIEW.md` como evidencia fechada.
- Tests existentes como evidencia del comportamiento/contrato de su fecha; no mandan sobre un requisito posterior.

### DESACTUALIZADA O NO AUTORITATIVA PARA REGLAS LABORALES

- `CLAUDE.md` y comentarios antiguos sobre 480 o tolerancias.
- Cualquier texto que reduzca domingo trabajado a “como feriado” sin afirmar 100 % para todo el tiempo.
- Cualquier texto que presente 14+1+7 como repetición universal.
- `ARCHITECTURE.md` y `modules/ATTENDANCE_TAREO.md` siguen válidos para estructura, pero su frase previa sobre domingo no es fuente normativa; prevalecen aceptación y esta reconciliación.

## 6. Cobertura futura

No se ejecutó la suite ni se modificaron tests. Se requieren contratos nuevos para domingo 100 %, salida anticipada 0, regularización inmutable/auditada y rotaciones variables. La lista ejecutable propuesta está en `REQUIREMENTS_TRACEABILITY.md`.

## 7. Prioridades reconciliadas

- P0: jornada/HE L–V, sábado, domingo 100 %, tardanza 0, salida anticipada 0 y aislamiento PMV.
- P1: liquidación, `PeriodoLaboral`, incidencias, vigencia, rotación/descanso, historia, importación no atómica y regularización completa.
- P2: aprobación parcial, borrado físico, refrigerio/nocturnidad y tasas por empresa.
- P3: drift, constantes, observabilidad, pruebas Web complementarias y retiro PMV.

## 8. Conclusión

El requisito ahora está separado del comportamiento. El código sigue describiendo qué hace el sistema, no qué debe hacer. `ACCEPTANCE_RULES.md` es la fuente autoritativa de comportamiento esperado y esta fase no implementa ninguna corrección.
