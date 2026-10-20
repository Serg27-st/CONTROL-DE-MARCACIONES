using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Bandeja de incidencias: los días que esperan una decisión humana.
/// </summary>
/// <remarks>
/// <para>
/// Es la pantalla que faltaba. El sistema ya distinguía un día incompleto de una falta, pero
/// no había dónde verlos juntos ni cómo resolverlos: había que salir a
/// <c>/Justificaciones/Crear</c> y volver a teclear trabajador y fecha, y el único arreglo
/// disponible era declarar el día ausencia justificada, que en un día trabajado es falso.
/// </para>
/// <para>
/// <b>Quién hace qué.</b> El <c>ADMINISTRADOR</c> registra la regularización, que queda
/// <i>solicitada</i> y no modifica el tareo hasta que se autorice; el <c>COORDINADOR</c>
/// aprueba o rechaza. Cuando la registra el propio coordinador queda aprobada en un paso:
/// exigirle que se apruebe a sí mismo no agrega control, y sí convertiría el cierre mensual
/// en una fila de trámites suyos. Es la separación de funciones aplicada donde sirve —quien
/// opera el tareo no se autoriza sus propias correcciones— sin volverla ceremonia.
/// </para>
/// <para>
/// ⚠️ El reparto real son los <c>[Authorize]</c> de abajo y las validaciones de
/// <c>Application</c>. Lo que la vista muestre u oculte es ayuda, no control.
/// </para>
/// </remarks>
[Authorize(Roles = $"{RolesSistema.Coordinador},{RolesSistema.Administrador}")]
public sealed partial class IncidenciasController(
    IServicioCalculoTareo tareo,
    IServicioJustificaciones justificaciones,
    IServicioPeriodos periodos,
    ILogger<IncidenciasController> registro) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        long? periodoId,
        long? trabajadorId,
        TipoIncidencia? tipo,
        bool? soloPendientes,
        int pagina,
        CancellationToken cancelacion)
    {
        var listaPeriodos = await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false);

        var filtro = new FiltroIncidencias
        {
            PeriodoId = periodoId,
            TrabajadorId = trabajadorId,
            Tipo = tipo,
            SoloPendientes = soloPendientes ?? true,
            Pagina = pagina
        };

        return View(new BandejaIncidenciasVm
        {
            PeriodoId = periodoId,
            TrabajadorId = trabajadorId,
            Tipo = tipo,
            SoloPendientes = filtro.SoloPendientes,
            PuedeResolver = User.IsInRole(RolesSistema.Coordinador),
            Periodos = [.. listaPeriodos.Select(p => new OpcionDto(p.Id, $"{p.Codigo} — {p.Nombre}"))],
            Resultado = await tareo.ListarIncidenciasAsync(filtro, cancelacion).ConfigureAwait(false)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regularizar(
        RegistrarRegularizacionRequest datos,
        long? periodoId,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var usuario = User.Identity?.Name ?? "sistema";

        // El coordinador registra y autoriza en un paso; el administrador deja la
        // regularización pedida, y hasta que se apruebe el tareo no cambia.
        var apruebaDirectamente = User.IsInRole(RolesSistema.Coordinador);

        try
        {
            var id = await justificaciones
                .RegularizarAsync(datos, usuario, apruebaDirectamente, cancelacion)
                .ConfigureAwait(false);

            RegularizacionRegistrada(
                registro, id, datos.TrabajadorId, datos.Fecha,
                apruebaDirectamente ? "Aprobada" : "Solicitada");

            TempData["Mensaje"] = apruebaDirectamente
                ? "Regularización registrada y aprobada. Recalcule el periodo para que se refleje en el tareo."
                : "Regularización registrada. Queda pendiente de que un coordinador la apruebe.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (PeriodoCerradoException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { periodoId });
    }

    /// <summary>
    /// Aprueba o rechaza una regularización pendiente. Solo <c>COORDINADOR</c>.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RolesSistema.Coordinador)]
    public async Task<IActionResult> Resolver(
        long id,
        bool aprueba,
        string? motivo,
        long? periodoId,
        CancellationToken cancelacion)
    {
        var usuario = User.Identity?.Name ?? "sistema";

        try
        {
            await justificaciones.ResolverAsync(id, aprueba, motivo, usuario, cancelacion).ConfigureAwait(false);

            RegularizacionResuelta(registro, id, aprueba ? "aprobada" : "rechazada", usuario);

            TempData["Mensaje"] = aprueba
                ? "Regularización aprobada. Recalcule el periodo para que se refleje en el tareo."
                : "Regularización rechazada. El día vuelve a quedar disponible para pedir otra.";
        }
        catch (ReglaNegocioException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (PeriodoCerradoException excepcion)
        {
            TempData["Error"] = excepcion.Message;
        }
        catch (RecursoNoEncontradoException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { periodoId });
    }

    // ------------------------------------------------------------------
    // Registro de eventos.
    //
    // ⚠️ Ley 29733: ni documento, ni dirección, ni teléfono, ni correo, ni diagnóstico.
    // Va el identificador del trabajador, que basta para reconstruir el caso cruzando con
    // la auditoría, y nada que sea dato personal por sí mismo.
    //
    // Se generan con [LoggerMessage] en vez de LogInformation directo: el analizador lo
    // exige (CA1848/CA1873) y además evita armar el mensaje cuando el nivel está apagado.
    // ------------------------------------------------------------------

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "Regularización {Id} registrada para el trabajador {TrabajadorId} del {Fecha}, estado {Estado}.")]
    private static partial void RegularizacionRegistrada(
        ILogger registro, long id, long trabajadorId, DateOnly fecha, string estado);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "Regularización {Id} {Decision} por {Usuario}.")]
    private static partial void RegularizacionResuelta(
        ILogger registro, long id, string decision, string usuario);
}
