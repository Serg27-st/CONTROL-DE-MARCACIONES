using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Application.Programacion;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ControlMarcacion.Web.Controllers;

[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class ProgramacionMasivaController(
    IServicioProgramacionMasiva servicio,
    IServicioTurnos turnos,
    IConsultaTrabajadores consultaTrabajadores,
    IProveedorAlcancePlantas alcance,
    IMemoryCache cache) : Controller
{
    private const int TamanioPagina = 50;
    private static readonly TimeSpan VigenciaSeleccion = TimeSpan.FromMinutes(30);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var modelo = new ProgramacionMasivaAlcanceVm
        {
            Plantas = await ObtenerPlantasAsync(cancelacion).ConfigureAwait(false),
            Turnos = await ObtenerTurnosAsync(cancelacion).ConfigureAwait(false)
        };
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seleccion(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva,
        int pagina,
        CancellationToken cancelacion)
    {
        if (plantaId <= 0 || turnoId <= 0)
        {
            TempData["Error"] = "Seleccione una planta y un turno.";
            return RedirectToAction(nameof(Index));
        }

        if (!await alcance.PuedeVerPlantaAsync(plantaId, cancelacion).ConfigureAwait(false))
        {
            return Forbid();
        }

        if (pagina < 1)
        {
            pagina = 1;
        }

        var filtro = new FiltroTrabajadores
        {
            PlantaId = plantaId,
            SoloActivos = true,
            Pagina = pagina,
            TamanioPagina = TamanioPagina
        };

        var resultado = await consultaTrabajadores.ListarAsync(filtro, cancelacion).ConfigureAwait(false);

        var turnoInfo = await turnos.ObtenerAsync(turnoId, cancelacion).ConfigureAwait(false);
        var plantasDisponibles = await ObtenerPlantasAsync(cancelacion).ConfigureAwait(false);
        var plantaNombre = plantasDisponibles.FirstOrDefault(p => p.Id == plantaId)?.Nombre ?? "";

        var clave = ClaveSeleccion();
        if (!cache.TryGetValue<HashSet<long>>(clave, out var seleccionados) || seleccionados is null)
        {
            seleccionados = [];
            cache.Set(clave, seleccionados, VigenciaSeleccion);
        }

        var modelo = new ProgramacionMasivaSeleccionVm
        {
            PlantaId = plantaId,
            TurnoId = turnoId,
            FechaEfectiva = fechaEfectiva,
            PlantaNombre = plantaNombre,
            TurnoNombre = turnoInfo?.Nombre ?? "",
            Trabajadores = resultado.Elementos.Select(t => new TrabajadorSeleccionDto(
                t.Id,
                t.NumeroDocumento,
                t.ApellidosYNombres,
                t.TurnoVigente,
                null)).ToList(),
            Seleccionados = seleccionados,
            TotalTrabajadores = resultado.TotalElementos,
            Pagina = pagina,
            TamanioPagina = TamanioPagina
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActualizarSeleccion(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva,
        int pagina,
        long[]? agregar,
        long[]? quitar,
        bool seleccionarTodos)
    {
        var clave = ClaveSeleccion();
        if (!cache.TryGetValue<HashSet<long>>(clave, out var seleccionados) || seleccionados is null)
        {
            seleccionados = [];
        }

        if (quitar is { Length: > 0 })
        {
            foreach (var id in quitar)
            {
                seleccionados.Remove(id);
            }
        }

        if (agregar is { Length: > 0 })
        {
            foreach (var id in agregar)
            {
                seleccionados.Add(id);
            }
        }

        cache.Set(clave, seleccionados, VigenciaSeleccion);

        return RedirectToAction(nameof(Seleccion), new
        {
            plantaId,
            turnoId,
            fechaEfectiva,
            pagina
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeleccionarTodos(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva,
        CancellationToken cancelacion)
    {
        var filtro = new FiltroTrabajadores
        {
            PlantaId = plantaId,
            SoloActivos = true,
            Pagina = 1,
            TamanioPagina = 10000
        };

        var resultado = await consultaTrabajadores.ListarAsync(filtro, cancelacion).ConfigureAwait(false);
        var todos = resultado.Elementos.Select(t => t.Id).ToHashSet();

        var clave = ClaveSeleccion();
        cache.Set(clave, todos, VigenciaSeleccion);

        return RedirectToAction(nameof(Seleccion), new
        {
            plantaId,
            turnoId,
            fechaEfectiva,
            pagina = 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LimpiarSeleccion(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva)
    {
        var clave = ClaveSeleccion();
        cache.Remove(clave);

        return RedirectToAction(nameof(Seleccion), new
        {
            plantaId,
            turnoId,
            fechaEfectiva,
            pagina = 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Previsualizar(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva,
        CancellationToken cancelacion)
    {
        var clave = ClaveSeleccion();
        if (!cache.TryGetValue<HashSet<long>>(clave, out var seleccionados)
            || seleccionados is null
            || seleccionados.Count == 0)
        {
            TempData["Error"] = "No hay trabajadores seleccionados.";
            return RedirectToAction(nameof(Seleccion), new { plantaId, turnoId, fechaEfectiva, pagina = 1 });
        }

        try
        {
            var solicitud = new SolicitudProgramacionMasiva(
                plantaId, turnoId, fechaEfectiva, seleccionados.ToList());

            var previsualizacion = await servicio.PrevisualizarAsync(solicitud, cancelacion)
                .ConfigureAwait(false);

            var plantasDisponibles = await ObtenerPlantasAsync(cancelacion).ConfigureAwait(false);
            var turnoInfo = await turnos.ObtenerAsync(turnoId, cancelacion).ConfigureAwait(false);

            return View(new ProgramacionMasivaPrevisualizarVm
            {
                Previsualizacion = previsualizacion,
                PlantaId = plantaId,
                TurnoId = turnoId,
                FechaEfectiva = fechaEfectiva,
                PlantaNombre = plantasDisponibles.FirstOrDefault(p => p.Id == plantaId)?.Nombre ?? "",
                TurnoNombre = turnoInfo?.Nombre ?? "",
                TotalSeleccionados = seleccionados.Count
            });
        }
        catch (ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Seleccion), new { plantaId, turnoId, fechaEfectiva, pagina = 1 });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(
        long plantaId,
        long turnoId,
        DateOnly fechaEfectiva,
        string hash,
        CancellationToken cancelacion)
    {
        var clave = ClaveSeleccion();
        if (!cache.TryGetValue<HashSet<long>>(clave, out var seleccionados)
            || seleccionados is null
            || seleccionados.Count == 0)
        {
            TempData["Error"] = "La selección venció. Vuelva a seleccionar los trabajadores.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var solicitud = new SolicitudProgramacionMasiva(
                plantaId, turnoId, fechaEfectiva, seleccionados.ToList());
            var usuario = User.Identity?.Name ?? "sistema";

            var resultado = await servicio.ConfirmarAsync(solicitud, hash, usuario, cancelacion)
                .ConfigureAwait(false);

            cache.Remove(clave);

            var plantasDisponibles = await ObtenerPlantasAsync(cancelacion).ConfigureAwait(false);
            var turnoInfo = await turnos.ObtenerAsync(turnoId, cancelacion).ConfigureAwait(false);

            return View("Resultado", new ProgramacionMasivaResultadoVm
            {
                Resultado = resultado,
                PlantaNombre = plantasDisponibles.FirstOrDefault(p => p.Id == plantaId)?.Nombre ?? "",
                TurnoNombre = turnoInfo?.Nombre ?? "",
                FechaEfectiva = fechaEfectiva
            });
        }
        catch (ReglaNegocioException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Seleccion), new { plantaId, turnoId, fechaEfectiva, pagina = 1 });
        }
    }

    private string ClaveSeleccion() =>
        $"prog-masiva:{User.Identity?.Name ?? "anon"}";

    private async Task<IReadOnlyList<OpcionDto>> ObtenerPlantasAsync(CancellationToken cancelacion)
    {
        return await turnos.PlantasAsync(cancelacion).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<OpcionDto>> ObtenerTurnosAsync(CancellationToken cancelacion)
    {
        var lista = await turnos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false);
        return lista.Select(t => new OpcionDto(t.Id, t.Nombre)).ToList();
    }
}
