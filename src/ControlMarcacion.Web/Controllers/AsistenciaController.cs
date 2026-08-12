using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Tareo diario: ejecución del motor de cálculo y consulta del resultado.
/// </summary>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class AsistenciaController(
    IServicioCalculoTareo servicio,
    IServicioPeriodos periodos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long? periodoId,
        EstadoTareo? estado,
        CancellationToken cancelacion)
    {
        var lista = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false);

        // Sin periodo elegido se toma el más reciente: es lo que el usuario quiere ver
        // el 99 % de las veces, y evita una pantalla vacía en el primer ingreso.
        // La lista viene ordenada por fecha de inicio descendente.
        var seleccionado = periodoId ?? (lista.Count > 0 ? lista[0].Id : null);

        var modelo = new TareoVm
        {
            Periodos = lista,
            PeriodoId = seleccionado,
            Estado = estado,
            Tareos = seleccionado is null
                ? []
                : await servicio
                    .ListarAsync(new FiltroTareo { PeriodoId = seleccionado, Estado = estado }, cancelacion)
                    .ConfigureAwait(false),
            Liquidacion = seleccionado is null
                ? null
                : await servicio.LiquidarPeriodoAsync(seleccionado.Value, cancelacion).ConfigureAwait(false)
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recalcular(long periodoId, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            var resultado = await servicio
                .RecalcularPeriodoAsync(periodoId, usuario, cancelacion)
                .ConfigureAwait(false);

            TempData["Mensaje"] =
                $"Periodo {resultado.PeriodoCodigo} recalculado: {resultado.DiasCalculados} día(s) " +
                $"de {resultado.TrabajadoresProcesados} trabajador(es). " +
                $"{resultado.DiasCreados} creado(s), {resultado.DiasActualizados} actualizado(s).";
        }
        catch (PeriodoCerradoException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { periodoId });
    }
}
