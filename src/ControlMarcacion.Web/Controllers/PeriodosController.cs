using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de periodos de tareo.
/// </summary>
/// <remarks>
/// El periodo gobierna el ciclo de apertura y cierre que congela la información. Por eso
/// va en un controlador propio y no en el mantenimiento genérico de catálogos: no es
/// código más nombre, y el cierre y la reapertura son acciones con reglas de negocio.
/// </remarks>
public sealed class PeriodosController(IServicioPeriodos servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool incluirInactivos, CancellationToken cancelacion) =>
        View(new ListaPeriodosVm
        {
            IncluirInactivos = incluirInactivos,
            Periodos = await servicio.ListarAsync(incluirInactivos, cancelacion).ConfigureAwait(false),
            DiasSinPeriodo = await servicio.DetectarDiasSinPeriodoAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarPeriodoRequest();

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarPeriodoRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                FechaInicio = actual.FechaInicio,
                FechaFin = actual.FechaFin,
                Activo = actual.Activo
            };
        }

        return View(new EditarPeriodoVm { Datos = datos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarPeriodoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró el periodo correctamente."
                    : "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        return View("Editar", modelo);
    }

    // Cerrar y reabrir un periodo son, junto con la gestión de usuarios, las acciones más
    // sensibles del sistema: una vez cerrado, ningún tareo de ese mes vuelve a escribirse
    // salvo que alguien lo reabra explícitamente. Restringido a ADMINISTRADOR mientras no
    // haya una matriz de roles más fina confirmada por GCA.
    [Authorize(Roles = RolesSistema.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(long id, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            await servicio.CerrarAsync(id, usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = "El periodo se cerró. Desde ahora no admite ninguna modificación.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = RolesSistema.Administrador)]
    [HttpGet]
    public async Task<IActionResult> Reabrir(long id, CancellationToken cancelacion)
    {
        var actual = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (actual is null)
        {
            return NotFound();
        }

        // Sin esto, la pantalla pedía el motivo de reapertura de un periodo que ya estaba
        // abierto y recién al enviar decía que no correspondía. El rechazo tiene que
        // llegar antes de que el usuario escriba nada.
        if (actual.EstaAbierto)
        {
            TempData["Error"] = $"El periodo '{actual.Codigo}' ya está abierto: no hay nada que reabrir.";

            return RedirectToAction(nameof(Index));
        }

        return View(new ReabrirPeriodoVm
        {
            Id = actual.Id,
            Codigo = actual.Codigo,
            Nombre = actual.Nombre
        });
    }

    [Authorize(Roles = RolesSistema.Administrador)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reabrir(ReabrirPeriodoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.ReabrirAsync(modelo.Id, modelo.Motivo, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = "El periodo se reabrió. Vuelve a admitir modificaciones.";

                return RedirectToAction(nameof(Index));
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        return View("Reabrir", modelo);
    }
}
