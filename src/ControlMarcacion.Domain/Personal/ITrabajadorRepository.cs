namespace ControlMarcacion.Domain.Personal;

/// <summary>
/// Acceso de escritura al padrón de trabajadores.
/// </summary>
/// <remarks>
/// La interfaz vive en Domain y su implementación en Infrastructure: es lo que permite
/// que Domain no dependa de EF Core.
///
/// Solo cubre el lado de escritura. Las consultas de listado van por
/// <c>IConsultaTrabajadores</c>, que proyecta a DTO en vez de materializar entidades
/// completas — un listado de 500 trabajadores con sus asignaciones sería un desperdicio.
/// </remarks>
public interface ITrabajadorRepository
{
    Task<Trabajador?> ObtenerPorIdAsync(long id, CancellationToken cancelacion = default);

    /// <summary>
    /// Indica si ya existe otro trabajador con el mismo documento de identidad.
    /// </summary>
    /// <param name="excluyendoId">
    /// Id que se debe ignorar en la comprobación. Se usa al editar, para que un
    /// trabajador no choque consigo mismo.
    /// </param>
    /// <summary>
    /// Id del trabajador que tiene ese documento, o <c>null</c> si no existe.
    /// </summary>
    /// <remarks>
    /// Lo usa la carga masiva cuando se le pide actualizar en vez de rechazar: el archivo
    /// trae el documento, no la clave interna, y sin esto la única salida sería rechazar
    /// la fila por duplicada.
    /// </remarks>
    Task<long?> IdPorDocumentoAsync(
        long tipoDocumentoId,
        string numeroDocumento,
        CancellationToken cancelacion = default);

    Task<bool> ExisteDocumentoAsync(
        long tipoDocumentoId,
        string numeroDocumento,
        long? excluyendoId = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Indica si el identificador del marcador ya está asignado a otra persona
    /// con vigencia solapada.
    /// </summary>
    Task<bool> IdMarcadorEnUsoAsync(
        long equipoId,
        string idMarcador,
        DateOnly desde,
        DateOnly? hasta,
        long? excluyendoTrabajadorId = null,
        CancellationToken cancelacion = default);

    Task AgregarAsync(Trabajador trabajador, CancellationToken cancelacion = default);

    void Actualizar(Trabajador trabajador);

    Task<int> GuardarCambiosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Persiste un reingreso y traduce la colisión del índice que impide dos vínculos
    /// abiertos en un mensaje funcional. El guardado sigue siendo una sola transacción.
    /// </summary>
    Task<int> GuardarReingresoAsync(CancellationToken cancelacion = default);
}
