using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Personal.Dto;

/// <summary>
/// Contexto laboral canónico de un trabajador en una fecha efectiva. No contiene ningún
/// respaldo a los campos planos de la ficha.
/// </summary>
public sealed record ContextoLaboralFechaDto(
    long PeriodoLaboralId,
    long EmpresaId,
    long? PlantaId,
    DateOnly FechaIngreso,
    DateOnly? FechaCese);

/// <summary>Quien integra una cuadrilla en una fecha, y desde cuándo.</summary>
public sealed record IntegranteCuadrillaDto(
    long TrabajadorId,
    string Nombre,
    string NumeroDocumento,
    DateOnly Desde,
    DateOnly? Hasta);

/// <summary>
/// Trabajador de la planta que se puede sumar a una cuadrilla, con lo que tiene hoy: un
/// turno fijo, otra cuadrilla o nada.
/// </summary>
public sealed record CandidatoCuadrillaDto(
    long TrabajadorId,
    string Nombre,
    string NumeroDocumento,
    string? AsignacionActual);

/// <summary>
/// Suma varios trabajadores a una cuadrilla desde una fecha. Es la misma operación que
/// elegir la cuadrilla en la ficha de cada uno, hecha de una vez.
/// </summary>
public sealed record AsignarCuadrillaRequest
{
    public long CuadrillaId { get; init; }

    [Required(ErrorMessage = "Indique desde qué fecha integran la cuadrilla.")]
    public DateOnly? Desde { get; init; }

    public IReadOnlyList<long> TrabajadorIds { get; init; } = [];
}
