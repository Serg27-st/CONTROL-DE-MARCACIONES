using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Un día cuyas marcas encajan mucho mejor en otro turno que en el que tenía asignado.
/// </summary>
/// <param name="TurnoQueEncaja">El turno con el que las marcas son coherentes.</param>
/// <param name="DesvioProgramado">
/// Minutos de desvío contra el turno que tenía asignado: cuánto se apartan la entrada y la
/// salida de sus horas.
/// </param>
/// <param name="DesvioQueEncaja">Lo mismo contra <see cref="TurnoQueEncaja"/>.</param>
public sealed record DiscrepanciaTurno(Turno TurnoQueEncaja, int DesvioProgramado, int DesvioQueEncaja)
{
    /// <summary>Frase para la observación del día y para la bandeja de revisión.</summary>
    public string Explicacion =>
        $"Las marcas de este día no encajan con el turno asignado (se apartan " +
        $"{DesvioProgramado} min de su horario) y sí con {TurnoQueEncaja.Codigo} " +
        $"({DesvioQueEncaja} min). Revise si la rotación ocurrió en otra fecha.";
}

/// <summary>
/// Detecta que las marcas de un día contradicen el turno que el sistema creía vigente.
/// </summary>
/// <remarks>
/// <para>
/// El turno de un día sale del padrón: de la asignación del trabajador o del ciclo de su
/// cuadrilla ([ADR 0002](docs/adr/0002-rotacion-por-cuadrilla.md)). Las dos cosas son
/// <b>hipótesis sobre lo que iba a pasar</b>, y en planta se incumplen: la cuadrilla rota un
/// día antes, alguien cubre a un compañero, el ciclo se anota tarde. Cuando eso ocurre el
/// motor no falla —calcula con el turno que cree— y produce un día que <b>parece correcto</b>:
/// una noche calculada contra el turno diurno da tardanza enorme o sobretiempo inventado, sin
/// que nada lo señale.
/// </para>
/// <para>
/// Esta clase no corrige nada y <b>no cambia ni un minuto del cálculo</b>: solo señala el día
/// para que una persona decida. Corregir la rotación es una decisión con autor —mover el
/// ancla de la cuadrilla o anotar un cambio de turno con fecha—, no algo que el motor deba
/// hacer solo: si el sistema reescribiera el turno por su cuenta, el tareo dejaría de ser
/// reconstruible desde el padrón y nadie podría auditar por qué un día se pagó como se pagó.
/// </para>
/// <para>
/// Es pura, así que se prueba con fechas literales y sin base de datos.
/// </para>
/// </remarks>
public static class DetectorTurnoDiscrepante
{
    /// <summary>
    /// Cuánto tiene que mejorar el desvío para dar por buena la sospecha.
    /// </summary>
    /// <remarks>
    /// El turno asignado es la hipótesis por omisión y solo se pone en duda cuando la
    /// diferencia es grande: sin este margen, media hora de diferencia entre dos turnos
    /// parecidos llenaría la bandeja de ruido y nadie volvería a mirarla. Dos horas es más
    /// que cualquier tardanza o sobretiempo razonable dentro del turno correcto.
    /// </remarks>
    private const int MejoraMinimaMinutos = 120;

    /// <summary>
    /// Desvío máximo contra el turno candidato para considerar que las marcas «encajan».
    /// </summary>
    /// <remarks>
    /// No basta con que el candidato sea mejor: tiene que ser bueno. Un día con marcas
    /// dispersas no encaja en ningún turno, y proponer el «menos malo» sería inventar.
    /// </remarks>
    private const int DesvioMaximoParaEncajar = 90;

    /// <summary>
    /// Devuelve el turno con el que las marcas del día son coherentes, o <c>null</c> si el
    /// turno asignado explica el día tan bien como cualquier otro.
    /// </summary>
    /// <param name="fecha">Día al que se imputa la jornada.</param>
    /// <param name="turnoAsignado">El que el padrón dice que regía.</param>
    /// <param name="candidatos">
    /// Otros turnos posibles: los del ciclo de su cuadrilla y los habilitados en su planta.
    /// El asignado puede venir dentro; se ignora.
    /// </param>
    /// <param name="marcaciones">Marcas del día, en bruto y sin ordenar.</param>
    public static DiscrepanciaTurno? Detectar(
        DateOnly fecha,
        Turno turnoAsignado,
        IReadOnlyList<Turno> candidatos,
        IReadOnlyList<DateTime> marcaciones)
    {
        ArgumentNullException.ThrowIfNull(turnoAsignado);
        ArgumentNullException.ThrowIfNull(candidatos);
        ArgumentNullException.ThrowIfNull(marcaciones);

        // Con una sola marca el día ya sale «Incompleto» y va al filtro donde RRHH revisa.
        // Proponer un turno a partir de un solo extremo sería adivinar cuál falta.
        if (marcaciones.Count < 2)
        {
            return null;
        }

        var ordenadas = marcaciones.OrderBy(m => m).ToList();
        var entrada = ordenadas[0];
        var salida = ordenadas[^1];

        var desvioAsignado = Desvio(turnoAsignado, fecha, entrada, salida);

        var mejor = candidatos
            .Where(c => c.Id != turnoAsignado.Id)
            .Select(c => new { Turno = c, Desvio = Desvio(c, fecha, entrada, salida) })
            .OrderBy(c => c.Desvio)
            .FirstOrDefault();

        if (mejor is null || mejor.Desvio > DesvioMaximoParaEncajar)
        {
            return null;
        }

        return desvioAsignado - mejor.Desvio >= MejoraMinimaMinutos
            ? new DiscrepanciaTurno(mejor.Turno, desvioAsignado, mejor.Desvio)
            : null;
    }

    /// <summary>
    /// Minutos que se apartan la entrada y la salida de las horas de ese turno.
    /// </summary>
    /// <remarks>
    /// Se mide contra las horas del turno y no contra sus ventanas: las ventanas dicen si una
    /// marca <i>pertenece</i> al turno, y aquí la pregunta es otra —cuál de todos lo explica
    /// mejor—. Un turno que cruza medianoche termina al día siguiente.
    /// </remarks>
    private static int Desvio(Turno turno, DateOnly fecha, DateTime entrada, DateTime salida)
    {
        var inicio = fecha.ToDateTime(turno.HoraInicio);

        var fin = turno.CruzaMedianoche
            ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
            : fecha.ToDateTime(turno.HoraFin);

        return (int)Math.Abs((entrada - inicio).TotalMinutes)
               + (int)Math.Abs((salida - fin).TotalMinutes);
    }
}
