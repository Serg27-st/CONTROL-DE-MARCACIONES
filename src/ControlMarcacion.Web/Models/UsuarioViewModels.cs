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
public sealed class CrearUsuarioVm
{
    public CrearUsuarioRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<string> Roles { get; set; } = [];
}
