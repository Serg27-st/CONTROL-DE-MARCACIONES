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

    public int MinutosTardanza { get; set; }

    public int MinutosSalidaAnticipada { get; set; }

    /// <summary>Trabajo en día de descanso semanal o feriado, con su propia sobretasa.</summary>
    public bool EsDiaNoLaborable { get; set; }

    // ---------- Estado y trazabilidad ----------

    public EstadoTareo Estado { get; set; }

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
    SinVinculo = 9
}
