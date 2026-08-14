using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento de los catálogos simples, despachado por clave.
/// </summary>
public interface IServicioCatalogos
{
    Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(
        string clave,
        long? padreId,
        bool incluirInactivos,
        CancellationToken cancelacion = default);

    Task<CatalogoItemDto?> ObtenerAsync(string clave, long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        string clave,
        GuardarCatalogoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Activa o desactiva un elemento.
    /// </summary>
    /// <remarks>
    /// Es una baja lógica: los catálogos nunca se borran físicamente porque hay tareos y
    /// marcaciones históricas que los referencian y deben seguir siendo consultables.
    /// </remarks>
    Task CambiarEstadoAsync(
        string clave,
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Opciones del catálogo padre, para el desplegable del formulario.</summary>
    Task<IReadOnlyList<OpcionDto>> OpcionesPadreAsync(string clave, CancellationToken cancelacion = default);

    /// <summary>Cuántos elementos activos e inactivos tiene cada catálogo.</summary>
    /// <remarks>
    /// Es lo que convierte la portada de catálogos en información en vez de un menú: sin
    /// los conteos hay que entrar a cada uno para descubrir cuál está vacío, que es
    /// justamente lo primero que se quiere saber al configurar el sistema.
    /// </remarks>
    Task<IReadOnlyDictionary<string, ConteoCatalogo>> ConteosAsync(CancellationToken cancelacion = default);
}
