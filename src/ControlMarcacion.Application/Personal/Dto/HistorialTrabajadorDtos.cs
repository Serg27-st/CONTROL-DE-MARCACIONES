using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Personal;

namespace ControlMarcacion.Application.Personal.Dto;

/// <summary>
/// Datos de la baja de un trabajador.
/// </summary>
/// <remarks>
/// La baja es una acción propia y no un campo más del formulario de edición: exige
/// motivo, cierra las asignaciones vigentes de marcador, turno y sueldo, y es el único
/// hecho que habilita cambiar de razón social. Mezclarla con el resto de la ficha haría
/// que un guardado distraído cesara a alguien sin que nadie lo decidiera.
/// </remarks>
public sealed record BajaTrabajadorRequest
{
    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "No se indicó qué trabajador se va a reingresar.")]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "La fecha de cese es obligatoria.")]
    public DateOnly FechaCese { get; init; }

    [Required(ErrorMessage = "El motivo de la baja es obligatorio.")]
    [EnumDataType(typeof(MotivoCese), ErrorMessage = "Seleccione un motivo de baja válido.")]
    public MotivoCese? Motivo { get; init; }

    [StringLength(300, ErrorMessage = "La observación no puede pasar de 300 caracteres.")]
    public string? Observacion { get; init; }
}

/// <summary>
/// Datos mínimos para abrir un nuevo vínculo laboral de una persona ya registrada.
/// </summary>
/// <remarks>
/// Turno, marcador, sueldo y área conservan sus operaciones propias. Un reingreso no
/// inventa asignaciones para esos conceptos: solo abre el vínculo y su planta inicial.
/// </remarks>
public sealed record ReingresarTrabajadorRequest
{
    [Required]
    public long TrabajadorId { get; init; }

    [Required(ErrorMessage = "La nueva fecha de ingreso es obligatoria.")]
    public DateOnly FechaIngreso { get; init; }

    [Required(ErrorMessage = "Seleccione la empresa del nuevo vínculo.")]
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la empresa del nuevo vínculo.")]
    public long EmpresaId { get; init; }

    [Required(ErrorMessage = "Seleccione la unidad inicial del nuevo vínculo.")]
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la unidad inicial del nuevo vínculo.")]
    public long PlantaId { get; init; }
}

/// <summary>
/// Un vínculo laboral con su rango de fechas, para la pantalla de historial.
/// </summary>
public sealed record PeriodoLaboralDto(
    long Id,
    string Empresa,
    DateOnly FechaIngreso,
    DateOnly? FechaCese,
    MotivoCese? MotivoCese,
    string? ObservacionCese,
    string? RegistradoPor,
    DateTime RegistradoEn)
{
    public bool EstaAbierto => FechaCese is null;

    public string? MotivoCeseLegible => MotivoCese is { } motivo ? EtiquetasPersonal.Describir(motivo) : null;
}

/// <summary>Un tramo de sueldo básico con su vigencia.</summary>
public sealed record HistorialSueldoDto(
    long Id,
    decimal SueldoBasicoMensual,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string? RegistradoPor,
    DateTime RegistradoEn)
{
    public bool EstaVigente => FechaFin is null;
}

/// <summary>Un tramo de asignación de ID de marcador con su vigencia.</summary>
public sealed record HistorialMarcadorDto(
    long Id,
    string IdMarcador,
    string? Equipo,
    string? Planta,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string? RegistradoPor,
    DateTime RegistradoEn)
{
    public bool EstaVigente => FechaFin is null;
}

/// <summary>Un tramo de asignación de turno con su vigencia.</summary>
public sealed record HistorialTurnoDto(
    long Id,
    string Turno,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string? RegistradoPor,
    DateTime RegistradoEn)
{
    public bool EstaVigente => FechaFin is null;
}

/// <summary>
/// Historial completo de un trabajador: los cuatro datos que se rastrean por rango de
/// fechas, más la bitácora de auditoría de su ficha.
/// </summary>
/// <remarks>
/// Van juntos en un solo DTO porque la pantalla los muestra juntos y separarlos
/// obligaría a cuatro viajes a la base para pintar una sola página.
/// </remarks>
public sealed record HistorialTrabajadorDto(
    long TrabajadorId,
    string ApellidosYNombres,
    string NumeroDocumento,
    IReadOnlyList<PeriodoLaboralDto> PeriodosLaborales,
    IReadOnlyList<HistorialSueldoDto> Sueldos,
    IReadOnlyList<HistorialMarcadorDto> Marcadores,
    IReadOnlyList<HistorialTurnoDto> Turnos);
