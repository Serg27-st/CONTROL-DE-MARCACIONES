using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Catálogo de feriados: sincronización con la fuente oficial y carga manual.
/// </summary>
/// <remarks>
/// Dos caminos con propósitos distintos. <b>Sincronizar</b> trae los feriados nacionales
/// del año y agrega los que falten, para no teclear doce fechas cada enero. El <b>alta
/// manual</b> cubre lo que ninguna fuente pública sabe: el aniversario de una planta, un
/// feriado regional, o un nacional decretado que el proveedor todavía no publicó.
///
/// El alta manual existe <b>desde aquí y solo desde aquí</b>. Antes, este mismo comentario
/// afirmaba que el catálogo se cargaba "desde otras rutas del sistema" y era falso: no
/// había ninguna, así que un feriado local de planta no se podía registrar. Sin registrar,
/// el motor trata ese día como laborable — falta para quien no fue, y sin el 100 % para
/// quien sí.
///
/// Todo restringido a Coordinador: un feriado cambia la sobretasa de una planta entera.
/// </remarks>
[Authorize(Roles = RolesSistema.Coordinador)]
public sealed class FeriadosController(
    IServicioFeriados servicio,
    IConsultaCatalogos catalogos) : Controller
{
    /// <summary>Recordatorio común a toda escritura: el feriado no reescribe tareos ya calculados.</summary>
    private const string AvisoRecalculo =
        "El cálculo del tareo lo toma en cuenta recién en el próximo recálculo del periodo.";

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

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, int? anio, CancellationToken cancelacion)
    {
        var anioActual = anio ?? DateTime.Now.Year;
        var datos = new GuardarFeriadoRequest();

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarFeriadoRequest
            {
                Id = actual.Id,
                Fecha = actual.Fecha,
                Descripcion = actual.Descripcion,
                PlantaId = actual.PlantaId,
                Activo = actual.Activo
            };

            anioActual = actual.Fecha.Year;
        }

        return View(new EditarFeriadoVm
        {
            Datos = datos,
            Anio = anioActual,
            Plantas = await catalogos.PlantasAsync(null, cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarFeriadoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var esNuevo = modelo.Datos.Id is null;

                await servicio
                    .GuardarAsync(modelo.Datos, User.Identity?.Name ?? "sistema", cancelacion)
                    .ConfigureAwait(false);

                TempData["Mensaje"] = esNuevo
                    ? $"Feriado registrado. {AvisoRecalculo}"
                    : $"Feriado actualizado. {AvisoRecalculo}";

                return RedirectToAction(
                    nameof(Index),
                    new { anio = modelo.Datos.Fecha?.Year ?? modelo.Anio });
            }
            catch (ReglaNegocioException excepcion)
            {
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        modelo.Plantas = await catalogos.PlantasAsync(null, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        long id,
        bool activo,
        int anio,
        CancellationToken cancelacion)
    {
        try
        {
            await servicio
                .CambiarEstadoAsync(id, activo, User.Identity?.Name ?? "sistema", cancelacion)
                .ConfigureAwait(false);

            TempData["Mensaje"] = activo
                ? $"Feriado reactivado. {AvisoRecalculo}"
                : $"Feriado desactivado: deja de contar como feriado. {AvisoRecalculo}";
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { anio });
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
                $"{resultado.YaExistian} ya estaban cargados. {AvisoRecalculo}";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }

        return RedirectToAction(nameof(Index), new { anio });
    }
}
