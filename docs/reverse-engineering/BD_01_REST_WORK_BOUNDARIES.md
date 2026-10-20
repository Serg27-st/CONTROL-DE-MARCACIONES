# BD-01 — Delimitación temporal del trabajo en descanso programado

- **Fecha:** 2026-10-06
- **Estado:** **CERRADA E IMPLEMENTADA** por R-P1-05
- **Alcance:** regla implementada en el tareo y cubierta por ROT-MARK-01..06.
- **Baseline previa:** 785 pruebas; 784 verdes; `ROT-DESC-01` rojo deliberado;
  cero fallos inesperados.

## 1. Decisiones previas que este análisis no reabre

El descanso rotativo implementado es un `TramoRotacion` explícito:

```text
Tipo = Trabajo  → TurnoId obligatorio
Tipo = Descanso → TurnoId nulo y duración de 1 día
```

En un descanso sin trabajo no hay falta ni minutos. En un descanso con un intervalo válido,
todo minuto reconocido es HE100 desde el primero. No existen ordinarias, HE25, HE35,
umbral de 15 minutos, tardanza ni salida anticipada.

La atribución de la marca ya está resuelta y no cambia:

```text
EquipoId + IdMarcador + FechaHora + AsignacionMarcador vigente → trabajador
```

BD-01 decide únicamente qué marcas ya atribuidas forman el intervalo trabajado.

## 2. Algoritmo actual confirmado en código

### 2.1 Orquestación y selección

```text
Archivo: src/ControlMarcacion.Infrastructure/Persistencia/Servicios/ServicioCalculoTareo.cs
Clase:   ServicioCalculoTareo
Método:  RecalcularPeriodoAsync
Ayudas:  MarcacionesDeLaJornada, SinReclamar, FinJornadaTeorica, MasTardia
```

Para cada trabajador, el servicio ordena todas las marcas atribuidas y procesa las fechas
cronológicamente. Mantiene:

- `reclamadas`: `HashSet<DateTime>` con las horas ya incorporadas a alguna jornada;
- `finJornadaAnterior`: el mayor entre la salida real y el fin teórico del día anterior;
- una semilla calculada con el turno del día previo al inicio del periodo.

Para una fecha con turno, `MarcacionesDeLaJornada` construye:

```text
desde = InicioTurno - MinutosAnticipacionEntrada
hasta = FinTurno + 6 horas
```

Si el turno cruza medianoche, `FinTurno` cae al día siguiente. Una marca entra cuando:

```text
marca >= desde
AND marca <= hasta
AND (finJornadaAnterior es nulo OR marca > finJornadaAnterior)
```

Las marcas seleccionadas se agregan a `reclamadas`. `SinReclamar` devuelve las marcas cuya
fecha natural es la fecha procesada y cuyo `DateTime` no está en ese conjunto.

Consecuencias confirmadas:

1. La salida de un NOC anterior no se reutiliza como entrada del día siguiente.
2. Un turno nocturno puede reclamar marcas del día natural siguiente.
3. Las marcas fuera de ventana se conservan como evidencia y se declaran en la observación.
4. `reclamadas` identifica por `DateTime`, no por la clave de `MarcacionCruda`; el modelo
   actual presupone que no quedan dos hechos atribuibles distintos con el mismo instante.
5. En un día futuro `DescansoProgramado`, `Turno == null`; la ruta actual seleccionaría
   cero marcas y terminaría perdiendo además el corte en `finJornadaAnterior`. No basta con
   cambiar el estado que devuelve el resolver.

### 2.2 Rebotes y doble fichada

Hay dos capas distintas:

```text
Archivo: src/ControlMarcacion.Infrastructure/Marcaciones/ServicioImportacion.cs
Método:  ColapsarDobleFichada
```

En la importación se descartan repeticiones del mismo `IdMarcador` dentro de la ventana
configurada (`VentanaDeduplicacionSegundos`, actualmente 60 s). Esta capa actúa antes de
atribuir la marca al trabajador.

```text
Archivo: src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs
Clase:   CalculadoraTareo
Método:  ConsolidarRebotes
```

El motor vuelve a consolidar eventos separados por menos de dos minutos. Compara cada
marca con el último evento aceptado, evitando que una cadena de rebotes se prolongue sin
límite. Esta segunda capa solo decide cuántos eventos existen; el intervalo económico usa
la primera y la última marca de la lista bruta seleccionada.

### 2.3 Primera, última, intermedias y marca única

```text
Archivo: src/ControlMarcacion.Domain/Asistencia/CalculoTareo.cs
Clase:   CalculadoraTareo
Métodos: CalcularSinJustificar, Jornada, Incompleta
```

- cero eventos: no hay jornada;
- un evento: `Incompleto`, cero minutos;
- dos o más eventos: primera marca bruta = entrada y última marca bruta = salida;
- las marcas intermedias se informan, pero no forman pares ni descuentan pausas;
- la marca única se clasifica como entrada o salida comparándola con el inicio y fin del
  turno. Esta heurística no es válida en descanso porque no existen esos extremos.

### 2.4 Cruce de medianoche

El cruce actual no se descubre a partir de las marcas: lo autoriza `Turno.CruzaMedianoche`.
La ventana y el fin teórico se extienden al día siguiente y toda la jornada se imputa a su
fecha de inicio. Un descanso no tiene ese dato; por tanto, aceptar cualquier marca del día
siguiente sería una nueva regla, no una reutilización automática del algoritmo vigente.

## 3. Riesgo de usar el día calendario completo

Tomar sin más `D 00:00–23:59` evita inventar un turno, pero no resuelve propiedad temporal:

- puede capturar una salida del NOC iniciado en D-1 si no se preserva `reclamadas`;
- no puede cerrar trabajo iniciado tarde en D y terminado en D+1;
- puede tomar como salida una segunda marca accidental muy alejada;
- una entrada anticipada del turno de D+1 puede convertirse en salida falsa del descanso;
- dos marcas civiles no prueban por sí solas que pertenezcan al mismo intervalo.

Incluso preservando `reclamadas`, el día civil sirve como fuente de candidatos, no como
delimitación suficiente.

## 4. Alternativas evaluadas

### Opción A — Primera y última marca no reclamada del día civil

```text
candidatas = SinReclamar(D 00:00–23:59)
entrada = primera
salida = última
```

**Ventajas:** mínima complejidad; conserva extremos, rebotes y marca única del motor.

**Riesgos:** no admite salida en D+1; puede unir hechos accidentales; puede absorber una
entrada del trabajo siguiente si esta cae todavía en D; su seguridad depende por completo
de que todas las jornadas anteriores ya hayan reclamado correctamente.

**Conclusión:** útil como heurística diurna, insuficiente como contrato general.

### Opción B — Ventana global configurable para descanso

Define una hora desde/hasta por planta o por sistema y aplica dentro de ella los extremos.
No se proponen horas concretas porque no existen en los requisitos.

**Ventajas:** acota el ruido; reutiliza el patrón de ventana actual.

**Riesgos:** convierte una política global en un turno ficticio; requiere vigencia y dueño
de configuración; falla con trabajo nocturno voluntario y con plantas distintas; una
ventana demasiado amplia reproduce el problema del día civil y una estrecha oculta trabajo.

**Conclusión:** descartada como regla principal. Solo sería válida si negocio define y
versiona ventanas reales por contexto.

### Opción C — Programación previa explícita de HE en descanso

El coordinador registra para trabajador/fecha un intervalo autorizado y las marcas se
seleccionan dentro de él.

**Ventajas:** límites inequívocos, alta auditabilidad y soporte natural de cruces.

**Riesgos:** crea un módulo y flujo nuevos; una omisión administrativa podría borrar
trabajo biométricamente acreditado. El código vigente documenta que el sobretiempo de
salida se reconoce por evidencia aun sin una orden previa, por lo que la autorización no
puede ser una condición absoluta sin una nueva decisión de negocio.

**Conclusión:** buena evidencia auxiliar, no fuente exclusiva de verdad.

### Opción D — Confirmación manual de cada intervalo

Toda presencia en descanso genera una incidencia; una persona autorizada confirma entrada
y salida mediante un flujo inmutable y auditado.

**Ventajas:** resuelve todos los casos ambiguos y cruces; no inventa horas.

**Riesgos:** vuelve manual incluso el caso evidente `07:00–17:00`; aumenta el cierre
operativo. La regularización actual conserva marca original, horas verificadas, motivo,
actor, aprobación y auditoría, pero su cálculo exige `Turno`; puede reutilizarse el workflow,
no el algoritmo actual sin adaptación.

**Conclusión:** respaldo obligatorio para ambigüedad, excesivo como única ruta.

### Opción E — Híbrida: propiedad por jornadas + inferencia conservadora + validación

Primero se asignan las marcas a todas las jornadas con turno. Después, el descanso trabaja
solo con el residuo no reclamado y con fronteras aportadas por las jornadas adyacentes. Los
casos inequívocos se calculan por extremos; los ambiguos van a confirmación auditada.

**Ventajas:** conserva evidencia y anti-doble-conteo, admite cruces reales, automatiza el
caso común y no depende de una ventana horaria inventada.

**Costes:** exige resolver en dos fases o con mirada hacia adelante; necesita un resultado
explícito `Valido | Incompleto | Ambiguo`; el workflow de regularización debe admitir un día
sin turno.

**Conclusión:** **APROBADA** mediante BD01-D01..D03.

## 5. Regla ejecutable aprobada

### 5.0 Decisiones funcionales

- **BD01-D01 — fecha de atribución:** el trabajo pertenece al día natural de la primera
  marcación válida no reclamada. No se crea una segunda jornada al cruzar medianoche.
- **BD01-D02 — cierre en D+1:** puede terminar al día siguiente si empieza en el descanso,
  la salida es anterior a la siguiente jornada laboral aplicable y ninguna marca fue
  reclamada por otra jornada.
- **BD01-D03 — ambigüedad:** no se calculan horas automáticas. El día requiere validación
  mediante incidencia/regularización auditada; no se inventan entrada, salida, duración,
  jornada completa ni HE.

### 5.1 Orden de propiedad

La propiedad de una marca se resuelve antes que la aritmética:

1. Resolver la programación diaria de todo el rango, incluido D-1 y D+1.
2. Delimitar primero las jornadas `Trabajo` mediante sus ventanas vigentes.
3. Marcar como reclamadas todas las marcas de esas jornadas, incluidas las salidas que
   caen dentro de un descanso posterior.
4. Para cada descanso D, tomar únicamente marcas atribuidas al trabajador que sigan sin
   reclamar.
5. Exigir que la primera marca del intervalo pertenezca a la fecha natural D. Esto fija la
   fecha de inicio; una marca aislada de D+1 no crea retroactivamente trabajo en D.
6. Permitir como posible cierre una marca de D+1 solo antes de la apertura de la ventana de
   la siguiente jornada `Trabajo`. Esa frontera no se incluye y nunca se roba una marca ya
   reclamada.
7. Reclamar el intervalo aceptado para que ninguna fecha posterior lo reutilice.

Esto requiere una selección en dos fases. El bucle actual calcula y persiste cada día en
una sola pasada; reutilizarlo sin separar propiedad y cálculo dejaría decisiones dependientes
del orden.

### 5.2 Resultado de delimitación

```text
ResultadoDelimitacionDescanso
- Estado: SinTrabajo | Valido | Incompleto | Ambiguo
- FechaInicio: DateOnly
- Entrada: DateTime?
- Salida: DateTime?
- MarcasUsadas: colección de identidades
- MarcasObservadas: colección de identidades
- CodigoMotivo: string?
- RequiereValidacion: bool
```

La implementación futura debe reclamar por identidad estable de marca, no únicamente por
`DateTime`. Si el agregado de dominio continúa recibiendo solo horas, la identidad debe
permanecer al menos en la capa de selección/orquestación.

### 5.3 Reglas de decisión

Después de las dos capas de deduplicación existentes:

- **0 eventos:** `SinTrabajo`; estado de tareo `Descanso`; todos los minutos en cero.
- **1 evento:** `Incompleto`; cero minutos; incidencia genérica
  `MarcacionIncompletaEnDescanso`; no decidir si falta entrada o salida.
- **2 o más eventos, todos dentro de una frontera inequívoca:** primera y última marca
  bruta; las intermedias no forman pares, igual que hoy; todo el intervalo reconocido es
  HE100.
- **La última marca cae en D+1 y existe una siguiente jornada:** solo puede usarse si es
  anterior a la apertura de esa jornada y no fue reclamada por ella.
- **No hay marca inicial en D:** no formar jornada de D con marcas de D+1.
- **Fronteras no determinables, descansos consecutivos, solapamiento con la siguiente
  jornada o más de un agrupamiento plausible:** `Ambiguo`; cero minutos automáticos hasta
  confirmación. No elegir el intervalo que pague más ni el que pague menos.

No se descuenta refrigerio: sin turno ni regla confirmada, restarlo sería inventar tiempo
no trabajado. Si negocio requiere pausas, debe definir emparejamiento o descuento separado.

### 5.4 Papel de la confirmación humana

La confirmación aporta uno o ambos extremos faltantes, o selecciona cuáles marcas forman
el intervalo. Debe conservar:

- las marcas crudas originales;
- la propuesta automática, si existió;
- entrada/salida confirmadas;
- motivo y sustento;
- autor, aprobador y fechas;
- estado anterior y posterior.

No se debe insertar ni editar una `MarcacionCruda`. El mecanismo actual de regularización
es el patrón a extender, pero no puede reutilizarse literalmente porque `AplicarRegularizacion`
rechaza el cálculo cuando `Turno` es nulo.

### 5.5 Hoja de horas extra del coordinador

La hoja o listado de trabajadores voluntarios puede servir posteriormente como evidencia
operativa, autorización o apoyo de validación. No constituye `InicioTurno`/`FinTurno` ni
una frontera temporal automática mientras no contenga una programación horaria explícita
y confiable.

### 5.6 Clasificaciones superpuestas

- Si el descanso coincide con domingo, el intervalo reconocido produce HE100 una sola vez;
  no se duplican minutos ni conceptos al 100 % sobre la misma base.
- Si coincide con feriado nacional, BD-01 delimita la cantidad física y FER-NAC-01..08
  determinan la valorización. La acumulación económica residual de BD-02 permanece separada.

## 6. Casos de aceptación futuros

| ID | Escenario | Resultado esperado |
|---|---|---|
| BD01-01 | Descanso sin marcas no reclamadas | Descanso; todo 0; sin validación. |
| BD01-02 | 07:00, 17:00 en D | intervalo 600 min brutos; todo HE100. |
| BD01-03 | 07:00, 12:00, 13:00, 17:00 | extremos 07:00–17:00; intermedias declaradas; no descontar pausa. |
| BD01-04 | solo 07:00 | Incompleto; 0 minutos; incidencia genérica; validación. |
| BD01-05 | NOC D-1 19:00 → D 05:22, sin otras marcas | la salida queda reclamada por D-1; D es descanso sin trabajo. |
| BD01-06 | NOC D-1 termina 05:22; trabajo en descanso 07:00–17:00 | 05:22 excluida; 07:00–17:00 = 600 HE100. |
| BD01-07 | descanso D 19:00 → D+1 05:22; siguiente trabajo abre después | intervalo cruza medianoche y se imputa a D; 622 HE100. |
| BD01-08 | descanso 19:00 y marca coincidente/solapada con apertura de D+1 | Ambiguo; no cálculo automático; ninguna marca duplicada. |
| BD01-09 | dos marcas dentro de la ventana de la jornada siguiente | pertenecen a la jornada siguiente; no al descanso. |
| BD01-10 | rebotes 07:00, 07:00:40 sin salida | un evento efectivo; Incompleto; 0 minutos. |
| BD01-11 | dos descansos consecutivos con marcas alrededor de medianoche | Ambiguo salvo confirmación explícita; no inferir propiedad por día civil. |
| BD01-12 | regularización confirma extremo faltante | marca cruda intacta; intervalo auditado; todo HE100. |
| BD01-13 | dos marcas accidentales forman agrupamientos plausibles distintos | Ambiguo; no tomar ciegamente primera/última global. |
| BD01-14 | primera marca solo en D+1 | no genera trabajo del descanso D. |

Los minutos de BD01-02/03/06/07 son brutos porque no existe una deducción de refrigerio
confirmada para trabajo en descanso.

## 7. Cierre funcional

Negocio aprobó la **Opción E** y sus tres fronteras:

1. la jornada de descanso pertenece al día natural de su primera marca;
2. puede terminar en D+1 antes de la ventana de la siguiente jornada laboral;
3. los casos ambiguos no se calculan automáticamente y requieren confirmación auditada.

Por tanto:

```text
BD-01 = CERRADA FUNCIONALMENTE
```

R-P1-05 implementó la selección en dos pasadas, la identidad estable de marca y los
contratos ejecutables ROT-MARK-01..06. BD-02 fue cerrada posteriormente en
`BD_02_HOLIDAY_REST_COINCIDENCE.md`; descanso sustitutorio y día no laborable compensable
siguen fuera de este alcance.
