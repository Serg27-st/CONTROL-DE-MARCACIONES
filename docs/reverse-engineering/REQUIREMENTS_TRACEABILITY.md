# Trazabilidad maestra de requisitos — corte histórico Fase 6.1

> **SUPERADA como matriz operativa el 2026-10-16.** Se conserva para trazabilidad de `RQ-*`.
> La matriz final vigente es
> [`REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md`](REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md).
> RQ-017 quedó **SUPERADO parcialmente en su economía pendiente** por P0-FER-002/007 el
> 2026-10-19: FER-001/002/004/007 están implementados y con contratos directos. Permanecen
> separados el sustitutorio (FER-003) y el día no laborable compensable (FER-005).

Fecha de corte: 2026-10-06  
Autoridad: requisitos explícitos de Fase 6.1 → `ACCEPTANCE_RULES.md` → `BUSINESS_RULES.md` → comportamiento observado.

## Matriz requisito ↔ sistema

| Req | Requisito vigente | Código actual | Test | Estado | Tipo de diferencia | Acción futura |
|---|---|---|---|---|---|---|
| RQ-001 | DIU 07:00–17:22, NOC 19:00–05:22; L–V ordinario hasta el fin teórico; HE solo si el exceso ≥ 15 min, contado entero desde el fin teórico | R-P0-03.1: `Repartir` corta en `FinTurno`; R-P0-HE.2 confirma que viernes→sábado conserva ese tratamiento y solo reconoce HE posterior a 05:22 | HE-01/02, HE-DIU-00..07, HE-NOC-00..05, NOC-WE-01/02 y canónicos verdes | CUMPLE EN CÓDIGO | CONFIGURACIÓN: semilla DIU 07:00–19:00 y NOC 19:00–07:00 | Plan operativo: nueva versión DIU/NOC con «Rige desde» (sin migración) y recálculo de periodos abiertos. |
| RQ-002 | Sábado: todo HE desde el primer minuto, sin umbral; 2 h al 25 %, resto 35 %; si el NOC entra al domingo, este reparto termina a las 00:00 | Sábado laborable sin ordinarios; `Repartir` limita HE25/35 al tramo anterior a domingo 00:00 | HE-03/04, HE-SAB y NOC-WE-03..06 verdes | CUMPLE | NINGUNO | Feriado en sábado y sábado de descanso por turno conservan el comportamiento vigente (ambigüedad documentada). |
| RQ-003 | Domingo: descanso semanal; todo minuto trabajado al 100 % desde el primero, incluso el tramo dominical de una jornada iniciada el sábado | Domingo completo sigue como no laborable; en sábado→domingo el HE100 es el remanente posterior a las 00:00; liquidación no vuelve a sumar base ordinaria | HE-05, DOM-01..05, NOC-WE-03..06 y LIQ-04 verdes | CUMPLE | NINGUNO | Mantener contratos. Domingo-feriado conserva precedencia y cero ordinarios. |
| RQ-004 | Rotaciones variables; descanso interno de 1 día; sin repetición rígida | Tipo explícito, `TurnoId` nullable, secuencia finita y delimitación BD-01 en dos pasadas | ROT-DESC-01..09, ROT-MARK-01..06 y ROT-REP-01 verdes | CUMPLE | NINGUNO | Mantener contratos y migración. |
| RQ-005 | Tardanza minuto a minuto, tolerancia 0 (DIU y NOC) | Calcula desde la entrada teórica sin aplicar `MinutosTolerancia` | TAR-00..04, TAR-NOC y cruce nocturno verdes | CUMPLE | NINGUNO | Mantener contrato; configuración histórica queda como deuda candidata. |
| RQ-006 | Salida anticipada minuto a minuto, tolerancia 0 (DIU 17:22, NOC 05:22) | Calcula hasta `FinTurno` teórico sin aplicar `MinutosAnticipacionSalida` | SA-00..04, SA-NOC y cruce nocturno verdes | CUMPLE | NINGUNO | Mantener contrato; configuración histórica queda como deuda candidata. |
| RQ-007 | Marca cruda inmutable; corrección separada con original, corrección, motivo, usuario, fecha, validación y auditoría | Preserva marca, corrección separada, workflow/versionado y bitácora | REG-01..04 pasan con SQLite temporal e interceptor real | CUMPLE | NINGUNO | Mantener contrato; composición con otras justificaciones sigue bajo RQ-011/P2-03. |
| RQ-008 | PMV solo decodifica, consulta, cruza, agrupa, exporta y traza; no toca tareo oficial ni maestros | Tablas `prov_*`, lectores compartidos y flujo aislado; no escribe tablas oficiales | Suite PMV cubre aislamiento funcional | CUMPLE | NINGUNO | Mantener frontera y añadir prueba negativa integral. |
| RQ-009 | Una base remunerativa no se suma dos veces | R-P1-01: nocturnidad aporta solo prima; HE/HE100 aportan base y recargo; ordinarios superpuestos en no laborables trabajados se excluyen | LIQ-01..04 y LIQ-HE-NOC-01 verdes | CUMPLE | NINGUNO | Mantener contrato; fórmula económica nocturna definitiva sigue como decisión separada. |
| RQ-010 | Un único `PeriodoLaboral` abierto; el cese lo cierra | R-P1-02 carga solo el vínculo abierto en la ruta de escritura; edición no duplica y baja cierra entre contextos distintos | PL-01/02 verdes | CUMPLE EN CÓDIGO | PROBLEMA DE DATOS + CONCURRENCIA ESTRUCTURAL PENDIENTE | `DATA-PERIODOLABORAL`: sanear 85 grupos y después agregar unicidad física del vínculo abierto. |
| RQ-011 | Justificación parcial no oculta incidencia pendiente | R-P1-03 evalúa la incidencia después de aplicar la justificación; solo desaparece cuando el resultado quedó completo o explicado | INC-01..03 e INC-PARCIAL-01 verdes | CUMPLE | NINGUNO | Mantener contratos; la política general de múltiples decisiones por día sigue separada en P2. |
| RQ-012 | Tareo “actualizado” observa todos los insumos y todo el alcance | R-P1-04 usa `MIN(CalculadoEn)` en periodo + plantas visibles; consulta fuentes consumidas y auditoría para borrados | VIG-01..05 y VIG-07..09 verdes; VIG-06 no aplica porque `ParametroLegal` no participa en `ServicioCalculoTareo` | CUMPLE | NINGUNO | Mantener contratos; la política de reliquidación de periodos cerrados sigue siendo decisión de negocio. |
| RQ-013 | Reemisión cerrada reproducible | Mezcla tareo persistido con maestros actuales | Cobertura histórica parcial | NO IMPLEMENTADO | GAP FUNCIONAL | Decidir snapshot o resolución histórica. |
| RQ-014 | Importación principal no deja cabecera parcial | R-P1-07 mantiene los `SaveChanges` necesarios dentro de una transacción SQLite iniciada después de parsing/dedupe; incluye serie aprendida y auditoría | IMP-AT-01..07 verdes; el pre-fix dejó 1 cabecera, 2 errores y 1 auditoría huérfanos | CUMPLE | NINGUNO | Mantener contratos; concurrencia general y política de recarga por hash son decisiones separadas. |
| RQ-015 | Usuario restringido solo opera sus plantas, incluido PMV | PMV aplica `IProveedorAlcancePlantas` antes de listar, importar o consultar; exportación reutiliza la consulta protegida | SEC-01..04 verdes | CUMPLE | NINGUNO | Mantener el aislamiento y la prueba de acceso autorizado. |
| RQ-016 | Licencia o ausencia pagada completa acredita 480 min efectivos sin almuerzo, independientemente de los 622 min de intervalo DIU/NOC; no genera HE ni nocturnidad por sí sola | `Aplicar` acredita `Turno.MinutosJornadaOrdinaria`; DIU/NOC vigentes conservan 480. `PermisoParcial` usa una rama separada | AUS-PAG-01..05 verdes; recálculo SQLite confirma VAC y LCG | CUMPLE | DOCUMENTACIÓN DESACTUALIZADA, sin defecto productivo | Mantener los contratos y no usar 480 como frontera de jornada trabajada. |
| RQ-017 | FER-NAC-01..08 + BD02-D01..D09: feriado ordinario trabajado = base efectiva + 100 %; coincidencia con descanso no duplica concepto; 1 de mayo conserva tratamiento especial; clasificación por fecha de inicio; minutos físicos únicos | Descanso+feriado persiste una base física y clasificación de feriado; 1 de mayo queda estructuralmente distinguido y bloquea una valorización incompleta | ROT-FER-01/02 verdes; cobertura heredada FER-NAC se conserva | CUMPLE PARCIAL | Economía adicional del 1 de mayo, sustitutorio y compensable siguen separados | Modelar esos tres gaps solo con decisión/dato aprobados. |

## Resumen de cumplimiento

| Estado | Cantidad | Requisitos |
|---|---:|---|
| CUMPLE | 15 | RQ-001, RQ-002, RQ-003, RQ-004, RQ-005, RQ-006, RQ-007, RQ-008, RQ-009, RQ-010, RQ-011, RQ-012, RQ-014, RQ-015, RQ-016 |
| CUMPLE PARCIAL | 1 | RQ-017 |
| NO CUMPLE | 0 | — |
| NO IMPLEMENTADO | 1 | RQ-013 |
| NO DETERMINADO | 0 | — |

## Matriz de impacto

`X` indica cambio o verificación probable; `—` indica que no debería intervenir salvo efecto secundario demostrado.

| Req | Domain | Application | Infrastructure | Web | DB | Tests | Reportes |
|---|---:|---:|---:|---:|---:|---:|---:|
| RQ-001 | X | X | X | — | — | X | X |
| RQ-002 | X | X | X | — | — | X | X |
| RQ-003 | X | X | X | — | X | X | X |
| RQ-004 | X | X | X | X | X | X | X |
| RQ-005 | X | X | X | — | — | X | X |
| RQ-006 | X | X | X | — | — | X | X |
| RQ-007 | X | X | X | X | X | X | X |
| RQ-008 | — | X | X | X | X | X | — |
| RQ-009 | X | X | X | — | — | X | X |
| RQ-010 | X | X | X | X | X | X | X |
| RQ-011 | X | X | X | X | — | X | X |
| RQ-012 | X | X | X | X | X | X | X |
| RQ-013 | X | X | X | X | X | X | X |
| RQ-014 | — | X | X | — | X | X | — |
| RQ-015 | — | X | X | X | X | X | X |
| RQ-016 | X | — | X | — | — | X | X |

## Estado de contratos añadidos en Fase 6.2

| Contrato | Estado |
|---|---|
| DOM-01/02/03 | Verdes desde R-P0-03, sin cambiar expectativas. |
| HE-01..05 | Verdes desde R-P0-03, sin cambiar expectativas. |
| HE-DIU-00..07 / HE-NOC-00..05 / canónicos / tardanza-corte / TAR-NOC / SA-NOC / HE-SAB / DOM-04/05 | R-P0-03.1; verdes. Reemplazan HE-LV-01..06 de R-P0-03 («17:23 → 1 HE» quedó obsoleto por el umbral). |
| NOC-WE-01..06 | R-P0-HE.2; verdes. Viernes→sábado no reclasifica por sábado; sábado→domingo segmenta HE25/35 y HE100 en domingo 00:00 sin umbral. |
| REG-01/02/03/04 | Creados; los cuatro pasan. |
| ROT-01/02/03 | Creados; los tres pasan. |
| ROT-DESC-01..09 / ROT-MARK-01..06 / ROT-FER-01/02 / ROT-REP-01 | Verdes desde R-P1-05; 18 contratos nominales. |
| SEC-01/02/03/04 | Verdes; cubren listado/historial, importación, consulta/exportación fuera de alcance y operación autorizada. P0-SEC resuelto. |
| TAR-00..04 / SA-00..04 / TAR-SA-NOC-01 | Verdes; P0-TARD y P0-SA resueltos con tolerancia de planilla 0. |
| LIQ-01..04 / LIQ-HE-NOC-01 | Verdes desde R-P1-01; base física y primas superpuestas quedan reconciliadas sin modificar el tareo diario. |
| PL-01/02 | Verdes desde R-P1-02 con contextos independientes; no se modificaron los 85 grupos históricos. |
| INC-01/02/03 e INC-PARCIAL-01 | Verdes desde R-P1-03; una justificación parcial conserva la anomalía independiente y la bandeja real la lista. |
| VIG-01..05 y VIG-07..09 | Verdes desde R-P1-04; cubren marca, justificación, borrado auditado, versión de turno, feriado, tramo de rotación, recálculo parcial por planta y cambio descriptivo irrelevante. VIG-06 no aplica. |
| AUS-PAG-01..05 | Verdes desde R-P1-08: VAC/LCG completas persisten 480; DIU/NOC no usan 622; no nacen HE ni nocturnidad; permiso parcial no se aplana a 480. |
| IMP-AT-01..07 | Verdes desde R-P1-07: rollback total ante fallo técnico; camino positivo, errores funcionales, hash/dedupe, duplicados y contadores protegidos. |

## Prioridad resultante

- P0: ninguno pendiente en código. RQ-001/002/003 (P0-HE, R-P0-03 + R-P0-03.1), RQ-005/P0-TARD, RQ-006/P0-SA y RQ-015/P0-SEC están resueltos. Acción operativa previa a usar datos reales: reconfigurar DIU/NOC sembrados (VIG-TURNO-01).
- P1 pendiente: RQ-004 y RQ-013. RQ-009/P1-01, RQ-010/P1-02, RQ-011/P1-03, RQ-012/P1-04 y RQ-014/P1-07 están resueltos en código; RQ-016/P1-08 quedó cerrado como requisito ya satisfecho. `DATA-PERIODOLABORAL` conserva separados el saneamiento y la unicidad física. RQ-007 queda cubierto para los flujos caracterizados.
- P2: decisiones de aprobación parcial, borrado físico, refrigerio/nocturnidad y tasas por empresa.
- P3: observabilidad, drift, constantes y retiro PMV.
