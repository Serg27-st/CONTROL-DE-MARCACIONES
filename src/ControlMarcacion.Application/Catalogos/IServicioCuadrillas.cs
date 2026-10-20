using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Cuadrillas que rotan de turno y el ciclo que sigue cada una.
/// </summary>
/// <remarks>
/// <para>
/// Quién integra cada cuadrilla no se decide aquí: es una asignación de turno del
/// trabajador, con vigencia por fecha, y la escribe <c>ServicioTrabajadores</c>. Aquí vive
/// el ciclo: qué turno le toca al grupo cada día.
/// </para>
/// <para>
/// Las fechas de consulta llegan como parámetro y no se leen del reloj, para que las
/// pruebas usen fechas literales.
/// </para>
/// </remarks>
public interface IServicioCuadrillas
{
    Task<IReadOnlyList<CuadrillaListaDto>> ListarAsync(
        DateOnly fecha,
        bool incluirInactivas,
        CancellationToken cancelacion = default);

    Task<CuadrillaListaDto?> ObtenerAsync(long id, DateOnly fecha, CancellationToken cancelacion = default);

    /// <summary>Todas las programaciones de la cuadrilla, la más reciente primero, incluidas las dadas de baja.</summary>
    Task<IReadOnlyList<ProgramacionCuadrillaDto>> ProgramacionesAsync(
        long id,
        CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarCuadrillaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Baja lógica o reactivación. No se da de baja una cuadrilla con integrantes vigentes.</summary>
    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Programa un ciclo nuevo desde una fecha. La programación vigente se cierra el día
    /// previo; si empezaba ese mismo día o después, queda reemplazada entera y se da de baja.
    /// </summary>
    Task ProgramarAsync(
        ProgramarCuadrillaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Desde una fecha, el mismo ciclo corrido unos días: la cuadrilla rotó antes o después
    /// de lo programado.
    /// </summary>
    Task CorrerRotacionAsync(
        CorrerRotacionRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Plantas disponibles para el formulario.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default);
}
