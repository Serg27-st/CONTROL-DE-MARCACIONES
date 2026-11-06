using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>Cuadrilla para los desplegables: la ficha del trabajador la ofrece por planta.</summary>
public sealed record CuadrillaOpcionDto(
    long Id,
    string Codigo,
    string Nombre,
    long PlantaId,
    string Planta,
    bool Activo);

/// <summary>Un tramo de la secuencia, laboral o descanso explícito.</summary>
public sealed record TramoRotacionDto(
    int Orden,
    TipoTramoRotacion Tipo,
    long? TurnoId,
    string? TurnoCodigo,
    string? TurnoNombre,
    int Dias)
{
    public string Descripcion => Tipo == TipoTramoRotacion.Descanso
        ? "DESCANSO PROGRAMADO"
        : $"{TurnoCodigo} {Dias} d";
}

/// <summary>Una programación de la cuadrilla, vigente o histórica.</summary>
public sealed record ProgramacionCuadrillaDto(
    long Id,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    DateOnly FechaAncla,
    bool Activo,
    IReadOnlyList<TramoRotacionDto> Tramos,
    string? RegistradoPor,
    DateTime RegistradoEn)
{
    /// <summary>«DIU 14 d → DESCANSO PROGRAMADO → NOC 7 d».</summary>
    public string Ciclo => string.Join(" → ", Tramos.Select(t => t.Descripcion));

    public int DiasDelCiclo => Tramos.Sum(t => t.Dias);
}

/// <summary>Una rotación que viene: desde qué día y a qué turno.</summary>
public sealed record CambioProgramadoDto(DateOnly Fecha, string TurnoCodigo, string TurnoNombre);

/// <summary>Fila del listado de cuadrillas, con lo que se viene a mirar: qué turno tiene hoy y cuándo rota.</summary>
public sealed record CuadrillaListaDto(
    long Id,
    string Codigo,
    string Nombre,
    long PlantaId,
    string Planta,
    bool Activo,
    int IntegrantesVigentes,
    ProgramacionCuadrillaDto? ProgramacionVigente,
    string? TurnoHoy,
    IReadOnlyList<CambioProgramadoDto> ProximosCambios);

/// <summary>Alta y edición de una cuadrilla.</summary>
public sealed record GuardarCuadrillaRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la unidad.")]
    public long PlantaId { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>Un tramo del ciclo tal como llega del formulario. Las filas vacías se ignoran.</summary>
public sealed record TramoProgramadoRequest
{
    public TipoTramoRotacion Tipo { get; init; } = TipoTramoRotacion.Trabajo;

    public long? TurnoId { get; init; }

    [Range(1, 366, ErrorMessage = "Cada tramo debe durar entre 1 y 366 días.")]
    public int? Dias { get; init; }
}

/// <summary>
/// Programa el ciclo de la cuadrilla desde una fecha: ese día empieza el primer tramo.
/// </summary>
public sealed record ProgramarCuadrillaRequest
{
    public long CuadrillaId { get; init; }

    [Required(ErrorMessage = "Indique desde qué fecha rige el ciclo.")]
    public DateOnly? Desde { get; init; }

    public IReadOnlyList<TramoProgramadoRequest> Tramos { get; init; } = [];
}

/// <summary>
/// Corre la rotación de la cuadrilla desde una fecha: el mismo ciclo, adelantado o atrasado
/// unos días.
/// </summary>
public sealed record CorrerRotacionRequest
{
    public long CuadrillaId { get; init; }

    [Required(ErrorMessage = "Indique desde qué fecha se corre la rotación.")]
    public DateOnly? Desde { get; init; }

    /// <summary>Negativo: rotaron antes de lo programado. Positivo: después.</summary>
    [Range(-60, 60, ErrorMessage = "Se puede correr entre 60 días antes y 60 días después.")]
    public int Dias { get; init; }
}
