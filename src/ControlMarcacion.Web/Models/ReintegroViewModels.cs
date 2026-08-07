using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes.Dto;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla que elige trabajador y periodo antes de capturar reintegros.</summary>
public sealed class SeleccionarReintegroVm
{
    public IReadOnlyList<OpcionDto> Trabajadores { get; init; } = [];

    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];
}

/// <summary>Modelo del formulario de captura de los 18 reintegros de un trabajador y periodo.</summary>
public sealed class EditarReintegroVm
{
    public GuardarReintegroTareoRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public string Trabajador { get; set; } = "-";

    [BindNever]
    [ValidateNever]
    public string Periodo { get; set; } = "-";

    [BindNever]
    [ValidateNever]
    public bool PeriodoAbierto { get; set; }
}
