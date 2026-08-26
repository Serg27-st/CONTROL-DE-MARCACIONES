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
    /// <summary>Trabajadores por página del listado del tareo.</summary>
    /// <remarks>
    /// Un periodo mensual son decenas de miles de filas (trabajadores × días), así que la
    /// pantalla tiene que paginar: con 200 trabajadores son 6 000 filas de un HTML que el
    /// equipo de planta no termina de pintar.
    ///
    /// La unidad es el <b>trabajador</b> y no la fila: diez personas de un mes son unas
    /// 310 filas, un tamaño de página razonable, y sobre todo ninguna aparece con el mes
    /// partido entre dos páginas. Ver <c>FiltroTareo.TamanioPagina</c>.
    /// </remarks>
    private const int TrabajadoresPorPagina = 10;

    [HttpGet]
    public async Task<IActionResult> Index(
        long? periodoId,
        EstadoTareo? estado,
        string? busqueda,
        int pagina,
        CancellationToken cancelacion)
    {
        var lista = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false);

        // Sin periodo elegido se toma el más reciente: es lo que el usuario quiere ver
        // el 99 % de las veces, y evita una pantalla vacía en el primer ingreso.
        // La lista viene ordenada por fecha de inicio descendente.
        var seleccionado = periodoId ?? (lista.Count > 0 ? lista[0].Id : null);

        if (seleccionado is null)
        {
            return View(new TareoVm { Periodos = lista, Estado = estado, Busqueda = busqueda });
        }

        var filtro = new FiltroTareo
        {
            PeriodoId = seleccionado,
            Estado = estado,
            Busqueda = busqueda,
            Pagina = Math.Max(pagina, 1),
            TamanioPagina = TrabajadoresPorPagina
        };

        var modelo = new TareoVm
        {
            Periodos = lista,
            PeriodoId = seleccionado,
            Estado = estado,
            Busqueda = busqueda,
            Resultado = await servicio.ListarPorTrabajadorAsync(filtro, cancelacion).ConfigureAwait(false),
            Resumen = await servicio.ResumirAsync(filtro, cancelacion).ConfigureAwait(false),
            Liquidacion = await servicio
                .LiquidarPeriodoAsync(seleccionado.Value, cancelacion)
                .ConfigureAwait(false)
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
