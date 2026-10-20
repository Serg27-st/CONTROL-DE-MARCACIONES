# Deuda y hallazgos clasificados

Este catálogo evita llamar “deuda técnica” a todo hallazgo. La prioridad expresa impacto; la categoría expresa naturaleza. La remediación detallada vive en [REMEDIATION_PLAN.md](REMEDIATION_PLAN.md).

## BUG

| ID | Prioridad | Hallazgo | Evidencia / salida |
|---|---:|---|---|
| BUG-01 | ~~P0~~ **RESUELTO** | El motor limitaba a 480 y no aplicaba el corte 17:22, el sábado íntegramente extra ni el domingo íntegramente al 100 %. | R-P0-03: HE-01..05 y DOM-01..03 verdes; LIQ-04 quedó verde con R-P1-01. |
| BUG-02 | P0 | Tardanza de planilla usa tolerancia configurada de 5; debe contar desde 07:01 con tolerancia 0. | TAR-02/03 fallan; TAR-01 pasa. |
| BUG-08 | P0 | Salida anticipada usa tolerancia configurada/sembrada de 5; debe contar minuto a minuto con tolerancia 0. | SA-01 pasa; SA-02/03 fallan y confirman la contradicción con RQ-006. |
| BUG-03 | ~~P1~~ **RESUELTO** | Liquidación sumaba base ordinaria y base nocturna/no laborable superpuesta. | R-P1-01: LIQ-03/04 y LIQ-HE-NOC-01 verdes; resultados diarios intactos. |
| BUG-04 | P1 | Crear y editar trabajador puede insertar varios `PeriodoLaboral` abiertos. | PL-01 falla; 85 grupos físicos. |
| BUG-05 | P1 | La baja no cierra de forma persistida el vínculo laboral activo. | PL-02 falla. |
| BUG-06 | ~~P1~~ **RESUELTO** | Una justificación parcial podía suprimir una incidencia aún no resuelta. | R-P1-03: INC-01..03 e INC-PARCIAL-01 verdes; bandeja real cubierta. |
| BUG-07 | ~~P1~~ **RESUELTO** | La alerta de frescura podía declarar actualizado un tareo con fuentes omitidas o recálculo parcial. | R-P1-04: VIG aplicables verdes; `MIN(CalculadoEn)` por alcance y borrados vía auditoría. |

## GAP FUNCIONAL

| ID | Prioridad | Hallazgo | Decisión requerida |
|---|---:|---|---|
| GAP-01 | ~~P0~~ **RESUELTO en el motor** | La regla 07:00–17:22 y el tratamiento de sábado y domingo no estaban expresados como contrato de calendario. | R-P0-03 la implementa. Siguen pendientes: el corte del nocturno L–V y la reliquidación de periodos cerrados. |
| GAP-02 | ~~P1~~ **RESUELTO EN CÓDIGO** | `TramoRotacion` representa explícitamente Trabajo/Descanso con FK nullable e invariantes de dominio y SQLite; el resolver distingue Trabajo, DescansoProgramado, SinProgramacion y ErrorConfiguracion. | R-P1-05 implementado: secuencia finita, delimitación BD-01, precedencia BD-02 y contratos ROT-DESC/ROT-MARK/ROT-FER verdes. La migración está versionada, pero aún no se aplicó a la base canónica. |
| GAP-03 | P1 | El reporte cerrado no congela todos los datos maestros/insumos. | Snapshot o resolución histórica. |
| GAP-04 | P2 | Aprobación parcial de solicitudes y composición de regularizaciones no están completamente definidas. | Matriz de negocio. |
| GAP-05 | P2 | Refrigerio, nocturnidad y tasas por empresa conservan preguntas abiertas. | Confirmación de planilla. |
| GAP-06 | ~~P1~~ **CERRADO / NO ERA GAP** | R-P1-08 confirmó que la ausencia/licencia pagada completa debe acreditar `MinutosJornadaOrdinaria` = 480, mientras la jornada trabajada DIU/NOC puede dar 622 ordinarios. | Requisito vigente: 8 horas efectivas sin almuerzo; AUS-PAG-01..05 verdes, sin cambio productivo. |

## DEUDA TÉCNICA

| ID | Prioridad | Hallazgo | Consecuencia |
|---|---:|---|---|
| DT-01 | ~~P1~~ **RESUELTO** | R-P1-07 conserva los `SaveChanges` necesarios dentro de una transacción SQLite que incluye serie aprendida, cabecera/errores, marcas y auditoría. | IMP-AT-01..07 verdes; rollback completo y excepción propagada. |
| DT-02 | P2 | No hay proyecto de pruebas Web para autorización y rutas HTTP. | P0 de PMV no tiene prueba extremo a extremo. |
| DT-03 | P2 | `PeriodoLaboral` y programación abierta dependen de guardias de servicio en vez de protección física suficiente. | Concurrencia o rutas alternativas pueden duplicar. |
| DT-04 | P3 | Constantes de dominio están dispersas: 480, 360/6 h, 22:00, 06:00, 5 min, 20 MB, 30/40 días. | Semántica y dueño difíciles de rastrear. |
| DT-05 | P3 | Observabilidad de importaciones, recálculos y exportaciones es limitada. | Diagnóstico operativo costoso. |
| DT-06 | P3 | PMV carece de criterio técnico ejecutable de retiro. | Provisionalidad indefinida. |
| DT-07 | P3 (candidato) | Desde R-P0-03.1 el corte L–V sale de `FinTurno`. `MinutosJornadaOrdinaria` sigue usándose en feriado/descanso fuera de domingo y en el crédito de ausencia pagada, y permanece en BD, UI y semilla. R-P1-08 confirmó 480 para este último uso. El umbral de 15 min es constante del motor. | El campo editable mezcla usos y ya no gobierna los días laborables L–V; proteger el 480 confirmado sin reabrir la regla funcional. |

Nota de clasificación: Fase 5 la registró como P2-01; la reconciliación explícita de Fase 6.1 la eleva a **P1-07**. Se conserva el antecedente sin mantener la prioridad anterior.

## SEGURIDAD

| ID | Prioridad | Hallazgo | Riesgo |
|---|---:|---|---|
| SEC-01 | P0 | PMV no aplica `IProveedorAlcancePlantas` en listar, importar, consultar y exportar. | Un administrador puede cruzar datos de plantas. |
| SEC-02 | P2 | La cobertura automatizada de autorización está concentrada en servicios, no en controladores/políticas Web. | Regresión de rutas sin detección. |
| SEC-03 | P2 | Secretos y configuración de despliegue requieren disciplina externa al repositorio. | Exposición por empaquetado o configuración local. |

## PROBLEMA DE DATOS

| ID | Prioridad | Hallazgo | Tratamiento |
|---|---:|---|---|
| DATA-01 | P1 | 85 grupos de `PeriodoLaboral` abierto duplicado, 170 filas en total. | Reparación separada, auditable, después del fix preventivo. |
| DATA-02 | P2 | La base canónica no contiene muestra representativa del flujo principal de marcas/tareo/rotación. | No extrapolar resultados productivos; obtener dataset autorizado. |
| DATA-03 | P2 | Puede haber tareos considerados vigentes tras mutaciones no observadas por la alerta. | Identificar y marcar periodos afectados. |
| DATA-04 | Previo a uso real | Semilla/base: DIU 07:00–19:00 y NOC 19:00–07:00 (`Turno` y `VersionTurno` desde 2000-01-01) frente a 07:00–17:22 / 19:00–05:22 vigentes. | CONFIGURACIÓN DE DATOS REQUIERE PLAN DE ACTUALIZACIÓN: nueva versión con «Rige desde» y recálculo de periodos abiertos; sin migración ni reescritura histórica (OP-01, **validado en copia**; pendiente FechaRigeDesde). |
| DEPLOY-01 | P2 | Una instalación nueva crea la base con la semilla DIU 07:00–19:00 y NOC 19:00–07:00. | Decidir migración con versión nueva o paso de instalación documentado; no resuelto en OP-01. |

## RIESGO

| ID | Prioridad | Riesgo | Estado |
|---|---:|---|---|
| RSK-01 | P1 | Reemisión histórica puede cambiar por maestros actuales. | Confirmado como riesgo; mitigación no diseñada. |
| RSK-02 | P1 | Migrar el motor a `PeriodoLaboral` sin sanear la divergencia acumulada alteraría resultados. | No usar esa tabla como fuente todavía. |
| RSK-03 | ~~P2~~ **MITIGADO EN CÓDIGO** | El fallo entre escrituras de importación podía dejar estado parcialmente grabado. | R-P1-07 impide nuevos huérfanos; no se inventarió ni reparó historia en esta intervención. |
| RSK-04 | P2 | Baseline PMV sintético, de una ejecución y con arranque frío en 1 día, puede interpretarse como SLA. | Usarlo solo como referencia comparativa. |
| RSK-05 | P2 | Feriados externos introducen disponibilidad y reproducibilidad dependientes de sincronización. | Persistencia local reduce, no elimina, el riesgo. |
| RSK-06 | ~~P1~~ **MITIGADO EN CÓDIGO** | Un fallo de migración podía dejar el proceso atendiendo rutas funcionales sobre un esquema parcial. | NOT-READY conserva solo `/vivo` y `/estado`; el resto responde 503 hasta recuperar compatibilidad. |

## DOCUMENTACIÓN

| ID | Prioridad | Hallazgo | Estado Fase 6 |
|---|---:|---|---|
| DOC-01 | P3 | Comentarios antiguos sostienen 480 como frontera universal o cinco minutos de tolerancia de tardanza/salida. | No reintroducirlos para jornada trabajada; 480 sí es vigente para licencia/ausencia pagada completa desde R-P1-08. |
| DOC-02 | P3 | Comentarios de reporte mencionan tres hojas o AO:BF frente a cuatro hojas y A:BE observadas. | Contradicción registrada; código/aceptación mandan. |
| DOC-03 | P3 | Documentación por fases era extensa para contexto cotidiano. | Resuelta con dossier, estado, mapa IA y fichas. |
| DOC-04 | P3 | 14+1+7 podía leerse como ciclo universal. | Consolidado como patrón observado, no universal. |
| DOC-05 | P3 | “Domingo como feriado/no laborable” no expresa que todo minuto va al 100 %. | Reemplazado por RQ-003. |

## Orden recomendado

1. P0: BUG-01, BUG-02, BUG-08 y SEC-01 antes de usar resultados para pago o abrir PMV a administradores.
2. P1: BUG-03..07, DATA-01 y GAP-03 según su estado individual; GAP-02 y DT-01 quedaron
   resueltos por R-P1-05 y R-P1-07 respectivamente. REG-01..04 cierran
   RQ-007 para el flujo estándar; R-P1-03 resolvió BUG-06 y la política general de
   composición diaria sigue en GAP-04.
3. P2: cobertura Web, reglas abiertas y vigencia de datos.
4. P3: constantes, observabilidad, retiro PMV y limpieza documental.

No mezclar reparación de datos con cambio de código: primero impedir nuevos duplicados, después sanear con bitácora y reconciliación.
