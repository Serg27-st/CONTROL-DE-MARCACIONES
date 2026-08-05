using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Inicio y cierre de sesión.
/// </summary>
/// <remarks>
/// No hay autorregistro: las cuentas las crea un ADMINISTRADOR desde
/// <see cref="UsuariosController"/>, igual que RRHH da de alta al trabajador en Personal.
/// </remarks>
[AllowAnonymous]
public sealed class CuentaController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    public IActionResult Ingresar(string? returnUrl = null) =>
        View(new IngresarVm { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ingresar(IngresarVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario = await userManager.FindByEmailAsync(modelo.Correo).ConfigureAwait(false);

        // Mismo mensaje genérico si el correo no existe, si la cuenta está desactivada o
        // si la contraseña es incorrecta: distinguirlos le dice a quien ataca cuáles
        // correos sí tienen cuenta.
        if (usuario is null || !usuario.Activo)
        {
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(modelo);
        }

        var resultado = await signInManager
            .PasswordSignInAsync(usuario, modelo.Clave, modelo.Recordarme, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "La cuenta quedó bloqueada temporalmente por varios intentos fallidos.");
            return View(modelo);
        }

        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View(modelo);
        }

        if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl))
        {
            return Redirect(modelo.ReturnUrl);
        }

        return RedirectToAction("Index", "Inicio");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salir()
    {
        await signInManager.SignOutAsync().ConfigureAwait(false);

        return RedirectToAction(nameof(Ingresar));
    }

    [HttpGet]
    public IActionResult AccesoDenegado() => View();
}
