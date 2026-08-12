using ControlMarcacion.Application.Catalogos.Dto;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento de tipos de ausencia.
/// </summary>
/// <remarks>
/// Va aparte del mantenimiento genérico de catálogos porque un tipo de ausencia no es
/// código más nombre: el <see cref="Domain.Catalogos.ComportamientoAusencia"/> decide cómo
/// el motor de cálculo lo trata (si genera pago, si requiere subsidio, si es parcial, etc.),
/// y crear uno sin elegirlo correctamente produciría tareos incorrectos en silencio.
/// </remarks>
public interface IServicioTiposAusencia
{
    Task<IReadOnlyList<TipoAusenciaListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default);

    Task<TipoAusenciaListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarTipoAusenciaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Activa o desactiva un tipo de ausencia.
    /// </summary>
    /// <remarks>
    /// No se puede desactivar un tipo que esté en uso por solicitudes o justificaciones,
    /// porque dejaría registros huérfanos y el cálculo no sabría cómo interpretarlos.
    /// </remarks>
    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);
}

