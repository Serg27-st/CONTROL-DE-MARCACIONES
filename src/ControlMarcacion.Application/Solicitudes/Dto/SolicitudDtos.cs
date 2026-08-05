using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Solicitudes;

namespace ControlMarcacion.Application.Solicitudes.Dto;

/// <summary>Fila del listado de solicitudes de ausencia.</summary>
public sealed record SolicitudListaDto(
    long Id,
    long TrabajadorId,
    string Trabajador,
    string TipoAusencia,
    string ComportamientoEtiqueta,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFin,
    EstadoSolicitud Estado,
    bool RequiereSustento,
    string? Sustento,
    string? Observacion,
    string? MotivoAnulacion,
    int JustificacionesGeneradas)
{
    public int Dias => FechaFin.DayNumber - FechaInicio.DayNumber + 1;

    public bool EsPorHoras => HoraInicio is not null;

    /// <summary>Solo una solicitud pendiente admite decisión.</summary>
    public bool AdmiteDecision => Estado == EstadoSolicitud.Pendiente;

    /// <summary>Una solicitud aprobada puede anularse; una anulada o rechazada, no.</summary>
    public bool AdmiteAnulacion => Estado == EstadoSolicitud.Aprobada;
}

/// <summary>Alta y edición de una solicitud de ausencia.</summary>
public sealed record GuardarSolicitudRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "Seleccione el tipo de ausencia.")]
    public long TipoAusenciaId { get; init; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaInicio { get; init; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaFin { get; init; }

    /// <summary>Obligatorias solo si el tipo de ausencia se registra por horas.</summary>
    public TimeOnly? HoraInicio { get; init; }

    public TimeOnly? HoraFin { get; init; }

    [StringLength(500, ErrorMessage = "El sustento no puede superar los 500 caracteres.")]
    public string? Sustento { get; init; }

    [StringLength(400)]
    public string? RutaDocumentoSustento { get; init; }

    [StringLength(500)]
    public string? Observacion { get; init; }
}

/// <summary>Filtro del listado de solicitudes.</summary>
public sealed record FiltroSolicitudes
{
    public long? TrabajadorId { get; init; }

    public EstadoSolicitud? Estado { get; init; }

    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }
}

/// <summary>
/// Resultado de aprobar una solicitud.
/// </summary>
/// <remarks>
/// Aprobar no es solo cambiar un estado: genera una justificación por cada día cubierto.
/// Los días que caen en un periodo cerrado no se pueden justificar y se informan aparte,
/// en vez de fallar entera la aprobación o —peor— saltárselos en silencio.
/// </remarks>
public sealed record ResultadoAprobacion(
    long SolicitudId,
    int JustificacionesCreadas,
    IReadOnlyList<DateOnly> DiasEnPeriodoCerrado,
    IReadOnlyList<DateOnly> DiasSinPeriodo,
    IReadOnlyList<DateOnly> DiasConJustificacionExistente)
{
    public bool HuboOmisiones =>
        DiasEnPeriodoCerrado.Count > 0 || DiasSinPeriodo.Count > 0 || DiasConJustificacionExistente.Count > 0;
}
