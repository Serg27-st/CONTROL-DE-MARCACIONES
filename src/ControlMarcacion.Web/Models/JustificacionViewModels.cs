using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Justificaciones;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de justificaciones.</summary>
public sealed class ListaJustificacionesVm
{
    public ResultadoPaginado<JustificacionListaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<JustificacionListaDto>(50);

    public long? PeriodoId { get; init; }

    public long? TrabajadorId { get; init; }

    public OrigenJustificacion? Origen { get; init; }

    public int Pagina { get; init; } = 1;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Periodos { get; init; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; init; } = [];
}

/// <summary>Modelo del formulario de alta manual.</summary>
public sealed class CrearJustificacionVm
{
    public CrearJustificacionManualRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; set; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> TiposAusencia { get; set; } = [];
}
