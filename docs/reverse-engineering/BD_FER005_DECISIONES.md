# BD-FER005-D01..D08 — decisiones del día no laborable compensable

Fecha: 2026-10-21  
Estado del catálogo: **BLOQUEADO — sin decisiones aprobadas por negocio**  
Alcance de esta fase: análisis y documentación; sin implementación

## Resultado

Las fuentes del proyecto y la normativa consultada permiten separar FER-005 de feriados,
descansos, licencias y permisos, pero no contienen la política privada que el sistema debe
ejecutar. El [Decreto Supremo 042-2025-PCM](https://busquedas.elperuano.pe/dispositivo/NL/2387094-1)
deja el acogimiento del sector privado y la forma de recuperación al acuerdo entre
empleador y trabajadores —o a la decisión del empleador cuando no hay acuerdo—. La
[explicación oficial de Sunafil](https://www.elperuano.pe/noticia/301157-hoy-es-dia-no-laborable-sunafil-precisa-reglas-para-el-sector-publico-privado-y-el-pago-por-feriados)
confirma que no es una medida privada automática.

Por tanto, este documento no suplanta a RR. HH., planilla ni asesoría legal. Presenta
opciones, impacto y una recomendación técnica; la columna «Decisión aprobada» queda vacía
hasta recibir una respuesta humana autorizada.

## Consolidación formal de aprobación — 2026-10-21

Se recibió el protocolo `BD-FER005-APROBACION`, pero no incluyó respuestas atribuidas
expresamente a `RRHH`, `PLANILLA`, `LEGAL` o `NEGOCIO AUTORIZADO`. Una instrucción para
recibir respuestas no constituye una respuesta. Por ello no se aprobó, rechazó ni modificó
ninguna decisión.

Posteriormente se recibió el formulario
[`FER005_CUESTIONARIO_DECISION.md`](FER005_CUESTIONARIO_DECISION.md), también sin completar:
todos los campos `Fuente`, `Respuesta` y aprobación final permanecen vacíos. El formulario
es el instrumento oficial para la siguiente entrada, no evidencia de aprobación.

### Respuestas recibidas

| Fuente autorizada | Cantidad |
|---|---:|
| RRHH | 0 |
| PLANILLA | 0 |
| LEGAL | 0 |
| NEGOCIO AUTORIZADO | 0 |

### Trazabilidad de las 12 preguntas

Las preguntas se conservan literalmente en la sección «Preguntas finales para RR. HH.,
planilla y asesoría legal». Su relación con las decisiones es:

| Pregunta | Decisión asociada | Respuesta autorizada |
|---:|---|---|
| 1 | D01 | No recibida |
| 2 | D01 | No recibida |
| 3 | D02 | No recibida |
| 4 | D03 | No recibida |
| 5 | D03 y D08 | No recibida |
| 6 | D04 | No recibida |
| 7 | D05 | No recibida |
| 8 | D06 | No recibida |
| 9 | D06 y D07 | No recibida |
| 10 | D08 | No recibida |
| 11 | D01/D05 y regla transversal de vínculo | No recibida |
| 12 | D04 y reglas transversales de anulación/auditoría | No recibida |

### Matriz final de aprobación

| ID | Pregunta | Respuesta autorizada | Fuente | Fecha | Estado | Regla resultante | Impacto técnico |
|---|---|---|---|---|---|---|---|
| D01 | ¿Cómo se acredita la adhesión y quién queda alcanzado? | No recibida | — | — | PENDIENTE_NEGOCIO | Ninguna | No puede modelarse el alcance ni decidir quién no genera falta. |
| D02 | ¿La unidad es minutos, horas o jornada? | No recibida | — | — | PENDIENTE_NEGOCIO | Ninguna | No puede fijarse tipo/unidad persistida. |
| D03 | ¿Cómo nace la obligación y qué ocurre si se trabaja en el origen? | No recibida | — | — | PENDIENTE_NEGOCIO | Ninguna | No puede calcularse `MinutosObjetivo`. |
| D04 | ¿Se permite parcialidad y múltiples aplicaciones? | No recibida | — | — | PENDIENTE_NEGOCIO | Ninguna | No puede fijarse cardinalidad, saldo ni exceso. |
| D05 | ¿Qué fechas y plazo son válidos? | No recibida | — | — | PENDIENTE_LEGAL | Ninguna | No puede validarse temporalidad. |
| D06 | ¿Qué precedencia existe entre compensación y HE? | No recibida | — | — | PENDIENTE_LEGAL | Ninguna | No puede repartirse compensación/HE sin riesgo económico. |
| D07 | ¿Son elegibles sábado, domingo y feriado? | No recibida | — | — | PENDIENTE_LEGAL | Ninguna | Se preservan las reglas actuales y no se habilita compensación. |
| D08 | ¿Cuál es la economía del origen y del trabajo realizado? | No recibida | — | — | PENDIENTE_LEGAL | Ninguna | No puede conectarse saldo con liquidación. |

### CONFLICTOS-FER005

No se registraron contradicciones porque no se recibió ninguna respuesta autorizada. Esto
no resuelve las decisiones: ausencia de respuesta significa pendiente, no conformidad.

### CONTRATO_FER005_APROBADO

**Vacío.** No existen reglas nuevas efectivamente aprobadas. Permanecen vigentes únicamente
los invariantes previos: concepto separado, cero minutos físicos y ninguna incidencia de
asistencia para una persona que llegue a estar expresamente alcanzada y no trabaje, y cero
HE100 por el solo rótulo. Esos invariantes no resuelven alcance, obligación, saldo, economía
ni elegibilidad de fechas.

### Reevaluación final de escenarios

| Escenario | Estado | Parte aún necesaria |
|---|---|---|
| Origen sin trabajo | PENDIENTE | Alcance, minutos objetivo y economía; solo asistencia está definida. |
| Origen con trabajo parcial | PENDIENTE | Deuda/remanente, clasificación y economía. |
| Origen con trabajo completo o exceso | PENDIENTE | Deuda, ordinario/HE y economía. |
| Compensación L-V | PENDIENTE | Temporalidad y precedencia con HE. |
| Compensación sábado | PENDIENTE | Elegibilidad y HE25/HE35. |
| Compensación domingo | PENDIENTE | Elegibilidad y HE100. |
| Compensación feriado | PENDIENTE | Elegibilidad y economía propia del feriado. |
| Compensación nocturna | PENDIENTE | Precedencia económica; el bono sigue conceptualmente separado. |
| Compensación parcial/múltiple | PENDIENTE | Cardinalidad, saldo y solapes. |
| Exceso sobre la compensación | PENDIENTE | Corte y categoría de HE. |
| Anulación | PENDIENTE | Autorización, restauración de saldo y efecto en liquidación. |

Resultado: **0 escenarios integralmente resueltos; 11 pendientes**.

## Correspondencia con el dossier inicial

El dossier exploratorio agrupaba cantidad, trabajo en origen, temporalidad, parcialidad y
precedencias con otra numeración. Este catálogo conserva sus ocho problemas, pero adopta
como numeración canónica la solicitada para esta fase:

| Catálogo canónico | Contenido | Antecedente del dossier inicial |
|---|---|---|
| D01 | Adhesión y alcance | D01 |
| D02 | Unidad | parte de D02 |
| D03 | Cantidad inicial y trabajo en origen | D02 + D03 |
| D04 | Parcialidad/cardinalidad | D05 |
| D05 | Fecha y plazo | D04 |
| D06 | Precedencia compensación/HE y exceso | D08 + parte de D06/D07 |
| D07 | Elegibilidad de sábado, domingo y feriado | D06 + D07 |
| D08 | Economía del origen | parte económica de D03 |

## D01 — adhesión y alcance

**Pregunta.** ¿Qué evidencia acredita que la empresa se acogió y cómo se identifica sin
ambigüedad a cada trabajador alcanzado o excluido?

**Contexto.** Una declaración estatal no incorpora automáticamente a la empresa privada;
además, puede haber alcance geográfico o exclusiones por continuidad de servicios. Sin el
alcance, el tareo no puede saber quién deja de generar falta.

**Opciones.**

- A. Toda la organización: simple, pero impide diferencias entre empresas y plantas.
- B. Por empresa: conserva separación societaria, pero no excepciones operativas locales.
- C. Por planta: resuelve alcance territorial, pero no grupos ni puestos esenciales.
- D. Por cuadrilla/grupo: acompaña operación, pero sus integrantes cambian en el tiempo.
- E. Por trabajador: exactitud máxima, con mayor carga administrativa.
- F. Combinación explícita con inclusiones/exclusiones: flexible y auditable, pero exige
  materializar el resultado por persona antes de calcular.

**Recomendación técnica.** F: declaración con empresa/planta/grupo y excepciones, seguida
por una obligación materializada por trabajador usando sus vigencias históricas. Congelar
el alcance cuando afecte un tareo; corregir mediante versión/anulación, no reescribirlo.

**Decisión aprobada.** Ninguna.

**Justificación.** No existe acuerdo interno ni respuesta de negocio en las fuentes.

**Módulos afectados.** Calendario especial, organización, cuadrillas, `PeriodoLaboral`,
tareo, reportes, autorización y auditoría.

**Estado.** `PENDIENTE_NEGOCIO`.

## D02 — unidad de compensación

**Pregunta.** ¿El derecho y su recuperación se controlan en minutos, horas o jornadas
completas?

**Contexto.** El motor persiste minutos enteros. Horas decimales introducen redondeo y una
jornada completa no representa turnos de distinta duración.

**Opciones.**

- Minutos: precisión, suma parcial y compatibilidad con tareo.
- Horas: presentación familiar, pero exige una regla de conversión/redondeo.
- Jornada completa: operación sencilla, pero no soporta duraciones variables ni
  recuperación parcial.

**Recomendación técnica.** Minutos como unidad persistida; mostrar horas solo como valor
derivado.

**Decisión aprobada.** Ninguna.

**Justificación.** La ventaja técnica no equivale a una definición de negocio.

**Módulos afectados.** Dominio, persistencia, UI, reportes y liquidación.

**Estado.** `PENDIENTE_NEGOCIO`.

## D03 — cantidad inicial y trabajo en el origen

**Pregunta.** ¿Cuántos minutos debe recuperar cada trabajador y qué sucede si trabaja total
o parcialmente el día declarado?

**Contexto.** No todos los trabajadores tienen 480 minutos ni la misma programación. Sin
una base definida no puede nacer una obligación ni calcularse saldo.

**Opciones.**

- Minutos teóricos netos de la jornada vigente: reproduce la programación individual,
  pero requiere decidir qué hacer sin turno o en descanso.
- 480 minutos fijos: simple, pero inventa deuda para jornadas distintas.
- Cantidad explícita del acuerdo: fiel al documento, con carga administrativa.
- Regla híbrida: jornada vigente por defecto y cantidad explícita como excepción; requiere
  precedencia y auditoría.

Para trabajo en el origen deben elegirse además una de estas políticas:

- deuda cero si trabajó cualquier cantidad;
- deuda por el remanente no trabajado;
- deuda completa aun cuando trabajó;
- resultado explícito por persona.

**Recomendación técnica.** Cantidad explícita o jornada teórica neta congelada por persona;
nunca 480 implícitos. Si se permite trabajo parcial, usar remanente solo después de definir
qué intervalo constituye la jornada y cómo se clasifica económicamente el trabajo.

**Decisión aprobada.** Ninguna.

**Justificación.** Las fuentes no definen fórmula ni tratamiento del trabajo en origen.

**Módulos afectados.** Turnos/rotaciones, tareo, obligación, saldo, reportes y economía.

**Estado.** `PENDIENTE_NEGOCIO`.

## D04 — parcialidad y cardinalidad

**Pregunta.** ¿Una obligación puede recuperarse por partes y en varias fechas?

**Contexto.** Esta decisión determina si existe `1 origen -> 1 aplicación` o
`1 origen -> N aplicaciones`, y si el sistema necesita saldo vivo.

**Opciones.**

- Solo completa en una fecha: modelo simple, poca flexibilidad y rechaza recuperación real
  fraccionada.
- Parcial en una fecha: permite remanente, pero no distribuirlo.
- Parcial en múltiples fechas: refleja escenarios operativos; requiere saldo, control de
  solapes y concurrencia.

**Recomendación técnica.** Parcial en múltiples fechas, en minutos, con saldo por persona y
origen; cada aplicación debe identificar el intervalo trabajado que consume.

**Decisión aprobada.** Ninguna.

**Justificación.** No existe política interna sobre fraccionamiento.

**Módulos afectados.** Dominio, persistencia, tareo, UI, reportes y concurrencia.

**Estado.** `PENDIENTE_NEGOCIO`.

## D05 — fecha y plazo de recuperación

**Pregunta.** ¿La recuperación puede ser anterior, solo posterior, y dentro de qué plazo?

**Contexto.** Las normas pueden fijar condiciones distintas para cada declaración. La
regla pública de un decreto no debe copiarse automáticamente a la adhesión privada. Admitir
compensación anticipada o sin plazo cambia la evidencia exigida y el cierre de planilla.

**Opciones.**

- Solo posterior, sin plazo técnico.
- Solo posterior, con plazo explícito de la declaración/acuerdo.
- Anterior o posterior, únicamente cuando el acuerdo lo autoriza expresamente.
- Fecha/rango explícitos por cada declaración.

**Recomendación técnica.** Fechas elegibles explícitas en cada declaración; por defecto,
solo posteriores. No codificar un número fijo de días. Permitir anterior únicamente después
de validación legal y texto expreso del acuerdo.

**Decisión aprobada.** Ninguna.

**Justificación.** La política solicitada puede afectar la interpretación del acuerdo y
requiere revisión legal antes de ser una regla de producto.

**Módulos afectados.** Dominio, validación Application, periodos cerrados y auditoría.

**Estado.** `PENDIENTE_LEGAL`.

## D06 — precedencia entre compensación y horas extra

**Pregunta.** Cuando se trabaja fuera del horario para recuperar, ¿los minutos consumen
primero deuda o conservan su clasificación de HE, y desde qué punto existe exceso?

**Contexto.** El motor actual reconoce HE según fecha, turno y corte. Reclasificar
retroactivamente esos minutos podría absorber un derecho económico; mantener ambos sin
definir su relación podría duplicar minutos.

**Opciones.**

- Compensación primero y solo el exceso es HE.
- HE siempre; además reduce saldo como dimensión independiente.
- Compensación solo dentro de una ventana aprobada; fuera de ella sigue la HE normal.
- No permitir compensación fuera de jornada hasta contar con política específica.

**Impacto por día.** En lunes-viernes la primera opción desplazaría el inicio de HE; en
sábado eliminaría HE25/35 vigente; en domingo o feriado podría absorber HE100/economía
especial.

**Recomendación técnica.** Usar una ventana explícitamente autorizada. Dentro de ella,
registrar minutos físicos una sola vez y etiquetar la aplicación de saldo; fuera, aplicar
HE vigente. La decisión de si la etiqueta sustituye o coexiste con la sobretasa requiere
dictamen legal y regla económica escrita.

**Decisión aprobada.** Ninguna.

**Justificación.** Negocio no puede renunciar por configuración a una sobretasa que pudiera
corresponder legalmente.

**Módulos afectados.** Cálculo de tareo, liquidación, reportes, nocturnidad y auditoría.

**Estado.** `PENDIENTE_LEGAL`.

## D07 — sábado, domingo y feriado como fechas de compensación

**Pregunta.** ¿Cuáles de estos días son elegibles y qué tratamiento económico conservan?

**Contexto.** Hoy sábado es íntegramente HE25/HE35, domingo es descanso semanal/HE100 y el
feriado tiene economía propia. No existe una regla que autorice convertirlos en jornada
ordinaria de recuperación.

**Opciones para cada tipo de día.**

- No permitido.
- Permitido y mantiene íntegramente su HE/economía, además de aplicar saldo.
- Permitido y la compensación consume primero minutos, con HE solo en el exceso.
- Otra regla expresa validada legalmente.

**Recomendación técnica.** No permitir sábado, domingo ni feriado como fechas de
compensación en la primera versión. Si negocio necesita alguno, aprobarlo por separado con
su tratamiento económico; nunca heredar una única respuesta para los tres.

**Decisión aprobada.** Ninguna.

**Justificación.** Las tres alternativas afectan reglas laborales y económicas ya cerradas.

**Módulos afectados.** Calendario, tareo, feriados, descansos, liquidación y reportes.

**Estado.** `PENDIENTE_LEGAL`.

## D08 — economía del día origen

**Pregunta.** ¿Mantiene remuneración ordinaria, genera deuda y afecta la liquidación; qué
ocurre económicamente cuando sí se trabaja?

**Contexto.** El día no es feriado y no genera por sí mismo HE100 ni triple remuneración.
Eso no define si la remuneración ordinaria se mantiene, cuándo nace deuda o cómo se liquida
trabajo total/parcial.

**Opciones.**

- Mantiene remuneración ordinaria y genera deuda de minutos no trabajados.
- Mantiene remuneración solo al completar la recuperación.
- No afecta remuneración y la deuda es únicamente operativa.
- Política explícita por declaración/acuerdo.

Si trabaja, deben fijarse de forma separada: deuda cero o remanente, ordinario dentro del
horario y HE fuera de él, y efecto de incumplir el acuerdo.

**Recomendación técnica.** Política económica explícita por declaración, con una opción por
defecto aprobada legalmente. Mantener separados: minutos físicos, saldo de recuperación,
remuneración ordinaria y sobretasas. El saldo no debe descontar pago automáticamente sin
una regla de liquidación aprobada.

**Decisión aprobada.** Ninguna.

**Justificación.** Es una definición laboral/económica de alto impacto no contenida en el
repositorio ni deducible del tratamiento de feriados.

**Módulos afectados.** Tareo, liquidación, reportes, periodos cerrados y auditoría.

**Estado.** `PENDIENTE_LEGAL`.

## Reglas transversales propuestas, aún no aprobadas

### Vínculo laboral

Recomendación: exigir `PeriodoLaboral` vigente en origen y compensación y que ambas fechas
pertenezcan a un vínculo compatible. Sin vínculo en origen no nace obligación; sin vínculo
en compensación se rechaza la aplicación. Estado: **PENDIENTE_NEGOCIO**.

### Saldo

Si se aprueba parcialidad, mantener saldo por trabajador y origen:

```text
saldo = minutos objetivo - suma de aplicaciones vigentes
0 <= saldo <= minutos objetivo
```

Nunca permitir saldo negativo. No recalcular el objetivo desde un turno modificado después.
Estado: **PENDIENTE_NEGOCIO**, condicionado a D02..D04.

### Anulación

Recomendación: toda aplicación puede anularse, sin borrado físico, con actor, instante,
motivo y versión; la anulación restaura exactamente sus minutos al saldo. Estado:
**PENDIENTE_NEGOCIO**.

### Auditoría

Recomendación mínima: creación/versionado/anulación de la declaración y alcance;
materialización de obligación; creación/anulación de aplicaciones; actor, fecha/hora,
motivo y valor anterior/nuevo. Estado: **PENDIENTE_NEGOCIO**.

### Nocturnidad

El bono nocturno permanece como dimensión separada de los minutos físicos, saldo y HE. No
se elimina por registrar compensación. Su convivencia económica exacta queda condicionada
a D06 y D07. Estado: **PENDIENTE_LEGAL**.

## Matriz de escenarios

| Escenario | Decisión requerida | Minutos físicos | Saldo | HE | Bono nocturno | Economía | Estado |
|---|---|---:|---|---|---|---|---|
| Origen, alcanzado, sin trabajo | D01/D03/D08 | 0 | objetivo no definido | 0 por el solo rótulo | 0 | no definida | PARCIAL: sin incidencias aprobado |
| Origen, alcanzado, con trabajo | D03/D08 | minutos reales únicos | cero/completo/remanente por decidir | según horario, pendiente | según franja real | pendiente | PENDIENTE |
| Compensación L-V | D05/D06 | minutos reales únicos | reducción pendiente | precedencia pendiente | se conserva | pendiente | PENDIENTE |
| Compensación sábado | D06/D07 | minutos reales únicos | pendiente | actualmente HE25/35 | se conserva | pendiente | CONFLICTO_REGLA |
| Compensación domingo | D06/D07 | minutos reales únicos | pendiente | actualmente HE100 | se conserva | pendiente | CONFLICTO_REGLA |
| Compensación feriado | D06/D07 | minutos reales únicos | pendiente | economía de feriado | se conserva | pendiente | CONFLICTO_REGLA |
| Compensación nocturna | D06 | minutos reales únicos | pendiente | pendiente | sí, separado | pendiente | PENDIENTE |
| Compensación parcial/múltiple | D02/D04 | minutos aplicados | objetivo menos aplicaciones | pendiente D06 | según tramo | pendiente | PENDIENTE |
| Anulación | D04 + regla transversal | no cambia historia física | restauraría aplicación | no reclasificar sin recálculo controlado | no cambia | pendiente | PENDIENTE |

## Preguntas finales para RR. HH., planilla y asesoría legal

1. ¿Qué documento confirma que la empresa se acoge y quién puede aprobarlo?
2. ¿El día alcanza a toda la empresa, a plantas/grupos concretos o a una lista de personas;
   qué puestos deben excluirse?
3. ¿La deuda se controla en minutos, horas o jornadas completas?
4. ¿Cuántos minutos debe recuperar cada persona: los de su turno, una cantidad indicada en
   el acuerdo u otra regla?
5. Si una persona trabaja parte o todo el día origen, ¿queda sin deuda o recupera solo el
   tiempo que no trabajó?
6. ¿Puede recuperar por partes y en varias fechas?
7. ¿La recuperación debe ser posterior; cuál es el plazo y puede el acuerdo autorizarla
   antes del día origen?
8. En lunes-viernes fuera del horario, ¿la recuperación sustituye la HE, coexiste con ella o
   solo puede hacerse en una ventana especial?
9. ¿Se permite recuperar en sábado, domingo o feriado? Responder por separado e indicar el
   pago/sobretasa de cada caso.
10. ¿El día origen conserva remuneración ordinaria y el saldo pendiente tiene algún efecto
    automático en planilla?
11. ¿Se exige vínculo vigente en ambas fechas y qué ocurre si hay cese/reingreso entre ellas?
12. ¿Quién puede anular una recuperación, qué motivo debe registrar y debe restaurar saldo?

## Modelo futuro recomendado, no aprobado ni implementado

- `DiaNoLaborableCompensable`: declaración, fuente/acuerdo, política, estado, auditoría y
  versión.
- alcance versionado y materializado por trabajador;
- `ObligacionCompensacionDiaNoLaborable`: trabajador, origen y minutos objetivo congelados;
- `AplicacionCompensacionDiaNoLaborable`: obligación, fecha/intervalo, minutos, estado,
  auditoría y versión;
- saldo derivado de aplicaciones vigentes, nunca negativo;
- validación con `PeriodoLaboral` en ambas fechas;
- minutos físicos, saldo, HE, nocturnidad y economía como dimensiones separadas.

No se crearon entidades, configuraciones, `DbSet`, servicios, controladores, vistas,
migraciones ni pruebas.

## Estado de decisión

| Estado | Cantidad | IDs |
|---|---:|---|
| APROBADA | 0 | — |
| RECHAZADA | 0 | — |
| PENDIENTE_NEGOCIO | 4 | D01, D02, D03, D04 |
| PENDIENTE_LEGAL | 4 | D05, D06, D07, D08 |

`FER-005 = BLOQUEADO` hasta obtener decisiones humanas explícitas para D01..D08.
