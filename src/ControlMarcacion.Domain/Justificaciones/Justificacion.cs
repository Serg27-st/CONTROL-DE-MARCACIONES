using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Justificaciones;

/// <summary>
/// Capa que reconcilia el dato duro del marcador con la realidad, sin falsificarlo.
/// </summary>
/// <remarks>
/// El marcador dice que alguien no vino; tres días después esa persona presenta un
/// descanso médico. La justificación es lo que permite reflejar ese hecho
/// <b>sin tocar la marcación original</b>:
/// <code>Marcador (inmutable) + Justificaciones (editable) = Tareo final</code>
///
/// Se alimenta automáticamente desde las solicitudes aprobadas y además el administrador
/// puede crearlas a mano durante el cierre previo. Solo se puede escribir mientras el
/// periodo esté abierto.
///
/// Es el punto más sensible del sistema desde el lado de la auditoría: es donde se podría
/// maquillar una falta. Por eso registra siempre quién, cuándo y el valor anterior.
/// </remarks>
public class Justificacion : EntidadBase
{
    public long TrabajadorId { get; set; }

    public long PeriodoId { get; set; }

    public DateOnly Fecha { get; set; }

    public OrigenJustificacion Origen { get; set; }

    /// <summary>Solicitud que la originó. Nulo cuando el origen es manual.</summary>
    public long? SolicitudAusenciaId { get; set; }

    /// <summary>Tipo de ausencia aplicado. Nulo si solo justifica una tardanza.</summary>
    public long? TipoAusenciaId { get; set; }

    public TipoJustificacion Tipo { get; set; }

    /// <summary>Minutos justificados, cuando la justificación es parcial (tardanza).</summary>
    public int? MinutosJustificados { get; set; }

    /// <summary>
    /// Estado que tenía el tareo antes de aplicar esta justificación.
    /// Es la evidencia de qué se cambió, y no se recalcula: se congela al crearla.
    /// </summary>
    public string? EstadoAnterior { get; set; }

    public string Motivo { get; set; } = null!;

    public string? RutaDocumentoSustento { get; set; }

    public string RegistradoPorUsuario { get; set; } = null!;

    public DateTime RegistradoEn { get; set; }
}

public enum OrigenJustificacion
{
    /// <summary>Generada automáticamente desde una solicitud de ausencia aprobada.</summary>
    Solicitud = 1,

    /// <summary>Cargada a mano por el administrador durante el cierre previo.</summary>
    Manual = 2
}

public enum TipoJustificacion
{
    /// <summary>Convierte una falta en ausencia justificada.</summary>
    Ausencia = 1,

    /// <summary>Perdona total o parcialmente una tardanza.</summary>
    Tardanza = 2,

    /// <summary>Completa una jornada con marcación impar (entrada sin salida).</summary>
    MarcacionIncompleta = 3,

    /// <summary>Justifica una salida anticipada.</summary>
    SalidaAnticipada = 4
}
