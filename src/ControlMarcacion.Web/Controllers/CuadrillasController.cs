using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Cuadrillas que rotan de turno: su ciclo y quiénes las integran.
/// </summary>
/// <remarks>
/// Reemplaza el trámite de cada quincena —editar ficha por ficha el turno de cada obrero que
/// rota— por programar una vez el ciclo de la cuadrilla. El turno de cada día sale de ahí,
/// sin que nadie tenga que aplicarlo.
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed class CuadrillasController(
    IServicioCuadrillas servicio,
    ServicioTrabajadores personal,
    IConsultaCatalogos catalogos,
    ILogger<CuadrillasController> logger) : Controller
{
    private static readonly Action<ILogger, string, Exception?> RegistrarModelStateInvalido =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(RegistrarModelStateInvalido)),
            "Programación de cuadrilla rechazada por ModelState: {Errores}");

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    [HttpGet]
    public async Task<IActionResult> Index(bool incluirInactivas, CancellationToken cancelacion) =>
        View(new ListaCuadrillasVm
        {
            Cuadrillas = await servicio.ListarAsync(Hoy, incluirInactivas, cancelacion).ConfigureAwait(false),
            IncluirInactivas = incluirInactivas,
            Hoy = Hoy
        });

    [HttpGet]
    public async Task<IActionResult> Editar(long? id, CancellationToken cancelacion)
    {
        var datos = new GuardarCuadrillaRequest { Activo = true };

        if (id is { } identificador)
        {
            var actual = await servicio.ObtenerAsync(identificador, Hoy, cancelacion).ConfigureAwait(false);

            if (actual is null)
            {
                return NotFound();
            }

            datos = new GuardarCuadrillaRequest
            {
                Id = actual.Id,
                Codigo = actual.Codigo,
                Nombre = actual.Nombre,
                PlantaId = actual.PlantaId,
                Activo = actual.Activo
            };
        }

        return View(new EditarCuadrillaVm
        {
            Datos = datos,
            Plantas = await servicio.PlantasAsync(cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(EditarCuadrillaVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (ModelState.IsValid)
        {
            try
            {
                var id = await servicio.GuardarAsync(modelo.Datos, Usuario, cancelacion).ConfigureAwait(false);

                TempData["Mensaje"] = modelo.Datos.Id is null
                    ? "Se registró la cuadrilla. Ahora programe su ciclo y sume a sus integrantes."
                    : "Cambios guardados correctamente.";

                return RedirectToAction(nameof(Detalle), new { id });
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
            await servicio.CambiarEstadoAsync(id, activo, Usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = activo ? "Se reactivó la cuadrilla." : "Se dio de baja la cuadrilla.";
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

    [HttpGet]
    public async Task<IActionResult> Detalle(long id, CancellationToken cancelacion)
    {
        var cuadrilla = await servicio.ObtenerAsync(id, Hoy, cancelacion).ConfigureAwait(false);

        if (cuadrilla is null)
        {
            return NotFound();
        }

        return View(new DetalleCuadrillaVm
        {
            Cuadrilla = cuadrilla,
            Programaciones = await servicio.ProgramacionesAsync(id, cancelacion).ConfigureAwait(false),
            Integrantes = await personal.IntegrantesDeCuadrillaAsync(id, Hoy, cancelacion).ConfigureAwait(false),
            Candidatos = cuadrilla.Activo
                ? await personal.CandidatosParaCuadrillaAsync(id, cuadrilla.PlantaId, Hoy, cancelacion).ConfigureAwait(false)
                : [],
            Hoy = Hoy
        });
    }

    [HttpGet]
    public async Task<IActionResult> Programar(long id, CancellationToken cancelacion)
    {
        var cuadrilla = await servicio.ObtenerAsync(id, Hoy, cancelacion).ConfigureAwait(false);

        if (cuadrilla is null)
        {
            return NotFound();
        }

        // Se propone el próximo lunes —la rotación de planta ocurre tras el descanso del
        // domingo— y el ciclo que ya tenía, para que reprogramar sea cambiar solo lo que cambia.
        var tramos = cuadrilla.ProgramacionVigente?.Tramos
            .Select(t => new TramoProgramadoVm
            {
                Tipo = t.Tipo,
                TurnoId = t.TurnoId,
                Dias = t.Dias
            })
            .ToList() ?? [];

        var modelo = new ProgramarCuadrillaVm
        {
            Cuadrilla = cuadrilla,
            Datos = new ProgramarCuadrillaDatosVm
            {
                CuadrillaId = id,
                Desde = ProximoLunes(Hoy),
                Tramos = Completar(tramos)
            }
        };

        modelo.Turnos = await catalogos.TurnosAsync(cuadrilla.PlantaId, cancelacion).ConfigureAwait(false);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Programar(ProgramarCuadrillaVm modelo, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        if (!ModelState.IsValid)
        {
            RegistrarModelStateInvalido(
                logger,
                string.Join(
                    " | ",
                    ModelState
                        .Where(entrada => entrada.Value?.ValidationState != ModelValidationState.Valid)
                        .Select(entrada =>
                            $"{entrada.Key}: estado={entrada.Value!.ValidationState}, " +
                            $"errores=[{string.Join(", ", entrada.Value.Errors.Select(MensajeModelState))}]")),
                null);
        }

        if (ModelState.IsValid)
        {
            try
            {
                await servicio.ProgramarAsync(
                        MapearSolicitud(modelo.Datos),
                        Usuario,
                        cancelacion)
                    .ConfigureAwait(false);

                TempData["Mensaje"] =
                    $"Ciclo programado desde el {modelo.Datos.Desde:dd/MM/yyyy}. Si esa fecha ya pasó, " +
                    "recalcule los periodos abiertos que la incluyan.";

                return RedirectToAction(nameof(Detalle), new { id = modelo.Datos.CuadrillaId });
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

        var cuadrilla = await servicio.ObtenerAsync(modelo.Datos.CuadrillaId, Hoy, cancelacion).ConfigureAwait(false);

        if (cuadrilla is null)
        {
            return NotFound();
        }

        modelo.Cuadrilla = cuadrilla;
        modelo.Turnos = await catalogos.TurnosAsync(cuadrilla.PlantaId, cancelacion).ConfigureAwait(false);
        modelo.Datos.Tramos = Completar(modelo.Datos.Tramos);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CorrerRotacion(CorrerRotacionRequest solicitud, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        try
        {
            await servicio.CorrerRotacionAsync(solicitud, Usuario, cancelacion).ConfigureAwait(false);

            var sentido = solicitud.Dias < 0 ? "adelantó" : "atrasó";

            TempData["Mensaje"] =
                $"Se {sentido} la rotación {Math.Abs(solicitud.Dias)} día(s) desde el " +
                $"{solicitud.Desde:dd/MM/yyyy}. Recalcule los periodos abiertos que incluyan esa fecha.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Detalle), new { id = solicitud.CuadrillaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SumarIntegrantes(AsignarCuadrillaRequest solicitud, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        try
        {
            var sumados = await personal.AsignarCuadrillaAsync(solicitud, Usuario, cancelacion).ConfigureAwait(false);

            TempData["Mensaje"] = sumados == 0
                ? "No hubo cambios: esas personas ya integraban la cuadrilla desde esa fecha."
                : $"Se sumaron {sumados} persona(s) a la cuadrilla desde el {solicitud.Desde:dd/MM/yyyy}. " +
                  "Si esa fecha ya pasó, recalcule los periodos abiertos que la incluyan.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Detalle), new { id = solicitud.CuadrillaId });
    }

    // ------------------------------------------------------------------

    private string Usuario => User.Identity?.Name ?? "sistema";

    private static DateOnly ProximoLunes(DateOnly fecha) =>
        fecha.AddDays(((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7);

    /// <summary>Rellena con filas vacías hasta las que ofrece el formulario.</summary>
    private static List<TramoProgramadoVm> Completar(List<TramoProgramadoVm> tramos)
    {
        while (tramos.Count < ProgramarCuadrillaVm.FilasDeTramo)
        {
            tramos.Add(new TramoProgramadoVm());
        }

        return tramos;
    }

    private static ProgramarCuadrillaRequest MapearSolicitud(ProgramarCuadrillaDatosVm datos) =>
        new()
        {
            CuadrillaId = datos.CuadrillaId,
            Desde = datos.Desde,
            Tramos =
            [
                .. datos.Tramos.Select(tramo => new TramoProgramadoRequest
                {
                    Tipo = tramo.Tipo,
                    TurnoId = tramo.TurnoId,
                    Dias = tramo.Dias
                })
            ]
        };

    private static string MensajeModelState(ModelError error) =>
        !string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? error.ErrorMessage
            : error.Exception?.GetBaseException().Message ?? "Error de validación sin detalle.";
}
