using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de turnos.
/// </summary>
public sealed class TurnosController(IServicioTurnos servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool incluirInactivos, CancellationToken cancelacion) =>
        View(new ListaTurnosVm
        {
            IncluirInactivos = incluirInactivos,
            Turnos = await servicio.ListarAsync(incluirInactivos, cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarTurnoRequest
        {
            HoraInicio = new TimeOnly(7, 0),
            HoraFin = new TimeOnly(19, 0),
            MinutosRefrigerio = 60,
            MinutosTolerancia = 5,
            MinutosAnticipacionEntrada = 15,
            MinutosAnticipacionSalida = 5,
            MinutosJornadaOrdinaria = 480,
            DiasAplicables = DiasSemana.LunesASabado
        };

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarTurnoRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                HoraInicio = actual.HoraInicio,
                HoraFin = actual.HoraFin,
                MinutosRefrigerio = actual.MinutosRefrigerio,
                MinutosTolerancia = actual.MinutosTolerancia,
                MinutosAnticipacionEntrada = actual.MinutosAnticipacionEntrada,
                MinutosAnticipacionSalida = actual.MinutosAnticipacionSalida,
                MinutosJornadaOrdinaria = actual.MinutosJornadaOrdinaria,
                DiasAplicables = actual.DiasAplicables,
                Activo = actual.Activo,
                PlantasHabilitadas = actual.PlantasHabilitadasIds
            };
        }

        return View(new EditarTurnoVm
        {
            Datos = datos,
            Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarTurnoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró el turno correctamente."
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

        modelo.Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id, bool activo, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            await servicio.CambiarEstadoAsync(id, activo, usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = activo ? "Se activó el turno." : "Se desactivó el turno.";
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
}
