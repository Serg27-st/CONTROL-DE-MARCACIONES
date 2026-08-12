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
/// Captura manual de reintegros y descuentos: el bloque <c>REINTEGROS</c> (AO-BF) del
/// tareo final, que el sistema no calcula.
/// </summary>
/// <remarks>
/// Un formulario por trabajador y periodo, no una grilla masiva: son 18 campos y un caso
/// puntual (corregir un periodo anterior, cargar un viático), no una tarea rutinaria de
/// cientos de filas. Editable solo si el periodo sigue abierto, igual que el resto del
/// sistema — la validación real vive en <see cref="IServicioReintegrosTareo.GuardarAsync"/>,
/// esta pantalla solo lo refleja.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class ReintegrosController(
    IServicioReintegrosTareo servicio,
    IServicioPeriodos periodos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(new SeleccionarReintegroVm
        {
            Trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false),
            Periodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long trabajadorId, long periodoId, CancellationToken cancelacion)
    {
        var periodo = await periodos.ObtenerAsync(periodoId, cancelacion).ConfigureAwait(false);

        if (periodo is null)
        {
            return NotFound();
        }

        var trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false);
        var trabajador = trabajadores.FirstOrDefault(t => t.Id == trabajadorId);

        if (trabajador is null)
        {
            return NotFound();
        }

        var existente = await servicio.ObtenerAsync(trabajadorId, periodoId, cancelacion).ConfigureAwait(false);

        return View(new EditarReintegroVm
        {
            Datos = ARequest(trabajadorId, periodoId, existente),
            Trabajador = trabajador.Nombre,
            Periodo = $"{periodo.Codigo} — {periodo.Nombre}",
            PeriodoAbierto = periodo.EstaAbierto
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarReintegroVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] =
                    "Reintegros guardados. Vuelva a exportar el tareo final para que se reflejen.";

                return RedirectToAction(
                    nameof(Editar),
                    new { trabajadorId = modelo.Datos.TrabajadorId, periodoId = modelo.Datos.PeriodoId });
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (PeriodoCerradoException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        await CompletarDatosDeVistaAsync(modelo, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    // ------------------------------------------------------------------

    private static GuardarReintegroTareoRequest ARequest(
        long trabajadorId, long periodoId, ReintegroTareoDto? existente) =>
        existente is null
            ? new GuardarReintegroTareoRequest { TrabajadorId = trabajadorId, PeriodoId = periodoId }
            : new GuardarReintegroTareoRequest
            {
                TrabajadorId = trabajadorId,
                PeriodoId = periodoId,
                HorasNormales = existente.HorasNormales,
                HorasExtra25 = existente.HorasExtra25,
                HorasExtra35 = existente.HorasExtra35,
                HorasNoLaborable = existente.HorasNoLaborable,
                MontoMovilidad = existente.MontoMovilidad,
                HorasNocturnas = existente.HorasNocturnas,
                HorasExtra25Nocturnas = existente.HorasExtra25Nocturnas,
                HorasExtra35Nocturnas = existente.HorasExtra35Nocturnas,
                HorasNoLaborableNocturnas = existente.HorasNoLaborableNocturnas,
                Viatico = existente.Viatico,
                DiasDescansoMedico = existente.DiasDescansoMedico,
                DiasSubsidioMaternidad = existente.DiasSubsidioMaternidad,
                DiasSubsidioAccidente = existente.DiasSubsidioAccidente,
                DiasLicenciaPaternidad = existente.DiasLicenciaPaternidad,
                LicenciaConGoce = existente.LicenciaConGoce,
                ReintegroAsignacionFamiliar = existente.ReintegroAsignacionFamiliar,
                ReintegroAfecto = existente.ReintegroAfecto,
                DescuentoPagoIndebido = existente.DescuentoPagoIndebido
            };

    private async Task CompletarDatosDeVistaAsync(EditarReintegroVm modelo, CancellationToken cancelacion)
    {
        var periodo = await periodos.ObtenerAsync(modelo.Datos.PeriodoId, cancelacion).ConfigureAwait(false);
        var trabajadores = await servicio.TrabajadoresAsync(cancelacion).ConfigureAwait(false);

        modelo.Trabajador = trabajadores.FirstOrDefault(t => t.Id == modelo.Datos.TrabajadorId)?.Nombre ?? "-";
        modelo.Periodo = periodo is null ? "-" : $"{periodo.Codigo} — {periodo.Nombre}";
        modelo.PeriodoAbierto = periodo?.EstaAbierto ?? false;
    }
}
