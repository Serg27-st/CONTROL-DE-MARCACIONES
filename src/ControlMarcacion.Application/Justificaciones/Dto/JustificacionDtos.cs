using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Justificaciones;

namespace ControlMarcacion.Application.Justificaciones.Dto;

/// <summary>Fila del listado de justificaciones.</summary>
public sealed record JustificacionListaDto(
    long Id,
    long TrabajadorId,
    string Trabajador,
    long PeriodoId,
    string PeriodoCodigo,
    DateOnly Fecha,
    OrigenJustificacion Origen,
    long? SolicitudAusenciaId,
    string? TipoAusencia,
    TipoJustificacion Tipo,
    int? MinutosJustificados,
    string? EstadoAnterior,
    string Motivo,
    string RegistradoPorUsuario,
    DateTime RegistradoEn)
{
    /// <summary>
    /// Solo se retira desde aquí una justificación manual. La generada por una solicitud
    /// se retira anulando la solicitud, para que quede una sola puerta de salida y el
    /// motivo de anulación viaje con ella.
    /// </summary>
    public bool EsRetirableAqui => Origen == OrigenJustificacion.Manual;
}

/// <summary>Alta de una justificación manual.</summary>
/// <remarks>
/// Solo existe para el caso que una solicitud no cubre: una tardanza puntual, una
/// marcación impar de un solo día, algo que no amerita el trámite completo de una
/// solicitud de ausencia.
/// </remarks>
public sealed record CrearJustificacionManualRequest
{
    [Required(ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    public DateOnly Fecha { get; init; }

    [Required(ErrorMessage = "Seleccione qué tipo de justificación es.")]
    public TipoJustificacion Tipo { get; init; }

    /// <summary>
    /// Tipo de ausencia. Solo tiene sentido cuando <see cref="Tipo"/> es Ausencia:
    /// es lo que dice si el día se paga o no.
    /// </summary>
    public long? TipoAusenciaId { get; init; }

    /// <summary>
    /// Minutos perdonados, para Tardanza o SalidaAnticipada. Vacío perdona todos.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Los minutos deben estar entre 1 y 1440.")]
    public int? MinutosJustificados { get; init; }

    [Required(ErrorMessage = "El motivo es obligatorio: es lo que sustenta la corrección.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;

    [StringLength(400)]
    public string? RutaDocumentoSustento { get; init; }
}

/// <summary>Filtro del listado de justificaciones.</summary>
/// <remarks>
/// Los tres filtros son opcionales, así que sin paginar la pantalla traería la tabla
/// entera. Esta crece <b>una fila por trabajador y por día de ausencia</b>: aprobar unas
/// vacaciones de 30 días genera 30 filas de una sola vez, y en una planta de 50 obreros
/// son miles al año.
/// </remarks>
public sealed record FiltroJustificaciones
{
    public long? PeriodoId { get; init; }

    public long? TrabajadorId { get; init; }

    public OrigenJustificacion? Origen { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanioPagina { get; init; } = 50;
}
