using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Listado de cuadrillas.</summary>
public sealed class ListaCuadrillasVm
{
    public IReadOnlyList<CuadrillaListaDto> Cuadrillas { get; init; } = [];

    public bool IncluirInactivas { get; init; }

    public DateOnly Hoy { get; init; }
}

/// <summary>Alta y edición de una cuadrilla: código, nombre y planta.</summary>
public sealed class EditarCuadrillaVm
{
    public GuardarCuadrillaRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
}

/// <summary>
/// Una cuadrilla con todo lo que se hace con ella: su ciclo, sus integrantes y la historia
/// de sus programaciones.
/// </summary>
public sealed class DetalleCuadrillaVm
{
    public CuadrillaListaDto Cuadrilla { get; init; } = null!;

    public IReadOnlyList<ProgramacionCuadrillaDto> Programaciones { get; init; } = [];

    public IReadOnlyList<IntegranteCuadrillaDto> Integrantes { get; init; } = [];

    public IReadOnlyList<CandidatoCuadrillaDto> Candidatos { get; init; } = [];

    public DateOnly Hoy { get; init; }
}

/// <summary>Formulario del ciclo: desde qué día y qué tramos.</summary>
public sealed class ProgramarCuadrillaVm
{
    /// <summary>Filas de tramo que ofrece el formulario. Las vacías se ignoran.</summary>
    public const int FilasDeTramo = 6;

    public ProgramarCuadrillaDatosVm Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public CuadrillaListaDto Cuadrilla { get; set; } = null!;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Turnos { get; set; } = [];
}

/// <summary>
/// Modelo de binding del formulario. No expone directamente el DTO de Application: MVC
/// necesita una colección mutable para completar y validar cada índice del POST.
/// </summary>
public sealed class ProgramarCuadrillaDatosVm
{
    public long CuadrillaId { get; set; }

    [Required(ErrorMessage = "Indique desde qué fecha rige el ciclo.")]
    public DateOnly? Desde { get; set; }

    public List<TramoProgramadoVm> Tramos { get; set; } = [];
}

/// <summary>Una fila del formulario, incluida la fila vacía que todavía no se usa.</summary>
public sealed class TramoProgramadoVm : IValidatableObject
{
    public TipoTramoRotacion Tipo { get; set; } = TipoTramoRotacion.Trabajo;

    public long? TurnoId { get; set; }

    [Range(1, 366, ErrorMessage = "Cada tramo debe durar entre 1 y 366 días.")]
    public int? Dias { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Tipo == TipoTramoRotacion.Descanso)
        {
            if (TurnoId is not null)
            {
                yield return new ValidationResult(
                    "Un descanso no puede tener turno.",
                    [nameof(TurnoId)]);
            }

            if (Dias != 1)
            {
                yield return new ValidationResult(
                    "Un descanso debe durar exactamente un día.",
                    [nameof(Dias)]);
            }

            yield break;
        }

        // Las filas completamente vacías son las plazas libres del formulario y se ignoran.
        if (TurnoId is null && Dias is null)
        {
            yield break;
        }

        if (TurnoId is null)
        {
            yield return new ValidationResult(
                "Un tramo de trabajo necesita turno.",
                [nameof(TurnoId)]);
        }

        if (Dias is null)
        {
            yield return new ValidationResult(
                "Un tramo de trabajo necesita duración.",
                [nameof(Dias)]);
        }
    }
}
