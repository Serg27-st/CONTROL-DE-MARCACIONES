using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Catálogo de marcadores: los identificadores que emite cada equipo ZKTeco, con la
/// planta donde está instalado.
/// </summary>
/// <remarks>
/// Tiene pantalla propia y no entra en el mantenimiento genérico de catálogos por lo
/// mismo que los turnos: no es código más nombre. Pertenece a una planta, su código es
/// único en todo el sistema y no puede retirarse mientras alguien lo tenga asignado.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class MarcadoresController(IServicioMarcadores servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroMarcadores filtro, CancellationToken cancelacion) =>
        View(new ListaMarcadoresVm
        {
            Filtro = filtro,
            Marcadores = await servicio.ListarAsync(filtro, cancelacion).ConfigureAwait(false),
            Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarMarcadorRequest { Activo = true };
        var asignaciones = 0;

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarMarcadorRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                EquipoId = actual.EquipoId,
                Descripcion = actual.Descripcion,
                Activo = actual.Activo
            };

            asignaciones = actual.AsignacionesTotales;
        }

        return View(new EditarMarcadorVm
        {
            Datos = datos,
            AsignacionesTotales = asignaciones,
            Equipos = await servicio.EquiposAsync(cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarMarcadorVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró el marcador correctamente."
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

        modelo.Equipos = await servicio.EquiposAsync(cancelacion).ConfigureAwait(false);

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

            TempData["Mensaje"] = activo ? "Se activó el marcador." : "Se dio de baja el marcador.";
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
