using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
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
/// periodo desde Asistencia, no algo que este controlador pueda resolver. Lo que sí hace
/// ahora es <b>avisarlo</b> — ver <see cref="EstadoCalculoTareo"/>—, porque antes un reporte
/// viejo salía idéntico a uno al día.
///
/// El alcance puede ser un periodo de planilla, un mes calendario, un día suelto o un rango
/// de fechas. El <b>tareo final</b> es la excepción: se emite solo por periodo, porque sus
/// reintegros y su estado de cierre son del periodo.
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
    public async Task<IActionResult> Resumen(
        FiltroReporteVm filtro,
        int pagina,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var resultado = ResultadoPaginado.Vacio<ResumenTareoDto>(ResumenTareoVm.FilasPorPagina);
        var totales = TotalesResumenTareo.Vacio;
        MetadatosReporte? metadatos = null;

        if (filtro.EstaCompleto)
        {
            try
            {
                var filtroReporte = AFiltro(filtro);

                metadatos = await servicio
                    .ObtenerMetadatosAsync(filtroReporte, UsuarioActual, cancelacion)
                    .ConfigureAwait(false);

                var respuesta = await servicio
                    .ResumenTareoPaginadoAsync(
                        filtroReporte, pagina, ResumenTareoVm.FilasPorPagina, cancelacion)
                    .ConfigureAwait(false);

                resultado = respuesta.Filas;
                totales = respuesta.Totales;
            }
            catch (RecursoNoEncontradoException)
            {
                TempData["Error"] = "El periodo elegido ya no existe.";
            }
            catch (ReglaNegocioException excepcion)
            {
                // Alcance incompleto o mal formado, o falta un parámetro legal (RMV,
                // sobretasa nocturna o sueldo básico) para resolver el bono nocturno: se
                // avisa en vez de mostrar un 500 o, peor, un bono en cero sin explicación.
                TempData["Error"] = excepcion.Message;
            }
        }

        var catalogos = await CargarCatalogosAsync(filtro, cancelacion).ConfigureAwait(false);

        return View(new ResumenTareoVm
        {
            Filtro = filtro,
            Metadatos = metadatos,
            Resultado = resultado,
            Totales = totales,
            Periodos = catalogos.Periodos,
            Empresas = catalogos.Empresas,
            Plantas = catalogos.Plantas,
            Centros = catalogos.Centros,
            CentrosCosto = catalogos.CentrosCosto,
            Equipos = catalogos.Equipos
        });
    }

    [HttpGet]
    public async Task<IActionResult> Excepciones(
        FiltroReporteVm filtro,
        int pagina,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var resultado = ResultadoPaginado.Vacio<ExcepcionDto>(ExcepcionesVm.FilasPorPagina);
        MetadatosReporte? metadatos = null;

        if (filtro.EstaCompleto)
        {
            try
            {
                var filtroReporte = AFiltro(filtro);

                metadatos = await servicio
                    .ObtenerMetadatosAsync(filtroReporte, UsuarioActual, cancelacion)
                    .ConfigureAwait(false);

                resultado = await servicio
                    .ExcepcionesPaginadasAsync(
                        filtroReporte, pagina, ExcepcionesVm.FilasPorPagina, cancelacion)
                    .ConfigureAwait(false);
            }
            catch (RecursoNoEncontradoException)
            {
                TempData["Error"] = "El periodo elegido ya no existe.";
            }
            catch (ReglaNegocioException excepcion)
            {
                TempData["Error"] = excepcion.Message;
            }
        }

        var catalogos = await CargarCatalogosAsync(filtro, cancelacion).ConfigureAwait(false);

        return View(new ExcepcionesVm
        {
            Filtro = filtro,
            Metadatos = metadatos,
            Resultado = resultado,
            Periodos = catalogos.Periodos,
            Empresas = catalogos.Empresas,
            Plantas = catalogos.Plantas,
            Centros = catalogos.Centros,
            CentrosCosto = catalogos.CentrosCosto,
            Equipos = catalogos.Equipos
        });
    }

    [HttpGet]
    public Task<IActionResult> ExportarResumen(FiltroReporteVm filtro, CancellationToken cancelacion) =>
        ExportarAsync(
            filtro,
            nameof(Resumen),
            "Resumen-tareo",
            async (filtroReporte, metadatos, ct) => exportador.ExportarResumenTareo(
                metadatos,
                await servicio.ResumenTareoAsync(filtroReporte, ct).ConfigureAwait(false)),
            cancelacion);

    [HttpGet]
    public Task<IActionResult> ExportarExcepciones(FiltroReporteVm filtro, CancellationToken cancelacion) =>
        ExportarAsync(
            filtro,
            nameof(Excepciones),
            "Excepciones",
            async (filtroReporte, metadatos, ct) => exportador.ExportarExcepciones(
                metadatos,
                await servicio.ExcepcionesAsync(filtroReporte, ct).ConfigureAwait(false)),
            cancelacion);

    /// <summary>
    /// El tareo final en el formato de GCA: un libro con las hojas Tareo, Descanso Medico,
    /// Vacaciones-Licencias y Trazabilidad.
    /// </summary>
    [HttpGet]
    public Task<IActionResult> ExportarTareoFinal(FiltroReporteVm filtro, CancellationToken cancelacion) =>
        ExportarAsync(
            filtro,
            nameof(Resumen),
            "Tareo-final",
            async (filtroReporte, metadatos, ct) => exportador.ExportarTareoFinal(
                metadatos,
                await servicio.TareoFinalAsync(filtroReporte, ct).ConfigureAwait(false)),
            cancelacion);

    // ------------------------------------------------------------------

    private string UsuarioActual => User.Identity?.Name ?? "sistema";

    /// <summary>
    /// El camino común de las tres exportaciones: validar, pedir metadatos, armar y bajar.
    /// </summary>
    /// <remarks>
    /// Los metadatos se piden <b>antes</b> que los datos a propósito: es lo que valida el
    /// alcance y resuelve el periodo, así que un filtro mal formado falla con su mensaje sin
    /// haber consultado ni una fila de tareo.
    /// </remarks>
    private async Task<IActionResult> ExportarAsync(
        FiltroReporteVm filtro,
        string accionDeVuelta,
        string nombreBase,
        Func<FiltroReporte, MetadatosReporte, CancellationToken, Task<byte[]>> generar,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (!filtro.EstaCompleto)
        {
            TempData["Error"] = "Complete el alcance del reporte antes de exportar.";

            return RedirectToAction(accionDeVuelta, filtro.ARuta());
        }

        var filtroReporte = AFiltro(filtro);

        try
        {
            var metadatos = await servicio
                .ObtenerMetadatosAsync(filtroReporte, UsuarioActual, cancelacion)
                .ConfigureAwait(false);

            var archivo = await generar(filtroReporte, metadatos, cancelacion).ConfigureAwait(false);

            return Excel(archivo, nombreBase, metadatos);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }
        catch (ReglaNegocioException excepcion)
        {
            // Alcance no admitido (el tareo final exige periodo) o falta un parámetro legal
            // —RMV, % de asignación familiar, movilidad diaria o el sueldo básico de algún
            // trabajador—: no se entrega un archivo con montos en cero sin que nadie se
            // entere, se dice qué cargar antes de reintentar.
            TempData["Error"] = excepcion.Message;

            return RedirectToAction(accionDeVuelta, filtro.ARuta());
        }
    }

    /// <summary>Los desplegables del formulario de filtros, iguales en las dos pantallas.</summary>
    private sealed record CatalogosDeFiltro(
        IReadOnlyList<Application.Catalogos.Dto.PeriodoListaDto> Periodos,
        IReadOnlyList<Application.Comun.OpcionDto> Empresas,
        IReadOnlyList<Application.Comun.OpcionDto> Plantas,
        IReadOnlyList<Application.Comun.OpcionDto> Centros,
        IReadOnlyList<Application.Comun.OpcionDto> CentrosCosto,
        IReadOnlyList<Application.Catalogos.Dto.EquipoOpcionDto> Equipos);

    private async Task<CatalogosDeFiltro> CargarCatalogosAsync(
        FiltroReporteVm filtro,
        CancellationToken cancelacion)
    {
        var empresaDeLaPlanta = filtro.PlantaId is { } plantaId
            ? await catalogos.EmpresaDePlantaAsync(plantaId, cancelacion).ConfigureAwait(false)
            : null;

        var equipos = await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false);

        // Con planta elegida se ofrecen solo los suyos: los seis aparatos de todas las
        // plantas en un desplegable obligan a saberse de memoria cuál es de cuál.
        //
        // ⚠️ El ya elegido se conserva aunque sea de otra planta. Si no, al cambiar de planta
        // el desplegable mostraba «Todos» mientras el EquipoId seguía viajando en la query y
        // filtrando: el reporte salía vacío y la pantalla no decía por qué.
        if (filtro.PlantaId is { } planta)
        {
            equipos = [.. equipos.Where(e => e.PlantaId == planta || e.Id == filtro.EquipoId)];
        }

        return new CatalogosDeFiltro(
            await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false),
            await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false),
            await catalogos.CentrosAsync(empresaDeLaPlanta, cancelacion).ConfigureAwait(false),
            await catalogos.CentrosCostoAsync(filtro.CentroId, cancelacion).ConfigureAwait(false),
            equipos);
    }

    private static FiltroReporte AFiltro(FiltroReporteVm filtro) => new()
    {
        Alcance = filtro.Alcance,
        PeriodoId = filtro.PeriodoId,
        Anio = filtro.Anio,
        Mes = filtro.Mes,
        Desde = filtro.Desde,
        Hasta = filtro.Hasta,
        EmpresaId = filtro.EmpresaId,
        PlantaId = filtro.PlantaId,
        CentroId = filtro.CentroId,
        CentroCostoId = filtro.CentroCostoId,
        EquipoId = filtro.EquipoId
    };

    /// <summary>
    /// Descarga con un nombre que dice qué trae y ordena bien en una carpeta.
    /// </summary>
    /// <remarks>
    /// Fechas en ISO (<c>2026-07-01_a_2026-07-31</c>) y no en <c>dd/MM/yyyy</c>: es el
    /// formato de la interfaz, pero la barra no es válida en un nombre de archivo y, sobre
    /// todo, doce exportaciones de un año se ordenan alfabéticamente en orden cronológico.
    /// La marca de generación va al final para que las reediciones del mismo alcance queden
    /// juntas en vez de dispersas por fecha de descarga.
    /// </remarks>
    private FileContentResult Excel(byte[] contenido, string nombreBase, MetadatosReporte metadatos)
    {
        var generado = metadatos.GeneradoEn.ToString(
            "yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);

        return File(
            contenido,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{nombreBase}_{metadatos.SufijoArchivo}_generado-{generado}.xlsx");
    }
}
