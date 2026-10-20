using ControlMarcacion.Application.Asistencia.Dto;
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
    /// Registra una regularización: la hora que el marcador no registró.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No toca la marcación original. Crea una justificación de tipo
    /// <c>MarcacionRegularizada</c> con la hora verificada, y el cálculo la suma encima del
    /// dato del equipo.
    /// </para>
    /// <para>
    /// <paramref name="apruebaDirectamente"/> lo decide <b>el controlador según el rol</b>,
    /// no este servicio: quién puede autorizar es una regla de acceso y vive donde vive el
    /// <c>[Authorize]</c>. Con el valor en falso la regularización nace <c>Solicitada</c> y
    /// no modifica nada hasta que alguien la apruebe.
    /// </para>
    /// </remarks>
    Task<long> RegularizarAsync(
        RegistrarRegularizacionRequest solicitud,
        string usuario,
        bool apruebaDirectamente,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Aprueba o rechaza una regularización pendiente.
    /// </summary>
    /// <remarks>
    /// El motivo es obligatorio al rechazar: un rechazo sin explicación deja al trabajador
    /// sin saber qué corregir y a quien audita sin saber por qué el día quedó como quedó.
    /// Dos personas resolviendo a la vez no se pisan — la segunda choca con el token de
    /// concurrencia y recibe un mensaje que dice quién se adelantó.
    /// </remarks>
    Task ResolverAsync(
        long id,
        bool aprueba,
        string? motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Retira una regularización que todavía nadie resolvió.
    /// </summary>
    /// <remarks>
    /// Es la salida del caso corriente: quien la registró nota que la hora estaba mal antes
    /// de que el coordinador la mire. La fila se conserva —el intento también es historia— y
    /// el día queda libre para volver a pedirla.
    ///
    /// Solo la retira <b>quien la pidió</b>: cancelar la de otro sería resolverla sin dejar
    /// constancia de la decisión, que es justo lo que <see cref="ResolverAsync"/> existe para
    /// evitar. Un coordinador que no esté de acuerdo la rechaza, con motivo.
    /// </remarks>
    Task CancelarAsync(long id, string usuario, CancellationToken cancelacion = default);

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
