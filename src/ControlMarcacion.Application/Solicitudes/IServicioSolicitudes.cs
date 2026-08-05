using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Solicitudes.Dto;

namespace ControlMarcacion.Application.Solicitudes;

/// <summary>
/// Solicitudes de ausencia y su ciclo de aprobación.
/// </summary>
/// <remarks>
/// La solicitud es la fuente formal de las ausencias previstas. Al aprobarse se convierte
/// en <b>justificaciones</b>, una por cada día cubierto, y son esas justificaciones —no la
/// solicitud— las que el motor de cálculo consume.
///
/// Esa conversión es deliberada: el tareo se explica día por día, y una solicitud de quince
/// días no dice qué pasó con el martes en que además hubo tardanza.
/// </remarks>
public interface IServicioSolicitudes
{
    Task<IReadOnlyList<SolicitudListaDto>> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken cancelacion = default);

    Task<SolicitudListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarSolicitudRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Aprueba la solicitud y genera una justificación por cada día cubierto.
    /// </summary>
    Task<ResultadoAprobacion> AprobarAsync(
        long id,
        string usuario,
        CancellationToken cancelacion = default);

    Task RechazarAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Anula una solicitud aprobada y retira las justificaciones que había generado.
    /// </summary>
    /// <remarks>
    /// Las solicitudes no se borran: se anulan con motivo. Las justificaciones que generó
    /// sí se retiran, porque dejarlas seguiría justificando una ausencia que ya no tiene
    /// respaldo.
    /// </remarks>
    Task AnularAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Trabajadores vigentes, para el desplegable del formulario.</summary>
    Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default);

    /// <summary>Tipos de ausencia activos.</summary>
    Task<IReadOnlyList<OpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default);
}
