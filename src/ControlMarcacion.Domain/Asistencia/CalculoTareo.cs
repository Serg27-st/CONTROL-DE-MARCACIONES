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
    /// El tipo de ausencia hace que el día cuente como asistido.
    /// </summary>
    /// <remarks>
    /// Distingue vacaciones o licencia con goce —que pagan el día— de una licencia sin
    /// goce, que justifica la falta pero no la remunera.
    /// </remarks>
    public bool ComputaComoAsistencia { get; init; }

    /// <summary>
    /// Minutos perdonados en una justificación parcial. Nulo significa "todos".
    /// </summary>
    public int? MinutosJustificados { get; init; }

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

    /// <summary>
    /// Marcaciones de la jornada, ya deduplicadas. Pueden abarcar dos días naturales
    /// cuando el turno cruza medianoche.
    /// </summary>
    public IReadOnlyList<DateTime> Marcaciones { get; init; } = [];

    /// <summary>El trabajador tenía vínculo laboral vigente ese día.</summary>
    public bool TieneVinculo { get; init; } = true;

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

    public bool EsDiaNoLaborable => TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno;

    public required EstadoTareo Estado { get; init; }

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
///   sí hubo trabajo.</item>
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

    public static ResultadoCalculo Calcular(EntradaCalculo entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var baseCalculo = CalcularSinJustificar(entrada);

        // La justificación se aplica ENCIMA del cálculo crudo, nunca en lugar de él. Así el
        // resultado conserva qué decía el marcador y qué lo modificó, que es exactamente lo
        // que hace auditable al sistema.
        return entrada.Justificacion is { } justificacion
            ? Aplicar(justificacion, baseCalculo, entrada.Turno)
            : baseCalculo;
    }

    private static ResultadoCalculo CalcularSinJustificar(EntradaCalculo entrada)
    {
        if (!entrada.TieneVinculo)
        {
            return Vacio(
                EstadoTareo.SinVinculo,
                "Fuera del vínculo laboral: el día es anterior al ingreso o posterior al cese.");
        }

        if (entrada.Turno is not { } turno)
        {
            // Sin turno no hay contra qué comparar. No es falta: es un dato que falta.
            return Vacio(
                EstadoTareo.SinHorario,
                "El trabajador no tenía turno asignado ese día. Asigne el turno para poder calcular.");
        }

        var marcaciones = entrada.Marcaciones.OrderBy(m => m).ToList();
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

            var etiqueta = entrada.EsFeriado ? "Feriado" : "Día de descanso semanal";

            if (marcaciones.Count == 0)
            {
                return Vacio(
                    entrada.EsFeriado ? EstadoTareo.Feriado : EstadoTareo.Descanso,
                    $"{etiqueta}: no correspondía trabajar y no hubo marcaciones.");
            }

            if (marcaciones.Count == 1)
            {
                return Incompleta(
                    marcaciones[0], turno, entrada.Fecha, tipo,
                    $"{etiqueta} con una sola marcación a las {marcaciones[0]:HH:mm}. " +
                    "No se calculan horas: falta la marcación complementaria.");
            }

            return Jornada(entrada, turno, marcaciones, tipo, etiqueta);
        }

        if (marcaciones.Count == 0)
        {
            return Vacio(
                EstadoTareo.Falta,
                "No registró ninguna marcación en un día laborable.");
        }

        if (marcaciones.Count == 1)
        {
            return Incompleta(
                marcaciones[0], turno, entrada.Fecha, TipoDiaNoLaborable.Ninguno,
                $"Marcación impar: solo se registró una marca a las {marcaciones[0]:HH:mm}. " +
                "No se calculan horas porque no se conoce la hora faltante.");
        }

        return Jornada(entrada, turno, marcaciones, TipoDiaNoLaborable.Ninguno, etiqueta: null);
    }

    /// <summary>
    /// Aplica la justificación sobre el resultado crudo del marcador.
    /// </summary>
    /// <remarks>
    /// Cada tipo de justificación toca una cosa distinta y solo esa:
    /// <list type="bullet">
    ///   <item><b>Ausencia</b> e <b>incompleta</b>: cambian el estado. Si el tipo de
    ///   ausencia computa como asistencia, el día se paga con la jornada ordinaria del
    ///   turno — es lo que hace que las vacaciones se remuneren.</item>
    ///   <item><b>Tardanza</b> y <b>salida anticipada</b>: descuentan minutos, total o
    ///   parcialmente. No tocan las horas trabajadas: el trabajador estuvo lo que estuvo.</item>
    /// </list>
    ///
    /// Nunca genera horas extras: justificar una ausencia no puede producir sobretiempo.
    /// </remarks>
    private static ResultadoCalculo Aplicar(
        JustificacionAplicable justificacion,
        ResultadoCalculo calculo,
        Turno? turno)
    {
        var estadoAnterior = calculo.Estado.ToString();

        return justificacion.Tipo switch
        {
            TipoJustificacion.Ausencia or TipoJustificacion.MarcacionIncompleta =>
                calculo with
                {
                    Estado = EstadoTareo.AusenciaJustificada,
                    // Solo los tipos que computan como asistencia pagan el día. Una licencia
                    // sin goce justifica la falta pero deja el día en cero.
                    MinutosTrabajados = justificacion.ComputaComoAsistencia && turno is not null
                        ? turno.MinutosJornadaOrdinaria
                        : calculo.MinutosTrabajados,
                    MinutosOrdinarios = justificacion.ComputaComoAsistencia && turno is not null
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

            _ => calculo
        };
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
        TipoDiaNoLaborable tipoDiaNoLaborable,
        string? etiqueta)
    {
        // Con tres o más marcas se toman los extremos. La deduplicación de dobles fichadas
        // ya ocurrió en la ingesta; lo que quede en medio son salidas intermedias que este
        // sistema no modela (solo hay 2 marcaciones por día por diseño).
        var entradaReal = marcaciones[0];
        var salidaReal = marcaciones[^1];

        var inicioTurno = entrada.Fecha.ToDateTime(turno.HoraInicio);

        // La jornada se computa desde la hora del turno, no desde la marca: llegar antes
        // no genera sobretiempo. Llegar tarde sí recorta el cómputo.
        var inicioComputo = entradaReal > inicioTurno ? entradaReal : inicioTurno;

        var brutos = (int)(salidaReal - inicioComputo).TotalMinutes;

        if (brutos <= 0)
        {
            return Incompleta(
                entradaReal, turno, entrada.Fecha, tipoDiaNoLaborable,
                $"Las marcaciones no forman una jornada válida: entrada {entradaReal:dd/MM HH:mm} " +
                $"y salida {salidaReal:dd/MM HH:mm}.");
        }

        var trabajados = Math.Max(0, brutos - turno.MinutosRefrigerio);

        var ordinarios = Math.Min(trabajados, turno.MinutosJornadaOrdinaria);
        var extras = trabajados - ordinarios;
        var extra25 = Math.Min(extras, MinutosExtraAlVeinticinco);
        var extra35 = extras - extra25;

        var tardanza = CalcularTardanza(entradaReal, turno, inicioTurno);
        var salidaAnticipada = CalcularSalidaAnticipada(salidaReal, turno, entrada.Fecha);
        var nocturnos = MinutosEnFranjaNocturna(inicioComputo, salidaReal);

        // El sobretiempo es siempre la cola de la jornada: se trabaja de corrido desde el
        // inicio del cómputo hasta la salida, así que los últimos `extras` minutos de reloj
        // son las horas extras, y dentro de ellas el 25 % va antes que el 35 %. Eso da un
        // intervalo exacto que se puede cruzar contra la franja nocturna sin suponer nada.
        var inicioExtras = salidaReal.AddMinutes(-extras);
        var finExtra25 = inicioExtras.AddMinutes(extra25);

        var extra25Nocturnos = MinutosEnFranjaNocturna(inicioExtras, finExtra25);
        var extra35Nocturnos = MinutosEnFranjaNocturna(finExtra25, salidaReal);

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
            Estado = estado,
            Observacion = Describir(
                etiqueta, entradaReal, salidaReal, trabajados, extras, tardanza,
                salidaAnticipada, nocturnos, entradaReal < inicioTurno)
        };
    }

    /// <summary>
    /// Minutos de tardanza. Cero si llegó dentro de la tolerancia.
    /// </summary>
    /// <remarks>
    /// La tolerancia es <b>inclusiva</b>: llegar exactamente en el minuto límite es puntual.
    /// Cuando se supera, la tardanza se cuenta desde la hora del turno y no desde el
    /// límite de tolerancia — la tolerancia es un margen de gracia, no un corrimiento
    /// del horario. ⚠️ Confirmar este criterio con GCA: cambia el descuento.
    /// </remarks>
    private static int CalcularTardanza(DateTime entradaReal, Turno turno, DateTime inicioTurno)
    {
        var limite = inicioTurno.AddMinutes(turno.MinutosTolerancia);

        return entradaReal <= limite
            ? 0
            : (int)(entradaReal - inicioTurno).TotalMinutes;
    }

    private static int CalcularSalidaAnticipada(DateTime salidaReal, Turno turno, DateOnly fecha)
    {
        var finTurno = turno.CruzaMedianoche
            ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
            : fecha.ToDateTime(turno.HoraFin);

        // La ventana de anticipación de salida absorbe los minutos que la empresa acepta
        // como salida normal. Más allá de eso, es salida anticipada y queda registrada.
        var limite = finTurno.AddMinutes(-turno.MinutosAnticipacionSalida);

        return salidaReal >= limite ? 0 : (int)(finTurno - salidaReal).TotalMinutes;
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

    private static ResultadoCalculo Incompleta(
        DateTime marca,
        Turno turno,
        DateOnly fecha,
        TipoDiaNoLaborable tipoDiaNoLaborable,
        string observacion)
    {
        // Se guarda la marca en el extremo que corresponda según a cuál esté más cerca,
        // para que la pantalla de justificaciones muestre qué falta. No se calcula
        // ningún minuto: inventar la hora que falta sería inventar horas pagadas.
        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);
        var finTurno = turno.CruzaMedianoche
            ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
            : fecha.ToDateTime(turno.HoraFin);

        var esEntrada = Math.Abs((marca - inicioTurno).TotalMinutes)
                        <= Math.Abs((marca - finTurno).TotalMinutes);

        return new ResultadoCalculo
        {
            EntradaReal = esEntrada ? marca : null,
            SalidaReal = esEntrada ? null : marca,
            TipoDiaNoLaborable = tipoDiaNoLaborable,
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
        bool llegoAntes)
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

        if (tardanza > 0)
        {
            partes.Add($"tardanza de {tardanza} min");
        }

        if (salidaAnticipada > 0)
        {
            partes.Add($"salida anticipada de {salidaAnticipada} min");
        }

        if (extras > 0)
        {
            partes.Add($"{extras} min de sobretiempo");
        }

        if (nocturnos > 0)
        {
            partes.Add($"{nocturnos} min en franja nocturna");
        }

        return string.Join(". ", partes) + ".";
    }
}
