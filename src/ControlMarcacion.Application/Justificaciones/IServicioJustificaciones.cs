using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones.Dto;

namespace ControlMarcacion.Application.Justificaciones;

/// <summary>
/// Justificaciones: la capa que reconcilia el dato duro del marcador con la realidad,
/// sin tocar la marcación original.
/// </summary>
/// <remarks>
/// La mayoría nacen automáticamente al aprobar una solicitud
/// (<see cref="Solicitudes.IServicioSolicitudes"/>). Este servicio cubre lo que queda:
/// consultarlas con su trazabilidad completa, y el alta manual para el caso puntual que
/// no amerita el trámite de una solicitud — perdonar una tardanza, completar una marca
/// impar de un solo día.
/// </remarks>
public interface IServicioJustificaciones
{
    Task<ResultadoPaginado<JustificacionListaDto>> ListarAsync(
        FiltroJustificaciones filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Crea una justificación manual.
    /// </summary>
    /// <remarks>
    /// Solo se escribe si el periodo de la fecha está abierto. Y solo puede existir una
    /// justificación por trabajador y fecha: el motor de cálculo resuelve una sola por
    /// día, así que una segunda no se ignoraría, chocaría.
    /// </remarks>
    Task<long> CrearManualAsync(
        CrearJustificacionManualRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Retira una justificación manual. Las generadas por una solicitud se retiran
    /// anulando la solicitud, no desde aquí.
    /// </summary>
    Task EliminarAsync(long id, string usuario, CancellationToken cancelacion = default);

    /// <summary>Trabajadores vigentes, para el desplegable del formulario manual.</summary>
    Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default);

    /// <summary>Tipos de ausencia activos.</summary>
    Task<IReadOnlyList<OpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default);
}
