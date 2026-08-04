using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Listas de catálogo para poblar los desplegables de los formularios.
/// </summary>
public interface IConsultaCatalogos
{
    Task<IReadOnlyList<OpcionDto>> TiposDocumentoAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> EmpresasAsync(CancellationToken cancelacion = default);

    /// <summary>Plantas de una empresa. Sin empresa, devuelve todas.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(long? empresaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> AreasAsync(long? plantaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> SeccionesAsync(long? areaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> PuestosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Turnos disponibles. Si se indica la planta, se excluyen los que no están
    /// habilitados en ella: el turno especial de sábado solo existe en Planta 01.
    /// </summary>
    Task<IReadOnlyList<OpcionDto>> TurnosAsync(long? plantaId, CancellationToken cancelacion = default);
}
