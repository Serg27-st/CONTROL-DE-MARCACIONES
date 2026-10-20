# Matriz histórica de contratos de aceptación

Fecha de ejecución: 2026-10-07 (corte tras R-P1-05 / ROT-DESC)  
Suite: 803 totales, 803 verdes, 0 rojos, 0 inesperados, 0 omitidos.

> **Corte posterior 2026-10-19:** P0-FER-002/007 añadió 10 contratos; build 0/0 y suite
> 958/958 verdes, 0 fallos, 0 omitidos. El encabezado anterior se conserva como evidencia
> histórica de ROT-DESC.

No quedan contratos rojos. La fila conserva la trazabilidad del último gap cerrado.

| Test | Requisito | Resultado esperado | Resultado actual | Bug/Gap | Prioridad |
|---|---|---|---|---|---|
| ROT-DESC-01 | RQ-004 · descanso explícito sin turno | `TurnoId` opcional para tramo de descanso | Implementado y verde en R-P1-05 | RESUELTO | CERRADO |

## Contratos nuevos que pasan

| Contrato | Resultado |
|---|---|
| ROT-DESC-01..09 / ROT-MARK-01..06 / ROT-FER-01/02 / ROT-REP-01 | Pasan desde R-P1-05; incluyen secuencia finita, dos pasadas de marcas, SQLite real y feriado sin duplicación física. |
| INC-01 · permiso parcial | Pasa desde R-P1-03: la salida faltante conserva incidencia y validación pendiente. |
| INC-02 · tardanza justificada | Pasa desde R-P1-03: justificar tardanza no oculta la salida faltante. |
| INC-03 · regularización parcial | Pasa desde R-P1-03: conserva la incidencia del extremo faltante. |
| INC-PARCIAL-01 · bandeja real | Pasa: una regularización incompleta persiste y se lista como pendiente; una completa deja cero pendientes. |
| PL-01 · vínculo abierto único | Pasa desde R-P1-02: alta y dos ediciones en contextos distintos conservan una fila total y una abierta. |
| PL-02 · cierre por baja | Pasa desde R-P1-02: la baja en un contexto distinto persiste fecha y motivo de cese en el vínculo. |
| SA-01 · salida 17:22 | Pasa: 0 minutos anticipados. |
| REG-01 · olvido de ingreso | Pasa: marca original intacta y corrección separada. |
| REG-02 · olvido de salida | Pasa: marca original intacta y corrección separada. |
| REG-03 · auditoría | Pasa: motivo, actores, fechas, validación y bitácora presentes. |
| REG-04 · histórico | Pasa: intento previo y nueva corrección coexisten sin sobrescribir la marca. |
| ROT-01 · 7 Día/7 Noche | Pasa. |
| ROT-02 · 10 Día/5 Noche | Pasa. |
| ROT-03 · programación futura diferente | Pasa y conserva resolución histórica. |
| SEC-01 · listado/historial PMV | Pasa: solo devuelve equipos e importaciones de plantas autorizadas. |
| SEC-02 · consulta/exportación PMV | Pasa: el backend rechaza el equipo fuera de alcance antes de consultar; exportación reutiliza esa consulta. |
| SEC-03 · importación PMV | Pasa: el backend rechaza el equipo fuera de alcance. |
| SEC-04 · planta autorizada | Pasa: consulta e importación funcionan dentro del alcance. |
| TAR-00..04 · tardanza | Pasan: 06:59/07:00/07:01/07:05/07:17 producen 0/0/1/5/17 minutos. |
| SA-00..04 · salida anticipada | Pasan: 17:23/17:22/17:21/17:17/17:00 producen 0/0/1/5/22 minutos. |
| TAR-SA-NOC-01 · cruce nocturno | Pasa: 19:01 y 06:59 producen 1 minuto de tardanza y 1 anticipado. |
| HE-01..05 · jornada 17:22, sábado, domingo | Pasan; desde R-P0-03.1 con fixture DIU 07:00–17:22 (antes 07:00–19:00): 622 ordinarios; 622 + 120 HE25; sábado 0 ordinarios; domingo 0 ordinarios/25/35. Expectativas sin cambio. |
| DOM-01..03 · domingo al 100 % | Pasan desde R-P0-03: 120, 622 y 480 minutos al 100 %, cero ordinarios/25/35; DOM-03 conserva 480 nocturnos. |
| HE-DIU-00..07 · umbral DIU (R-P0-03.1) | Pasan: 17:22/17:23/17:30/17:36 → 0 HE; 17:37 → 15; 17:38 → 16; 18:22 → 60; 19:22 → 120; ordinarios 622; salida anticipada 0. |
| HE-NOC-00..05 · umbral NOC (R-P0-03.1) | Pasan: 05:22/05:23/05:36 → 0 HE; 05:37 → 15; 05:38 → 16; 06:22 → 60; ordinarios 622. |
| DIU-CANONICO / NOC-CANONICO (R-P0-03.1) | Pasan: tardanza 0, salida anticipada 0, HE 0, 622 ordinarios; el NOC conserva 442 nocturnos. |
| Tardanza y corte (R-P0-03.1) | Pasan: DIU 07:10–17:37 → 10 tardanza, 612 ord., 15 HE; 07:10–17:32 → 0 HE; NOC 19:10–05:37 → 10 tardanza, 15 HE; llegada temprana DIU/NOC → 0 HE. |
| TAR-NOC / SA-NOC (R-P0-03.1) | Pasan: 19:00/19:01/19:05 → 0/1/5; 05:22/05:21/05:17 → 0/1/5. |
| HE-SAB · sábado sin umbral | Pasa: 1 h → 60 HE25; 2 h → 120; 2 h 01 → 120 + 1 HE35; 5 h → 120 + 180; 10 min → 10 HE25; ordinarios 0. |
| DOM-04/05 · domingo | Pasan: con DOM1 el domingo trabajado va a `DescansoSemanal`, 480 al 100 %; 10 min de domingo = 10 al 100 % (sin umbral). |
| NOC-WE-01..06 · cruces nocturnos de fin de semana | Pasan: viernes→sábado conserva jornada regular y umbral posterior a 05:22; sábado→domingo reparte 120 HE25 + 180 HE35 antes de las 00:00 y HE100 desde el primer minuto dominical. |
| LIQ-01..04 / LIQ-HE-NOC-01 · liquidación sin doble base | Pasan desde R-P1-01: nocturnidad aporta solo su prima; descanso/HE100 aporta base y recargo una sola vez; el cruce sábado→domingo se liquida sin duplicar sus 442 minutos nocturnos. |
| FER-001/002/007 · economía de feriados | Pasan desde P0-FER-002/007: derecho ordinario separado, completo/parcial con base + 100 %, 1 de mayo con cuatro/dos componentes y minutos físicos únicos. |
| FER-NAC-05/06 · cruce nocturno de feriado | Pasan: el turno iniciado antes del feriado conserva el día de inicio; el iniciado en feriado mantiene esa clasificación hasta D+1. |
| VIG-01..05 · marca, justificación, borrado, versión y feriado | Pasan desde R-P1-04; los borrados relevantes se detectan con la auditoría existente. |
| VIG-06 · parámetro | No aplica: ningún `ParametroLegal` participa en `ServicioCalculoTareo`. |
| VIG-07/08/09 · rotación, recálculo parcial y control irrelevante | Pasan desde R-P1-04; VIG-07 cubre ahora el cambio a descanso programado. |
| IMP-AT-01..07 · importación principal atómica | Pasan desde R-P1-07: el fallo técnico revierte cabecera, errores, marcas, serie aprendida y auditoría; éxito, errores funcionales, dedupe y contadores se preservan. |

## P0 y suficiencia contractual

| P0 | Requisito | Contrato | Archivos afectados probables | Criterio de terminado | Suficiente |
|---|---|---|---|---|---|
| P0-HE | RQ-001/002/003 | HE-01..05, DOM-01..05, HE-DIU, HE-NOC, canónicos, HE-SAB | `CalculoTareo.cs` | Todos verdes sin regresiones | **RESUELTO EN CÓDIGO** (R-P0-03 + R-P0-03.1). Pendiente operativo: reconfigurar DIU/NOC sembrados (VIG-TURNO-01). LIQ-04 está verde desde R-P1-01. |
| P0-TARD | RQ-005 | TAR-00..04 verdes | `CalculoTareo.cs`, configuración de turno | 0/0/1/5/17 exactos; tolerancia de planilla 0 | **RESUELTO** |
| P0-SA | RQ-006 | SA-00..04 verdes | `CalculoTareo.cs`, configuración de turno | 0/0/1/5/22 exactos; tolerancia de planilla 0 | **RESUELTO** |
| P0-SEC | RQ-015 | SEC-01..04 verdes | catálogo, servicios/controlador PMV, proveedor de alcance | Admin A no lista/importa/consulta/exporta B y opera A normalmente | **RESUELTO** |
