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
}
