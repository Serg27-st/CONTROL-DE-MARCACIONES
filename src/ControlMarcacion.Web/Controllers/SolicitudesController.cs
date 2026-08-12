using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Solicitudes de ausencia: registro y ciclo de aprobación.
/// </summary>
/// <remarks>
/// Aprobar genera justificaciones, una por día cubierto; anular las retira. El
/// controlador no decide nada de eso: solo traduce HTTP hacia <see cref="IServicioSolicitudes"/>.
///
/// El atributo cubre <b>toda</b> la clase, incluidas Aprobar, Rechazar y Anular: aprobar una
/// solicitud escribe justificaciones que cambian el tareo, así que no es una acción de
/// consulta y no puede quedar abierta a cualquier autenticado.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador},{RolesSistema.TrabajadorSocial}")]
public sealed class SolicitudesController(IServicioSolicitudes servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long? trabajadorId,
        EstadoSolicitud? estado,
        int pagina,
        CancellationToken cancelacion) =>
        View(new ListaSolicitudesVm
        {
            TrabajadorId = trabajadorId,
            Estado = estado,
            Pagina = pagina,
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false),
            Resultado = await servicio
                .ListarAsync(
                    new FiltroSolicitudes
                    {
                        TrabajadorId = trabajadorId,
                        Estado = estado,
                        // Sin página en la URL llega 0, que el servicio eleva a 1.
                        Pagina = pagina
                    },
                    cancelacion)
                .ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion) =>
        View(new EditarSolicitudVm
        {
            Datos = new GuardarSolicitudRequest { FechaInicio = DateOnly.FromDateTime(DateTime.Now) },
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false),
            TiposAusencia = await servicio.TiposAusenciaAsync(cancelacion).ConfigureAwait(false),
            PeriodosVacacionales = IServicioSolicitudes.GenerarPeriodosVacaciones()
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarSolicitudVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = "Solicitud registrada correctamente.";

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

        modelo.Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false);
        modelo.TiposAusencia = await servicio.TiposAusenciaAsync(cancelacion).ConfigureAwait(false);
        modelo.PeriodosVacacionales = IServicioSolicitudes.GenerarPeriodosVacaciones();

        return View("Crear", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(long id, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            var resultado = await servicio.AprobarAsync(id, usuario, cancelacion).ConfigureAwait(false);

            if (!resultado.HuboOmisiones)
            {
                TempData["Mensaje"] =
                    $"Solicitud aprobada: se generaron {resultado.JustificacionesCreadas} justificación(es). " +
                    "Recalcule el periodo para que el tareo las refleje.";
            }
            else
            {
                var partes = new List<string>
                {
                    $"Solicitud aprobada con {resultado.JustificacionesCreadas} justificación(es) generada(s)." +
                    " Recalcule el periodo para que el tareo las refleje."
                };

                if (resultado.DiasEnPeriodoCerrado.Count > 0)
                {
                    partes.Add(
                        $"{resultado.DiasEnPeriodoCerrado.Count} día(s) cayeron en un periodo cerrado y " +
                        "no se justificaron: reabra el periodo si corresponde.");
                }

                if (resultado.DiasSinPeriodo.Count > 0)
                {
                    partes.Add(
                        $"{resultado.DiasSinPeriodo.Count} día(s) no pertenecen a ningún periodo registrado.");
                }

                if (resultado.DiasConJustificacionExistente.Count > 0)
                {
                    partes.Add(
                        $"{resultado.DiasConJustificacionExistente.Count} día(s) ya tenían otra " +
                        "justificación registrada y no se sobrescribieron.");
                }

                TempData["Error"] = string.Join(" ", partes);
            }
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

    [HttpGet]
    public async Task<IActionResult> Rechazar(long id, CancellationToken cancelacion)
    {
        var solicitud = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (solicitud is null)
        {
            return NotFound();
        }

        return View(new DecisionSolicitudVm
        {
            Id = solicitud.Id,
            Trabajador = solicitud.Trabajador,
            TipoAusencia = solicitud.TipoAusencia
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(DecisionSolicitudVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.RechazarAsync(modelo.Id, modelo.Motivo, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = "Solicitud rechazada.";

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

        return View("Rechazar", modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Anular(long id, CancellationToken cancelacion)
    {
        var solicitud = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);

        if (solicitud is null)
        {
            return NotFound();
        }

        return View(new DecisionSolicitudVm
        {
            Id = solicitud.Id,
            Trabajador = solicitud.Trabajador,
            TipoAusencia = solicitud.TipoAusencia
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(DecisionSolicitudVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.AnularAsync(modelo.Id, modelo.Motivo, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] =
                    "Solicitud anulada. Sus justificaciones se retiraron; recalcule el periodo " +
                    "para que el tareo deje de reflejarlas.";

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

        return View("Anular", modelo);
    }
}
