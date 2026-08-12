using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de tipos de ausencia.
/// </summary>
/// <remarks>
/// Va en un controlador propio y no en el mantenimiento genérico de catálogos porque un
/// tipo de ausencia no es código más nombre: el comportamiento decide cómo lo trata el
/// motor de cálculo, y crearlo sin elegirlo produciría tareos incorrectos en silencio.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class TiposAusenciaController(IServicioTiposAusencia servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(bool incluirInactivos, CancellationToken cancelacion) =>
        View(new ListaTiposAusenciaVm
        {
            IncluirInactivos = incluirInactivos,
            Tipos = await servicio.ListarAsync(incluirInactivos, cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarTipoAusenciaRequest();

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarTipoAusenciaRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                Comportamiento = actual.Comportamiento,
                ComputaComoAsistencia = actual.ComputaComoAsistencia,
                RequiereSustento = actual.RequiereSustento,
                EsPorHoras = actual.EsPorHoras,
                Activo = actual.Activo
            };
        }

        return View(new EditarTipoAusenciaVm { Datos = datos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarTipoAusenciaVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró el tipo de ausencia correctamente."
                    : "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Index));
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

        return View("Editar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(long id, bool activo, CancellationToken cancelacion)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            await servicio.CambiarEstadoAsync(id, activo, usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = activo
                ? "Se activó el tipo de ausencia."
                : "Se desactivó el tipo de ausencia.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
