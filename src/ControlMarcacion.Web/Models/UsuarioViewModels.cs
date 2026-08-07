using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Seguridad.Dto;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de usuarios.</summary>
public sealed class ListaUsuariosVm
{
    public IReadOnlyList<UsuarioListaDto> Usuarios { get; init; } = [];
}

/// <summary>Modelo del formulario de alta de un usuario.</summary>
/// <remarks>
/// Las listas van marcadas con <c>[BindNever]</c>: son catálogos que rellena el servidor,
/// y aceptarlas desde el POST dejaría que el navegador dicte qué plantas existen.
/// </remarks>
public sealed class CrearUsuarioVm
{
    public CrearUsuarioRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<string> Roles { get; set; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> TiposDocumento { get; set; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Empresas { get; set; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];
}
