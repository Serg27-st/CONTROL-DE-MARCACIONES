using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Mantenimiento del catálogo de marcadores.
/// </summary>
/// <remarks>
/// Va aparte del mantenimiento genérico de catálogos por el mismo motivo que los turnos:
/// un marcador no es código más nombre. Pertenece a una planta, su código tiene que ser
/// único en todo el sistema — el TXT del G3 no dice de qué planta salió cada marcación —
/// y no puede darse de baja mientras alguien lo tenga asignado.
/// </remarks>
public interface IServicioMarcadores
{
    Task<IReadOnlyList<MarcadorListaDto>> ListarAsync(
        FiltroMarcadores filtro,
        CancellationToken cancelacion = default);

    Task<MarcadorListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarMarcadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Plantas disponibles, con su empresa, para el filtro del listado.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default);

    /// <summary>Equipos activos, con su planta, para el desplegable del formulario.</summary>
    /// <remarks>
    /// Un marcador se da de alta contra un <b>equipo</b>, no contra una planta: la planta
    /// se hereda del aparato. Si la lista vuelve vacía no hay nada que registrar todavía y
    /// el formulario debe decirlo, en vez de ofrecer un desplegable sin opciones.
    /// </remarks>
    Task<IReadOnlyList<EquipoOpcionDto>> EquiposAsync(CancellationToken cancelacion = default);
}
