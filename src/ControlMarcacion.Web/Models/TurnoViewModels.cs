using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de turnos.</summary>
public sealed class ListaTurnosVm
{
    public IReadOnlyList<TurnoListaDto> Turnos { get; init; } = [];

    public bool IncluirInactivos { get; init; }
}

/// <summary>Modelo del formulario de alta y edición de un turno.</summary>
public sealed class EditarTurnoVm
{
    public GuardarTurnoRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
}
