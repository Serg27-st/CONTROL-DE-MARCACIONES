# P0-FER-002 + P0-FER-007 — cierre legal/económico de feriados

Fecha: 2026-10-19  
Estado: **CERRADO EN CÓDIGO Y CONTRATOS**

## Alcance cerrado

`FER-002` exigía que el feriado trabajado sin descanso sustitutorio conservara una sola
cantidad física y expusiera tres componentes económicos: remuneración ordinaria del feriado,
base por la labor efectuada y sobretasa del 100 %. El trabajo parcial debía prorratear solo la
base laborada y su recargo.

`FER-007` exigía que el 1 de mayo coincidente con descanso semanal/programado expusiera dos
componentes diarios si no se trabaja y cuatro si se trabaja, sin duplicar la base física.

## Evidencia normativa contrastada

- Decreto Legislativo 713, arts. 8 y 9:
  <https://leyes.congreso.gob.pe/Documentos/DecretosLegislativos/00713.pdf>.
- Decreto Supremo 012-92-TR, art. 9:
  <https://www2.congreso.gob.pe/sicr/cendocbib/con4_uibd.nsf/0D48067F03F11D1405257E23005CE18D/%24FILE/2_DECRETO_SUPREMO_012_03_12_92.pdf>.
- Explicación operativa oficial del MTPE para los cuatro/dos componentes del 1 de mayo:
  <https://www.gob.pe/institucion/mtpe/noticias/1386502-sabes-cuanto-te-corresponde-de-pago-por-trabajar-el-dia-del-trabajo-descubre-como-calcular-tu-pago>.

La fórmula implementada coincide con las reglas internas FER-NAC-01..08 y BD02-D01..D09.

## Inventario y cambio mínimo

| Archivo | Responsabilidad confirmada | Cambio |
|---|---|---|
| `Domain/Asistencia/CalculoTareo.cs` | clasificación física por fecha de inicio | persiste `Feriado` también sin trabajo e identifica 1 de mayo coincidente con descanso semanal, incluido domingo |
| `Application/Asistencia/SobretasaDtos.cs` | contrato de valorización sin dinero | separa minutos físicos, base económica, recargo y derechos remunerados en días-persona |
| `Infrastructure/.../ServicioCalculoTareo.cs` | agregación y tasas vigentes | retira el bloqueo del 1 de mayo; emite componentes ordinarios y adicionales sin duplicar HE100 |
| `Web/Views/Asistencia/Index.cshtml` | reporte auditable de liquidación | muestra derechos diarios, horas físicas, base, recargo, equivalente y base legal por separado |

No se agregó persistencia, migración ni cálculo monetario. El sistema no conoce remuneraciones:
entrega bases auditables para planilla.

## Contrato económico resultante

### Feriado ordinario

- No trabajado: un derecho de remuneración ordinaria; cero minutos trabajados y cero HE100.
- Trabajado completo: un derecho ordinario incluido en la remuneración base + N minutos de
  labor + N minutos equivalentes de recargo al 100 %.
- Trabajado parcialmente: la base y el recargo usan exactamente los minutos físicos reales.
- Feriado + descanso: prevalece `FeriadoTrabajado`; `DescansoSemanalTrabajado` queda en cero
  sobre esos mismos minutos.

### 1 de mayo coincidente con descanso

- Sin trabajo: remuneración por descanso semanal incluida en base + remuneración adicional
  del 1 de mayo; cero minutos físicos.
- Con trabajo: los dos componentes anteriores + base de la labor + recargo del 100 %.
- Los derechos diarios no se convierten a horas ni se suman a `TotalHorasEquivalentes`, porque
  no existe turno teórico en el descanso y hacerlo inventaría minutos.

## Contratos ejecutables

- `FeriadosEconomiaAcceptanceTests`: 5/5 verdes. Cubre coincidencia del 1 de mayo,
  parcialidad y ambos sentidos del cruce nocturno.
- `LiquidacionPeriodoAcceptanceTests`: 10/10 verdes. Incluye feriado completo/parcial/no
  trabajado y 1 de mayo trabajado/no trabajado sobre SQLite real.
- Suite completa: **958/958 verdes**, 0 fallos, 0 omitidos.
- Build Release: **0 errores, 0 advertencias**.

## Deuda separada que permanece abierta

- `FER-003`: **SUPERADO** el 2026-10-20 por `P1_FER003_DESCANSO_SUSTITUTORIO.md`;
  existe relación persistida, flujo autorizado y efecto económico sin duplicar minutos.
- `FER-005`: el día no laborable compensable sigue sin clasificación propia.
- El sistema no calcula dinero; la remuneración diaria pertenece a planilla y se multiplica
  fuera de este módulo por las bases aquí emitidas.

## Límites operativos

Producción no tocada. OP-03 no ejecutado. Migraciones productivas no aplicadas. Sin commit.
