# P1-DATA-PERIODOLABORAL — Revisión humana de Fase 3B

Fecha: 2026-10-12  
Resultado: **0 casos pendientes en la copia analizada**  
Alcance: copia controlada no productiva; no autoriza saneamiento ni migración productiva

## Manifiesto

No se encontró ningún caso que deba remitirse a negocio en esta copia. Por tanto, el manifiesto
individual requerido (`ID`, `TrabajadorId`, problema, evidencia, dato faltante, pregunta,
opciones y consecuencias) está vacío de forma deliberada, no omitido.

| Clase | Casos | Resultado |
|---|---:|---|
| Nivel B de planta | 0 | Sin propuestas pendientes de aprobación |
| Nivel C de planta | 0 | Sin inferencias plausibles pendientes |
| Nivel D de planta | 0 | Sin periodos sin evidencia o con conflicto |
| Reingreso mal representado | 0 | No detectado tras deduplicación semántica |
| Cambio de empresa ambiguo | 0 | No detectado |
| Solapamiento material | 0 | No queda ninguno al retirar copias exactas |
| Trabajador sin periodo | 0 | No requiere creación histórica |
| Fecha imposible | 0 | No detectada |
| Campo plano ambiguo | 0 | Los cinco campos comparados coinciden |

## Casos automáticos excluidos de este manifiesto

Los identificadores `SAN-001` a `SAN-307` están documentados en
`P1_DATA_PERIODOLABORAL_FASE3B_AUTO_FIX.csv`. Son grupos de duplicados técnicos
inequívocos y no casos humanos: comparten toda la semántica del vínculo, no tienen referencias
operacionales por FK, conservan su auditoría textual y tienen una regla estable de supervivencia.

## Evidencia de planta

Los 352 periodos canónicos alcanzaron Nivel A:

- los 352 tienen una `AsignacionArea` temporal que cubre el periodo completo y resuelve una sola
  planta mediante `Area.PlantaId`;
- 241 cuentan además con `AsignacionMarcador` temporal y `EquipoMarcador.PlantaId`; no existe
  conflicto con la fuente de área;
- la planta resuelta coincide con `Trabajador.PlantaId` en 352/352, pero el campo plano se usó
  solo para contrastar, nunca para decidir el backfill;
- no había `MarcacionCruda` ni `TareoDiario` en la copia; no se presentaron como evidencia.

## Condición de reapertura

Este manifiesto solo describe la copia cuyo SHA-256 inicial es
`642550DA9E8697A607391ED5D40E576710FED60AE191DD579202A03FBAB9F928`. Si otra base presenta
un Nivel B/C/D, diferencias materiales, referencias operacionales, periodos sin evidencia o
solapamientos que sobrevivan a la deduplicación exacta, el saneamiento debe abortar y generarse
un caso individual antes de modificarlo.

