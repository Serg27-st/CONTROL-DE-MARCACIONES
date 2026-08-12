using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Application.Marcaciones;

/// <summary>
/// Origen de las marcaciones.
/// </summary>
/// <remarks>
/// Hoy la única implementación lee el TXT que se extrae por USB del ZKTeco G3. La interfaz
/// existe para que mañana se pueda conectar directamente al equipo por red sin tocar el
/// resto del sistema: el servicio de importación no sabe de dónde vienen los datos.
/// </remarks>
public interface IFuenteDeMarcaciones
{
    /// <summary>
    /// Interpreta el contenido y devuelve las marcaciones válidas junto con las líneas
    /// que no se pudieron leer.
    /// </summary>
    /// <remarks>
    /// No lanza excepción por una línea mala: una línea corrupta no puede impedir que se
    /// carguen las otras dos mil quinientas. Los problemas se reportan, no se propagan.
    /// </remarks>
    ResultadoLectura Leer(Stream contenido);
}
