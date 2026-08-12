using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del listado de periodos.
/// </summary>
public sealed record PeriodoListaDto(
    long Id,
    string Codigo,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    EstadoPeriodo Estado,
    DateTime? CerradoEn,
    string? CerradoPor,
    DateTime? ReabiertoEn,
    string? ReabiertoPor,
    string? MotivoReapertura,
    bool Activo,
    int TareosGenerados)
{
    /// <summary>Días que abarca el periodo, extremos incluidos.</summary>
    public int Dias => FechaFin.DayNumber - FechaInicio.DayNumber + 1;

    public bool EstaAbierto => Estado == EstadoPeriodo.Abierto;
}

/// <summary>
/// Día con marcaciones cargadas que no cae dentro de ningún periodo activo.
/// </summary>
/// <remarks>
/// Es la contraparte del control de solapamiento. Impedir que dos periodos se pisen evita
/// ambigüedad, pero no evita lo contrario: que queden <b>huecos</b>. Un día fuera de todo
/// periodo nunca se podrá tarear, y el error es silencioso — nadie lo nota hasta que la
/// planilla sale corta.
///
/// El caso típico es cerrar julio del 1 al 30 olvidando que tiene 31 días.
/// </remarks>
public sealed record DiaSinPeriodo(DateOnly Fecha, int Marcaciones);

/// <summary>
/// Solicitud de alta y edición de un periodo.
/// </summary>
public sealed record GuardarPeriodoRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaInicio { get; init; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaFin { get; init; }

    public bool Activo { get; init; } = true;
}
