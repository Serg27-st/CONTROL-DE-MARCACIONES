using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Resultado del cálculo para un trabajador y un día. Es el producto final del sistema.
/// </summary>
/// <remarks>
/// Se obtiene de: marcaciones crudas + turno vigente + solicitudes + justificaciones.
///
/// El recálculo es <b>idempotente</b>: recalcular el mismo periodo dos veces debe producir
/// exactamente el mismo resultado, sin duplicar filas. Lo garantiza el índice único
/// (TrabajadorId, Fecha).
///
/// Los tiempos se guardan en <b>minutos enteros</b>, nunca en decimales de hora: los
/// <c>double</c> acumulan error de redondeo y el área de planilla lo detecta al sumar.
/// </remarks>
public class TareoDiario : EntidadBase
{
    public long PeriodoId { get; set; }

    public long TrabajadorId { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Turno vigente ese día, resuelto por la asignación con vigencia a la fecha.
    /// Nulo cuando el trabajador no tenía turno asignado.
    /// </summary>
    public long? TurnoId { get; set; }

    // ---------- Lo que dijo el marcador ----------

    public DateTime? EntradaReal { get; set; }

    public DateTime? SalidaReal { get; set; }

    // ---------- Resultado del cálculo, en minutos ----------

    /// <summary>Minutos efectivos trabajados, ya descontado el refrigerio.</summary>
    public int MinutosTrabajados { get; set; }

    /// <summary>Jornada ordinaria: hasta 480 minutos (8 horas).</summary>
    public int MinutosOrdinarios { get; set; }

    /// <summary>Horas extras con sobretasa del 25 %: las 2 primeras horas.</summary>
    public int MinutosExtra25 { get; set; }

    /// <summary>Horas extras con sobretasa del 35 %: a partir de la tercera hora.</summary>
    public int MinutosExtra35 { get; set; }

    /// <summary>
    /// Minutos caídos dentro de la franja nocturna 22:00–06:00.
    /// Se calculan sobre las horas realmente trabajadas en esa franja, no sobre el
    /// turno completo.
    /// </summary>
    public int MinutosNocturnos { get; set; }

    /// <summary>
    /// Sobretiempo al 25 % que cayó dentro de la franja nocturna. Subconjunto de
    /// <see cref="MinutosExtra25"/>.
    /// </summary>
    /// <remarks>
    /// Se persiste en vez de derivarse al leer porque el tareo de un periodo cerrado debe
    /// quedar congelado tal como se calculó: recomputar la franja al momento de exportar
    /// haría que un cambio futuro en el criterio de nocturnidad reescribiera el pasado.
    /// </remarks>
    public int MinutosExtra25Nocturnos { get; set; }

    /// <summary>
    /// Sobretiempo al 35 % que cayó dentro de la franja nocturna. Subconjunto de
    /// <see cref="MinutosExtra35"/>.
    /// </summary>
    public int MinutosExtra35Nocturnos { get; set; }

    /// <summary>
    /// Minutos nocturnos de jornada ordinaria: la base de la bonificación nocturna.
    /// Derivado, no se persiste.
    /// </summary>
    public int MinutosOrdinariosNocturnos =>
        Math.Max(0, MinutosNocturnos - MinutosExtra25Nocturnos - MinutosExtra35Nocturnos);

    public int MinutosTardanza { get; set; }

    public int MinutosSalidaAnticipada { get; set; }

    /// <summary>
    /// Tipo de día no laborable en que se trabajó, si corresponde.
    /// </summary>
    /// <remarks>
    /// No es un booleano porque el descanso semanal y el feriado son conceptos legales
    /// distintos: D. Leg. 713 art. 3 y art. 9 respectivamente. Hoy ambos pagan 100 %, pero
    /// son tasas independientes con vigencia propia y pueden divergir. Guardar solo
    /// "es no laborable" obligaría a volver a consultar el calendario de feriados para
    /// saber qué tasa aplicar, y un feriado cargado después cambiaría el pasado.
    /// </remarks>
    public TipoDiaNoLaborable TipoDiaNoLaborable { get; set; }

    /// <summary>
    /// El día provino de un tramo de descanso explícito de la rotación. Se persiste porque
    /// un TurnoId nulo también puede significar falta de programación y no es una señal
    /// suficiente para reportes ni liquidación.
    /// </summary>
    public bool EsDescansoProgramado { get; set; }

    /// <summary>
    /// Coincidencia estructural entre descanso programado y feriado. Mantiene únicos los
    /// minutos físicos y permite detener la valorización especial del 1 de mayo hasta que
    /// exista una regla económica aprobada.
    /// </summary>
    public TipoCoincidenciaFeriadoDescanso CoincidenciaFeriadoDescanso { get; set; }

    /// <summary>Se trabajó en un día que no correspondía. Derivado, no se persiste.</summary>
    public bool EsDiaNoLaborable => TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno;

    // ---------- Estado y trazabilidad ----------

    public EstadoTareo Estado { get; set; }

    /// <summary>
    /// Qué hay que resolver en este día, si hay algo. Nulo cuando el día no tiene nada
    /// pendiente.
    /// </summary>
    /// <remarks>
    /// Es un campo aparte del estado porque responde otra pregunta: el estado dice cómo
    /// quedó el día, la incidencia dice qué falta para cerrarlo. Un día
    /// <see cref="EstadoTareo.Incompleto"/> puede necesitar la entrada o la salida —lo que
    /// se regulariza es distinto—, y un día <see cref="EstadoTareo.Puntual"/> puede tener
    /// que revisarse igual si sus marcas contradicen el turno asignado.
    /// </remarks>
    public TipoIncidencia? TipoIncidencia { get; set; }

    /// <summary>
    /// El día espera una decisión humana: es lo que lo pone en la bandeja de incidencias.
    /// </summary>
    /// <remarks>
    /// Se persiste en vez de derivarse del estado porque no hay correspondencia uno a uno:
    /// una marca impar ya regularizada deja de requerir validación sin cambiar de
    /// naturaleza, y un día puntual con turno discrepante la requiere sin dejar de ser
    /// puntual. Derivarlo del estado obligaría a repetir esa tabla de equivalencias en cada
    /// consulta que arme la bandeja.
    /// </remarks>
    public bool RequiereValidacion { get; set; }

    /// <summary>
    /// Cuántas marcaciones del trabajador cayeron fuera de la ventana de su turno y no se
    /// usaron para calcular.
    /// </summary>
    /// <remarks>
    /// Hoy se descartan en silencio. Guardar el conteo es lo que permite que el día lo
    /// declare —el invariante es que todo resultado se explica— y que la bandeja distinga
    /// «no marcó» de «marcó a una hora que no encaja con su turno», que son problemas
    /// distintos y se resuelven distinto.
    /// </remarks>
    public int MarcacionesFueraVentana { get; set; }

    /// <summary>Justificación aplicada, si el estado fue modificado por una.</summary>
    public long? JustificacionId { get; set; }

    /// <summary>
    /// Explicación en lenguaje natural de por qué el día quedó así.
    /// Existe para que RRHH pueda auditar sin leer código: es la diferencia entre
    /// "el sistema dice falta" y "faltó, pero tiene descanso médico del 12 al 15".
    /// </summary>
    public string? Observacion { get; set; }

    public DateTime CalculadoEn { get; set; }
}

/// <summary>
/// Naturaleza del día no laborable trabajado, que determina qué sobretasa aplica.
/// </summary>
public enum TipoDiaNoLaborable
{
    /// <summary>Día laborable normal.</summary>
    Ninguno = 0,

    /// <summary>Descanso semanal trabajado. D. Leg. 713, art. 3.</summary>
    DescansoSemanal = 1,

    /// <summary>Feriado trabajado. D. Leg. 713, art. 9.</summary>
    Feriado = 2
}

public enum TipoCoincidenciaFeriadoDescanso
{
    Ninguna = 0,
    FeriadoOrdinario = 1,
    PrimeroDeMayo = 2
}

public enum EstadoTareo
{
    /// <summary>Asistió dentro de la tolerancia.</summary>
    Puntual = 1,

    /// <summary>Asistió, pero después del límite de tolerancia.</summary>
    Tardanza = 2,

    /// <summary>No asistió y no hay justificación.</summary>
    Falta = 3,

    /// <summary>No asistió, pero hay solicitud aprobada o justificación.</summary>
    AusenciaJustificada = 4,

    /// <summary>
    /// Marcación impar: entrada sin salida o salida sin entrada.
    /// Nunca se inventa la hora faltante ni se calculan horas asumiendo el cierre.
    /// </summary>
    Incompleto = 5,

    /// <summary>No tenía turno asignado ese día. No es falta.</summary>
    SinHorario = 6,

    /// <summary>Día de descanso semanal.</summary>
    Descanso = 7,

    /// <summary>Feriado.</summary>
    Feriado = 8,

    /// <summary>Fuera del vínculo laboral: antes del ingreso o después del cese.</summary>
    SinVinculo = 9,

    /// <summary>
    /// El sistema no puede afirmar qué pasó ese día y hace falta que una persona lo decida.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No reemplaza a <see cref="Incompleto"/>, que ya es correcto y ya significa «una sola
    /// marca, cero minutos, no se inventa la hora que falta». Este estado cubre lo que
    /// <see cref="Incompleto"/> no puede: el día en que <b>no se cargó el archivo</b> del
    /// aparato de esa persona. Ahí no hay marca impar que señalar —no hay ninguna marca— y
    /// el día salía como <see cref="Falta"/>, indistinguible de una falta real en el Excel
    /// que va a planilla.
    /// </para>
    /// <para>
    /// ⚠️ La diferencia entre los dos es de qué se duda: en <see cref="Incompleto"/> se sabe
    /// que la persona vino y falta una hora; aquí no se sabe ni siquiera si vino, porque el
    /// dato nunca llegó al sistema. Convertirlo en falta es afirmar lo que no se sabe, y en
    /// planilla eso se cobra.
    /// </para>
    /// </remarks>
    PendienteValidacion = 10,

    /// <summary>
    /// Día completo no trabajado otorgado como sustitución de un feriado ya trabajado.
    /// Mantiene cero minutos físicos y no genera incidencias de asistencia.
    /// </summary>
    DescansoSustitutorio = 11
}
