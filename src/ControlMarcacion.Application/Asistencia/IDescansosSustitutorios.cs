using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;

namespace ControlMarcacion.Application.Asistencia;

public sealed record RegistrarDescansoSustitutorioRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione el trabajador.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "La fecha del feriado trabajado es obligatoria.")]
    public DateOnly FechaFeriadoOrigen { get; init; }

    [Required(ErrorMessage = "La fecha del descanso sustitutorio es obligatoria.")]
    public DateOnly FechaDescanso { get; init; }
}

public sealed record AnularDescansoSustitutorioRequest
{
    [Range(1, long.MaxValue)]
    public long Id { get; init; }

    [Range(0, int.MaxValue)]
    public int Version { get; init; }

    [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
    [StringLength(500, MinimumLength = 5,
        ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

public sealed record DescansoSustitutorioDto(
    long Id,
    long TrabajadorId,
    string Trabajador,
    DateOnly FechaFeriadoOrigen,
    int MinutosTrabajadosFeriado,
    DateOnly FechaDescanso,
    EstadoDescansoSustitutorio Estado,
    string RegistradoPor,
    DateTime RegistradoEn,
    string AprobadoPor,
    DateTime AprobadoEn,
    string? AnuladoPor,
    DateTime? AnuladoEn,
    string? MotivoAnulacion,
    int Version);

public interface IServicioDescansosSustitutorios
{
    Task<IReadOnlyList<DescansoSustitutorioDto>> ListarAsync(
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(
        CancellationToken cancelacion = default);

    Task<long> OtorgarAsync(
        RegistrarDescansoSustitutorioRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    Task AnularAsync(
        AnularDescansoSustitutorioRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);
}
