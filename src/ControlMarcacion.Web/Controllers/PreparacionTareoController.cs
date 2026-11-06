using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>Diagnóstico de solo lectura previo al cálculo de tareo.</summary>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class PreparacionTareoController(
    IValidadorPreparacionTareo validador,
    IServicioPeriodos periodos,
    IConsultaCatalogos catalogos,
    IProveedorAlcancePlantas alcance) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long? periodoId,
        long? plantaId,
        EstadoPreparacionTareo? estado,
        OperacionPreparacionTareo operacion = OperacionPreparacionTareo.ConsultaHistorica,
        CancellationToken cancelacion = default)
    {
        var listaPeriodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion)
            .ConfigureAwait(false);
        var seleccionado = periodoId ?? (listaPeriodos.Count > 0 ? listaPeriodos[0].Id : null);
        var periodo = listaPeriodos.FirstOrDefault(p => p.Id == seleccionado);
        IReadOnlyList<PreparacionTrabajadorTareo> resultados = [];

        if (periodo is not null)
        {
            resultados = await validador.EvaluarAutorizadosAsync(
                new FiltroPreparacionTareo
                {
                    PeriodoId = periodo.Id,
                    Desde = periodo.FechaInicio,
                    Hasta = periodo.FechaFin,
                    PlantaId = plantaId,
                    Estado = estado,
                    Operacion = operacion
                }, cancelacion).ConfigureAwait(false);

            if (plantaId is { } unidad)
            {
                resultados = resultados
                    .Where(r => r.Dias.Any(d => d.PlantaId == unidad))
                    .ToList();
            }
        }

        var plantas = await catalogos.PlantasAsync(null, cancelacion).ConfigureAwait(false);
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        if (plantasVisibles is not null)
        {
            plantas = plantas.Where(p => plantasVisibles.Contains(p.Id)).ToList();
        }

        return View(new PreparacionTareoVm
        {
            PeriodoId = seleccionado,
            PlantaId = plantaId,
            Estado = estado,
            Operacion = operacion,
            Periodos = listaPeriodos,
            Plantas = plantas,
            Resultados = resultados
        });
    }
}
