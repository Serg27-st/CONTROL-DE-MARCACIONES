using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Identificador que un equipo ZKTeco asigna a una persona, dado de alta como catálogo y
/// <b>vinculado al aparato</b> que lo emite.
/// </summary>
/// <remarks>
/// Antes el ID de marcador se escribía a mano en la ficha del trabajador. Eso admitía
/// cualquier texto: un dígito de más quedaba registrado sin queja y sus marcaciones caían
/// en el reporte de "IDs sin trabajador asignado" hasta que alguien lo notara a fin de mes.
/// Con el catálogo, el formulario ofrece solo los IDs que existen en el equipo elegido.
///
/// ⚠️ <b>El código es único dentro de su equipo, no en todo el sistema.</b> Antes lo era
/// globalmente, porque el aparato no se modelaba y el código era la única identidad
/// disponible; dos equipos en la misma planta hacían imposible registrar a la segunda
/// mitad del personal. Desde que existe <see cref="EquipoMarcador"/>, la identidad es el
/// par <b>(equipo, código)</b>: el usuario 14 del equipo A–L y el 14 del M–Z son dos
/// personas distintas, como lo son en la realidad.
///
/// La contrapartida es que <b>un archivo ya no se explica solo</b>: el TXT del G3 no trae
/// ninguna columna que diga de qué aparato salió, así que la carga exige declarar planta y
/// equipo antes de procesar nada. Ver <c>IServicioImportacion</c>.
/// </remarks>
public class Marcador : EntidadBase
{
    /// <summary>
    /// Código tal como lo reporta el equipo en el archivo.
    /// <b>Texto</b>, para conservar los ceros a la izquierda: un "00123" convertido a
    /// número deja de cruzar con el archivo.
    /// </summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Aparato que emite este código.</summary>
    public long EquipoId { get; set; }

    public EquipoMarcador? Equipo { get; set; }

    /// <summary>Planta donde está instalado el equipo que emite este código.</summary>
    /// <remarks>
    /// ⚠️ <b>Copia derivada de <see cref="EquipoMarcador.PlantaId"/>, no un dato propio.</b>
    /// La escribe únicamente el servicio de marcadores a partir del equipo, y existe para
    /// que las consultas que filtran por planta —el selector de la ficha del trabajador,
    /// los listados— no tengan que unir con el equipo en cada una. Nunca la asignes a mano:
    /// si diverge de la del equipo, el formulario de Personal ofrecería marcadores de otra
    /// planta.
    /// </remarks>
    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    /// <summary>Referencia libre del portador del código, opcional.</summary>
    public string? Descripcion { get; set; }
}
