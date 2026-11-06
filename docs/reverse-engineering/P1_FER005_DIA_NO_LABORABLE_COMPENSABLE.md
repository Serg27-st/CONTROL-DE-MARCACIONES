# P1-FER-005 — día no laborable compensable

Fecha de análisis: 2026-10-21  
Estado: **PARCIAL — BLOQUEADO PARA IMPLEMENTACIÓN**  
Producción: **no tocada**  
Migración productiva: **no ejecutada**

> Actualización 2026-10-21: el catálogo formal de opciones, impactos y preguntas está en
> [`BD_FER005_DECISIONES.md`](BD_FER005_DECISIONES.md). No se recibieron decisiones humanas
> de RR. HH./planilla ni validación legal: D01..D04 están `PENDIENTE_NEGOCIO` y D05..D08,
> `PENDIENTE_LEGAL`. La fase `BD-FER005-APROBACION` fue reconciliada con 0 respuestas
> autorizadas y 0/8 decisiones aprobadas. FER-005 continúa bloqueado.

## Resultado ejecutivo

`FER-005` no puede cerrarse de forma correcta con las reglas hoy aprobadas. El requisito
vigente solo confirma que un día no laborable compensable es distinto de un feriado; no
define la adhesión concreta de la empresa privada, el alcance humano, la cantidad adeudada
ni la precedencia de la compensación frente a las reglas actuales de sábado, domingo y
feriado.

Crear ahora tablas, saldos o reparto de minutos convertiría decisiones pendientes en reglas
ocultas. Por esa razón esta fase no modifica código productivo, esquema, tareo ni reportes.
Deja especificado qué sí se conoce, qué existe actualmente y cuáles son las decisiones
necesarias para construir contratos ejecutables sin inventar comportamiento laboral.

## FER-005 en las fuentes autoritativas del proyecto

| Fuente | Evidencia actual |
|---|---|
| `BUSINESS_RULES_FINAL.md` | `Feriado nacional != día no laborable compensable`; clasificación expresa no modelada; estado `VIGENTE_NO_IMPLEMENTADA`. |
| `REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md` | No existe clasificación propia; requiere catálogo, persistencia y tratamiento separado; prioridad P1; estado `NO_IMPLEMENTADO`. |
| `REQUIREMENTS_TRACEABILITY.md` | Mantiene FER-005 como gap separado y condiciona su modelado a una decisión/dato aprobado. |
| `BUSINESS_RULES.md` | No aporta fórmula de saldo, alcance privado, fraccionamiento ni precedencia con HE. |
| `PROJECT_STATE.md` | No registra un cierre ni una implementación previa de FER-005. |

Definición exacta disponible hoy:

> Un día no laborable compensable es una suspensión excepcional de la jornada que solo
> afecta a las personas expresamente alcanzadas y cuya recuperación, cuando corresponda,
> debe quedar definida y registrada. No es feriado, descanso semanal, descanso programado,
> licencia ni permiso.

La definición anterior separa conceptos, pero todavía no constituye una política de cálculo.

## Contraste normativo

El [Decreto Supremo 042-2025-PCM](https://busquedas.elperuano.pe/dispositivo/NL/2387094-1)
declara fechas compensables para el sector público. Para el sector privado establece una
facultad de acogimiento previo acuerdo entre empleador y trabajadores y deja a ese acuerdo
la forma de recuperación; a falta de acuerdo, decide el empleador. También permite excluir
puestos indispensables en determinados servicios.

La explicación oficial de Sunafil publicada por
[El Peruano](https://www.elperuano.pe/noticia/301157-hoy-es-dia-no-laborable-sunafil-precisa-reglas-para-el-sector-publico-privado-y-el-pago-por-feriados)
reitera que la medida no es obligatoria en el sector privado y que su aplicación y
recuperación dependen del acuerdo. Además, existen declaraciones con alcance geográfico
particular, como el caso APEC para Lima Metropolitana, Callao y Huaral documentado por
[El Peruano](https://www.elperuano.pe/noticia/257628-peru-decreta-dias-no-laborables-obligatorios-por-apec-publico-y-privado).

Consecuencia de diseño: no existe una regla universal segura equivalente a
`fecha publicada -> todas las plantas -> deuda de 480 minutos`. La fuente normativa puede
ser un antecedente, pero la aplicabilidad privada, el alcance y la recuperación deben ser
hechos explícitos del sistema.

## Inventario del modelo actual

No existe un concepto reutilizable semánticamente correcto:

- `Feriado` representa el calendario de feriados y activa su tratamiento propio; no debe
  recibir un indicador genérico para FER-005.
- `TipoDiaNoLaborable` solo contiene `Ninguno`, `DescansoSemanal` y `Feriado`.
- `DescansoSustitutorio` enlaza un feriado efectivamente trabajado con un día completo de
  descanso posterior. Su origen, cardinalidad y efecto económico son distintos.
- `ReintegroTareo.HorasNoLaborable` es un ajuste manual de reporte; no representa alcance,
  origen, deuda, aplicación de minutos ni auditoría de una compensación.
- faltas, permisos, licencias, rotaciones y marcaciones no constituyen una declaración de
  día no laborable compensable.

El motor confirma además tres reglas que chocan con una compensación implícita:

- sábado: todo minuto trabajado inicia como HE25/HE35;
- domingo: todo minuto trabajado corresponde al descanso semanal/HE100;
- feriado: conserva su clasificación y valorización propia.

Por tanto, tampoco es correcto reutilizar una categoría existente del tareo para ocultar
minutos de compensación.

## Comportamiento seguro ya determinado

Solo pueden fijarse estos invariantes sin completar la política:

1. La mera publicación de una fecha no incorpora automáticamente a la empresa privada ni a
   todos sus trabajadores.
2. Para una persona expresamente alcanzada que no trabaja en el día origen:
   - minutos físicos trabajados = 0;
   - no genera falta, tardanza ni salida anticipada;
   - no genera HE100 por el solo carácter no laborable.
3. No se asignan automáticamente 480 minutos ordinarios ni 480 minutos de deuda.
4. Ninguna marcación futura se convierte por sí sola en compensación.
5. Toda aplicación debe enlazar persona, día origen, fecha de compensación y minutos.
6. Debe existir vínculo según `PeriodoLaboral` tanto en el origen como en la compensación.
7. La nocturnidad es una dimensión separada y no desaparece por tratarse de compensación.
8. No se infiere ni se convierte historia existente.

Quedan **NO_DETERMINADOS** el pago ordinario del origen, la fórmula de minutos a compensar y
la clasificación de los minutos recuperados. Sin esas tres decisiones no hay un saldo
económica ni operativamente correcto.

## Catálogo exploratorio inicial — SUPERADO EN NUMERACIÓN

Esta sección conserva el análisis que originó los ocho bloqueos. Su numeración quedó
**SUPERADA** por el catálogo canónico `BD_FER005_DECISIONES.md`: allí D02 separa unidad de
cantidad, D03 reúne cantidad y trabajo en origen, y D05..D08 ordenan temporalidad,
precedencia, días especiales y economía. No interpretar los estados históricos siguientes
como decisiones aprobadas.

### BD-FER005-D01 — adhesión y alcance

Definir cómo se prueba que la empresa se acogió y a quién alcanza:

- toda la organización;
- una empresa;
- una o más plantas;
- puestos/grupos concretos;
- trabajadores incluidos o excluidos expresamente.

También debe definirse quién puede aprobar ese alcance y si una declaración puede cambiarse
después de que existan tareos. Estado: **NO_DETERMINADO**.

### BD-FER005-D02 — minutos del día origen

Definir la base de la obligación: minutos teóricos del turno, minutos ordinarios netos de
refrigerio, jornada contractual u otra magnitud. Debe cubrir turnos variables, rotaciones,
descanso programado y ausencia de horario, sin asumir ocho horas. Estado:
**NO_DETERMINADO**.

### BD-FER005-D03 — trabajo en el día origen

Definir qué ocurre si una persona alcanzada trabaja total o parcialmente ese día: si no
nace deuda, si nace por el remanente no trabajado, y cómo se clasifican los minutos
efectivos. La única regla cerrada es que no se convierten en feriado ni HE100 por el solo
nombre del día. Estado: **NO_DETERMINADO**.

### BD-FER005-D04 — temporalidad

Definir el plazo de recuperación y si se admite programación anterior al origen. En el
sector privado la forma depende del acuerdo aplicable; el proyecto no conserva hoy ese
acuerdo. Hasta aprobarlo no se aceptará ni rechazará por código una fecha anterior.
Estado: **NO_DETERMINADO**.

### BD-FER005-D05 — parcialidad y cardinalidad

Definir si un origen puede compensarse parcialmente y en una o varias fechas, así como el
redondeo y el tratamiento de un remanente. Estado: **NO_DETERMINADO**.

### BD-FER005-D06 — precedencia con sábado

El sábado es íntegramente HE25/HE35 en el sistema vigente. Transformar sus primeros minutos
en compensación ordinaria modificaría una regla cerrada. Debe decidirse si el sábado es
fecha elegible y cuál concepto prevalece. Estado: **CONFLICTO_REGLA**.

### BD-FER005-D07 — domingo y feriado

Definir si pueden elegirse y, de ser así, si la compensación coexiste con o cede ante el
descanso semanal/feriado. Ninguna de estas categorías puede degradarse silenciosamente a
tiempo ordinario. Estado: **CONFLICTO_REGLA**.

### BD-FER005-D08 — exceso sobre la compensación

Después de fijar la ventana o cantidad compensable, definir desde qué instante el exceso
vuelve a HE25/HE35/HE100 y cómo se cruza con el turno y la franja nocturna. Estado:
**NO_DETERMINADO**.

## Modelo objetivo condicionado, no implementado

Cuando se aprueben las decisiones anteriores, el mínimo modelo separado sería:

```text
DiaNoLaborableCompensable
  Fecha
  Ambito explícito
  Descripcion
  Fuente/Acuerdo
  Estado
  auditoría y versión

ObligacionCompensacionDiaNoLaborable
  DiaOrigenId
  TrabajadorId
  MinutosObjetivo
  Estado

AplicacionCompensacionDiaNoLaborable
  ObligacionId
  FechaCompensacion
  MinutosAplicados
  Estado
  auditoría y versión
```

La obligación por trabajador evita que un cambio posterior de planta o turno reinterprete
el pasado. Las aplicaciones separadas permiten trazabilidad `1 origen -> N aplicaciones`
si y solo si BD-FER005-D05 aprueba la parcialidad. El esquema definitivo no debe crearse
antes de decidir el alcance: una relación exclusiva con `PlantaId` sería insuficiente para
exclusiones individuales, mientras que una tabla por trabajador podría materializar de
forma prematura un alcance todavía no acordado.

Saldo futuro, condicionado a esas decisiones:

```text
Saldo = MinutosObjetivo - SUM(MinutosAplicados vigentes)
0 <= Saldo <= MinutosObjetivo
```

La unicidad, el control optimista de versión y una transacción deberán impedir doble POST,
solape de aplicaciones y saldo negativo. Esta fórmula no autoriza aún cómo calcular
`MinutosObjetivo` ni qué marcaciones son elegibles.

## Contratos solicitados

| Contrato | Estado | Motivo |
|---|---|---|
| FER005-01 | ESPECIFICABLE | Para una persona explícitamente alcanzada, ausencia en origen no produce falta/tardanza/salida anticipada. |
| FER005-02 | ESPECIFICABLE | El rótulo no genera HE100 ni tratamiento de feriado. |
| FER005-03 | BLOQUEADO | No existe unidad/fórmula ni regla de aplicación aprobada. |
| FER005-04 | BLOQUEADO | La definición de “mismos minutos” depende de la ventana/precedencia pendiente. |
| FER005-05 | ESPECIFICABLE | Sin `PeriodoLaboral` en origen no existe obligación aplicable. |
| FER005-06 | ESPECIFICABLE | Sin `PeriodoLaboral` en compensación se rechaza. |
| FER005-07 | BLOQUEADO | Restaurar saldo exige primero aprobar saldo, parcialidad y estados. |
| FER005-08 | BLOQUEADO | El corte entre compensación y HE depende de BD-FER005-D06..D08. |

No se añadieron pruebas rojas deliberadas: sin entradas de dominio aprobadas, esos tests
fijarían el mismo supuesto que deben evitar.

## Impacto por capa

| Capa | Estado de esta fase |
|---|---|
| Domain | Sin cambios; no se amplía `TipoDiaNoLaborable`. |
| Application | Sin cambios; no se crean comandos ni saldos incompletos. |
| Infrastructure | Sin cambios; no hay migración ni DML/backfill. |
| Web | Sin cambios; una pantalla no podría validar una política inexistente. |
| Tareo | Sin cambios; conserva sábado, domingo, feriado y minutos físicos actuales. |
| Reportes | Sin cambios; no se mezcla FER-005 con feriados o reintegros. |

## Criterio para reanudar implementación

Se requiere una decisión aprobada para BD-FER005-D01..D08, idealmente acompañada por un
ejemplo completo por cada combinación habilitada. Como mínimo:

```text
declaración/acuerdo privado
→ alcance verificable
→ trabajador y turno del día origen
→ minutos objetivo
→ fecha(s) elegibles
→ reparto compensación/HE
→ resultado esperado en tareo y liquidación
```

Con esa entrada, la implementación puede ser aditiva, auditable, sin backfill y con pruebas
de dominio, SQLite, web, tareo y reportes antes de generar una migración local.

## Estado final

`FER-005 = PARCIAL — BLOQUEADO PARA IMPLEMENTACIÓN POR DECISIONES DE NEGOCIO`.

La separación conceptual y el límite seguro quedaron cerrados. No se tocó producción, no
se aplicó la migración local de FER-003, no se ejecutó OP-03 y no se creó commit.
