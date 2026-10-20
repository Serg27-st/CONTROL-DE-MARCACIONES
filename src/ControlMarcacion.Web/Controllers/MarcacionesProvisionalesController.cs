using System.Globalization;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class MarcacionesProvisionalesController(
    IServicioImportacionProvisional importacion,
    IServicioConsultaMarcacionProvisional consulta,
    IExportadorMarcacionesProvisionales exportador,
    IConsultaCatalogos catalogos) : Controller
{
    private const long TamanioMaximoBytes = 20 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = new MarcacionesProvisionalesVm
        {
            Equipos = await catalogos.EquiposActivosAsync(ct).ConfigureAwait(false),
            Historial = await importacion.ListarImportacionesAsync().ConfigureAwait(false)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(TamanioMaximoBytes)]
    public async Task<IActionResult> Importar(long equipoId, IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            TempData["Error"] = "Seleccione un archivo.";
            return RedirectToAction(nameof(Index));
        }

        if (equipoId <= 0)
        {
            TempData["Error"] = "Seleccione el equipo marcador.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var flujo = archivo.OpenReadStream();
            var resultado = await importacion.ImportarAsync(
                flujo,
                archivo.FileName,
                equipoId,
                User.Identity?.Name ?? "sistema").ConfigureAwait(false);

            TempData["Mensaje"] =
                $"Carga provisional completada: {resultado.FilasInsertadas} marcaciones insertadas" +
                (resultado.FilasRechazadas > 0 ? $", {resultado.FilasRechazadas} rechazadas" : "") + ".";
        }
        catch (ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Consulta(
        long equipoId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken ct)
    {
        if (equipoId <= 0)
        {
            TempData["Error"] = "Seleccione el equipo marcador.";
            return RedirectToAction(nameof(Index));
        }

        if (fechaInicio == default || fechaFin == default)
        {
            TempData["Error"] = "Debe indicar las fechas de inicio y fin.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var resultado = await consulta.ConsultarAsync(equipoId, fechaInicio, fechaFin, ct)
                .ConfigureAwait(false);

            var vm = new MarcacionesProvisionalesVm
            {
                Equipos = await catalogos.EquiposActivosAsync(ct).ConfigureAwait(false),
                Historial = await importacion.ListarImportacionesAsync().ConfigureAwait(false),
                EquipoId = equipoId,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                Resultado = resultado
            };
            return View("Index", vm);
        }
        catch (ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Exportar(
        long equipoId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken ct)
    {
        if (equipoId <= 0)
        {
            TempData["Error"] = "Seleccione el equipo marcador.";
            return RedirectToAction(nameof(Index));
        }

        if (fechaInicio == default || fechaFin == default)
        {
            TempData["Error"] = "Debe indicar las fechas de inicio y fin.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var resultado = await consulta.ConsultarAsync(equipoId, fechaInicio, fechaFin, ct)
                .ConfigureAwait(false);

            if (resultado.RangoExcedeMaximoExportacion)
            {
                TempData["Error"] = "El rango excede el máximo permitido para exportación (40 días).";
                return RedirectToAction(nameof(Index));
            }

            var equipos = await catalogos.EquiposActivosAsync(ct).ConfigureAwait(false);
            var equipo = equipos.FirstOrDefault(e => e.Id == equipoId);
            var equipoDesc = equipo is not null ? $"{equipo.Codigo} - {equipo.Nombre}" : equipoId.ToString(CultureInfo.InvariantCulture);

            var advertencias = new List<string>();
            if (resultado.AdvertenciaRangoExcesivo)
            {
                advertencias.Add("El rango consultado supera los 30 días recomendados.");
            }

            var contenido = exportador.Exportar(
                resultado.Jornadas,
                equipoDesc,
                fechaInicio,
                fechaFin,
                User.Identity?.Name,
                advertencias.Count > 0 ? advertencias : null);

            var codigoEquipo = equipo?.Codigo ?? "EQ";
            var nombreArchivo = $"Marcaciones_{codigoEquipo}_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.xlsx";

            return File(
                contenido,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo);
        }
        catch (ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
