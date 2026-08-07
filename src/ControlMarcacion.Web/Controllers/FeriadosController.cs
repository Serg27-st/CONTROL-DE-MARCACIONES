using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Feriados: sincronización manual contra un proveedor externo, no mantenimiento.
/// </summary>
/// <remarks>
/// No hay alta/edición/baja manual aquí a propósito: el catálogo <c>Feriado</c> ya admite
/// carga manual de feriados locales desde otras rutas del sistema. Esta pantalla es
/// exclusivamente el botón "traer los feriados nacionales del año que falten" — restringido
/// a Coordinador porque toca datos que alimentan el cálculo de sobretasas de toda la planta.
/// </remarks>
[Authorize(Roles = RolesSistema.Coordinador)]
public sealed class FeriadosController(IServicioFeriados servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? anio, CancellationToken cancelacion)
    {
        var anioConsultado = anio ?? DateTime.Now.Year;

        return View(new ListaFeriadosVm
        {
            Anio = anioConsultado,
            Feriados = await servicio.ListarAsync(anioConsultado, cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sincronizar(int anio, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";
            var resultado = await servicio.SincronizarAsync(anio, usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] =
                $"Sincronización de {resultado.Anio}: {resultado.Agregados} feriado(s) nuevo(s) agregados, " +
                $"{resultado.YaExistian} ya estaban cargados. El cálculo del tareo los toma en cuenta " +
                "recién en el próximo recálculo del periodo.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }

        return RedirectToAction(nameof(Index), new { anio });
    }
}
