using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Catálogo de equipos marcadores: los aparatos ZKTeco físicos.
/// </summary>
/// <remarks>
/// Es el catálogo <b>raíz</b> de la ingesta y por eso tiene pantalla propia: sin un equipo
/// dado de alta no se pueden registrar marcadores ni cargar archivos.
///
/// Existe porque el código del ZKTeco dejó de ser una identidad global. En Planta
/// Detergentes conviven un aparato para apellidos A–L y otro para M–Z, ambos numerados del
/// 1 al 100: sin modelar el aparato, el usuario 14 de uno y el 14 del otro eran la misma
/// persona para el sistema y sus jornadas se fundían en una sola, sin que la fila
/// resultante pareciera incorrecta.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class EquiposController(IServicioEquipos servicio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroEquipos filtro, CancellationToken cancelacion) =>
        View(new ListaEquiposVm
        {
            Filtro = filtro,
            Equipos = await servicio.ListarAsync(filtro, cancelacion).ConfigureAwait(false),
            Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false)
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarEquipoRequest { Activo = true };
        var marcadores = 0;
        var importaciones = 0;

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarEquipoRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                Serie = actual.Serie,
                PlantaId = actual.PlantaId,
                Descripcion = actual.Descripcion,
                Activo = actual.Activo
            };

            marcadores = actual.MarcadoresTotales;
            importaciones = actual.Importaciones;
        }

        return View(new EditarEquipoVm
        {
            Datos = datos,
            MarcadoresTotales = marcadores,
            Importaciones = importaciones,
            Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarEquipoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio.GuardarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró el equipo correctamente. Ya puede darle de alta sus códigos en Marcadores."
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

        modelo.Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false);

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

            TempData["Mensaje"] = activo ? "Se activó el equipo." : "Se dio de baja el equipo.";
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
