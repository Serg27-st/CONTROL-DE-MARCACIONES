using System.Globalization;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ControlMarcacion.Web.Controllers;

[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class ProgramacionLaboralController(
    IImportadorProgramacionLaboral importador,
    IProveedorAlcancePlantas alcance,
    IMemoryCache cache) : Controller
{
    private const long MaximoBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(20);

    [HttpGet]
    public IActionResult Index() => View(new ImportarProgramacionVm());

    [HttpGet]
    public IActionResult Plantilla() => File(
        importador.GenerarPlantillaExcel(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "plantilla-programacion-laboral.xlsx");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaximoBytes)]
    public async Task<IActionResult> Previsualizar(IFormFile? archivo, CancellationToken cancelacion)
    {
        if (archivo is null || archivo.Length == 0 || archivo.Length > MaximoBytes)
        {
            TempData["Error"] = archivo is null || archivo.Length == 0
                ? "Seleccione un archivo Excel antes de continuar."
                : "El archivo supera el máximo de 5 MB.";
            return RedirectToAction(nameof(Index));
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancelacion).ConfigureAwait(false);
        try
        {
            var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
            var resultado = await importador.PrevisualizarAsync(
                memoria, archivo.FileName, plantas, cancelacion).ConfigureAwait(false);
            string? token = null;
            if (resultado.PuedeConfirmar)
            {
                token = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                cache.Set(Clave(token), new Pendiente(
                    memoria.ToArray(), archivo.FileName, User.Identity?.Name ?? "sistema", resultado.Huella), Vigencia);
            }
            return View("Index", new ImportarProgramacionVm
            {
                Previsualizacion = resultado,
                TokenConfirmacion = token
            });
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(string? token, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(token)
            || !cache.TryGetValue<Pendiente>(Clave(token), out var pendiente)
            || pendiente is null)
        {
            TempData["Error"] = "La previsualización venció o ya fue utilizada. Analice nuevamente el Excel.";
            return RedirectToAction(nameof(Index));
        }
        cache.Remove(Clave(token));
        var usuario = User.Identity?.Name ?? "sistema";
        if (!string.Equals(usuario, pendiente.Usuario, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        try
        {
            using var contenido = new MemoryStream(pendiente.Contenido, writable: false);
            var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
            var resultado = await importador.ConfirmarAsync(
                contenido, pendiente.NombreArchivo, pendiente.Huella, usuario, plantas, cancelacion)
                .ConfigureAwait(false);
            TempData["Mensaje"] = "La programación laboral fue confirmada y se reevaluó la preparación para tareo.";
            return View("Index", new ImportarProgramacionVm { Confirmacion = resultado });
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancelar(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            cache.Remove(Clave(token));
        }
        TempData["Mensaje"] = "La previsualización fue cancelada. No se guardó ningún cambio.";
        return RedirectToAction(nameof(Index));
    }

    private static string Clave(string token) => $"programacion-laboral:{token}";
    private sealed record Pendiente(byte[] Contenido, string NombreArchivo, string Usuario, string Huella);
}
