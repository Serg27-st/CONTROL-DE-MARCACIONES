using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Justificaciones;

namespace ControlMarcacion.Application.Asistencia.Dto;

/// <summary>Fila de la bandeja de incidencias: un día que espera una decisión.</summary>
public sealed record IncidenciaDto(
    long TareoId,
    long TrabajadorId,
    string Trabajador,
    string? IdMarcador,
    string? Equipo,
    DateOnly Fecha,
    long PeriodoId,
    string PeriodoCodigo,
    bool PeriodoAbierto,
    string? Turno,
    TimeOnly? HoraInicioTurno,
    TimeOnly? HoraFinTurno,
    DateTime? EntradaReal,
    DateTime? SalidaReal,
    EstadoTareo Estado,
    TipoIncidencia? TipoIncidencia,
    int MarcacionesFueraVentana,
    string? Observacion,
    long? JustificacionId,
    EstadoJustificacion? EstadoJustificacion)
{
    /// <summary>
    /// Se puede pedir una regularización para este día.
    /// </summary>
    /// <remarks>
    /// Refleja el mismo criterio que aplica el servidor al guardar. Ocultar el botón no es
    /// el control —la validación sigue en <c>Application</c>—, pero ofrecer una acción que
    /// el servidor ya va a rechazar tampoco ayuda a nadie.
    /// </remarks>
    public bool SePuedeRegularizar =>
        PeriodoAbierto
        && EstadoJustificacion is null or Domain.Justificaciones.EstadoJustificacion.Rechazada
                                       or Domain.Justificaciones.EstadoJustificacion.Cancelada;

    /// <summary>Hay una regularización pedida esperando que alguien la resuelva.</summary>
    public bool EsperaResolucion =>
        EstadoJustificacion is Domain.Justificaciones.EstadoJustificacion.Solicitada;
}

/// <summary>Filtro de la bandeja de incidencias.</summary>
public sealed record FiltroIncidencias
{
    public long? PeriodoId { get; init; }

    public long? TrabajadorId { get; init; }

    public TipoIncidencia? Tipo { get; init; }

    /// <summary>
    /// Solo lo que espera decisión. Apagado muestra también lo ya resuelto, que es como se
    /// audita después de un cierre.
    /// </summary>
    public bool SoloPendientes { get; init; } = true;

    public int Pagina { get; init; } = 1;

    public int TamanioPagina { get; init; } = 50;
}

/// <summary>Alta de una regularización sobre un día concreto.</summary>
/// <remarks>
/// Ninguna de las dos horas lleva <c>[Required]</c>: se exige <b>al menos una</b>, y cuál
/// depende de qué falte. Pedir las dos en el DTO bloquearía el caso más común, que es
/// aportar solo la que el marcador no registró.
/// </remarks>
public sealed record RegistrarRegularizacionRequest
{
    [Required(ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateOnly Fecha { get; init; }

    /// <summary>Hora de entrada verificada. Vacía si lo que falta es la salida.</summary>
    public DateTime? EntradaRegularizada { get; init; }

    /// <summary>Hora de salida verificada.</summary>
    public DateTime? SalidaRegularizada { get; init; }

    /// <summary>Qué incidencia viene a resolver, para la trazabilidad de la bandeja.</summary>
    public TipoIncidencia? TipoIncidencia { get; init; }

    [Required(ErrorMessage = "El motivo es obligatorio: es lo que sustenta la corrección.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;

    [StringLength(400)]
    public string? RutaDocumentoSustento { get; init; }
}
