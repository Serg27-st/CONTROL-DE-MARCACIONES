using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del catálogo de marcadores.</summary>
public sealed class ListaMarcadoresVm
{
    public FiltroMarcadores Filtro { get; init; } = new();

    public IReadOnlyList<MarcadorListaDto> Marcadores { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];
}

/// <summary>Modelo del formulario de alta y edición de un marcador.</summary>
public sealed class EditarMarcadorVm
{
    public GuardarMarcadorRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;

    /// <summary>
    /// Asignaciones que ya referencian este código. Cuando hay alguna, el formulario
    /// bloquea el código: <c>AsignacionMarcador</c> lo guarda como texto y renombrarlo
    /// dejaría esas asignaciones apuntando a un código inexistente.
    /// </summary>
    public int AsignacionesTotales { get; set; }

    public bool EstaEnUso => AsignacionesTotales > 0;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
}
