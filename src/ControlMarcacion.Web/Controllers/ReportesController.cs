using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Reportes sobre el tareo ya calculado.
/// </summary>
/// <remarks>
/// Solo lee y exporta: si el tareo está desactualizado, la corrección es recalcular el
/// periodo desde Asistencia, no algo que este controlador pueda resolver.
///
/// ⚠️ El formato de columnas es el que el sistema puede sustentar hoy con el tareo
/// calculado. GCA no ha entregado el archivo modelo del tareo final; cuando llegue, estas
/// pantallas y sus exportaciones se ajustan a esa plantilla.
/// </remarks>
public sealed class ReportesController(
    IServicioReportes servicio,
    IExportadorReportesExcel exportador,
    IServicioPeriodos periodos,
    IConsultaCatalogos catalogos) : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Resumen(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        var modelo = new ResumenTareoVm
        {
            Filtro = filtro,
            Periodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false),
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Areas = await catalogos.AreasAsync(filtro.PlantaId, cancelacion).ConfigureAwait(false),
            Secciones = await catalogos.SeccionesAsync(filtro.AreaId, cancelacion).ConfigureAwait(false),
            Filas = filtro.PeriodoId is { } periodoId
                ? await servicio.ResumenTareoAsync(AFiltro(filtro, periodoId), cancelacion).ConfigureAwait(false)
                : []
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarResumen(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        if (filtro.PeriodoId is not { } periodoId)
        {
            return BadRequest("Seleccione un periodo antes de exportar.");
        }

        var usuario = User.Identity?.Name ?? "sistema";
        var filtroReporte = AFiltro(filtro, periodoId);

        try
        {
            var metadatos = await servicio.ObtenerMetadatosAsync(filtroReporte, usuario, cancelacion).ConfigureAwait(false);
            var filas = await servicio.ResumenTareoAsync(filtroReporte, cancelacion).ConfigureAwait(false);
            var archivo = exportador.ExportarResumenTareo(metadatos, filas);

            return Excel(archivo, $"Resumen_tareo_{metadatos.PeriodoCodigo}");
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Excepciones(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        var modelo = new ExcepcionesVm
        {
            Filtro = filtro,
            Periodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false),
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Areas = await catalogos.AreasAsync(filtro.PlantaId, cancelacion).ConfigureAwait(false),
            Secciones = await catalogos.SeccionesAsync(filtro.AreaId, cancelacion).ConfigureAwait(false),
            Filas = filtro.PeriodoId is { } periodoId
                ? await servicio.ExcepcionesAsync(AFiltro(filtro, periodoId), cancelacion).ConfigureAwait(false)
                : []
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarExcepciones(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        if (filtro.PeriodoId is not { } periodoId)
        {
            return BadRequest("Seleccione un periodo antes de exportar.");
        }

        var usuario = User.Identity?.Name ?? "sistema";
        var filtroReporte = AFiltro(filtro, periodoId);

        try
        {
            var metadatos = await servicio.ObtenerMetadatosAsync(filtroReporte, usuario, cancelacion).ConfigureAwait(false);
            var filas = await servicio.ExcepcionesAsync(filtroReporte, cancelacion).ConfigureAwait(false);
            var archivo = exportador.ExportarExcepciones(metadatos, filas);

            return Excel(archivo, $"Excepciones_{metadatos.PeriodoCodigo}");
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// El tareo final en el formato de GCA: un libro con las hojas Tareo, Descanso Medico y
    /// Vacaciones-Licencias.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportarTareoFinal(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        if (filtro.PeriodoId is not { } periodoId)
        {
            return BadRequest("Seleccione un periodo antes de exportar.");
        }

        var usuario = User.Identity?.Name ?? "sistema";
        var filtroReporte = AFiltro(filtro, periodoId);

        try
        {
            var metadatos = await servicio.ObtenerMetadatosAsync(filtroReporte, usuario, cancelacion).ConfigureAwait(false);
            var tareo = await servicio.TareoFinalAsync(filtroReporte, cancelacion).ConfigureAwait(false);
            var archivo = exportador.ExportarTareoFinal(metadatos, tareo);

            return Excel(archivo, $"Tareo_{metadatos.PeriodoCodigo}");
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
    }

    // ------------------------------------------------------------------

    private static FiltroReporte AFiltro(FiltroReporteVm filtro, long periodoId) => new()
    {
        PeriodoId = periodoId,
        EmpresaId = filtro.EmpresaId,
        PlantaId = filtro.PlantaId,
        AreaId = filtro.AreaId,
        SeccionId = filtro.SeccionId
    };

    private FileContentResult Excel(byte[] contenido, string nombreBase)
    {
        var marcaDeTiempo = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        return File(
            contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{nombreBase}_{marcaDeTiempo}.xlsx");
    }
}
