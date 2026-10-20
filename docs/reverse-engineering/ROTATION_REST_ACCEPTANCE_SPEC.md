# S-ROT-DESC — Especificación ejecutable del descanso rotativo

- **Fecha:** 2026-10-06
- **Estado:** IMPLEMENTADA Y VERIFICADA por R-P1-05 / ROT-DESC
- **Decisión base:** opción B aprobada en `ADR_ROTATION_REST_MODEL.md`
- **Baseline previa:** 785 pruebas; 784 verdes; un rojo deliberado (`ROT-DESC-01`);
  cero fallos inesperados
- **Alcance:** contratos, límites y resultado verificable de la implementación.

## 1. Decisiones confirmadas

| ID | Decisión ejecutable |
|---|---|
| D-01 | La secuencia y las duraciones son variables. 14 DIU + 1 descanso + 7 NOC es un ejemplo, no una constante. |
| D-02 | Una secuencia no se repite implícitamente. El módulo anterior fue retirado y no autoriza ciclos infinitos. |
| D-03 | Descanso programado sin marcas: cero en todos los conceptos, no falta y no requiere validación por ausencia de marcas. |
| D-04 | Todo minuto reconocido de trabajo en descanso programado es HE100 desde el primero. Ordinarias, HE25 y HE35 quedan en cero. |
| D-05 | No existe tardanza en descanso programado porque no existe `InicioTurno`. |
| D-06 | No existe salida anticipada en descanso programado porque no existe `FinTurno`. |
| D-07 | La atribución trabajador↔marca no cambia: equipo + número + fecha/hora + vigencia de `AsignacionMarcador`. |
| D-08 | Domingo trabajado conserva HE100 desde el primer minuto. Domingo + descanso no duplica minutos físicos. |

El umbral de 15 minutos no aplica al descanso: mide exceso posterior al fin teórico de un
turno regular y en descanso no existe ese fin.

## 2. Modelo aprobado

```text
TramoRotacion
- Tipo: Trabajo | Descanso
- TurnoId: long?
- Dias: int
- Orden: int
```

Invariantes:

```text
Trabajo  → TurnoId != null
Descanso → TurnoId == null AND Dias == 1
```

El descanso pertenece a la colección ordenada de tramos, consume un día y participa en la
posición respecto de `FechaAncla`. No es un turno y no es la ausencia de programación.

## 3. Contrato concreto de resolución

El contrato se limita a resolver una programación de cuadrilla para una fecha. La
asignación directa continúa siendo una ruta exterior y entrega un turno directamente.

```text
enum EstadoProgramacionDiaria
{
    Trabajo,
    DescansoProgramado,
    SinProgramacion,
    ErrorConfiguracion
}

ResultadoProgramacionDiaria
- Estado: EstadoProgramacionDiaria
- TurnoId: long?
- ProgramacionId: long?
- TramoId: long?
- CodigoMotivo: string?
- Descripcion: string
```

### Matriz de invariantes del resultado

| Estado | TurnoId | ProgramacionId | TramoId | Código de motivo | Significado |
|---|---:|---:|---:|---|---|
| Trabajo | obligatorio | obligatorio | obligatorio | nulo | Tramo Trabajo válido; permite resolver `Turno` y `VersionTurno`. |
| DescansoProgramado | nulo | obligatorio | obligatorio | nulo | Tramo Descanso válido; no se resuelve horario. |
| SinProgramacion | nulo | nulo | nulo | obligatorio | No hay asignación/programación aplicable a la fecha. No es descanso. |
| ErrorConfiguracion | nulo | si se conoce | si se conoce | obligatorio | Hay datos, pero contradicen invariantes o no pueden resolverse. |

`CodigoMotivo` debe ser estable y comprobable, por ejemplo:

- `SIN_ASIGNACION_VIGENTE`;
- `CUADRILLA_SIN_PROGRAMACION_VIGENTE`;
- `TRABAJO_SIN_TURNO`;
- `DESCANSO_CON_TURNO`;
- `DESCANSO_DURACION_INVALIDA`;
- `TURNO_REFERENCIADO_NO_RESUELTO`;
- `VIGENCIA_EXCEDE_SECUENCIA_SIN_REPETICION`.

La descripción es texto operativo; los consumidores no deben tomar decisiones por ella.

### Reglas de transición

1. Sin asignación a cuadrilla aplicable → `SinProgramacion`.
2. Con cuadrilla pero sin `ProgramacionCuadrilla` activa y vigente → `SinProgramacion`.
3. Programación vigente sin tramos, con días inválidos o con tipo/turno incoherentes →
   `ErrorConfiguracion`.
4. Tramo Trabajo válido → `Trabajo`; después se resuelven `Turno` y `VersionTurno`.
5. Tramo Descanso válido → `DescansoProgramado`; nunca se busca un turno.
6. Una referencia de turno de Trabajo que no puede hidratarse → `ErrorConfiguracion`, no
   `SinProgramacion` ni descanso.
7. Pasada la última posición explícita, sin autorización explícita de repetición → no se
   aplica módulo. La programación debe haber terminado; si su vigencia declara lo
   contrario, la configuración es inválida.

## 4. Comportamiento del tareo

### 4.1 Descanso sin marcas

```gherkin
Dado un ResultadoProgramacionDiaria DescansoProgramado
Y ninguna marcación delimitada para ese descanso
Cuando se calcula el día
Entonces EstadoTareo es Descanso
Y MinutosTrabajados es 0
Y MinutosOrdinarios es 0
Y MinutosExtra25 es 0
Y MinutosExtra35 es 0
Y MinutosExtra100 es 0
Y MinutosTardanza es 0
Y MinutosSalidaAnticipada es 0
Y RequiereValidacion es falso
Y el estado no es Falta ni SinHorario
```

La falta de marcas es normal en un descanso; la cobertura del archivo del aparato no debe
convertir ese hecho por sí solo en una incidencia de asistencia.

### 4.2 Descanso con intervalo válido

```gherkin
Dado un ResultadoProgramacionDiaria DescansoProgramado
Y un intervalo trabajado válido delimitado por la política BD-01
Cuando se calcula el día
Entonces MinutosOrdinarios es 0
Y MinutosExtra25 es 0
Y MinutosExtra35 es 0
Y MinutosExtra100 equivale a todos los minutos reconocidos
Y MinutosTardanza es 0
Y MinutosSalidaAnticipada es 0
Y no se aplica el umbral de 15 minutos
```

La tasa monetaria continúa saliendo de la configuración vigente del concepto; este
contrato fija minutos, no introduce un porcentaje literal.

### 4.3 Una sola marca

```gherkin
Dado un descanso programado
Y una sola marcación efectiva
Cuando se calcula el día
Entonces EstadoTareo es Incompleto
Y todos los minutos calculados son 0
Y RequiereValidacion es verdadero
Y la observación identifica la marca
```

Esto es compatible con `EstadoTareo.Incompleto` y con la regla actual de no inventar el
extremo faltante. No es completamente compatible con `TipoIncidencia`: hoy el motor elige
`IngresoNoRegistrado` o `SalidaNoRegistrada` comparando la marca con el inicio/fin del
turno. En descanso no existen esos extremos. La implementación necesitará una incidencia
genérica de marcación incompleta en descanso, o un contrato equivalente que no invente si
la marca fue entrada o salida.

## 5. BD-01 — Delimitación de marcas

### 5.1 Comportamiento actual con turno

La atribución ocurre antes y no usa turnos. Después, para delimitar una jornada, el
orquestador:

1. abre una ventana en `InicioTurno - MinutosAnticipacionEntrada`;
2. la cierra en `FinTurno + 6 horas`;
3. excluye marcas que ya pertenecen a la jornada anterior;
4. procesa los días cronológicamente y mantiene un conjunto `reclamadas` para que una marca
   pertenezca a una sola jornada;
5. reporta por fecha natural las marcas no reclamadas;
6. colapsa rebotes menores a dos minutos solo para contar eventos;
7. con dos o más eventos usa la primera y última marca **en bruto**; no empareja intervalos
   interiores;
8. con una marca devuelve jornada incompleta y cero minutos.

Por eso `07:00, 12:00, 13:00, 17:00` se interpreta hoy como un único intervalo 07:00–17:00;
las marcas centrales quedan declaradas, pero no descuentan una pausa.

### 5.2 Alternativas

#### Alternativa 1 — primera y última marca del descanso

Tomar la primera y última de las marcas candidatas no reclamadas.

**Ventajas:** coincide con la semántica actual una vez elegido el conjunto candidato;
conserva rebotes, extremos e incidencia por marca única.

**Consecuencias:** sin límite temporal puede capturar dos marcas accidentales muy alejadas,
no resuelve cruces de medianoche y no descuenta intervalos interiores.

#### Alternativa 2 — ventana configurable de descanso trabajado

Definir una ventana temporal que seleccione marcas y usar dentro de ella los extremos.

**Ventajas:** acota capturas y reutiliza el patrón actual de ventanas.

**Consecuencias:** necesita dueño, vigencia, planta y regla para cruces; una ventana fija
puede convertirse en un turno ficticio encubierto. Las marcas fuera de ella deben
declararse, no desaparecer.

#### Alternativa 3 — programación explícita de trabajo extraordinario

Registrar para ese trabajador/fecha una autorización o ventana extraordinaria, sin
convertirla en turno regular.

**Ventajas:** máxima auditabilidad y límites claros.

**Consecuencias:** requiere un concepto y flujo nuevos. No puede ser requisito previo para
reconocer trabajo ya acreditado sin validación de negocio, porque el sistema vigente
reconoce sobretiempo registrado aunque no exista una orden previa.

#### Alternativa 4 — confirmación manual del intervalo

Toda presencia en descanso genera una incidencia y una persona autorizada confirma los
extremos mediante un registro separado, inmutable y auditable.

**Ventajas:** no inventa horas y resuelve cruces o agrupamientos ambiguos.

**Consecuencias:** vuelve manual incluso el caso evidente. La regularización existente
sirve como patrón de workflow, pero hoy su aritmética exige un `Turno`; no se puede reutilizar
literalmente para un descanso.

#### Mecanismo existente reutilizable — marcas no reclamadas

`reclamadas` + `SinReclamar` ya evita que la salida matutina de un NOC anterior se compute
otra vez como trabajo del descanso y conserva las marcas atribuidas por fecha natural.
Es una buena fuente de **candidatos**, pero no delimita por sí sola el intervalo del
descanso ni resuelve trabajo que cruza medianoche.

### 5.3 Ejemplos comparativos

Minutos brutos, sin deducción de pausa no confirmada. Para las alternativas 2 y 3 se supone,
solo a efectos comparativos, que la ventana/autorización incluye 07:00–17:00.

| Marcas | Alt. 1 extremos | Alt. 2 ventana + extremos | Alt. 3 autorización + extremos | Observación |
|---|---:|---:|---:|---|
| 07:00, 17:00 | 600 min HE100 | 600 min HE100 | 600 min HE100 | Intervalo completo conocido. |
| 07:00, 12:00, 13:00, 17:00 | 600 min HE100 | 600 min HE100 | 600 min HE100 | El motor actual no empareja 07–12 + 13–17; hacerlo daría 540 y sería una regla nueva. |
| 07:00, 12:00 | 300 min HE100 | 300 min HE100 | 300 min HE100 | No se completa hasta 17:00 por autorización/ventana. |
| 07:00 | 0 min; Incompleto | 0 min; Incompleto | 0 min; Incompleto | Nunca se inventa el extremo complementario. |

Estos números no eligen la política: muestran el resultado si el conjunto candidato ya
fue aceptado y se conserva la regla actual de extremos.

### 5.4 Decisión funcional aprobada

BD01-D01..D03 aprueban la composición híbrida:

1. resolver primero las jornadas con turno de todo el rango y reclamar sus marcas;
2. usar para el descanso solo el residuo no reclamado;
3. exigir que la primera marca pertenezca al día natural del descanso;
4. permitir una salida en el día siguiente únicamente antes de la ventana de la próxima
   jornada laboral y siempre que esa marca no haya sido reclamada;
5. aplicar las mismas dos capas de rebotes y la semántica actual de extremos;
6. una marca → Incompleto, cero minutos e incidencia genérica, sin adivinar qué extremo falta;
7. fronteras o agrupamientos ambiguos → cero automático y confirmación auditada;
8. intervalo inequívoco → todo HE100, sin deducir refrigerio no confirmado.

El detalle, la evidencia de código, cinco alternativas y los casos BD01-01..14 están en
`BD_01_REST_WORK_BOUNDARIES.md`.

La jornada pertenece al día de su primera marca válida no reclamada; puede cerrar en D+1
antes de la siguiente jornada aplicable; y todo caso ambiguo queda en cero automático y
requiere confirmación auditada. La hoja de HE del coordinador puede aportar evidencia o
autorización, pero no sustituye una frontera horaria explícita. **BD-01 está cerrada
funcionalmente.**

## 6. BD-02 — Feriado + descanso

Estado: **CERRADA FUNCIONALMENTE** mediante BD02-D01..D09.

Quedó confirmada la dimensión feriado nacional:

- no trabajado: conserva la remuneración ordinaria del feriado y no genera HE/sobretasa;
- trabajado sin descanso sustitutorio: una remuneración ordinaria del feriado —normalmente
  ya incluida en el sueldo mensual— más base del trabajo efectivo y sobretasa de 100 %;
- trabajo parcial: base y sobretasa proporcionales al intervalo efectivamente trabajado;
- con descanso sustitutorio: no corresponde el adicional por trabajo + 100 % bajo la regla
  general confirmada;
- la jornada que empieza antes del feriado conserva la fecha/tratamiento de inicio; la que
  empieza en feriado se evalúa como feriado;
- feriado nacional y día no laborable compensable son clasificaciones distintas;
- los minutos físicos se mantienen una sola vez; base y sobretasa son valorización, no
  duplicación del tiempo.

La estructura sí debe conservar ambos hechos:

```text
programación diaria = DescansoProgramado
calendario = Feriado
```

Para un feriado nacional ordinario distinto del 1 de mayo, si se trabaja, los N minutos
delimitados por BD-01 se clasifican una sola vez como `FeriadoTrabajado`; no se agrega
`DescansoSemanalTrabajado` sobre la misma base. Sin trabajo hay cero minutos y no se duplica
el descanso.

El 1 de mayo es una excepción. Si coincide con descanso y se trabaja, conserva cuatro
componentes económicos —descanso semanal, feriado, trabajo y sobretasa 100 %— sobre una
única cantidad física N. Si no se trabaja, conserva los dos componentes remunerativos de
descanso semanal y feriado, con cero minutos trabajados.

El registro de descanso sustitutorio y la clasificación de día no laborable compensable
permanecen como gaps independientes; no bloquean el modelado base de `DescansoProgramado`.
El contrato completo está en `BD_02_HOLIDAY_REST_COINCIDENCE.md`.

### 6.1 Contratos FER-NAC

#### FER-NAC-01 — feriado no trabajado

Sin marcas: estado Feriado, cero minutos trabajados/HE y ninguna sobretasa generada. La
remuneración ordinaria del feriado es un concepto de remuneración separado del tiempo
trabajado y normalmente ya forma parte del sueldo mensual.

#### FER-NAC-02 — trabajado sin descanso sustitutorio

Para N minutos válidos: una sola cantidad física N; valorización adicional = base N +
sobretasa 100 % sobre N. Sumada a la remuneración ordinaria del feriado, el día completo
equivale económicamente a tres remuneraciones diarias; respecto del sueldo ordinario ya
pagado, el adicional equivale a dos.

#### FER-NAC-03 — trabajo parcial

Para un intervalo válido de N minutos, base y sobretasa se calculan sobre N. No se acredita
automáticamente una jornada completa.

#### FER-NAC-04 — descanso sustitutorio

El resultado debe distinguir `SinDescansoSustitutorio` de `ConDescansoSustitutorio`. Con
descanso sustitutorio no genera el adicional por trabajo + 100 % del feriado. El modelo
actual no guarda esa decisión y constituye un gap previo a implementar este contrato.

#### FER-NAC-05 — jornada que cruza hacia feriado

Si la jornada inicia en día laborable y termina dentro del feriado, conserva la
clasificación de su fecha de inicio. El tramo posterior a medianoche no se reclasifica por
ese solo cruce.

#### FER-NAC-06 — jornada iniciada en feriado

Si inicia durante el feriado, toda la jornada imputada a esa fecha se evalúa como trabajo
en feriado, sin duplicar minutos por cruzar al día siguiente.

#### FER-NAC-07 — clasificación de calendario

`FeriadoNacional != DiaNoLaborableCompensable`. El catálogo/resolución debe transportar el
tipo; una mera fecha no laborable no activa el esquema del feriado. El modelo actual usa
`Feriado.PlantaId == null` para nacional y mezcla bajo `Feriado` días locales/paradas de
planta, por lo que no representa expresamente el día compensable.

#### FER-NAC-08 — liquidación sin doble base física

N minutos de feriado trabajado permanecen N minutos físicos. La valorización del concepto
aplica `N × (1 + 100 %)`. La remuneración ordinaria del feriado es separada y no se vuelve
a introducir como otros N minutos trabajados dentro de `TotalHorasEquivalentes`.

### 6.2 Contratos BD-02

- **BD02-D01/D02/D05:** feriado ordinario + descanso usa un solo día y una sola base;
  trabajado se clasifica como `FeriadoTrabajado`, no además como descanso trabajado.
- **BD02-D03:** sin sustitutorio, remuneración ordinaria del feriado + base del trabajo +
  100 %, sin duplicar N.
- **BD02-D04:** con sustitutorio no corresponde el adicional general; falta representar el
  dato en el sistema.
- **BD02-D06/D07:** el 1 de mayo conserva su tratamiento especial trabajado o no trabajado.
- **BD02-D08/D09:** la jornada se clasifica por su fecha de inicio, aunque cruce medianoche.

## 7. Domingo + descanso

```gherkin
Dado un descanso programado que cae domingo
Y un intervalo válido de N minutos trabajados
Cuando se calcula el día
Entonces MinutosExtra100 es N
Y los minutos físicos reconocidos son N, no 2N
Y Ordinarias, HE25 y HE35 son 0
```

Domingo y descanso son dos hechos, pero coinciden en la clasificación confirmada HE100. No
se crean dos conceptos sobre la misma base física.

## 8. Repetición y vigencias

### 8.1 Propiedades actuales

`ProgramacionCuadrilla` contiene:

- `FechaInicio`: primer día de vigencia;
- `FechaFin`: último día inclusivo; nulo significa vigencia abierta;
- `Activo`: las programaciones reemplazadas enteras quedan inactivas;
- `FechaAncla`: día que el algoritmo considera inicio del primer tramo;
- `Tramos`: secuencia y duración;
- `EstaVigenteEn`: exige activa y fecha dentro de inicio/fin.

`ServicioCuadrillas.ProgramarAsync` crea siempre una programación abierta y con
`FechaAncla = FechaInicio`. `ReemplazarDesdeAsync` cierra la anterior el día previo cuando
se registra otra. El usuario no puede declarar `FechaFin` al crearla. `TramoEn` usa hoy
módulo positivo, por lo que repite durante toda la vigencia.

### 8.2 ¿Basta la vigencia para no repetir?

**Sí en el modelo para el caso simple; no en el flujo operativo actual ni como semántica
general de repetición.**

Una ejecución única puede representarse con:

```text
FechaInicio = FechaAncla
FechaFin = FechaAncla + DiasDeLaSecuencia - 1
```

Fuera de ese rango no hay programación vigente. Pero hoy no se puede guardar ese fin desde
el request/UI, y una vigencia de tres meses sobre una secuencia de 22 días no dice si debe
repetirse o si existe un hueco de programación: vigencia y longitud no son equivalentes.

### 8.3 Comparación solicitada

| Alternativa | Capacidad | Ventaja | Límite |
|---|---|---|---|
| A. Usar `FechaInicio`/`FechaFin` | Una pasada si el fin coincide con la última posición | No agrega columna ni concepto | No expresa repetición; el flujo actual no acepta el fin y `CorrerRotacion` depende del módulo |
| B. Modo explícito de repetición | Distingue secuencia única de ciclo repetitivo | Semántica inequívoca | Agrega modelo, migración, UI y reglas aún no requeridas |

### 8.4 Cambio futuro recomendado

Primera implementación:

- la modalidad por defecto y única será **secuencia finita**;
- el servicio derivará/cerrará `FechaFin` en la última posición explícita;
- el resolver validará vigencia antes de resolver y no aplicará módulo fuera de la primera
  secuencia;
- una programación abierta o más larga que la secuencia será rechazada o resultará
  `ErrorConfiguracion`, nunca repetirá silenciosamente;
- `CorrerRotacionAsync` deberá reconstruir una secuencia finita/ancla válida, no conservar
  la dependencia modular actual.

Solo si negocio solicita ciclos repetitivos se diseñará un `ModoProgramacion` explícito
(preferible a un booleano aislado), con fin o número de repeticiones. No se agrega en esta
fase.

### 8.5 Caracterización `ROT-REP-01`

El algoritmo actual, si pudiera contener el nuevo tramo Descanso, produciría por módulo:

| Día | Resultado actual caracterizado |
|---:|---|
| 1 | DIU |
| 2 | DIU |
| 3 | Descanso |
| 4 | DIU |
| 5 | DIU |
| 6 | Descanso |

Este contrato documenta el comportamiento que debe dejar de ser implícito. No constituye
aceptación del resultado futuro.

## 9. Persistencia futura

### 9.1 Migración conceptual

1. agregar `tipo` no nulo;
2. convertir todos los tramos existentes a `Trabajo`;
3. hacer nullable `turno_id` y conservar su FK restrictiva;
4. agregar constraints;
5. no crear descansos históricos;
6. verificar `foreign_key_check`, `integrity_check`, conteos y rollback.

### 9.2 Mapeo EF/SQLite propuesto

El proyecto persiste enums legibles como texto y usa nombres físicos `snake_case`. Con el
patrón ya utilizado por EF Core 10/SQLite en este repositorio:

```text
Property(Tipo)
  .HasConversion<string>()
  .HasMaxLength(10)
  .IsRequired()

CHECK CK_tramo_rotacion_tipo_turno:
([tipo] = 'Trabajo' AND [turno_id] IS NOT NULL)
OR
([tipo] = 'Descanso' AND [turno_id] IS NULL)
```

Configuración conceptual equivalente:

```text
builder.ToTable(t => t.HasCheckConstraint(
    "CK_tramo_rotacion_tipo_turno",
    "([tipo] = 'Trabajo' AND [turno_id] IS NOT NULL) " +
    "OR ([tipo] = 'Descanso' AND [turno_id] IS NULL)"));
```

Los nombres del SQL son columnas físicas, no propiedades C#. SQLite acepta los corchetes y
la expresión booleana; el proveedor ya usa este patrón para
`CK_asignacion_turno_turno_o_cuadrilla`. La migración real deberá probar el constraint con
SQLite, no solo con InMemory.

### 9.3 Descanso de un día

Recomendación: protegerlo en **Domain y DB**, no duplicar la regla en una tercera
implementación independiente de Application.

- Domain produce el error de negocio inmediato y protege cualquier constructor/fábrica.
- Application valida forma y traduce el error para UI, pero delega la invariante al dominio.
- DB protege contra EF, SQL manual, importaciones o errores de una ruta futura:

```text
CHECK CK_tramo_rotacion_descanso_un_dia:
([tipo] <> 'Descanso') OR ([dias] = 1)
```

## 10. Contratos implementados

### ROT-DESC-01 — modelo

**Dado** Trabajo DIU, Descanso y Trabajo NOC, **entonces** el descanso tiene tipo explícito,
`TurnoId = null` y no existe turno ficticio. El contrato está verde.

### ROT-DESC-02 — descanso sin marcas

Cumple íntegramente el contrato 4.1 y no queda `SinHorario`.

### ROT-DESC-03 — descanso trabajado

Con un intervalo ya delimitado como válido, todos sus minutos son HE100, sin umbral,
tardanza ni salida anticipada. La prueba de integración seleccionará marcas según
BD01-D01..D03: jornadas con turno primero, residuo no reclamado, primera marca en D,
posible salida en D+1 antes de la siguiente jornada y validación ante ambigüedad.

### ROT-DESC-04 — compatibilidad

Para toda fecha cubierta, una programación migrada compuesta solo por Trabajo resuelve el
mismo turno que antes. La migración no cambia IDs, orden, días ni ancla.

### ROT-DESC-05 — frontera del descanso

Con ancla 2026-07-06 y DIU×14, Descanso×1, NOC×7:

- 2026-07-19 → Trabajo DIU;
- 2026-07-20 → DescansoProgramado;
- 2026-07-21 → Trabajo NOC.

No se consulta el día 23 como repetición aceptada.

### ROT-DESC-06 — nueva programación futura

Una programación que empieza en F cierra la anterior en F−1. Resolver antes de F conserva
los IDs y resultados anteriores; desde F usa exclusivamente la nueva secuencia.

### ROT-DESC-07 — descanso no es ausencia

Para la misma fecha, un tramo Descanso válido devuelve `DescansoProgramado` con IDs; una
cuadrilla sin programación devuelve `SinProgramacion` sin IDs. Los resultados no son
intercambiables.

### ROT-DESC-08 — configuración inválida

Trabajo sin turno, Descanso con turno o Descanso con `Dias != 1` produce
`ErrorConfiguracion` con código estable. No produce descanso, `SinHorario` ni
`SinProgramacion` silenciosos.

### ROT-DESC-09 — domingo sin doble conteo

Descanso programado + domingo + intervalo válido N produce exactamente N minutos HE100 y
cero en los demás tramos físicos. Liquidación incorpora una sola base y un solo recargo
aplicable.

### ROT-FER-01 — descanso + feriado ordinario trabajado

DescansoProgramado + feriado nacional distinto del 1 de mayo + N minutos delimitados por
BD-01 produce N minutos físicos únicos, clasificados como `FeriadoTrabajado`. No se agrega
`DescansoSemanalTrabajado` sobre N. Ordinarias, HE25 y HE35 quedan en cero.

### ROT-FER-02 — descanso + 1 de mayo trabajado

DescansoProgramado + 1 de mayo + N minutos delimitados por BD-01 conserva N una sola vez y
los cuatro componentes económicos de BD02-D06. No se degrada al feriado ordinario ni se
duplica la base física.

Los contratos complementarios ROT-FER-03..06 —sin trabajo y cruces de medianoche— están en
`BD_02_HOLIDAY_REST_COINCIDENCE.md`.

### ROT-REP-01 — fin de la secuencia

Demuestra que una secuencia DIU, DIU, Descanso termina después de su tercera posición y
devuelve `SinProgramacion`; no vuelve automáticamente a DIU.

## 11. Estado de preparación

| Área | Estado |
|---|---|
| Modelo B e invariantes | Implementado |
| Descanso sin trabajo | Implementado |
| Intervalo pre-delimitado → HE100 | Implementado |
| Selección extremo a extremo de marcas | Implementada en dos pasadas con identidad de marca |
| Domingo + descanso | Implementado; sin duplicación |
| FER-NAC-01/03/05/06/08 | Regla definida; contrastada con comportamiento actual |
| FER-NAC-02/04 | Regla definida; falta modelar descanso sustitutorio |
| FER-NAC-07 | Regla definida; clasificación compensable no existe en el modelo actual |
| Feriado ordinario + descanso | Implementado sin doble concepto físico |
| 1 de mayo + descanso | Clasificación estructural implementada; valorización especial detenida |
| Secuencia no repetitiva | Implementada; al terminar devuelve `SinProgramacion` |
| Migración | Creada y validada sobre SQLite temporal; no aplicada a producción |

BD-01 y BD-02 están implementadas: `ROT-DESC` está resuelto con 803 pruebas verdes.
FER-NAC-04 requiere representar el descanso sustitutorio y FER-NAC-07 separar el día no
laborable compensable, como gaps independientes que no bloquean el modelo base. El
componente económico adicional del 1 de mayo permanece como gap explícito.
