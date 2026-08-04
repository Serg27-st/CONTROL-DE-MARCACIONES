using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Turno de trabajo. Los cinco turnos iniciales son datos semilla, no código:
/// DIU (07:00–19:00), NOC (19:00–07:00), SAB (07:00–15:00, solo Planta 01),
/// DOM1 (07:00–15:00) y DOM2 (07:00–19:00).
/// </summary>
public class Turno : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public TimeOnly HoraInicio { get; set; }

    public TimeOnly HoraFin { get; set; }

    /// <summary>
    /// Minutos de refrigerio que se descuentan de la jornada.
    /// </summary>
    /// <remarks>
    /// El refrigerio <b>no se marca</b>: el trabajador registra solo entrada y salida,
    /// así que se descuenta como valor fijo configurable por turno. El mínimo legal en
    /// Perú es de 45 minutos y no forma parte de la jornada salvo pacto en contrario.
    /// </remarks>
    public int MinutosRefrigerio { get; set; }

    /// <summary>
    /// Tolerancia de llegada antes de considerar la marcación como tardanza.
    /// La tolerancia es <b>inclusiva</b>: llegar exactamente en el minuto límite es puntual.
    /// </summary>
    /// <remarks>
    /// Mide minutos <b>después</b> de <see cref="HoraInicio"/> y solo decide si hubo tardanza.
    /// No confundir con <see cref="MinutosAnticipacionEntrada"/>, que mide minutos
    /// <b>antes</b> y decide si la marcación pertenece a este turno.
    /// </remarks>
    public int MinutosTolerancia { get; set; }

    /// <summary>
    /// Cuántos minutos antes de <see cref="HoraInicio"/> una marcación todavía cuenta como
    /// la <b>entrada</b> de este turno.
    /// </summary>
    /// <remarks>
    /// Resuelve una pregunta distinta de la tolerancia: la tolerancia pregunta
    /// "¿llegó tarde?", esta ventana pregunta "¿esta marca es la entrada de este turno?".
    /// El equipo no distingue entrada de salida, así que el motor tiene que decidirlo por
    /// cercanía a los extremos del turno, y necesita saber cuánta anticipación es normal.
    ///
    /// Es configurable por turno desde el catálogo, no una constante: cada planta tiene su
    /// propia costumbre de a qué hora empieza a llegar la gente.
    /// </remarks>
    public int MinutosAnticipacionEntrada { get; set; } = 15;

    /// <summary>
    /// Cuántos minutos antes de <see cref="HoraFin"/> una marcación todavía cuenta como
    /// la <b>salida</b> de este turno.
    /// </summary>
    /// <remarks>
    /// Es deliberadamente más estrecha que la de entrada: llegar temprano es normal, pero
    /// irse temprano es una salida anticipada que debe quedar registrada como tal y no
    /// absorberse en silencio dentro de la ventana.
    /// </remarks>
    public int MinutosAnticipacionSalida { get; set; } = 5;

    /// <summary>
    /// Jornada ordinaria en minutos. Por defecto 480 (8 horas), que es el máximo legal
    /// diario en Perú. Todo lo trabajado por encima es hora extra.
    /// </summary>
    public int MinutosJornadaOrdinaria { get; set; } = 480;

    /// <summary>Días de la semana en que aplica este turno.</summary>
    public DiasSemana DiasAplicables { get; set; }

    /// <summary>
    /// Plantas donde este turno está habilitado. Vacío significa que aplica a todas.
    /// Es lo que modela que el turno especial de sábado exista solo en Planta 01,
    /// sin condiciones cableadas en el código.
    /// </summary>
    public ICollection<TurnoPlanta> PlantasHabilitadas { get; set; } = [];

    /// <summary>
    /// El turno termina al día siguiente (por ejemplo 19:00 → 07:00).
    /// </summary>
    /// <remarks>
    /// Cuando esto es verdadero, <b>la jornada se imputa al día en que inicia el turno</b>.
    /// Es la principal fuente de errores en los sistemas de asistencia.
    /// </remarks>
    public bool CruzaMedianoche => HoraFin <= HoraInicio;

    /// <summary>Duración bruta del turno en minutos, sin descontar el refrigerio.</summary>
    public int DuracionBrutaMinutos => CruzaMedianoche
        ? (int)(TimeSpan.FromDays(1) - HoraInicio.ToTimeSpan() + HoraFin.ToTimeSpan()).TotalMinutes
        : (int)(HoraFin.ToTimeSpan() - HoraInicio.ToTimeSpan()).TotalMinutes;

    /// <summary>Duración efectiva del turno, ya descontado el refrigerio.</summary>
    public int DuracionEfectivaMinutos => DuracionBrutaMinutos - MinutosRefrigerio;

    /// <summary>
    /// Hora a partir de la cual una marcación se considera la entrada de este turno.
    /// </summary>
    public TimeOnly InicioVentanaEntrada => HoraInicio.AddMinutes(-MinutosAnticipacionEntrada);

    /// <summary>
    /// Hora a partir de la cual una marcación se considera la salida de este turno.
    /// </summary>
    public TimeOnly InicioVentanaSalida => HoraFin.AddMinutes(-MinutosAnticipacionSalida);

    /// <summary>
    /// Hora límite para llegar sin que se considere tardanza.
    /// </summary>
    public TimeOnly LimiteTolerancia => HoraInicio.AddMinutes(MinutosTolerancia);
}

[Flags]
public enum DiasSemana
{
    Ninguno = 0,
    Lunes = 1,
    Martes = 2,
    Miercoles = 4,
    Jueves = 8,
    Viernes = 16,
    Sabado = 32,
    Domingo = 64,

    LunesASabado = Lunes | Martes | Miercoles | Jueves | Viernes | Sabado,
    Todos = LunesASabado | Domingo
}
