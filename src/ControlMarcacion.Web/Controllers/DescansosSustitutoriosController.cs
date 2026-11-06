using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Otorgamiento administrativo y auditable del día completo que sustituye a un feriado
/// trabajado. El controlador nunca escribe el contexto directamente.
/// </summary>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class DescansosSustitutoriosController(IServicioDescansosSustitutorios servicio)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(new ListaDescansosSustitutoriosVm
        {
            Elementos = await servicio.ListarAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion) =>
        View(new CrearDescansoSustitutorioVm
        {
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(
        CrearDescansoSustitutorioVm modelo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                await servicio.OtorgarAsync(
                        modelo.Datos, User.Identity?.Name ?? "sistema", cancelacion)
                    .ConfigureAwait(false);
                TempData["Mensaje"] =
                    "Descanso sustitutorio otorgado. Recalcule los periodos involucrados antes de liquidar.";
                return RedirectToAction(nameof(Index));
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (PeriodoCerradoException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        modelo.Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false);
        return View("Crear", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(
        AnularDescansoSustitutorioRequest solicitud,
        CancellationToken cancelacion)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Indique un motivo de anulación válido.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await servicio.AnularAsync(
                    solicitud, User.Identity?.Name ?? "sistema", cancelacion)
                .ConfigureAwait(false);
            TempData["Mensaje"] =
                "Descanso sustitutorio anulado. Recalcule los periodos involucrados antes de liquidar.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (PeriodoCerradoException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
