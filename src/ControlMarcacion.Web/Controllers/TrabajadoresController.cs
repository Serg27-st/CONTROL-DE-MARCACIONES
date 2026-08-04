using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Mantenimiento de personal.
/// </summary>
/// <remarks>
/// El controlador valida, delega en el servicio de aplicación y arma el modelo de la
/// vista. Sin lógica de negocio y sin acceso al DbContext.
/// </remarks>
public sealed class TrabajadoresController(
    ServicioTrabajadores servicio,
    IConsultaCatalogos catalogos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroTrabajadores filtro, CancellationToken cancelacion)
    {
        var resultado = await servicio.ListarAsync(filtro, cancelacion).ConfigureAwait(false);

        var modelo = new ListadoTrabajadoresVm
        {
            Filtro = filtro,
            Resultado = resultado,
            Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false),
            Plantas = await catalogos.PlantasAsync(filtro.EmpresaId, cancelacion).ConfigureAwait(false)
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken cancelacion)
    {
        var modelo = new EditarTrabajadorVm
        {
            Datos = new GuardarTrabajadorRequest
            {
                FechaIngreso = DateOnly.FromDateTime(DateTime.Now)
            }
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(long id, CancellationToken cancelacion)
    {
        TrabajadorDetalleDto detalle;

        try
        {
            detalle = await servicio.ObtenerAsync(id, cancelacion).ConfigureAwait(false);
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        var modelo = new EditarTrabajadorVm
        {
            Datos = new GuardarTrabajadorRequest
            {
                Id = detalle.Id,
                TipoDocumentoId = detalle.TipoDocumentoId,
                NumeroDocumento = detalle.NumeroDocumento,
                ApellidoPaterno = detalle.ApellidoPaterno,
                ApellidoMaterno = detalle.ApellidoMaterno,
                Nombres = detalle.Nombres,
                Sexo = detalle.Sexo,
                Telefono = detalle.Telefono,
                Direccion = detalle.Direccion,
                CorreoElectronico = detalle.CorreoElectronico,
                TieneHijos = detalle.TieneHijos,
                EmpresaId = detalle.EmpresaId,
                Unidad = detalle.Unidad,
                TipoPlanilla = detalle.TipoPlanilla,
                PlantaId = detalle.PlantaId,
                AreaId = detalle.AreaId,
                SeccionId = detalle.SeccionId,
                FechaIngreso = detalle.FechaIngreso,
                FechaCese = detalle.FechaCese,
                PuestoId = detalle.PuestoId,
                IdMarcador = detalle.IdMarcador,
                TurnoId = detalle.TurnoId
            }
        };

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarTrabajadorVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                // El usuario autenticado se resolverá con Identity en la fase 2.
                var usuario = User.Identity?.Name ?? "sistema";

                if (modelo.Datos.Id is null)
                {
                    var id = await servicio.CrearAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);
                    TempData["Mensaje"] = "Trabajador registrado correctamente.";

                    return RedirectToAction(nameof(Editar), new { id });
                }

                await servicio.ActualizarAsync(modelo.Datos, usuario, cancelacion).ConfigureAwait(false);
                TempData["Mensaje"] = "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Editar), new { id = modelo.Datos.Id });
            }
            catch (ReglaNegocioException excepcion)
            {
                // Los mensajes de regla de negocio están redactados para el usuario final,
                // así que se muestran tal cual en el formulario.
                ModelState.AddModelError(string.Empty, excepcion.Message);
            }
            catch (RecursoNoEncontradoException)
            {
                return NotFound();
            }
        }

        await CargarCatalogosAsync(modelo, cancelacion).ConfigureAwait(false);

        return View("Editar", modelo);
    }

    /// <summary>Plantas de una empresa, para encadenar los desplegables del formulario.</summary>
    [HttpGet]
    public async Task<IActionResult> PlantasDeEmpresa(long empresaId, CancellationToken cancelacion) =>
        Json(await catalogos.PlantasAsync(Opcional(empresaId), cancelacion).ConfigureAwait(false));

    /// <summary>Áreas de una planta.</summary>
    [HttpGet]
    public async Task<IActionResult> AreasDePlanta(long plantaId, CancellationToken cancelacion) =>
        Json(await catalogos.AreasAsync(Opcional(plantaId), cancelacion).ConfigureAwait(false));

    /// <summary>Secciones de un área.</summary>
    [HttpGet]
    public async Task<IActionResult> SeccionesDeArea(long areaId, CancellationToken cancelacion) =>
        Json(await catalogos.SeccionesAsync(Opcional(areaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Turnos habilitados en una planta. Depende de la planta porque el turno especial
    /// de sábado solo existe en Planta 01.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TurnosDePlanta(long plantaId, CancellationToken cancelacion) =>
        Json(await catalogos.TurnosAsync(Opcional(plantaId), cancelacion).ConfigureAwait(false));

    /// <summary>
    /// Convierte el 0 de un desplegable sin elegir en <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Los catálogos interpretan <c>null</c> como "sin filtro" y cualquier otro valor como
    /// un identificador real. Pasar el 0 tal cual filtraba por un id inexistente y dejaba
    /// las listas vacías en el alta de un trabajador nuevo.
    /// </remarks>
    private static long? Opcional(long id) => id == 0 ? null : id;

    private async Task CargarCatalogosAsync(EditarTrabajadorVm modelo, CancellationToken cancelacion)
    {
        modelo.TiposDocumento = await catalogos.TiposDocumentoAsync(cancelacion).ConfigureAwait(false);
        modelo.Empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        modelo.Plantas = await catalogos.PlantasAsync(Opcional(modelo.Datos.EmpresaId), cancelacion).ConfigureAwait(false);
        modelo.Areas = await catalogos.AreasAsync(Opcional(modelo.Datos.PlantaId), cancelacion).ConfigureAwait(false);
        modelo.Secciones = await catalogos.SeccionesAsync(Opcional(modelo.Datos.AreaId), cancelacion).ConfigureAwait(false);
        modelo.Puestos = await catalogos.PuestosAsync(cancelacion).ConfigureAwait(false);
        modelo.Turnos = await catalogos.TurnosAsync(Opcional(modelo.Datos.PlantaId), cancelacion).ConfigureAwait(false);
    }
}
