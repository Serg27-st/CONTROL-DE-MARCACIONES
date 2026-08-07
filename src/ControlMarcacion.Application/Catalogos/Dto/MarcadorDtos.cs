using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del catálogo de marcadores.
/// </summary>
public sealed record MarcadorListaDto(
    long Id,
    string Codigo,
    long PlantaId,
    string Planta,
    string Empresa,
    string? Descripcion,
    bool Activo,
    /// <summary>Trabajadores con este ID asignado y sin fecha de fin.</summary>
    int AsignacionesVigentes,
    /// <summary>Asignaciones históricas, vigentes o ya cerradas. Bloquea la baja.</summary>
    int AsignacionesTotales)
{
    public bool EstaEnUso => AsignacionesTotales > 0;
}

/// <summary>
/// Ficha mínima de un marcador del catálogo, para validar una asignación desde Personal.
/// </summary>
public sealed record MarcadorCatalogoDto(
    long Id,
    string Codigo,
    long PlantaId,
    string Planta,
    bool Activo);

/// <summary>
/// Filtros del catálogo de marcadores.
/// </summary>
public sealed record FiltroMarcadores
{
    public string? Busqueda { get; init; }

    public long? PlantaId { get; init; }

    public bool IncluirInactivos { get; init; }
}

/// <summary>
/// Alta y edición de un marcador.
/// </summary>
public sealed record GuardarMarcadorRequest
{
    public long? Id { get; init; }

    /// <summary>
    /// Código tal como lo escribe el equipo en el TXT, incluidos los ceros a la
    /// izquierda. Texto y no número, por eso mismo.
    /// </summary>
    [Required(ErrorMessage = "El código del marcador es obligatorio.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "El código no puede superar los 30 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Seleccione la planta del marcador.")]
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione la planta del marcador.")]
    public long PlantaId { get; init; }

    [StringLength(150, ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string? Descripcion { get; init; }

    public bool Activo { get; init; } = true;
}
