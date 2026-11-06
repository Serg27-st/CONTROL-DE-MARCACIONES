using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

public sealed class ListaDescansosSustitutoriosVm
{
    public IReadOnlyList<DescansoSustitutorioDto> Elementos { get; init; } = [];
}

public sealed class CrearDescansoSustitutorioVm
{
    public RegistrarDescansoSustitutorioRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; set; } = [];
}
