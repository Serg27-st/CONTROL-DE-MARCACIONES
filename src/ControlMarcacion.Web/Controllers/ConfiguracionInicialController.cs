using System.Data;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Models;
using ControlMarcacion.Web.Operaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Web.Controllers;

/// <summary>Wizard de un solo uso para crear el primer coordinador.</summary>
[AllowAnonymous]
[Route("setup")]
public sealed class ConfiguracionInicialController(
    IVerificadorEstadoOperativo verificador,
    UserManager<ApplicationUser> userManager,
    MarcacionDbContext contexto) : Controller
{
    private static readonly SemaphoreSlim Exclusividad = new(1, 1);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var estado = await verificador.VerificarAsync(cancelacion).ConfigureAwait(false);
        return ResolverAcceso(estado) ?? View(new ConfiguracionInicialVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        ConfiguracionInicialVm modelo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        var acceso = ResolverAcceso(
            await verificador.VerificarAsync(cancelacion).ConfigureAwait(false));
        if (acceso is not null)
        {
            return acceso;
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        await Exclusividad.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            // Se vuelve a comprobar dentro de la exclusión: dos pestañas que enviaron el
            // formulario al mismo tiempo no pueden crear dos coordinadores iniciales.
            acceso = ResolverAcceso(
                await verificador.VerificarAsync(cancelacion).ConfigureAwait(false));
            if (acceso is not null)
            {
                return acceso;
            }

            await using var transaccion = await contexto.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancelacion)
                .ConfigureAwait(false);

            if (await userManager.Users.AnyAsync(cancelacion).ConfigureAwait(false))
            {
                return RedirectToAction("Ingresar", "Cuenta");
            }

            var coordinador = new ApplicationUser
            {
                UserName = modelo.Correo.Trim(),
                Email = modelo.Correo.Trim(),
                NombreCompleto = modelo.NombreCompleto.Trim(),
                EmailConfirmed = true,
                Activo = true
            };

            var creacion = await userManager
                .CreateAsync(coordinador, modelo.Clave)
                .ConfigureAwait(false);
            if (!creacion.Succeeded)
            {
                AgregarErrores(creacion);
                return View(modelo);
            }

            var rol = await userManager
                .AddToRoleAsync(coordinador, RolesSistema.Coordinador)
                .ConfigureAwait(false);
            if (!rol.Succeeded)
            {
                AgregarErrores(rol);
                return View(modelo);
            }

            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
            TempData["Mensaje"] = "Configuración inicial completada. Ya puede iniciar sesión.";
            return RedirectToAction("Ingresar", "Cuenta");
        }
        finally
        {
            Exclusividad.Release();
        }
    }

    private IActionResult? ResolverAcceso(ResultadoEstadoOperativo estado)
    {
        if (estado.Listo)
        {
            return RedirectToAction("Ingresar", "Cuenta");
        }

        return estado.RequiereConfiguracion ? null : NotFound();
    }

    private void AgregarErrores(IdentityResult resultado)
    {
        foreach (var error in resultado.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
