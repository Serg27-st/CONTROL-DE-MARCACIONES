# P1-FER-003 — descanso sustitutorio por trabajo en feriado

Fecha de cierre técnico: 2026-10-20  
Producción: **no tocada**  
Backfill: **no ejecutado**

## Definición reconciliada

Las fuentes `BUSINESS_RULES_FINAL.md`, `REQUIREMENTS_IMPLEMENTATION_GAP_MATRIX.md`,
`REQUIREMENTS_TRACEABILITY.md` y `P0_FER_002_007_HOLIDAY_ECONOMICS.md` describían el mismo
gap: la economía del feriado sin sustitución estaba implementada, pero no existía un flujo
operativo ni el efecto de una sustitución. El inventario de código precisó esa afirmación:
ya había un modelo y servicio Application/Infrastructure latentes y semánticamente correctos,
pero no tenían migración, UI, integración con tareo/liquidación ni contratos, de modo que el
dato no podía existir realmente en una instalación.

La regla cerrada es:

- sin sustitución vigente, los minutos únicos del feriado producen base trabajada y recargo
  del 100 %;
- con sustitución vigente, esos mismos minutos físicos permanecen en el tareo, pero dejan de
  producir el concepto económico general de `FeriadoTrabajado`;
- la remuneración ordinaria propia del feriado sigue siendo un componente separado;
- el día concedido es descanso completo: cero minutos físicos, sin falta, tardanza ni salida
  anticipada;
- nunca se infiere una sustitución desde ausencias, permisos, rotaciones o marcaciones.

## Modelo incorporado

Se reutilizó `DescansoSustitutorio`, el hecho separado de tareo, marcaciones y vínculo laboral
que ya estaba modelado en el código pero aún no era operativo.

| Campo | Semántica |
|---|---|
| `TrabajadorId` | persona beneficiaria; FK restrictiva a `Trabajador` |
| `FechaFeriadoOrigen` | jornada ya calculada como feriado efectivamente trabajado |
| `FechaDescanso` | día completo posterior otorgado en sustitución |
| `Estado` / `Activo` | `Otorgado` vigente o `Anulado` histórico |
| `CreadoPor` / `CreadoEn` | quién y cuándo lo registró |
| `AprobadoPorUsuario` / `AprobadoEn` | quién y cuándo lo otorgó |
| datos de anulación | actor, instante y motivo |
| `Version` | concurrencia optimista en SQLite |

El flujo actual es de otorgamiento directo: `COORDINADOR` o `ADMINISTRADOR` registra y
aprueba en una sola operación. Ambos hechos se conservan explícitos aunque hoy el actor sea
el mismo.

### Cardinalidad e integridad

- un feriado trabajado admite `0..1` sustituciones activas;
- un día de descanso no puede compensar dos feriados del mismo trabajador;
- dos índices únicos filtrados protegen ambas reglas en la base;
- una sustitución anulada permanece como evidencia y permite registrar la corrección;
- `FechaDescanso > FechaFeriadoOrigen` se protege en dominio y Application;
- no existe borrado físico desde el flujo.

La parcialidad no tiene regla aprobada y queda **NO SOPORTADA / NO DETERMINADA**. El modelo
representa únicamente una jornada completa y no inventa fracciones.

## Vínculo y alcance

`ServicioDescansosSustitutorios` resuelve ambas fechas exclusivamente con `PeriodoLaboral` y
`AsignacionPlanta`:

- debe existir vínculo en el feriado y en el descanso;
- ambas fechas deben pertenecer al mismo vínculo continuo;
- el usuario debe tener alcance sobre la planta histórica de ambas fechas;
- la fecha origen debe tener `TipoDiaNoLaborable=Feriado` y minutos trabajados positivos;
- un descanso con trabajo ya calculado se rechaza;
- los periodos de tareo de ambas fechas deben estar abiertos y se afirman atómicamente al
  guardar.

## Tareo y economía

El recálculo carga solo sustituciones explícitas vigentes. En el día de descanso produce
`EstadoTareo.DescansoSustitutorio`, cero minutos y ninguna incidencia. Si después aparecen
marcaciones, no se ocultan ni se inventa una jornada: el día queda
`PendienteValidacion/TrabajoEnDescansoSustitutorio`.

La liquidación reutiliza el concepto ya implementado por FER-002/007 y únicamente excluye de
su agregado los feriados origen con sustitución vigente. No existe una segunda fórmula. La
pantalla de tareo muestra `Feriado sustituido → fecha`, y el listado administrativo expone
origen, minutos físicos, fecha sustituta, estado y actores.

Crear o anular el hecho invalida la vigencia del tareo mediante el mecanismo existente; no
dispara recálculos ocultos. La anulación restaura el tratamiento económico anterior en la
liquidación y el próximo recálculo restaura el día concedido.

## Persistencia y operación

Migración aditiva y reversible:

`20261020143105_DescansoSustitutorioFeriado`

Solo crea la tabla, FK e índices. `Down` elimina exclusivamente esa tabla. No contiene
`UPDATE`, inferencias ni backfill. No se aplicó a producción ni a una base operativa.

El flujo web nuevo vive en `/DescansosSustitutorios`, pasa por el servicio existente
`IServicioDescansosSustitutorios`, usa antiforgery y está limitado a `COORDINADOR` y
`ADMINISTRADOR`. La bitácora automática existente registra alta y baja lógica; no se creó
otro sistema de auditoría.

## Contratos

Se añadieron 17 pruebas:

- Domain: 4;
- Infrastructure/SQLite: 7;
- Web/MVC: 6.

Cubren FER003-01..07, cero minutos en el día sustitutorio, marcas contradictorias, FK,
unicidad, rollback, doble envío, token de versión, proyección del reporte, ModelState,
antiforgery y matriz de roles.

Resultado dirigido: 27/27 verdes (incluye 10 contratos de liquidación preexistentes).  
Resultado final: 975/975 verdes, 0 fallos, 0 omitidos.  
Build Release: 0 errores, 0 warnings.

## Estado

`FER-003 = CERRADO`.

Permanece separado `FER-005`, día no laborable compensable. No se infirieron descansos
históricos: la ausencia de un registro explícito sigue significando **SIN REGISTRO**.
