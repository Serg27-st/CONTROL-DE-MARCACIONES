using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Application.Marcaciones;

/// <summary>
/// Carga de marcaciones desde un archivo del marcador.
/// </summary>
public interface IServicioImportacion
{
    /// <summary>
    /// Indica si este contenido exacto ya se importó antes, comparando su hash.
    /// </summary>
    /// <remarks>
    /// Permite avisar "este archivo ya se cargó el 12/07 a las 14:30" <b>antes</b> de
    /// procesarlo, en vez de que el usuario lo descubra por dos mil filas duplicadas.
    /// </remarks>
    Task<ImportacionPrevia?> BuscarImportacionPreviaAsync(
        Stream contenido,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Lee el archivo, resuelve cada marcación contra el padrón y persiste las nuevas.
    /// </summary>
    Task<ResultadoImportacion> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        string usuario,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<ImportacionHistorialDto>> HistorialAsync(
        int maximo = 50,
        CancellationToken cancelacion = default);

    Task<ResultadoImportacion?> ObtenerResultadoAsync(
        long importacionId,
        CancellationToken cancelacion = default);
}
