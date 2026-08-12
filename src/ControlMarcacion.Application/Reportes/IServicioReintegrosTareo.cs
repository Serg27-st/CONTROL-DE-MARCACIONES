using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;

namespace ControlMarcacion.Application.Reportes;

/// <summary>
/// Captura manual de los reintegros y descuentos que el tareo final no calcula: el bloque
/// <c>REINTEGROS</c> (AO-BF), cargado a mano por planilla.
/// </summary>
/// <remarks>
/// No recalcula ni deriva nada: es una ficha plana de 18 campos por trabajador y periodo.
/// <see cref="IServicioReportes"/> la lee al armar el tareo final; este servicio solo la
/// mantiene.
/// </remarks>
public interface IServicioReintegrosTareo
{
    /// <summary>Nulo si el trabajador todavía no tiene reintegros cargados para ese periodo.</summary>
    Task<ReintegroTareoDto?> ObtenerAsync(
        long trabajadorId, long periodoId, CancellationToken cancelacion = default);

    /// <summary>
    /// Crea o actualiza el único registro de reintegros del trabajador para el periodo.
    /// </summary>
    /// <remarks>Solo se admite mientras el periodo esté abierto.</remarks>
    Task GuardarAsync(
        GuardarReintegroTareoRequest solicitud, string usuario, CancellationToken cancelacion = default);

    /// <summary>Trabajadores vigentes, para el desplegable de la pantalla de captura.</summary>
    Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default);
}
