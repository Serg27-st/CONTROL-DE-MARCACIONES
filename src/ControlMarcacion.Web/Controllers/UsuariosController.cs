using ControlMarcacion.Application.Catalogos;
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
/// Restringido a COORDINADOR en su totalidad: quién puede iniciar sesión en el sistema
/// y con qué rol es, junto con el cierre de periodo, la decisión más sensible del sistema.
/// Ni ADMINISTRADOR entra aquí — si pudiera, se daría a sí mismo cualquier permiso.
/// </remarks>
[Authorize(Roles = RolesSistema.Coordinador)]
public sealed class UsuariosController(
    IServicioUsuarios servicio,
    IConsultaCatalogos catalogos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(new ListaUsuariosVm { Usuarios = await servicio.ListarAsync(cancelacion).ConfigureAwait(false) });

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion)
    {
        var modelo = new CrearUsuarioVm();

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View(modelo);
    }

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

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

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

    /// <summary>
    /// Plantas de una empresa, para encadenar el formulario de alta.
    /// </summary>
    /// <remarks>
    /// Endpoint propio y no el de Trabajadores: aquel está restringido a otro conjunto de
    /// roles, y colgar esta pantalla de él ataría dos matrices de permisos que no tienen
    /// por qué moverse juntas.
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> PlantasDeEmpresa(long empresaId, CancellationToken cancelacion) =>
        Json(await catalogos
            .PlantasAsync(empresaId == 0 ? null : empresaId, cancelacion)
            .ConfigureAwait(false));

    private async Task CargarCatalogosAsync(CrearUsuarioVm modelo, CancellationToken cancelacion)
    {
        modelo.Roles = servicio.RolesDisponibles();
        modelo.TiposDocumento = await catalogos.TiposDocumentoAsync(cancelacion).ConfigureAwait(false);
        modelo.Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        modelo.Plantas = await catalogos.PlantasAsync(modelo.Datos.EmpresaId, cancelacion).ConfigureAwait(false);
    }
}
