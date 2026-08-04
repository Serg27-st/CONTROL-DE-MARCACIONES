using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>
/// Modelo de la pantalla de listado de personal.
/// </summary>
public sealed class ListadoTrabajadoresVm
{
    public FiltroTrabajadores Filtro { get; init; } = new();

    public ResultadoPaginado<TrabajadorListaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<TrabajadorListaDto>(25);

    public IReadOnlyList<OpcionDto> Empresas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];
}

/// <summary>
/// Modelo del formulario de alta y edición.
/// </summary>
public sealed class EditarTrabajadorVm
{
    public GuardarTrabajadorRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;

    public IReadOnlyList<OpcionDto> TiposDocumento { get; set; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Areas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Secciones { get; set; } = [];

    public IReadOnlyList<OpcionDto> Puestos { get; set; } = [];

    public IReadOnlyList<OpcionDto> Turnos { get; set; } = [];
}

/// <summary>
/// Modelo de la página de error.
/// </summary>
public sealed class ErrorVm
{
    public string? IdSolicitud { get; init; }

    public bool MostrarIdSolicitud => !string.IsNullOrEmpty(IdSolicitud);
}
