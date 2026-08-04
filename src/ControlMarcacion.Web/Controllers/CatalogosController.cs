using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de catálogos.
/// </summary>
/// <remarks>
/// Un solo controlador atiende todos los catálogos simples, despachando por clave. Turnos,
/// periodos y tipos de ausencia quedan fuera porque tienen reglas propias y necesitan
/// pantallas dedicadas.
/// </remarks>
public sealed class CatalogosController(IServicioCatalogos servicio) : Controller
{
    /// <summary>Portada con todos los catálogos disponibles.</summary>
    [HttpGet]
    public IActionResult Index() => View(CatalogosDisponibles.Todos);

    [HttpGet]
    public async Task<IActionResult> Lista(
        string clave,
        long? padreId,
        bool incluirInactivos,
        CancellationToken cancelacion)
    {
        var definicion = CatalogosDisponibles.Buscar(clave);

        if (definicion is null)
        {
            return NotFound();
        }

        var modelo = new ListaCatalogoVm
        {
            Definicion = definicion,
            PadreId = padreId,
            IncluirInactivos = incluirInactivos,
            Elementos = await servicio
                .ListarAsync(definicion.Clave, padreId, incluirInactivos, cancelacion)
                .ConfigureAwait(false),
            OpcionesPadre = await servicio
                .OpcionesPadreAsync(definicion.Clave, cancelacion)
                .ConfigureAwait(false)
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(string clave, long? id, CancellationToken cancelacion)
    {
        var definicion = CatalogosDisponibles.Buscar(clave);

        if (definicion is null)
        {
            return NotFound();
        }

        var datos = new GuardarCatalogoRequest();

        if (id is { } identificador)
        {
            var actual = await servicio
                .ObtenerAsync(definicion.Clave, identificador, cancelacion)
                .ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarCatalogoRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                PadreId = actual.PadreId,
                Activo = actual.Activo
            };
        }

        var modelo = new EditarCatalogoVm
        {
            Clave = definicion.Clave,
            Definicion = definicion,
            Datos = datos,
            OpcionesPadre = await servicio
                .OpcionesPadreAsync(definicion.Clave, cancelacion)
                .ConfigureAwait(false)
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarCatalogoVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        var definicion = CatalogosDisponibles.Buscar(modelo.Clave);

        if (definicion is null)
        {
            return NotFound();
        }

        modelo.Definicion = definicion;

        if (ModelState.IsValid)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "sistema";

                await servicio
                    .GuardarAsync(definicion.Clave, modelo.Datos, usuario, cancelacion)
                    .ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? $"Se registró {definicion.ConArticulo} correctamente."
                    : "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Lista), new { clave = definicion.Clave });
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

        modelo.OpcionesPadre = await servicio
            .OpcionesPadreAsync(definicion.Clave, cancelacion)
            .ConfigureAwait(false);

        return View("Editar", modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        string clave,
        long id,
        bool activo,
        CancellationToken cancelacion)
    {
        var definicion = CatalogosDisponibles.Buscar(clave);

        if (definicion is null)
        {
            return NotFound();
        }

        try
        {
            var usuario = User.Identity?.Name ?? "sistema";

            await servicio
                .CambiarEstadoAsync(definicion.Clave, id, activo, usuario, cancelacion)
                .ConfigureAwait(false);

            TempData["Mensaje"] = activo
                ? $"Se activó {definicion.ConArticulo}."
                : $"Se desactivó {definicion.ConArticulo}.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Lista), new { clave = definicion.Clave });
    }
}
