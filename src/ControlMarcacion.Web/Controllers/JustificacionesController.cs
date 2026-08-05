using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Justificaciones: trazabilidad de marcador + justificaciones = tareo final.
/// </summary>
/// <remarks>
/// La mayoría se generan solas al aprobar una solicitud. Esta pantalla las muestra todas,
/// con su origen, y permite el alta manual para el caso puntual que no amerita el trámite
/// completo de una solicitud.
/// </remarks>
public sealed class JustificacionesController(
    IServicioJustificaciones servicio,
    IServicioPeriodos periodos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long? periodoId,
        long? trabajadorId,
        OrigenJustificacion? origen,
        CancellationToken cancelacion)
    {
        var listaPeriodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false);

        return View(new ListaJustificacionesVm
        {
            PeriodoId = periodoId,
            TrabajadorId = trabajadorId,
            Origen = origen,
            Periodos = [.. listaPeriodos.Select(p => new OpcionDto(p.Id, $"{p.Codigo} — {p.Nombre}"))],
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false),
            Justificaciones = await servicio
                .ListarAsync(
                    new FiltroJustificaciones { PeriodoId = periodoId, TrabajadorId = trabajadorId, Origen = origen },
                    cancelacion)
                .ConfigureAwait(false)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion) =>
        View(new CrearJustificacionVm
        {
            Datos = new CrearJustificacionManualRequest { Fecha = DateOnly.FromDateTime(DateTime.Now) },
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false),
            TiposAusencia = await servicio.TiposAusenciaAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(CrearJustificacionVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.CrearManualAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] =
                    "Justificación registrada. Recalcule el periodo para que se refleje en el tareo.";

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
        modelo.TiposAusencia = await servicio.TiposAusenciaAsync(cancelacion).ConfigureAwait(false);

        return View("Crear", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(long id, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            await servicio.EliminarAsync(id, usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] =
                "Justificación retirada. Recalcule el periodo para que se refleje en el tareo.";
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
