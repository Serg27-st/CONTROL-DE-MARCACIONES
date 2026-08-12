using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento de turnos.
/// </summary>
/// <remarks>
/// Va aparte del mantenimiento genérico de catálogos porque un turno no es código más
/// nombre: define horarios, refrigerio, tolerancia, las ventanas que deciden si una
/// marcación es entrada o salida, y en qué plantas está habilitado.
/// </remarks>
public interface IServicioTurnos
{
    Task<IReadOnlyList<TurnoListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default);

    Task<TurnoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarTurnoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Plantas disponibles para habilitar el turno.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default);
}
