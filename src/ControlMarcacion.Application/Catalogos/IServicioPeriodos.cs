using ControlMarcacion.Application.Catalogos.Dto;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento de periodos de tareo.
/// </summary>
/// <remarks>
/// Va aparte del mantenimiento genérico de catálogos porque un periodo no es código más
/// nombre: define un rango de fechas y, sobre todo, **gobierna el ciclo de apertura y
/// cierre** que congela la información. Un periodo cerrado no admite ninguna escritura.
/// </remarks>
public interface IServicioPeriodos
{
    Task<IReadOnlyList<PeriodoListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default);

    Task<PeriodoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarPeriodoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Cierra un periodo abierto: lo congela para toda escritura.
    /// </summary>
    Task CerrarAsync(long id, string usuario, CancellationToken cancelacion = default);

    /// <summary>
    /// Reabre un periodo cerrado. El motivo es obligatorio: es información de auditoría.
    /// </summary>
    Task ReabrirAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Días con marcaciones cargadas que no caen dentro de ningún periodo activo.
    /// </summary>
    /// <remarks>
    /// Complementa el control de solapamiento: ese impide que dos periodos se pisen, este
    /// detecta lo contrario, los huecos. Un día fuera de todo periodo no se puede tarear y
    /// nadie lo advierte hasta que la planilla sale corta.
    /// </remarks>
    Task<IReadOnlyList<DiaSinPeriodo>> DetectarDiasSinPeriodoAsync(
        CancellationToken cancelacion = default);
}
