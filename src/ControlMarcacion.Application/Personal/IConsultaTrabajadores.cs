using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;

namespace ControlMarcacion.Application.Personal;

/// <summary>
/// Lado de lectura del padrón de personal.
/// </summary>
/// <remarks>
/// Está separado del repositorio a propósito. El listado necesita el nombre de la planta,
/// del área, de la sección, del puesto y del turno vigente: resolverlo cargando entidades
/// completas provocaría una consulta por cada trabajador de la página.
/// Estas consultas proyectan directamente a DTO con un solo viaje a la base.
/// </remarks>
public interface IConsultaTrabajadores
{
    Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(
        FiltroTrabajadores filtro,
        CancellationToken cancelacion = default);

    Task<TrabajadorDetalleDto?> ObtenerDetalleAsync(
        long id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Ficha para el panel flotante: nombres ya resueltos (para el modo "ver") y
    /// sueldo básico vigente a hoy.
    /// </summary>
    Task<TrabajadorPanelDto?> ObtenerPanelAsync(
        long id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Búsqueda en vivo para el panel flotante: coincidencia parcial en documento,
    /// apellidos, nombres o el ID de marcador con asignación vigente. Máximo 50
    /// resultados.
    /// </summary>
    Task<IReadOnlyList<TrabajadorBusquedaRapidaDto>> BuscarRapidoAsync(
        string? texto,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Historial por rangos de fecha de un trabajador: vínculos laborales
    /// (ingreso y cese), sueldo básico, ID de marcador y turno.
    /// </summary>
    Task<HistorialTrabajadorDto?> ObtenerHistorialAsync(
        long id,
        CancellationToken cancelacion = default);
}
