using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
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
///
/// Restringido a COORDINADOR y GERENCIA. La exportación saca datos personales de todo el
/// padrón en un archivo que ya sale del sistema: no es una pantalla de consulta más.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Gerencia}")]
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
        IReadOnlyList<ResumenTareoDto> filas = [];

        if (filtro.PeriodoId is { } periodoId)
        {
            try
            {
                filas = await servicio.ResumenTareoAsync(AFiltro(filtro, periodoId), cancelacion)
                    .ConfigureAwait(false);
            }
            catch (ReglaNegocioException excepcion)
            {
                // Falta un parámetro legal (RMV, sobretasa nocturna o sueldo básico) para
                // resolver el bono nocturno: se avisa en vez de mostrar un 500 o, peor, un
                // bono en cero sin explicación.
                TempData["Error"] = excepcion.Message;
            }
        }

        var empresaDeLaPlanta = filtro.PlantaId is { } plantaIdResumen
            ? await catalogos.EmpresaDePlantaAsync(plantaIdResumen, cancelacion).ConfigureAwait(false)
            : null;

        var modelo = new ResumenTareoVm
        {
            Filtro = filtro,
            Periodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false),
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Centros = await catalogos.CentrosAsync(empresaDeLaPlanta, cancelacion).ConfigureAwait(false),
            CentrosCosto = await catalogos.CentrosCostoAsync(filtro.CentroId, cancelacion).ConfigureAwait(false),
            Filas = filas
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
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;

            return RedirectToAction(nameof(Resumen), RutaFiltro(filtro));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Excepciones(FiltroReporteVm filtro, CancellationToken cancelacion)
    {
        var empresaDeLaPlanta = filtro.PlantaId is { } plantaIdExcepciones
            ? await catalogos.EmpresaDePlantaAsync(plantaIdExcepciones, cancelacion).ConfigureAwait(false)
            : null;

        var modelo = new ExcepcionesVm
        {
            Filtro = filtro,
            Periodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false),
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            Centros = await catalogos.CentrosAsync(empresaDeLaPlanta, cancelacion).ConfigureAwait(false),
            CentrosCosto = await catalogos.CentrosCostoAsync(filtro.CentroId, cancelacion).ConfigureAwait(false),
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
        catch (ReglaNegocioException excepcion)
        {
            // Falta un parámetro legal (RMV, % asignación familiar, movilidad diaria o
            // sueldo básico de algún trabajador): no se genera un tareo con montos en cero
            // sin que nadie se entere, se avisa qué cargar antes de reintentar.
            TempData["Error"] = excepcion.Message;

            return RedirectToAction(nameof(Resumen), RutaFiltro(filtro));
        }
    }

    // ------------------------------------------------------------------

    private static FiltroReporte AFiltro(FiltroReporteVm filtro, long periodoId) => new()
    {
        PeriodoId = periodoId,
        EmpresaId = filtro.EmpresaId,
        PlantaId = filtro.PlantaId,
        CentroId = filtro.CentroId,
        CentroCostoId = filtro.CentroCostoId
    };

    private static object RutaFiltro(FiltroReporteVm filtro) => new
    {
        filtro.PeriodoId,
        filtro.EmpresaId,
        filtro.PlantaId,
        filtro.CentroId,
        filtro.CentroCostoId
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
