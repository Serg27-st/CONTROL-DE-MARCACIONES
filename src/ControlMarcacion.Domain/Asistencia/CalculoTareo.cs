using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;

namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Justificación vigente para el día que se está calculando.
/// </summary>
/// <remarks>
/// Es lo editable de la ecuación <c>marcador + justificaciones = tareo</c>. Llega ya
/// resuelta: quien llama decidió cuál aplica y trajo el comportamiento del tipo de
/// ausencia, para que el cálculo siga sin consultar nada.
/// </remarks>
public sealed record JustificacionAplicable
{
    public required long Id { get; init; }

    public required TipoJustificacion Tipo { get; init; }

    /// <summary>
    /// El tipo de ausencia hace que el día cuente como asistido, es decir, que no genere
    /// falta.
    /// </summary>
    /// <remarks>
    /// ⚠️ No decide por sí solo si el día se paga: eso lo resuelve
    /// <see cref="PagaJornadaOrdinaria"/>. Un tipo puede computar como asistencia y no
    /// remunerarse —la licencia sin goce es exactamente ese caso—.
    /// </remarks>
    public bool ComputaComoAsistencia { get; init; }

    /// <summary>
    /// Comportamiento del tipo de ausencia. Nulo cuando la justificación no lleva tipo
    /// —una tardanza perdonada, un permiso—.
    /// </summary>
    public ComportamientoAusencia? Comportamiento { get; init; }

    /// <summary>
    /// El día se paga con la jornada ordinaria del turno.
    /// </summary>
    /// <remarks>
    /// Son dos condiciones y no una: el día tiene que contar como asistido <b>y</b> el
    /// tipo tiene que ser remunerado. Antes bastaba con <see cref="ComputaComoAsistencia"/>,
    /// una casilla editable desde el catálogo, y con ella en verdadero una
    /// <b>licencia sin goce pagaba ocho horas</b> — que es lo que hacía el catálogo
    /// sembrado—. El comportamiento es la decisión legal del tipo, no una casilla, así que
    /// es el que manda: marcarla de nuevo en la pantalla ya no vuelve a pagar una licencia
    /// sin goce.
    /// </remarks>
    public bool PagaJornadaOrdinaria =>
        ComputaComoAsistencia
        && Comportamiento is not ComportamientoAusencia.JustificadaNoRemunerada;

    /// <summary>
    /// Minutos perdonados en una justificación parcial. Nulo significa "todos".
    /// </summary>
    public int? MinutosJustificados { get; init; }

    /// <summary>
    /// Hora en que empieza el permiso por horas. Solo viaja con
    /// <see cref="TipoJustificacion.PermisoParcial"/>.
    /// </summary>
    /// <remarks>
    /// Es lo que permite ubicar el permiso contra el turno: un permiso al final de la
    /// jornada descuenta salida anticipada y uno al inicio descuenta tardanza. Sin la
    /// ventana, el motor tendría que suponer, y suponer aquí es inventar minutos.
    /// </remarks>
    public TimeOnly? InicioPermiso { get; init; }

    /// <summary>Hora en que termina el permiso por horas.</summary>
    public TimeOnly? FinPermiso { get; init; }

    /// <summary>
    /// Hora de entrada verificada por quien autoriza, cuando el marcador no la registró.
    /// </summary>
    /// <remarks>
    /// Solo viaja con <see cref="TipoJustificacion.MarcacionRegularizada"/>. No sustituye a
    /// la marcación del equipo: la completa.
    /// </remarks>
    public DateTime? EntradaRegularizada { get; init; }

    /// <summary>Hora de salida verificada por quien autoriza.</summary>
    public DateTime? SalidaRegularizada { get; init; }

    public required string Motivo { get; init; }
}

/// <summary>
/// Datos de entrada para calcular el tareo de un trabajador en un día.
/// </summary>
/// <remarks>
/// Todo lo que el cálculo necesita llega por aquí. La resolución de cuál era el turno
/// vigente, qué marcaciones caen en la jornada y si el día era feriado la hace quien
/// llama; el cálculo en sí no consulta nada.
/// </remarks>
public sealed record EntradaCalculo
{
    /// <summary>Día al que se imputa la jornada.</summary>
    /// <remarks>
    /// En un turno que cruza medianoche la jornada se imputa al día en que <b>inicia</b>,
    /// aunque termine al siguiente. Es la principal fuente de errores en estos sistemas.
    /// </remarks>
    public required DateOnly Fecha { get; init; }

    /// <summary>Turno vigente. Nulo si el trabajador no tenía turno asignado ese día.</summary>
    public Turno? Turno { get; init; }

    /// <summary>El día fue resuelto como descanso explícito de una rotación.</summary>
    public bool EsDescansoProgramado { get; init; }

    /// <summary>
    /// Las marcas residuales no permiten fijar con seguridad el cierre del trabajo en
    /// descanso. En ese caso el motor conserva cero minutos y exige validación.
    /// </summary>
    public bool DelimitacionDescansoAmbigua { get; init; }

    /// <summary>
    /// De dónde salió el turno cuando no es un turno fijo —«Cuadrilla B: turno NOC.»—. Se
    /// antepone a la observación.
    /// </summary>
    /// <remarks>
    /// Con una cuadrilla, la ficha de la persona no dice qué turno tuvo cada día: lo dice la
    /// programación del grupo. Sin esta frase, quien audita un día nocturno no tendría cómo
    /// saber de dónde salió.
    /// </remarks>
    public string? OrigenTurno { get; init; }

    /// <summary>
    /// Por qué no hubo turno ese día, cuando se sabe algo más que «no tenía asignación»: por
    /// ejemplo, una cuadrilla sin programación vigente.
    /// </summary>
    public string? MotivoSinTurno { get; init; }

    /// <summary>
    /// Marcaciones de la jornada, ya deduplicadas. Pueden abarcar dos días naturales
    /// cuando el turno cruza medianoche.
    /// </summary>
    public IReadOnlyList<DateTime> Marcaciones { get; init; } = [];

    /// <summary>
    /// Marcaciones del trabajador que cayeron <b>fuera</b> de la ventana de su turno y no
    /// entran al cálculo.
    /// </summary>
    /// <remarks>
    /// Llegan para poder declararlas, no para usarlas. Se descartaban en silencio: una
    /// marca a las 05:30 con turno de 08:00 no aparecía en la observación, no generaba
    /// aviso y solo quedaba en <c>marcacion_cruda</c>, que nadie consulta a mano. Puede ser
    /// un turno cubierto, una rotación no anotada o un error de aparato, y ninguna de las
    /// tres la decide el motor: por eso se señalan y no se computan.
    /// </remarks>
    public IReadOnlyList<DateTime> MarcacionesFueraVentana { get; init; } = [];

    /// <summary>El trabajador tenía vínculo laboral vigente ese día.</summary>
    public bool TieneVinculo { get; init; } = true;

    /// <summary>
    /// No se cargó el archivo del aparato que le correspondía a esta persona ese día.
    /// </summary>
    /// <remarks>
    /// <b>Cambia por completo qué significa un día sin marcaciones.</b> Sin este dato, un
    /// día sin archivo cargado produce una falta por cada trabajador del aparato, con una
    /// observación que afirma algo que el sistema no sabe —«no registró ninguna
    /// marcación»—, indistinguible de una falta real en el Excel que va a planilla.
    ///
    /// La cobertura por aparato ya existía y ya alimentaba el calendario de Marcaciones y
    /// la portada; lo que faltaba era que el cálculo la mirara.
    /// </remarks>
    public bool SinCoberturaDeCarga { get; init; }

    /// <summary>
    /// Otros turnos con los que comparar las marcas del día, para detectar que el turno
    /// asignado no es el que se trabajó.
    /// </summary>
    /// <remarks>
    /// Vacío desactiva la detección. No cambia ni un minuto del cálculo: solo señala el día
    /// para que una persona decida, porque corregir una rotación es una decisión con autor.
    /// </remarks>
    public IReadOnlyList<Turno> TurnosCandidatos { get; init; } = [];

    public bool EsFeriado { get; init; }

    /// <summary>
    /// Justificación aplicable a este día, si existe.
    /// </summary>
    /// <remarks>
    /// Precedencia del sistema: <b>justificación &gt; solicitud &gt; marcación</b>. La
    /// solicitud aprobada ya se convirtió en justificación antes de llegar aquí, así que
    /// el cálculo solo conoce este último eslabón.
    /// </remarks>
    public JustificacionAplicable? Justificacion { get; init; }
}

/// <summary>
/// Resultado del cálculo de un día. Todos los tiempos en minutos enteros.
/// </summary>
public sealed record ResultadoCalculo
{
    public DateTime? EntradaReal { get; init; }

    public DateTime? SalidaReal { get; init; }

    public int MinutosTrabajados { get; init; }

    public int MinutosOrdinarios { get; init; }

    public int MinutosExtra25 { get; init; }

    public int MinutosExtra35 { get; init; }

    /// <summary>
    /// Minutos al 100 %. En un día íntegramente no laborable son todos los trabajados; en
    /// una jornada sabatina que entra al domingo son el remanente que no pertenece a
    /// ordinario, HE25 ni HE35.
    /// </summary>
    /// <remarks>
    /// Es derivado: ya se persisten el total físico y las categorías ordinario/HE25/HE35;
    /// su remanente conserva el HE100. Así el cruce sábado→domingo no obliga a partir la
    /// jornada ni a cambiar el esquema.
    /// </remarks>
    public int MinutosExtra100 =>
        TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno
            ? MinutosTrabajados
            : Math.Max(0, MinutosTrabajados - MinutosOrdinarios - MinutosExtra25 - MinutosExtra35);

    public int MinutosNocturnos { get; init; }

    /// <summary>Minutos de sobretiempo al 25 % que cayeron dentro de la franja nocturna.</summary>
    /// <remarks>
    /// El tareo final de GCA separa el sobretiempo en dos bloques, diurno y nocturno, cada
    /// uno con sus tramos 25 / 35 / 100. Sin este desglose la hoja sale con el bloque
    /// nocturno en blanco y alguien lo termina llenando a mano, que es justo lo que este
    /// sistema viene a eliminar.
    ///
    /// Es un <b>subconjunto</b> de <see cref="MinutosExtra25"/>, no un concepto aparte: los
    /// minutos diurnos del tramo son la resta.
    /// </remarks>
    public int MinutosExtra25Nocturnos { get; init; }

    /// <summary>Minutos de sobretiempo al 35 % que cayeron dentro de la franja nocturna.</summary>
    public int MinutosExtra35Nocturnos { get; init; }

    public int MinutosTardanza { get; init; }

    public int MinutosSalidaAnticipada { get; init; }

    /// <summary>Qué clase de día no laborable se trabajó, si corresponde.</summary>
    public TipoDiaNoLaborable TipoDiaNoLaborable { get; init; }

    public bool EsDescansoProgramado { get; init; }

    public TipoCoincidenciaFeriadoDescanso CoincidenciaFeriadoDescanso { get; init; }

    public bool EsDiaNoLaborable => TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno;

    public required EstadoTareo Estado { get; init; }

    /// <summary>Qué hay que resolver en este día, si hay algo.</summary>
    public TipoIncidencia? TipoIncidencia { get; init; }

    /// <summary>El día espera una decisión humana: es lo que lo pone en la bandeja.</summary>
    public bool RequiereValidacion { get; init; }

    /// <summary>Cuántas marcas del día quedaron fuera de la ventana del turno.</summary>
    public int MarcacionesFueraVentana { get; init; }

    /// <summary>Explicación legible de por qué el día quedó así.</summary>
    public required string Observacion { get; init; }

    /// <summary>Justificación que modificó el resultado, si alguna lo hizo.</summary>
    public long? JustificacionId { get; init; }

    /// <summary>
    /// Estado que tenía el día antes de aplicar la justificación.
    /// </summary>
    /// <remarks>
    /// Es la evidencia de auditoría: permite ver que un día era Falta y una justificación
    /// lo convirtió en AusenciaJustificada, sin tener que reconstruirlo.
    /// </remarks>
    public string? EstadoAnterior { get; init; }

    public int MinutosExtraTotales => MinutosExtra25 + MinutosExtra35;

    /// <summary>
    /// Minutos en franja nocturna que corresponden a jornada ordinaria: la base de la
    /// bonificación nocturna, ya descontado el sobretiempo.
    /// </summary>
    public int MinutosOrdinariosNocturnos =>
        Math.Max(0, MinutosNocturnos - MinutosExtra25Nocturnos - MinutosExtra35Nocturnos);
}

/// <summary>
/// Motor de cálculo del tareo diario.
/// </summary>
/// <remarks>
/// Es una clase <b>pura</b>: misma entrada, misma salida, sin base de datos ni reloj.
/// Eso es lo que permite probar el turno nocturno o el feriado trabajado con fechas
/// literales y sin levantar SQL Server.
///
/// Decisiones que gobiernan el cálculo, todas visibles aquí:
/// <list type="number">
///   <item><b>Nunca se inventa una hora faltante.</b> Con una sola marcación el día queda
///   Incompleto y con cero minutos. En el archivo real esto pasa en el 11 % de las
///   jornadas, así que el módulo de justificaciones es el que resuelve esos días.</item>
///   <item><b>La jornada se computa desde la hora del turno, no desde la marca.</b>
///   Llegar antes no genera sobretiempo: el D.S. 007-2002-TR art. 9 exige que el
///   sobretiempo sea voluntario y autorizado. Salir después sí se computa, porque ahí
///   sí hubo trabajo, salvo los minutos dentro de la gracia de salida del turno
///   (<see cref="Turno.MinutosGraciaSalida"/>, 0 por omisión), que quedan declarados en
///   la observación.</item>
///   <item><b>Todo resultado lleva observación.</b> RRHH tiene que poder auditar sin
///   leer código.</item>
/// </list>
/// </remarks>
public static class CalculadoraTareo
{
    /// <summary>Inicio de la franja nocturna según el D.S. 007-2002-TR, art. 8.</summary>
    private static readonly TimeOnly InicioNocturno = new(22, 0);

    /// <summary>Fin de la franja nocturna.</summary>
    private static readonly TimeOnly FinNocturno = new(6, 0);

    /// <summary>Las dos primeras horas extras llevan 25 %; de ahí en adelante, 35 %.</summary>
    private const int MinutosExtraAlVeinticinco = 120;

    /// <summary>
    /// Minutos después del fin teórico a partir de los cuales, de lunes a viernes, el
    /// exceso se reconoce como sobretiempo (ACCEPTANCE_RULES VIG-HE-03). No es un descuento:
    /// alcanzado el umbral se cuenta todo desde el fin teórico.
    /// </summary>
    private const int UmbralSobretiempoMinutos = 15;

    /// <summary>
    /// Margen dentro del cual dos marcas consecutivas son el mismo paso por el lector.
    /// </summary>
    /// <remarks>
    /// El G3 acepta un segundo dedo antes de que la persona termine de retirarlo, y en
    /// planta está extendida la costumbre de "marcar dos veces por si acaso".
    ///
    /// Es una <b>constante y no un campo configurable por turno</b>, a propósito: mide una
    /// característica física del lector, no una política de GCA. Un campo editable
    /// invitaría a subirlo a media hora para "limpiar" marcas incómodas, y eso ya no sería
    /// consolidar rebotes sino reescribir la jornada — que es trabajo de Justificaciones,
    /// donde queda auditado y con autor.
    /// </remarks>
    private static readonly TimeSpan VentanaRebote = TimeSpan.FromMinutes(2);

    public static ResultadoCalculo Calcular(EntradaCalculo entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var baseCalculo = CalcularSinJustificar(entrada);

        // La justificación se aplica ENCIMA del cálculo crudo, nunca en lugar de él. Así el
        // resultado conserva qué decía el marcador y qué lo modificó, que es exactamente lo
        // que hace auditable al sistema.
        var resultado = entrada.Justificacion is { } justificacion
            ? Aplicar(justificacion, baseCalculo, entrada.Turno, entrada.Fecha)
            : baseCalculo;

        // Las incidencias se resuelven DESPUÉS de la justificación: manda el resultado que
        // esta dejó. Una justificación parcial puede coexistir con otro problema pendiente;
        // una que realmente completa o explica el día deja un resultado sin incidencia.
        resultado = ConIncidencia(resultado, entrada);

        return entrada.OrigenTurno is { } origen
            ? resultado with { Observacion = $"{origen} {resultado.Observacion}" }
            : resultado;
    }

    /// <summary>
    /// Decide qué hay que resolver en el día, si hay algo, y lo declara en la observación.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola incidencia por día</b>, por orden de gravedad. Acumular varias llenaría
    /// la bandeja de filas repetidas del mismo día y ninguna diría por dónde empezar:
    /// </para>
    /// <list type="number">
    ///   <item><b>Sin cobertura de carga</b>: no llegó el archivo. Es lo primero porque
    ///   invalida cualquier lectura de lo demás — sin datos del aparato, «no marcó» no
    ///   significa nada.</item>
    ///   <item><b>Fuera de turno</b>: hubo marcas y quedaron fuera de la ventana. Solo se
    ///   levanta cuando el día salió falta o incompleto, es decir cuando esas marcas
    ///   <i>podrían</i> haber cambiado el resultado. Con la jornada ya resuelta se declaran
    ///   igual en la observación, pero no se manda a nadie a revisar un día correcto.</item>
    ///   <item><b>Ingreso o salida sin registrar</b>: la marca impar. Se distingue cuál
    ///   falta porque lo que se regulariza es distinto.</item>
    ///   <item><b>Turno discrepante</b>: el día cuadra, pero con otro turno. Va última
    ///   porque es una sospecha, no una certeza.</item>
    /// </list>
    /// <para>
    /// La mera existencia de una justificación no resuelve la incidencia. La decisión se
    /// toma sobre el resultado posterior a aplicarla: si sigue incompleto, todavía falta un
    /// extremo; si quedó completo o explicado, no vuelve a la bandeja.
    /// </para>
    /// </remarks>
    private static ResultadoCalculo ConIncidencia(ResultadoCalculo resultado, EntradaCalculo entrada)
    {
        var fuera = entrada.MarcacionesFueraVentana.Count;

        var conConteo = resultado with
        {
            MarcacionesFueraVentana = fuera,
            Observacion = fuera == 0
                ? resultado.Observacion
                : $"{resultado.Observacion} {FraseFueraDeVentana(entrada.MarcacionesFueraVentana)}"
        };

        if (resultado.RequiereValidacion && resultado.TipoIncidencia is not null)
        {
            return conConteo;
        }

        if (entrada.SinCoberturaDeCarga && !entrada.EsDescansoProgramado)
        {
            return conConteo with
            {
                TipoIncidencia = Asistencia.TipoIncidencia.SinCoberturaDeCarga,
                RequiereValidacion = true
            };
        }

        var sinJornada = resultado.Estado is EstadoTareo.Falta or EstadoTareo.Incompleto;

        if (fuera > 0 && sinJornada)
        {
            return conConteo with
            {
                TipoIncidencia = Asistencia.TipoIncidencia.FueraDeTurno,
                RequiereValidacion = true
            };
        }

        if (resultado.Estado == EstadoTareo.Incompleto)
        {
            return conConteo with
            {
                // Con entrada guardada, lo que falta es la salida, y al revés. El motor ya
                // decidió a qué extremo se parece la marca suelta.
                TipoIncidencia = resultado.EntradaReal is not null
                    ? Asistencia.TipoIncidencia.SalidaNoRegistrada
                    : Asistencia.TipoIncidencia.IngresoNoRegistrado,
                RequiereValidacion = true
            };
        }

        if (entrada.Turno is { } turno
            && DetectorTurnoDiscrepante.Detectar(
                entrada.Fecha, turno, entrada.TurnosCandidatos, entrada.Marcaciones) is { } discrepancia)
        {
            return conConteo with
            {
                TipoIncidencia = Asistencia.TipoIncidencia.TurnoDiscrepante,
                RequiereValidacion = true,
                Observacion = $"{conConteo.Observacion} {discrepancia.Explicacion}"
            };
        }

        return conConteo;
    }

    /// <summary>
    /// Frase que declara las marcas descartadas por caer fuera de la ventana del turno.
    /// </summary>
    /// <remarks>
    /// Va con las horas y no solo con el conteo: «se descartaron 2 marcas» no le dice a
    /// nadie qué pasó, y «a las 05:30 y 05:34» sí — esa es la pista de que la persona
    /// trabajó el turno de otro.
    /// </remarks>
    private static string FraseFueraDeVentana(IReadOnlyList<DateTime> marcas)
    {
        var horas = string.Join(", ", marcas.OrderBy(m => m).Select(m => m.ToString("dd/MM HH:mm", null)));

        return $"Quedaron fuera de la ventana de este turno {marcas.Count} marca(s) — {horas} — " +
               "que no se computaron: revise si corresponden a otro turno o a otra fecha.";
    }

    private static ResultadoCalculo CalcularSinJustificar(EntradaCalculo entrada)
    {
        if (!entrada.TieneVinculo)
        {
            return Vacio(
                EstadoTareo.SinVinculo,
                "Fuera del vínculo laboral: el día es anterior al ingreso o posterior al cese.");
        }

        if (entrada.EsDescansoProgramado)
        {
            return CalcularDescansoProgramado(entrada);
        }

        if (entrada.Turno is not { } turno)
        {
            // Sin turno no hay contra qué comparar. No es falta: es un dato que falta.
            return Vacio(
                EstadoTareo.SinHorario,
                entrada.MotivoSinTurno
                ?? "El trabajador no tenía turno asignado ese día. Asigne el turno para poder calcular.");
        }

        var marcaciones = entrada.Marcaciones.OrderBy(m => m).ToList();

        // Los rebotes se colapsan ANTES de decidir si el día tiene jornada: dos marcas a
        // 40 segundos son una persona entrando, no una entrada y una salida. Se usa solo
        // para CONTAR eventos —el cálculo de horas sigue tomando el mínimo y el máximo en
        // bruto—, así que consolidar no le quita ni un minuto de sobretiempo a nadie.
        var eventos = ConsolidarRebotes(marcaciones);
        var rebotes = Rebotes(marcaciones, eventos);

        var aplicaHoy = TurnoAplicaEn(turno, entrada.Fecha);

        // Feriado y descanso no son falta: son días en que no correspondía trabajar.
        // Si además se trabajó, el día lleva sobretasa y por eso se marca como no laborable.
        if (entrada.EsFeriado || !aplicaHoy)
        {
            // El feriado manda sobre el descanso semanal: si un feriado cae en domingo,
            // el concepto que aplica es el de feriado.
            var tipo = entrada.EsFeriado
                ? TipoDiaNoLaborable.Feriado
                : TipoDiaNoLaborable.DescansoSemanal;

            // El 1 de mayo necesita conservar si coincidió con el descanso semanal. No
            // basta mirar EsFeriado: esa condición también es verdadera en un viernes
            // laborable. Para el motor, un domingo siempre es descanso semanal aunque un
            // turno DOM lo declare aplicable; el resto de días descansa cuando el turno no
            // aplica. El feriado ordinario conserva la misma precedencia física, pero se
            // registra la coincidencia para que la liquidación no duplique conceptos.
            var coincideConDescanso = !aplicaHoy || entrada.Fecha.DayOfWeek == DayOfWeek.Sunday;
            var coincidencia = entrada.EsFeriado && coincideConDescanso
                ? entrada.Fecha.Month == 5 && entrada.Fecha.Day == 1
                    ? TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo
                    : TipoCoincidenciaFeriadoDescanso.FeriadoOrdinario
                : TipoCoincidenciaFeriadoDescanso.Ninguna;

            var etiqueta = entrada.EsFeriado ? "Feriado" : "Día de descanso semanal";

            if (eventos.Count == 0)
            {
                return new ResultadoCalculo
                {
                    TipoDiaNoLaborable = tipo,
                    CoincidenciaFeriadoDescanso = coincidencia,
                    Estado = entrada.EsFeriado ? EstadoTareo.Feriado : EstadoTareo.Descanso,
                    Observacion = $"{etiqueta}: no correspondía trabajar y no hubo marcaciones."
                };
            }

            if (eventos.Count == 1)
            {
                return Incompleta(
                    eventos[0], turno, entrada.Fecha, tipo,
                    $"{etiqueta} con una sola marcación efectiva a las {eventos[0]:HH:mm}." +
                    $"{rebotes} No se calculan horas: falta la marcación complementaria.",
                    coincidencia);
            }

            return Jornada(entrada, turno, marcaciones, eventos, tipo, etiqueta, coincidencia);
        }

        if (eventos.Count == 0)
        {
            // ⚠️ Sin archivo del aparato NO hay falta: no se sabe si la persona vino, porque
            // el dato nunca llegó al sistema. Afirmar que no marcó es afirmar lo que no se
            // sabe, y en planilla eso se cobra. Un día sin cargar producía una falta por
            // cada trabajador de ese aparato, idéntica a una falta real.
            return entrada.SinCoberturaDeCarga
                ? Vacio(
                    EstadoTareo.PendienteValidacion,
                    "No se cargó el archivo del marcador de esta persona para esta fecha, " +
                    "así que no se sabe si asistió. No se computa como falta: cargue el " +
                    "archivo del aparato y recalcule, o regularice el día.")
                : Vacio(
                    EstadoTareo.Falta,
                    "No registró ninguna marcación en un día laborable.");
        }

        if (eventos.Count == 1)
        {
            return Incompleta(
                eventos[0], turno, entrada.Fecha, TipoDiaNoLaborable.Ninguno,
                $"Marcación impar: una sola marcación efectiva, a las {eventos[0]:HH:mm}." +
                $"{rebotes} No se calculan horas porque no se conoce la hora faltante.");
        }

        return Jornada(entrada, turno, marcaciones, eventos, TipoDiaNoLaborable.Ninguno, etiqueta: null);
    }

    /// <summary>Calcula trabajo residual en un descanso sin inventar un turno teórico.</summary>
    private static ResultadoCalculo CalcularDescansoProgramado(EntradaCalculo entrada)
    {
        var marcaciones = entrada.Marcaciones.OrderBy(m => m).ToList();
        var eventos = ConsolidarRebotes(marcaciones);
        var rebotes = Rebotes(marcaciones, eventos);
        var tipo = entrada.EsFeriado
            ? TipoDiaNoLaborable.Feriado
            : TipoDiaNoLaborable.DescansoSemanal;
        var coincidencia = entrada.EsFeriado
            ? entrada.Fecha.Month == 5 && entrada.Fecha.Day == 1
                ? TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo
                : TipoCoincidenciaFeriadoDescanso.FeriadoOrdinario
            : TipoCoincidenciaFeriadoDescanso.Ninguna;
        var etiqueta = entrada.EsFeriado
            ? coincidencia == TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo
                ? "1 de mayo coincidente con descanso programado"
                : "Feriado coincidente con descanso programado"
            : "Descanso programado";

        if (entrada.DelimitacionDescansoAmbigua)
        {
            return new ResultadoCalculo
            {
                EsDescansoProgramado = true,
                CoincidenciaFeriadoDescanso = coincidencia,
                TipoDiaNoLaborable = tipo,
                Estado = EstadoTareo.PendienteValidacion,
                TipoIncidencia = Asistencia.TipoIncidencia.DelimitacionDescansoAmbigua,
                RequiereValidacion = true,
                Observacion =
                    $"{etiqueta}: las marcas residuales no delimitan inequívocamente el intervalo " +
                    "trabajado. No se calculan minutos; regularice las horas verificadas."
            };
        }

        if (eventos.Count == 0)
        {
            return new ResultadoCalculo
            {
                EsDescansoProgramado = true,
                CoincidenciaFeriadoDescanso = coincidencia,
                TipoDiaNoLaborable = tipo,
                Estado = entrada.EsFeriado ? EstadoTareo.Feriado : EstadoTareo.Descanso,
                Observacion = $"{etiqueta}: no correspondía trabajar y no hubo marcaciones."
            };
        }

        if (eventos.Count == 1)
        {
            return new ResultadoCalculo
            {
                EntradaReal = eventos[0],
                EsDescansoProgramado = true,
                CoincidenciaFeriadoDescanso = coincidencia,
                TipoDiaNoLaborable = tipo,
                Estado = EstadoTareo.Incompleto,
                Observacion =
                    $"{etiqueta} con una sola marcación efectiva a las {eventos[0]:HH:mm}." +
                    $"{rebotes} No se calculan horas: falta la marcación complementaria."
            };
        }

        var inicio = marcaciones[0];
        var fin = marcaciones[^1];
        var minutos = (int)(fin - inicio).TotalMinutes;

        if (minutos <= 0)
        {
            return new ResultadoCalculo
            {
                EntradaReal = inicio,
                EsDescansoProgramado = true,
                CoincidenciaFeriadoDescanso = coincidencia,
                TipoDiaNoLaborable = tipo,
                Estado = EstadoTareo.Incompleto,
                Observacion = $"{etiqueta}: las marcas no forman un intervalo válido."
            };
        }

        var intermedias = Math.Max(0, eventos.Count - 2);
        var detalleIntermedias = intermedias == 0
            ? string.Empty
            : $" Se ignoraron {intermedias} marca(s) intermedia(s); el intervalo va de la primera a la última.";

        return new ResultadoCalculo
        {
            EntradaReal = inicio,
            SalidaReal = fin,
            MinutosTrabajados = minutos,
            MinutosNocturnos = MinutosEnFranjaNocturna(inicio, fin),
            TipoDiaNoLaborable = tipo,
            EsDescansoProgramado = true,
            CoincidenciaFeriadoDescanso = coincidencia,
            Estado = EstadoTareo.Puntual,
            Observacion =
                $"{etiqueta} trabajado: entrada {inicio:dd/MM HH:mm}, salida {fin:dd/MM HH:mm}; " +
                $"{minutos} min físicos, todos al 100 %, sin refrigerio, tardanza ni salida anticipada." +
                detalleIntermedias
        };
    }

    /// <summary>
    /// Aplica la justificación sobre el resultado crudo del marcador.
    /// </summary>
    /// <remarks>
    /// Cada tipo de justificación toca una cosa distinta y solo esa:
    /// <list type="bullet">
    ///   <item><b>Ausencia</b> e <b>incompleta</b>: cambian el estado. Si el tipo de
    ///   ausencia paga la jornada (<see cref="JustificacionAplicable.PagaJornadaOrdinaria"/>),
    ///   el día se remunera con la jornada ordinaria del turno — es lo que hace que las
    ///   vacaciones se paguen y que la licencia sin goce no.</item>
    ///   <item><b>Tardanza</b> y <b>salida anticipada</b>: descuentan minutos, total o
    ///   parcialmente. No tocan las horas trabajadas: el trabajador estuvo lo que estuvo.</item>
    ///   <item><b>Permiso parcial</b>: descuenta los minutos que su ventana cubre, del
    ///   extremo de la jornada donde realmente cae.</item>
    /// </list>
    ///
    /// Nunca genera horas extras: justificar una ausencia no puede producir sobretiempo.
    /// </remarks>
    private static ResultadoCalculo Aplicar(
        JustificacionAplicable justificacion,
        ResultadoCalculo calculo,
        Turno? turno,
        DateOnly fecha)
    {
        var estadoAnterior = calculo.Estado.ToString();

        return justificacion.Tipo switch
        {
            TipoJustificacion.Ausencia or TipoJustificacion.MarcacionIncompleta =>
                calculo with
                {
                    Estado = EstadoTareo.AusenciaJustificada,
                    // Solo los tipos remunerados pagan el día. Una licencia sin goce
                    // justifica la falta pero deja el día en cero.
                    MinutosTrabajados = justificacion.PagaJornadaOrdinaria && turno is not null
                        ? turno.MinutosJornadaOrdinaria
                        : calculo.MinutosTrabajados,
                    MinutosOrdinarios = justificacion.PagaJornadaOrdinaria && turno is not null
                        ? turno.MinutosJornadaOrdinaria
                        : calculo.MinutosOrdinarios,
                    MinutosTardanza = 0,
                    MinutosSalidaAnticipada = 0,
                    JustificacionId = justificacion.Id,
                    EstadoAnterior = estadoAnterior,
                    Observacion = $"{calculo.Observacion} JUSTIFICADO: {justificacion.Motivo}."
                },

            TipoJustificacion.Tardanza =>
                AplicarDescuento(
                    calculo, justificacion, estadoAnterior,
                    calculo.MinutosTardanza,
                    "TARDANZA JUSTIFICADA",
                    (c, restante) => c with
                    {
                        MinutosTardanza = restante,
                        Estado = restante == 0 && c.Estado == EstadoTareo.Tardanza
                            ? EstadoTareo.Puntual
                            : c.Estado
                    }),

            TipoJustificacion.SalidaAnticipada =>
                AplicarDescuento(
                    calculo, justificacion, estadoAnterior,
                    calculo.MinutosSalidaAnticipada,
                    "SALIDA ANTICIPADA JUSTIFICADA",
                    (c, restante) => c with { MinutosSalidaAnticipada = restante }),

            TipoJustificacion.PermisoParcial =>
                AplicarPermisoParcial(justificacion, calculo, turno, fecha, estadoAnterior),

            TipoJustificacion.MarcacionRegularizada =>
                AplicarRegularizacion(justificacion, calculo, turno, fecha, estadoAnterior),

            _ => calculo
        };
    }

    /// <summary>
    /// Completa la jornada con la hora que el marcador no registró y la verificó quien
    /// autoriza.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la diferencia entre corregir un tareo y falsificar un registro biométrico: la
    /// marcación original <b>no se toca</b>, la hora regularizada llega por un camino
    /// aparte —con autor, fecha y motivo—, y el día se recalcula como la jornada que
    /// realmente ocurrió: con su tardanza, su sobretiempo y su nocturnidad reales.
    /// </para>
    /// <para>
    /// Hasta ahora lo más parecido era declarar el día ausencia justificada, que es falso
    /// —la persona trabajó— y además pagaba una jornada ordinaria plana: en un turno de doce
    /// horas, cuatro horas y todo el sobretiempo se evaporaban.
    /// </para>
    /// <para>
    /// La hora regularizada <b>completa</b>, no sustituye: si el marcador registró la salida
    /// y se regulariza la entrada, la salida que entra al cálculo sigue siendo la del
    /// aparato. Quien regulariza aporta lo que falta, no reescribe lo que hay.
    /// </para>
    /// <para>
    /// ⚠️ El día pasa por <see cref="Jornada"/>, el mismo camino que cualquier otro: no hay
    /// una segunda aritmética para los días regularizados. La observación conserva lo que
    /// dijo el marcador y declara qué hora se agregó y quién la respalda.
    /// </para>
    /// </remarks>
    private static ResultadoCalculo AplicarRegularizacion(
        JustificacionAplicable justificacion,
        ResultadoCalculo calculo,
        Turno? turno,
        DateOnly fecha,
        string estadoAnterior)
    {
        var entrada = justificacion.EntradaRegularizada ?? calculo.EntradaReal;
        var salida = justificacion.SalidaRegularizada ?? calculo.SalidaReal;

        if (entrada is null || salida is null || salida <= entrada)
        {
            // No alcanza para armar una jornada. Se deja el día como estaba y se dice por
            // qué: una regularización a medias no puede inventar el otro extremo.
            return calculo with
            {
                JustificacionId = justificacion.Id,
                EstadoAnterior = estadoAnterior,
                Observacion =
                    $"{calculo.Observacion} REGULARIZACIÓN INCOMPLETA: falta la otra hora o " +
                    $"no forma una jornada válida, el día queda como estaba. {justificacion.Motivo}."
            };
        }

        if (calculo.EsDescansoProgramado)
        {
            var recalculadoDescanso = CalcularDescansoProgramado(new EntradaCalculo
            {
                Fecha = fecha,
                EsDescansoProgramado = true,
                EsFeriado = calculo.TipoDiaNoLaborable == TipoDiaNoLaborable.Feriado,
                Marcaciones = [entrada.Value, salida.Value]
            });

            return recalculadoDescanso with
            {
                EntradaReal = calculo.EntradaReal,
                SalidaReal = calculo.SalidaReal,
                JustificacionId = justificacion.Id,
                EstadoAnterior = estadoAnterior,
                Observacion =
                    $"{calculo.Observacion} REGULARIZADO: {Regularizadas(justificacion)}. " +
                    $"El descanso trabajado se calcula de {entrada:dd/MM HH:mm} a " +
                    $"{salida:dd/MM HH:mm}; las marcaciones crudas se conservan. " +
                    $"{justificacion.Motivo}."
            };
        }

        if (turno is null)
        {
            return calculo with
            {
                JustificacionId = justificacion.Id,
                EstadoAnterior = estadoAnterior,
                Observacion =
                    $"{calculo.Observacion} REGULARIZACIÓN NO APLICADA: el día no tiene turno " +
                    $"ni descanso programado. {justificacion.Motivo}."
            };
        }

        var recalculado = Jornada(
            new EntradaCalculo { Fecha = fecha, Turno = turno },
            turno,
            [entrada.Value, salida.Value],
            [entrada.Value, salida.Value],
            calculo.TipoDiaNoLaborable,
            etiqueta: null);

        return recalculado with
        {
            // Se conserva lo que dijo el marcador. Lo regularizado va en la observación, que
            // es donde queda a la vista de quien audita sin confundirse con el dato duro.
            EntradaReal = calculo.EntradaReal,
            SalidaReal = calculo.SalidaReal,
            JustificacionId = justificacion.Id,
            EstadoAnterior = estadoAnterior,
            Observacion =
                $"{calculo.Observacion} REGULARIZADO: {Regularizadas(justificacion)}. " +
                $"La jornada se calcula de {entrada:dd/MM HH:mm} a {salida:dd/MM HH:mm}; " +
                $"las marcaciones del equipo se conservan sin cambios. {justificacion.Motivo}."
        };
    }

    /// <summary>Qué horas aportó la regularización, para que la observación lo diga.</summary>
    private static string Regularizadas(JustificacionAplicable justificacion)
    {
        var partes = new List<string>(2);

        if (justificacion.EntradaRegularizada is { } entrada)
        {
            partes.Add($"entrada verificada {entrada:dd/MM HH:mm}");
        }

        if (justificacion.SalidaRegularizada is { } salida)
        {
            partes.Add($"salida verificada {salida:dd/MM HH:mm}");
        }

        return partes.Count == 0 ? "sin horas" : string.Join(" y ", partes);
    }

    /// <summary>
    /// Aplica un permiso por horas sobre el extremo de la jornada donde realmente cae.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La regla es un cruce de intervalos, no una heurística: se descuenta de la tardanza
    /// lo que la ventana del permiso cubre del tramo <c>[inicio del turno, entrada real]</c>,
    /// y de la salida anticipada lo que cubre del tramo <c>[salida real, fin del turno]</c>.
    /// Un permiso que cae en medio de la jornada no descuenta nada, y así queda dicho: el
    /// motor toma la primera y la última marca, de modo que ese tramo no produjo ni
    /// tardanza ni salida anticipada que perdonar.
    /// </para>
    /// <para>
    /// ⚠️ <b>No acredita los minutos del permiso como trabajados.</b> Un permiso con goce de
    /// dos horas deja el día con la jornada realmente marcada, no con la jornada completa.
    /// Si GCA paga esas horas, es una regla de negocio que hay que confirmar antes de
    /// escribirla: inventarla aquí sería pagar tiempo que nadie autorizó pagar.
    /// </para>
    /// <para>
    /// Sin ventana —una justificación de este tipo creada a mano, que hoy el alta rechaza—
    /// se cae al comportamiento anterior: perdonar la tardanza. Es lo conservador, y nunca
    /// descarta el descuento en silencio.
    /// </para>
    /// </remarks>
    private static ResultadoCalculo AplicarPermisoParcial(
        JustificacionAplicable justificacion,
        ResultadoCalculo calculo,
        Turno? turno,
        DateOnly fecha,
        string estadoAnterior)
    {
        if (turno is null
            || justificacion.InicioPermiso is not { } horaInicio
            || justificacion.FinPermiso is not { } horaFin)
        {
            return AplicarDescuento(
                calculo, justificacion, estadoAnterior,
                calculo.MinutosTardanza,
                "PERMISO JUSTIFICADO",
                (c, restante) => c with
                {
                    MinutosTardanza = restante,
                    Estado = restante == 0 && c.Estado == EstadoTareo.Tardanza
                        ? EstadoTareo.Puntual
                        : c.Estado
                });
        }

        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);
        var finTurno = FinTurno(turno, fecha);

        var desdePermiso = AnclarEnLaJornada(horaInicio, turno, fecha);
        var hastaPermiso = AnclarEnLaJornada(horaFin, turno, fecha);

        // El tramo que falta al principio va desde la hora del turno hasta que la persona
        // marcó; el del final, desde que marcó hasta la hora de cierre. Sin marca no hay
        // tramo que perdonar: un permiso de dos horas no justifica un día entero.
        var descuentoTardanza = calculo.EntradaReal is { } entradaReal
            ? MinutosEnComun(desdePermiso, hastaPermiso, inicioTurno, entradaReal)
            : 0;

        var descuentoSalida = calculo.SalidaReal is { } salidaReal
            ? MinutosEnComun(desdePermiso, hastaPermiso, salidaReal, finTurno)
            : 0;

        var tardanza = Math.Max(0, calculo.MinutosTardanza - descuentoTardanza);
        var salidaAnticipada = Math.Max(0, calculo.MinutosSalidaAnticipada - descuentoSalida);

        var perdonados = (calculo.MinutosTardanza - tardanza)
                         + (calculo.MinutosSalidaAnticipada - salidaAnticipada);

        var detalle = perdonados == 0
            ? "no coincide con la tardanza ni con la salida anticipada del día, " +
              "no hubo minutos que descontar"
            : Detallar(calculo.MinutosTardanza - tardanza, calculo.MinutosSalidaAnticipada - salidaAnticipada);

        return calculo with
        {
            MinutosTardanza = tardanza,
            MinutosSalidaAnticipada = salidaAnticipada,
            Estado = tardanza == 0 && calculo.Estado == EstadoTareo.Tardanza
                ? EstadoTareo.Puntual
                : calculo.Estado,
            JustificacionId = justificacion.Id,
            EstadoAnterior = estadoAnterior,
            Observacion =
                $"{calculo.Observacion} PERMISO PARCIAL de {horaInicio:HH:mm} a " +
                $"{horaFin:HH:mm}: {detalle}. {justificacion.Motivo}."
        };
    }

    /// <summary>Qué se perdonó y de dónde, para que la observación lo diga sin ambigüedad.</summary>
    private static string Detallar(int tardanza, int salidaAnticipada)
    {
        var partes = new List<string>(2);

        if (tardanza > 0)
        {
            partes.Add($"se perdonaron {tardanza} min de tardanza");
        }

        if (salidaAnticipada > 0)
        {
            partes.Add($"se perdonaron {salidaAnticipada} min de salida anticipada");
        }

        return string.Join(" y ", partes);
    }

    /// <summary>
    /// Lleva una hora del permiso al día natural que le corresponde dentro de la jornada.
    /// </summary>
    /// <remarks>
    /// En un turno que cruza medianoche, las 05:00 de un permiso son las del día siguiente,
    /// porque la jornada empezó a las 19:00 del día al que se imputa. Una solicitud por
    /// horas es siempre de un solo día y exige fin posterior al inicio, así que la ventana
    /// nunca cruza medianoche por su cuenta.
    /// </remarks>
    private static DateTime AnclarEnLaJornada(TimeOnly hora, Turno turno, DateOnly fecha) =>
        turno.CruzaMedianoche && hora < turno.HoraInicio
            ? fecha.AddDays(1).ToDateTime(hora)
            : fecha.ToDateTime(hora);

    /// <summary>Minutos que comparten dos intervalos. Cero si no se tocan.</summary>
    private static int MinutosEnComun(DateTime desdeA, DateTime hastaA, DateTime desdeB, DateTime hastaB)
    {
        var inicio = desdeA > desdeB ? desdeA : desdeB;
        var fin = hastaA < hastaB ? hastaA : hastaB;

        return fin > inicio ? (int)(fin - inicio).TotalMinutes : 0;
    }

    /// <summary>Descuenta los minutos justificados. Nulo significa perdonarlos todos.</summary>
    private static int Descontar(int minutos, int? justificados) =>
        justificados is { } perdonados ? Math.Max(0, minutos - perdonados) : 0;

    /// <summary>
    /// Aplica un descuento parcial (tardanza o salida anticipada) y redacta la observación
    /// dejando explícito cuánto quedó, no solo cuánto había.
    /// </summary>
    /// <remarks>
    /// Sin esto, la observación decía "tardanza de 27 min" seguido de "TARDANZA
    /// JUSTIFICADA: motivo", sin aclarar que de esos 27 solo quedaron 17. Un dato a medio
    /// explicar es tan malo como uno sin explicar: viola la invariante de que todo
    /// resultado se explica.
    /// </remarks>
    private static ResultadoCalculo AplicarDescuento(
        ResultadoCalculo calculo,
        JustificacionAplicable justificacion,
        string estadoAnterior,
        int minutosOriginales,
        string etiqueta,
        Func<ResultadoCalculo, int, ResultadoCalculo> conMinutosRestantes)
    {
        var restante = Descontar(minutosOriginales, justificacion.MinutosJustificados);

        var resumen = restante == 0
            ? $"se perdonaron los {minutosOriginales} min"
            : $"se perdonaron {minutosOriginales - restante} min, quedan {restante} min";

        var actualizado = conMinutosRestantes(calculo, restante);

        return actualizado with
        {
            JustificacionId = justificacion.Id,
            EstadoAnterior = estadoAnterior,
            Observacion = $"{calculo.Observacion} {etiqueta}: {resumen}. {justificacion.Motivo}."
        };
    }

    // ------------------------------------------------------------------

    private static ResultadoCalculo Jornada(
        EntradaCalculo entrada,
        Turno turno,
        List<DateTime> marcaciones,
        List<DateTime> eventos,
        TipoDiaNoLaborable tipoDiaNoLaborable,
        string? etiqueta,
        TipoCoincidenciaFeriadoDescanso coincidenciaFeriadoDescanso =
            TipoCoincidenciaFeriadoDescanso.Ninguna)
    {
        // Con tres o más marcas se toman los extremos: lo que queda en medio son salidas
        // intermedias —el paso al baño, el almuerzo de quien marca por costumbre— que este
        // sistema no modela, porque la jornada va de la primera marca a la última.
        //
        // ⚠️ Los extremos salen de la lista EN BRUTO y no de `eventos`: consolidar rebotes
        // decide cuántos pasos hubo, no a qué hora ocurrieron. Tomar la salida de `eventos`
        // recortaría hasta 2 minutos de sobretiempo a quien marca dos veces al salir.
        //
        // La ingesta no ayuda aquí: solo descarta instantes idénticos, así que dos marcas
        // separadas por 40 segundos llegan las dos.
        var entradaReal = marcaciones[0];
        var salidaReal = marcaciones[^1];

        // El domingo es descanso semanal aunque el turno (DOM1/DOM2) lo declare aplicable:
        // trabajado, todo va al 100 % (VIG-DOM-01). Va aquí para cubrir también la jornada
        // armada por una regularización.
        if (entrada.Fecha.DayOfWeek == DayOfWeek.Sunday && tipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno)
        {
            tipoDiaNoLaborable = TipoDiaNoLaborable.DescansoSemanal;
            etiqueta ??= "Domingo";
        }

        // Eventos que no son ni la entrada ni la salida.
        var intermedias = Math.Max(0, eventos.Count - 2);

        var inicioTurno = entrada.Fecha.ToDateTime(turno.HoraInicio);

        // La jornada se computa desde la hora del turno, no desde la marca: llegar antes
        // no genera sobretiempo. Llegar tarde sí recorta el cómputo.
        var inicioComputo = entradaReal > inicioTurno ? entradaReal : inicioTurno;

        // El simétrico en la salida: dentro de la gracia del turno, salir unos minutos
        // después del fin se computa como salir a la hora del turno. Con gracia 0 —el valor
        // por omisión— la condición nunca se cumple y todo minuto posterior es sobretiempo.
        // Solo aplica si la jornada empezó antes del fin: si no, no hay turno que cerrar.
        var finTurno = FinTurno(turno, entrada.Fecha);

        var salidaEnGracia = salidaReal > finTurno
                             && salidaReal <= finTurno.AddMinutes(turno.MinutosGraciaSalida)
                             && inicioComputo < finTurno;

        var finComputo = salidaEnGracia ? finTurno : salidaReal;
        var minutosEnGracia = salidaEnGracia ? (int)(salidaReal - finTurno).TotalMinutes : 0;

        // Umbral de sobretiempo de lunes a viernes (VIG-HE-03): un exceso menor a 15 min
        // sobre el fin teórico no se reconoce; desde 15 min se cuenta entero. No aplica a
        // sábado ni domingo (todo es extra desde el primer minuto) ni a feriados.
        var minutosBajoUmbral = 0;

        if (!salidaEnGracia
            && tipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno
            && entrada.Fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && salidaReal > finTurno
            && inicioComputo < finTurno)
        {
            var exceso = (int)(salidaReal - finTurno).TotalMinutes;

            if (exceso < UmbralSobretiempoMinutos)
            {
                finComputo = finTurno;
                minutosBajoUmbral = exceso;
            }
        }

        var brutos = (int)(finComputo - inicioComputo).TotalMinutes;

        if (brutos <= 0)
        {
            return Incompleta(
                entradaReal, turno, entrada.Fecha, tipoDiaNoLaborable,
                $"Las marcaciones no forman una jornada válida: entrada {entradaReal:dd/MM HH:mm} " +
                $"y salida {salidaReal:dd/MM HH:mm}.",
                coincidenciaFeriadoDescanso);
        }

        var trabajados = Math.Max(0, brutos - turno.MinutosRefrigerio);

        var (ordinarios, extras, reparto) = Repartir(
            entrada.Fecha, turno, inicioComputo, finComputo, trabajados, tipoDiaNoLaborable);
        var extra25 = Math.Min(extras, MinutosExtraAlVeinticinco);
        var extra35 = extras - extra25;

        var tardanza = CalcularTardanza(entradaReal, inicioTurno);
        var salidaAnticipada = CalcularSalidaAnticipada(salidaReal, turno, entrada.Fecha);
        var nocturnos = MinutosEnFranjaNocturna(inicioComputo, finComputo);

        // De lunes a viernes el sobretiempo es la cola posterior al fin del turno. El
        // sábado, en cambio, empieza con la jornada y termina al entrar al domingo: desde
        // las 00:00 el remanente es HE100. Los intervalos exactos mantienen correcto el
        // solape 25/35 con la franja nocturna sin sumar minutos físicos.
        var extrasEmpiezanConLaJornada = entrada.Fecha.DayOfWeek == DayOfWeek.Saturday;
        var inicioExtras = extrasEmpiezanConLaJornada
            ? inicioComputo
            : finComputo.AddMinutes(-extras);
        var finExtra25 = inicioExtras.AddMinutes(extra25);
        var finExtras = inicioExtras.AddMinutes(extras);

        var extra25Nocturnos = MinutosEnFranjaNocturna(inicioExtras, finExtra25);
        var extra35Nocturnos = MinutosEnFranjaNocturna(finExtra25, finExtras);

        // En un día no laborable no hay tardanza: no había hora de entrada exigible.
        var estado = tipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno
            ? EstadoTareo.Puntual
            : tardanza > 0 ? EstadoTareo.Tardanza : EstadoTareo.Puntual;

        return new ResultadoCalculo
        {
            EntradaReal = entradaReal,
            SalidaReal = salidaReal,
            MinutosTrabajados = trabajados,
            MinutosOrdinarios = ordinarios,
            MinutosExtra25 = extra25,
            MinutosExtra35 = extra35,
            MinutosNocturnos = nocturnos,
            MinutosExtra25Nocturnos = extra25Nocturnos,
            MinutosExtra35Nocturnos = extra35Nocturnos,
            MinutosTardanza = tardanza,
            MinutosSalidaAnticipada = salidaAnticipada,
            TipoDiaNoLaborable = tipoDiaNoLaborable,
            CoincidenciaFeriadoDescanso = coincidenciaFeriadoDescanso,
            Estado = estado,
            Observacion = Describir(
                etiqueta, entradaReal, salidaReal, trabajados, extras, tardanza,
                salidaAnticipada, nocturnos, entradaReal < inicioTurno, minutosEnGracia, intermedias,
                reparto, minutosBajoUmbral)
        };
    }

    /// <summary>
    /// Reparte los minutos trabajados entre ordinarios y sobretiempo según el día
    /// (ACCEPTANCE_RULES VIG-HE-01, VIG-HE-02, VIG-DOM-01).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Domingo: ni ordinarios ni 25/35; todo lo trabajado es el 100 % del día no
    ///   laborable.</item>
    ///   <item>Feriado o descanso de otro día: comportamiento vigente, sin cambios.</item>
    ///   <item>Sábado laborable: todo minuto anterior al domingo es sobretiempo (25 % las
    ///   dos primeras horas); desde domingo 00:00 el remanente es HE100.</item>
    ///   <item>Lunes a viernes: ordinario hasta el fin teórico del turno (DIU 17:22, NOC
    ///   05:22 del día siguiente) y sobretiempo después, ya filtrado por el umbral de 15 min.
    ///   Llegar tarde no corre el corte; llegar antes no suma (el cómputo ya empieza en la
    ///   hora del turno). <see cref="Turno.MinutosJornadaOrdinaria"/> no interviene.</item>
    /// </list>
    /// Los intervalos 25/35 se conservan explícitos para cruzarlos con la franja nocturna:
    /// cola de la jornada en L–V y comienzo de la jornada en sábado.
    /// </remarks>
    private static (int Ordinarios, int Extras, string? Reparto) Repartir(
        DateOnly fecha,
        Turno turno,
        DateTime inicioComputo,
        DateTime finComputo,
        int trabajados,
        TipoDiaNoLaborable tipoDiaNoLaborable)
    {
        if (fecha.DayOfWeek == DayOfWeek.Sunday)
        {
            return (0, 0, "domingo: todo el tiempo trabajado va al 100 %");
        }

        if (tipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno)
        {
            var ordinariosNoLaborable = Math.Min(trabajados, turno.MinutosJornadaOrdinaria);
            return (ordinariosNoLaborable, trabajados - ordinariosNoLaborable, null);
        }

        if (fecha.DayOfWeek == DayOfWeek.Saturday)
        {
            var inicioDomingo = fecha.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var finTramoSabado = finComputo < inicioDomingo ? finComputo : inicioDomingo;
            var minutosSabado = Math.Max(0, (int)(finTramoSabado - inicioComputo).TotalMinutes);
            var extrasSabado = Math.Min(trabajados, minutosSabado);
            var minutosDomingo = trabajados - extrasSabado;

            return minutosDomingo == 0
                ? (0, extrasSabado, "sábado: todo el tiempo trabajado es sobretiempo")
                : (0, extrasSabado,
                    $"sábado: {extrasSabado} min de HE25/HE35; desde domingo 00:00, " +
                    $"{minutosDomingo} min de HE100");
        }

        var corte = FinTurno(turno, fecha);
        var finOrdinario = finComputo < corte ? finComputo : corte;
        var antesDelCorte = Math.Max(0, (int)(finOrdinario - inicioComputo).TotalMinutes);
        var ordinarios = Math.Min(trabajados, antesDelCorte);

        return (ordinarios, trabajados - ordinarios, null);
    }

    /// <summary>
    /// Momento en que termina el turno que empieza en <paramref name="fecha"/>: al día
    /// siguiente si cruza medianoche.
    /// </summary>
    private static DateTime FinTurno(Turno turno, DateOnly fecha) =>
        turno.CruzaMedianoche
            ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
            : fecha.ToDateTime(turno.HoraFin);

    /// <summary>
    /// Minutos de tardanza de planilla, contados desde el inicio teórico del turno.
    /// </summary>
    /// <remarks>
    /// La regla de planilla tiene tolerancia cero. <see cref="Turno.MinutosTolerancia"/>
    /// permanece como configuración histórica, pero no modifica este cálculo.
    /// </remarks>
    private static int CalcularTardanza(DateTime entradaReal, DateTime inicioTurno) =>
        entradaReal <= inicioTurno ? 0 : (int)(entradaReal - inicioTurno).TotalMinutes;

    private static int CalcularSalidaAnticipada(DateTime salidaReal, Turno turno, DateOnly fecha)
    {
        var finTurno = FinTurno(turno, fecha);

        // Planilla cuenta cada minuto anterior al fin teórico. La configuración histórica
        // MinutosAnticipacionSalida no perdona minutos ni altera el cruce de medianoche.
        return salidaReal >= finTurno ? 0 : (int)(finTurno - salidaReal).TotalMinutes;
    }

    /// <summary>
    /// Minutos trabajados que caen dentro de la franja nocturna 22:00–06:00.
    /// </summary>
    /// <remarks>
    /// Se calcula sobre las horas <b>realmente trabajadas</b>, no sobre el turno completo:
    /// un turno que empieza a las 19:00 solo genera nocturnidad a partir de las 22:00.
    /// Recorre día a día porque una jornada puede abarcar dos franjas nocturnas.
    /// </remarks>
    private static int MinutosEnFranjaNocturna(DateTime desde, DateTime hasta)
    {
        if (hasta <= desde)
        {
            return 0;
        }

        var total = 0;

        // Se empieza el día anterior para cubrir la franja que arranca a las 22:00 de la
        // víspera y termina a las 06:00 del día en que inicia la jornada.
        for (var dia = desde.Date.AddDays(-1); dia <= hasta.Date; dia = dia.AddDays(1))
        {
            var inicioFranja = dia.Add(InicioNocturno.ToTimeSpan());
            var finFranja = dia.AddDays(1).Add(FinNocturno.ToTimeSpan());

            var inicio = desde > inicioFranja ? desde : inicioFranja;
            var fin = hasta < finFranja ? hasta : finFranja;

            if (fin > inicio)
            {
                total += (int)(fin - inicio).TotalMinutes;
            }
        }

        return total;
    }

    private static bool TurnoAplicaEn(Turno turno, DateOnly fecha) =>
        turno.DiasAplicables.HasFlag(ADiaSemana(fecha.DayOfWeek));

    private static DiasSemana ADiaSemana(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => DiasSemana.Lunes,
        DayOfWeek.Tuesday => DiasSemana.Martes,
        DayOfWeek.Wednesday => DiasSemana.Miercoles,
        DayOfWeek.Thursday => DiasSemana.Jueves,
        DayOfWeek.Friday => DiasSemana.Viernes,
        DayOfWeek.Saturday => DiasSemana.Sabado,
        _ => DiasSemana.Domingo
    };

    private static ResultadoCalculo Vacio(EstadoTareo estado, string observacion) =>
        new() { Estado = estado, Observacion = observacion };

    /// <summary>
    /// Colapsa los rebotes del lector: devuelve un instante por paso real ante el equipo.
    /// </summary>
    /// <remarks>
    /// Cada marca se compara contra la última <b>aceptada</b>, no contra la anterior en
    /// bruto. Es lo que impide que una cadena se coma media jornada: comparando contra la
    /// anterior, 06:48 → 06:49 → 06:50 → … → 07:20 sería un solo evento porque ningún par
    /// consecutivo llega a la ventana. Contra la última aceptada, un racimo nunca abarca
    /// más que <see cref="VentanaRebote"/>.
    ///
    /// ⚠️ Sirve para <b>contar</b> eventos, no para calcular horas. Si la salida se tomara
    /// de esta lista se perderían hasta 2 minutos de sobretiempo cada vez que alguien
    /// marca dos veces al salir, y esos minutos sí se trabajaron. Quien calcula la jornada
    /// sigue usando el mínimo y el máximo de la lista en bruto.
    /// </remarks>
    private static List<DateTime> ConsolidarRebotes(List<DateTime> marcaciones)
    {
        var eventos = new List<DateTime>(marcaciones.Count);

        foreach (var marca in marcaciones)
        {
            if (eventos.Count == 0 || marca - eventos[^1] >= VentanaRebote)
            {
                eventos.Add(marca);
            }
        }

        return eventos;
    }

    /// <summary>
    /// Frase que explica los rebotes descartados, o cadena vacía si no hubo ninguno.
    /// </summary>
    /// <remarks>
    /// Va en la observación porque el invariante es que todo resultado se explique: sin
    /// esto, un día con tres marcas seguidas saldría "Incompleto por marcación impar" y
    /// quien audita vería tres marcas en el detalle, sin nada que reconcilie ambas cosas.
    /// </remarks>
    private static string Rebotes(List<DateTime> marcaciones, List<DateTime> eventos)
    {
        var descartadas = marcaciones.Count - eventos.Count;

        return descartadas == 0
            ? string.Empty
            : $" Se descartó {descartadas} marca(s) repetida(s) dentro de " +
              $"{VentanaRebote.TotalMinutes:0} minutos, que el lector registró de más.";
    }

    private static ResultadoCalculo Incompleta(
        DateTime marca,
        Turno turno,
        DateOnly fecha,
        TipoDiaNoLaborable tipoDiaNoLaborable,
        string observacion,
        TipoCoincidenciaFeriadoDescanso coincidenciaFeriadoDescanso =
            TipoCoincidenciaFeriadoDescanso.Ninguna)
    {
        // Se guarda la marca en el extremo que corresponda según a cuál esté más cerca,
        // para que la pantalla de justificaciones muestre qué falta. No se calcula
        // ningún minuto: inventar la hora que falta sería inventar horas pagadas.
        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);
        var finTurno = FinTurno(turno, fecha);

        var esEntrada =Math.Abs((marca - inicioTurno).TotalMinutes)
                        <= Math.Abs((marca - finTurno).TotalMinutes);

        return new ResultadoCalculo
        {
            EntradaReal = esEntrada ? marca : null,
            SalidaReal = esEntrada ? null : marca,
            TipoDiaNoLaborable = tipoDiaNoLaborable,
            CoincidenciaFeriadoDescanso = coincidenciaFeriadoDescanso,
            Estado = EstadoTareo.Incompleto,
            Observacion = observacion
        };
    }

    private static string Describir(
        string? etiqueta,
        DateTime entrada,
        DateTime salida,
        int trabajados,
        int extras,
        int tardanza,
        int salidaAnticipada,
        int nocturnos,
        bool llegoAntes,
        int minutosEnGracia,
        int intermedias,
        string? reparto,
        int minutosBajoUmbral)
    {
        var partes = new List<string>();

        if (etiqueta is not null)
        {
            partes.Add($"{etiqueta} trabajado");
        }

        partes.Add($"Entrada {entrada:HH:mm}, salida {salida:HH:mm}");
        partes.Add($"{trabajados / 60} h {trabajados % 60:00} min efectivos");

        if (llegoAntes)
        {
            partes.Add("marcó antes del inicio del turno; la jornada se computa desde la hora del turno");
        }

        // Sin esta frase la observación diría "salida 06:10" junto a cero minutos de
        // sobretiempo, y quien audita no podría distinguir una regla de un error.
        if (minutosEnGracia > 0)
        {
            partes.Add(
                $"salió {minutosEnGracia} min después del fin del turno, dentro de la gracia de " +
                "salida: esos minutos no se computan");
        }

        if (tardanza > 0)
        {
            partes.Add($"tardanza de {tardanza} min");
        }

        if (salidaAnticipada > 0)
        {
            partes.Add($"salida anticipada de {salidaAnticipada} min");
        }

        if (minutosBajoUmbral > 0)
        {
            partes.Add(
                $"salió {minutosBajoUmbral} min después del fin del turno, por debajo del umbral " +
                $"de {UmbralSobretiempoMinutos} min para horas extra: esos minutos no se computan");
        }

        if (reparto is not null)
        {
            partes.Add(reparto);
        }

        if (extras > 0)
        {
            partes.Add($"{extras} min de sobretiempo");
        }

        if (nocturnos > 0)
        {
            partes.Add($"{nocturnos} min en franja nocturna");
        }

        // Queda escrito para que quien audite pueda reconciliar la observación con el
        // detalle: si el día muestra seis marcas y la jornada dice "06:48 a 19:40", sin
        // esta frase no hay forma de saber que las cuatro del medio se ignoraron a
        // propósito y no que el sistema perdió marcas.
        if (intermedias > 0)
        {
            partes.Add(
                $"{intermedias} marca(s) intermedia(s) ignorada(s): la jornada va de la " +
                "primera marca a la última");
        }

        return string.Join(". ", partes) + ".";
    }
}
