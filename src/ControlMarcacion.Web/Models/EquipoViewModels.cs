using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del catálogo de equipos marcadores.</summary>
public sealed class ListaEquiposVm
{
    public FiltroEquipos Filtro { get; init; } = new();

    public IReadOnlyList<EquipoListaDto> Equipos { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];
}

/// <summary>Modelo del formulario de alta y edición de un equipo marcador.</summary>
public sealed class EditarEquipoVm
{
    public GuardarEquipoRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;

    /// <summary>
    /// Marcadores que ya cuelgan de este equipo. Cuando hay alguno, el formulario bloquea
    /// la planta: los marcadores la heredan, así que mudar el aparato arrastraría a otra
    /// planta a todo el personal que los tiene asignados.
    /// </summary>
    public int MarcadoresTotales { get; set; }

    /// <summary>Archivos ya cargados desde este equipo.</summary>
    public int Importaciones { get; set; }

    public bool EstaEnUso => MarcadoresTotales > 0 || Importaciones > 0;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
}
