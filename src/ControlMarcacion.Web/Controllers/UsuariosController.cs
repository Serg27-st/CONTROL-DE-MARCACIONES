using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Alta y baja lógica de cuentas de usuario.
/// </summary>
/// <remarks>
/// Restringido a ADMINISTRADOR en su totalidad: quién puede iniciar sesión en el sistema
/// y con qué rol es, junto con el cierre de periodo, la decisión más sensible del sistema.
/// </remarks>
[Authorize(Roles = RolesSistema.Administrador)]
public sealed class UsuariosController(IServicioUsuarios servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(new ListaUsuariosVm { Usuarios = await servicio.ListarAsync(cancelacion).ConfigureAwait(false) });

    [HttpGet]
    public IActionResult Crear() =>
        View(new CrearUsuarioVm { Roles = servicio.RolesDisponibles() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(CrearUsuarioVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                await servicio.CrearAsync(modelo.Datos, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = $"Usuario '{modelo.Datos.Correo}' creado correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
        }

        modelo.Roles = servicio.RolesDisponibles();

        return View("Crear", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(string id, bool activo, CancellationToken cancelacion)
    {
        try
        {
            await servicio.CambiarEstadoAsync(id, activo, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = activo ? "Usuario reactivado." : "Usuario desactivado.";
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
