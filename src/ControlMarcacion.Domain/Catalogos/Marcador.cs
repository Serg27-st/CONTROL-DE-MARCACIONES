using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Identificador que el equipo ZKTeco asigna a una persona, dado de alta como catálogo
/// y <b>vinculado a la planta</b> donde está instalado el equipo que lo emite.
/// </summary>
/// <remarks>
/// Antes el ID de marcador se escribía a mano en la ficha del trabajador. Eso admitía
/// cualquier texto: un dígito de más quedaba registrado sin queja y sus marcaciones
/// caían en el reporte de "IDs sin trabajador asignado" hasta que alguien lo notara a
/// fin de mes. Con el catálogo, el formulario ofrece solo los IDs que existen en la
/// planta del trabajador.
///
/// <b>El código es único en todo el sistema, no por planta.</b> Puede parecer natural
/// que cada planta numere sus marcadores desde el 1, pero el TXT del G3 no trae ninguna
/// columna que diga de qué planta salió: la ingesta resuelve ID → trabajador solo por el
/// texto del ID y la fecha de la marcación. Dos plantas con el código "001" harían que
/// esas marcaciones fueran inatribuibles. La planta describe dónde vive el marcador; no
/// desambigua su código.
/// </remarks>
public class Marcador : EntidadBase
{
    /// <summary>
    /// Código tal como lo reporta el equipo en el archivo TXT.
    /// <b>Texto</b>, para conservar los ceros a la izquierda: un "00123" convertido a
    /// número deja de cruzar con el archivo.
    /// </summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Planta donde está instalado el equipo que emite este código.</summary>
    public long PlantaId { get; set; }

    public Planta? Planta { get; set; }

    /// <summary>Referencia libre del equipo o su ubicación, opcional.</summary>
    public string? Descripcion { get; set; }
}
