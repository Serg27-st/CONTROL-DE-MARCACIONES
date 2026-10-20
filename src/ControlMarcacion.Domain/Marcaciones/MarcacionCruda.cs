using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Marcaciones;

/// <summary>
/// Marcación tal como la reportó un equipo ZKTeco. <b>Dato duro e inmutable.</b>
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
/// sobre (EquipoId, IdMarcador, FechaHora), no una validación en C#. ⚠️ El equipo forma
/// parte de la clave: sin él, dos aparatos que numeran a su gente desde el 1 producirían
/// marcaciones que el índice tomaría por repetidas y descartaría en silencio.
///
/// <b>No guarda de quién es.</b> Solo lo que dijo el aparato: equipo, número y hora. El
/// dueño lo decide <see cref="AtribucionMarcaciones"/> al calcular, con la asignación de
/// ID vigente en la fecha de la marcación. Guardarlo aquí congelaba la atribución del
/// momento de la carga: un número sin dueño ese día se descartaba, y uno asignado mal
/// quedaba para siempre a nombre de otra persona.
/// </remarks>
public class MarcacionCruda : EntidadBase
{
    /// <summary>Aparato del que salió esta marcación.</summary>
    /// <remarks>
    /// Es lo que hace atribuible el archivo: el TXT del G3 no trae ninguna columna que
    /// diga de qué equipo salió, así que este dato lo declara quien carga el archivo y se
    /// contrasta contra la serie cuando el formato la trae.
    /// </remarks>
    public long EquipoId { get; set; }

    public EquipoMarcador? Equipo { get; set; }

    /// <summary>
    /// Número del usuario en el aparato, tal como vino en el archivo, con sus ceros a la
    /// izquierda. Junto con <see cref="EquipoId"/> y la fecha es lo que identifica a la
    /// persona.
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
