# BD-ASIS001-FECHARIGEDESDE — fecha efectiva de DIU/NOC

Fecha de revisión: 2026-10-22  
Decisión: **PENDIENTE_NEGOCIO**  
OP-01: **BLOQUEADO**  
Alcance: evidencia y documentación; sin cambios de datos

## Pregunta presentada a negocio

```text
¿Desde qué fecha exacta comenzaron a aplicarse oficialmente
los siguientes horarios?

Turno diurno:
07:00–17:22

Turno nocturno:
19:00–05:22

¿DIU y NOC comenzaron a regir en la misma fecha?

¿La fecha aplica a todas las plantas o existen fechas por planta?

¿Hubo excepciones por trabajador, grupo o cuadrilla?

¿Existió un periodo de transición en el que coexistieron los
horarios anteriores y los nuevos?
```

Formato requerido para responder:

```text
FechaRigeDesdeDIU: YYYY-MM-DD
FechaRigeDesdeNOC: YYYY-MM-DD

Misma fecha: SÍ / NO
Ámbito: TODAS LAS PLANTAS / POR PLANTA

Transición: SÍ / NO
Detalle transición:

Excepciones: SÍ / NO
Detalle excepciones:

Fuente: RRHH / PLANILLA / OPERACIONES / NEGOCIO AUTORIZADO
Referencia: documento/correo/acuerdo/comunicación
Observación:
```

## Fuentes revisadas

| Fuente | Hallazgo | Valor para la fecha |
|---|---|---|
| `ACCEPTANCE_RULES.md` | Confirma DIU 07:00–17:22 y NOC 19:00–05:22. | Confirma la regla, no su inicio real. |
| `BUSINESS_RULES_FINAL.md` | Registra ASIS-001 como vigente parcial y remite a OP-01. | Sin fecha efectiva. |
| `REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md` | Identifica la semilla 19:00/07:00 como riesgo de datos. | Sin fecha efectiva. |
| `PROJECT_STATE.md` | Declara expresamente que falta `FechaRigeDesde` y que no hay evidencia documental. | Evidencia directa del bloqueo. |
| `OP_01_SHIFT_VERSION_UPDATE.md` | Usa 2026-09-15 para el piloto y declara que no es fecha productiva. | Fecha excluida. |
| `OP_01_PRODUCTION_PROCEDURE.md` | Exige aprobación escrita antes de ejecutar. | Define precondición, no fecha. |
| `CONTEXT_REGISTER.md` | Conserva el piloto y el pendiente operativo. | Sin fecha efectiva. |
| `01-requerimientos-negocio.md` | Documento histórico consigna 07:00–19:00 / 19:00–07:00 como decisión anterior. | Demuestra discontinuidad documental, no cuándo cambió. |
| `04-juego-de-datos-julio-2026.md` | Utiliza los horarios anteriores, pero se identifica como juego simulado de prueba. | No es evidencia operativa. |

También se buscaron referencias a vigencia, transición, plantas, grupos, cuadrillas y
excepciones en el resto de la documentación del proyecto. No se localizaron comunicaciones
internas, memorandos, acuerdos o documentos de planilla/RR. HH. fechados que respondan la
pregunta.

No se usaron fechas de desarrollo, migraciones, commits, pruebas ni redacción documental
como sustituto de la fecha real.

## Evaluación de evidencia

### DIU

```text
Fecha: NO DETERMINADA
Fuente autorizada: NO RECIBIDA
Referencia fechada: NO RECIBIDA
Nivel A/B: NO
Nivel C/D: NO APLICABLE PARA APROBAR
```

### NOC

```text
Fecha: NO DETERMINADA
Fuente autorizada: NO RECIBIDA
Referencia fechada: NO RECIBIDA
Nivel A/B: NO
Nivel C/D: NO APLICABLE PARA APROBAR
```

El material del proyecto confirma que el horario objetivo existe, pero no constituye
evidencia A o B de su inicio operacional. El archivo simulado de julio no se eleva a nivel C
porque no es un reporte operativo real. Tampoco existe un testimonio que pueda calificarse D.

## Misma fecha, ámbito y transición

| Pregunta | Resultado | Motivo |
|---|---|---|
| ¿DIU y NOC comenzaron en la misma fecha? | NO DETERMINADO | No hay respuesta autorizada. |
| ¿Aplica a todas las plantas? | NO DETERMINADO | La regla global del software no prueba adopción operacional uniforme. |
| ¿Hubo fechas distintas por planta? | NO DETERMINADO | No se halló comunicación por planta. |
| ¿Existió transición? | NO DETERMINADO | No hay rango ni grupos documentados. |
| ¿Hubo excepciones individuales/grupales? | NO DETERMINADO | No hay documento de excepciones. |

Si negocio informa fechas por planta, OP-01 global no podrá ejecutarse: `VersionTurno`
versiona el contenido del turno, no un horario distinto por planta. Ese caso exige detener y
rediseñar la granularidad antes de tocar datos.

Si existió coexistencia entre horarios o excepciones por trabajador/grupo, una sola frontera
global tampoco representaría la historia real. Debe documentarse el intervalo y quiénes
quedaron en cada régimen.

## Discontinuidad documental

`01-requerimientos-negocio.md` registra como decisión anterior los horarios sembrados,
mientras las reglas autoritativas posteriores confirman 17:22/05:22. No se crea
`CONFLICTO-ASIS001-01` porque no son dos respuestas actuales y autorizadas a este
cuestionario: la primera quedó históricamente superada. El hallazgo sí demuestra que una
fecha de corte es indispensable y no puede inferirse.

Contradicciones entre respuestas autorizadas: **0**, porque no se recibió ninguna respuesta.

## Decisión formal

```text
Fecha DIU = NO DETERMINADA
Fecha NOC = NO DETERMINADA
Misma fecha = NO DETERMINADO
Ámbito = NO DETERMINADO
Transición = NO DETERMINADO
Excepciones = NO DETERMINADO
Nivel de evidencia = SIN EVIDENCIA A/B

Decisión = PENDIENTE_NEGOCIO
OP-01 = BLOQUEADO
```

No se ejecutó OP-01, no se abrió ni modificó SQLite en esta fase, no se crearon migraciones,
no se modificó código productivo y no se ejecutaron build/tests.

## Criterio de desbloqueo

Se requiere una respuesta única o reconciliada, preferentemente con evidencia:

- A: documento/acuerdo oficial fechado; o
- B: comunicación operativa fechada.

La respuesta debe cubrir ambas fechas, si son iguales, ámbito por planta, transición y
excepciones. Solo entonces puede declararse `LISTO_PARA_EJECUTAR_EN_COPIA` y repetirse el
dry-run sobre una copia fresca de la base objetivo.
