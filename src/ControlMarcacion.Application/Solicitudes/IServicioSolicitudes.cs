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
    Task<ResultadoPaginado<SolicitudListaDto>> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken cancelacion = default);

    Task<SolicitudListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    Task<long> GuardarAsync(
        GuardarSolicitudRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Aprueba la solicitud y genera una justificación por cada día cubierto, en una sola
    /// operación atómica. Si cualquier día no puede justificarse, no aprueba ni genera filas.
    /// </summary>
    Task<ResultadoAprobacion> AprobarAsync(
        long id,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Registra el sustento de una solicitud que se dio de alta sin él.
    /// </summary>
    /// <remarks>
    /// Es la contraparte de que el sustento no sea obligatorio al registrar: quien pide un
    /// descanso médico avisa el mismo día y trae el certificado después. Sin esta acción,
    /// el mensaje de <see cref="AprobarAsync"/> —«regístrelo antes de aprobar»— apuntaba a
    /// algo que el sistema no permitía hacer, y la única salida era anular la solicitud y
    /// volver a crearla.
    ///
    /// <b>Deliberadamente estrecha.</b> No es una edición de la solicitud: no toca
    /// trabajador, tipo ni fechas, que son lo que el aprobador ya evaluó. Solo agrega el
    /// documento que faltaba.
    ///
    /// Solo sobre solicitudes <b>pendientes</b>: una ya aprobada generó justificaciones que
    /// heredaron el texto del sustento, y cambiarlo después las dejaría diciendo una cosa
    /// distinta de la solicitud que las originó.
    /// </remarks>
    /// <exception cref="Domain.Comun.ReglaNegocioException">
    /// Si la solicitud no está pendiente o el sustento viene vacío.
    /// </exception>
    Task RegistrarSustentoAsync(
        long id,
        string sustento,
        string? rutaDocumentoSustento,
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
    /// Las solicitudes no se borran: se anulan con motivo. Sus justificaciones tampoco se
    /// borran: pasan a canceladas para dejar de afectar el tareo sin perder la evidencia de
    /// qué días estuvieron justificados, quién los anuló y por qué.
    /// </remarks>
    Task AnularAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>Trabajadores vigentes, para el desplegable del formulario.</summary>
    Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default);

    /// <summary>Tipos de ausencia activos, con su código y sus topes de días.</summary>
    Task<IReadOnlyList<TipoAusenciaOpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<SolicitudCambioTurnoDto>> ListarCambiosTurnoAsync(
        CancellationToken cancelacion = default);

    Task<long> GuardarCambioTurnoAsync(
        GuardarCambioTurnoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    Task ResolverCambioTurnoAsync(
        long id,
        bool aprueba,
        string? motivo,
        string usuario,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> TurnosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Periodos vacacionales seleccionables, del <c>2020-2021</c> al que abre el año en curso.
    /// </summary>
    /// <remarks>
    /// Es una lista calculada y no una tabla: un periodo vacacional no tiene atributos
    /// propios que mantener, solo un rótulo. Se genera en el servidor y se envía a la vista
    /// para que el JavaScript no tenga que saber cómo se arma el rótulo.
    ///
    /// El límite inferior de 2020 es una decisión de captura: las vacaciones anteriores ya
    /// están liquidadas y no se registran en este sistema.
    /// </remarks>
    static IReadOnlyList<string> GenerarPeriodosVacaciones()
    {
        const int primerAnio = 2020;

        var ultimoAnio = DateTime.Now.Year;
        var periodos = new List<string>(ultimoAnio - primerAnio + 1);

        for (var anio = primerAnio; anio <= ultimoAnio; anio++)
        {
            periodos.Add($"{anio}-{anio + 1}");
        }

        return periodos;
    }
}
