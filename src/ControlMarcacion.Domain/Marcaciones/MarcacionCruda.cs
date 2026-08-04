using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Marcaciones;

/// <summary>
/// Marcación tal como la reportó el marcador ZKTeco. <b>Dato duro e inmutable.</b>
/// </summary>
/// <remarks>
/// Esta es la piedra angular de la auditabilidad del sistema:
/// <code>Marcador (inmutable) + Justificaciones (editable) = Tareo final</code>
///
/// Una vez insertada, una marcación <b>no se edita ni se borra jamás</b>. Si un tareo
/// sale mal, se corrige con una justificación, que deja rastro de quién la hizo y cuándo.
/// Editar la marcación destruiría la única evidencia de lo que el equipo registró
/// realmente, y con ella la capacidad de defender la planilla ante una inspección.
///
/// La protección real contra recargar dos veces el mismo archivo es el índice único
/// sobre (TrabajadorId, FechaHora, IdMarcador), no una validación en C#.
/// </remarks>
public class MarcacionCruda : EntidadBase
{
    /// <summary>Trabajador resuelto por la asignación de marcador vigente a la fecha.</summary>
    public long TrabajadorId { get; set; }

    /// <summary>
    /// Identificador tal como vino en el archivo. Se conserva textualmente aunque ya
    /// esté resuelto el trabajador: es la evidencia de lo que dijo el equipo.
    /// </summary>
    public string IdMarcador { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    /// <summary>Importación que trajo esta marcación. Permite rastrear su origen.</summary>
    public long ImportacionId { get; set; }

    /// <summary>Modo de verificación reportado por el equipo (huella, tarjeta, rostro).</summary>
    public string? ModoVerificacion { get; set; }

    /// <summary>Tipo de marcación reportado por el equipo, si lo informa.</summary>
    public string? TipoMarcacion { get; set; }

    /// <summary>Línea original del archivo, para poder auditar el parseo.</summary>
    public string? LineaOriginal { get; set; }
}
