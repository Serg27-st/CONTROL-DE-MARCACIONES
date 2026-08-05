using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Carga de marcaciones desde el archivo del marcador.
/// </summary>
/// <remarks>
/// Las marcaciones crudas son inmutables: esta pantalla solo inserta. No hay edición ni
/// borrado, y no debe agregarse: toda corrección va por Justificaciones.
/// </remarks>
public sealed class MarcacionesController(IServicioImportacion servicio) : Controller
{
    /// <summary>Tamaño máximo aceptado. El archivo real de un mes pesa unos 100 KB.</summary>
    private const long TamanioMaximoBytes = 20 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(new CargaMarcacionesVm
        {
            Historial = await servicio.HistorialAsync(cancelacion: cancelacion).ConfigureAwait(false)
        });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(TamanioMaximoBytes)]
    public async Task<IActionResult> Cargar(
        IFormFile? archivo,
        bool confirmarRecarga,
        CancellationToken cancelacion)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo del marcador antes de cargar.";
            return RedirectToAction(nameof(Index));
        }

        if (archivo.Length > TamanioMaximoBytes)
        {
            TempData["Error"] =
                $"El archivo pesa {archivo.Length / 1024d / 1024d:0.0} MB y el máximo es " +
                $"{TamanioMaximoBytes / 1024 / 1024} MB.";
            return RedirectToAction(nameof(Index));
        }

        // Se copia a memoria porque hay que recorrerlo dos veces: una para el hash y otra
        // para leerlo. El archivo de un mes pesa unos 100 KB, así que no es un problema.
        using var contenido = new MemoryStream();
        await archivo.CopyToAsync(contenido, cancelacion).ConfigureAwait(false);

        if (!confirmarRecarga)
        {
            var previa = await servicio
                .BuscarImportacionPreviaAsync(contenido, cancelacion)
                .ConfigureAwait(false);

            if (previa is not null)
            {
                return View("Index", new CargaMarcacionesVm
                {
                    Historial = await servicio.HistorialAsync(cancelacion: cancelacion).ConfigureAwait(false),
                    Previa = previa,
                    ArchivoPendiente = archivo.FileName
                });
            }
        }

        var usuario = User.Identity?.Name ?? "sistema";

        var resultado = await servicio
            .ImportarAsync(contenido, archivo.FileName, usuario, cancelacion)
            .ConfigureAwait(false);

        TempData["Mensaje"] =
            $"Archivo procesado: {resultado.FilasInsertadas} marcación(es) nueva(s) de " +
            $"{resultado.FilasLeidas} línea(s) leída(s).";

        return RedirectToAction(nameof(Resultado), new { id = resultado.ImportacionId });
    }

    [HttpGet]
    public async Task<IActionResult> Resultado(long id, CancellationToken cancelacion)
    {
        var resultado = await servicio.ObtenerResultadoAsync(id, cancelacion).ConfigureAwait(false);

        return resultado is null
            ? NotFound()
            : View(new ResultadoImportacionVm { Resultado = resultado });
    }
}
