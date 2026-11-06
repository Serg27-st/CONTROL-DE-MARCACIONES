using System.Reflection;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Trabajadores;

public sealed class CargaMasivaPuestosMvcTests
{
    [Fact]
    public void PUEIMP_11_confirmacion_conserva_autorizacion_y_antiforgery()
    {
        var autorizacion = typeof(TrabajadoresController)
            .GetCustomAttribute<AuthorizeAttribute>();
        var confirmar = typeof(TrabajadoresController)
            .GetMethod(nameof(TrabajadoresController.ConfirmarCargaMasiva));

        autorizacion.ShouldNotBeNull();
        autorizacion!.Roles.ShouldNotBeNull();
        autorizacion.Roles!.ShouldContain(RolesSistema.Coordinador);
        autorizacion.Roles.ShouldContain(RolesSistema.Administrador);
        confirmar.ShouldNotBeNull();
        confirmar.GetCustomAttribute<HttpPostAttribute>().ShouldNotBeNull();
        confirmar.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();
        confirmar.GetCustomAttribute<AllowAnonymousAttribute>().ShouldBeNull();
    }
}
