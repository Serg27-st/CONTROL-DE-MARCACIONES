using System.Diagnostics;
using ControlMarcacion.Application.Inicio;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

public sealed class InicioController(IServicioPanelInicio panel) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var gestionaTareo = User.IsInRole(RolesSistema.Coordinador)
                            || User.IsInRole(RolesSistema.Administrador);

        var veSolicitudes = gestionaTareo || User.IsInRole(RolesSistema.TrabajadorSocial);

        var veReportes = User.IsInRole(RolesSistema.Coordinador)
                         || User.IsInRole(RolesSistema.Gerencia);

        // Sin ninguna tarjeta que mostrar no se consulta nada: la portada de un rol sin
        // acceso al tareo ni a solicitudes es una guía de dos líneas, y hacerle pagar las
        // consultas del panel para no enseñar ninguna sería trabajo tirado.
        var vePanel = gestionaTareo || veSolicitudes || veReportes;

        return View(new PanelInicioVm
        {
            GestionaTareo = gestionaTareo,
            VeSolicitudes = veSolicitudes,
            VeReportes = veReportes,
            Panel = vePanel
                ? await panel
                    .ObtenerAsync(User.Identity?.Name ?? "sistema", cancelacion)
                    .ConfigureAwait(false)
                : PanelInicioDto.Vacio
        });
    }

    /// <summary>
    /// Página de error a la que deriva <c>UseExceptionHandler</c> fuera de desarrollo.
    /// </summary>
    /// <remarks>
    /// <b>Anónima a propósito</b>, y es la única excepción al <c>[Authorize]</c> global
    /// fuera de <c>CuentaController</c>. El manejador de excepciones reejecuta el pipeline
    /// sobre esta ruta conservando al usuario de la petición que falló; si esa petición era
    /// anónima —el caso más probable, porque la pantalla de login es la que se sirve sin
    /// sesión y la que falla si la base no responde— el filtro de autorización la desviaba
    /// al login y la persona nunca llegaba a ver el error ni el identificador con el que
    /// reportarlo. Un error se cuenta, no se esconde detrás de un inicio de sesión que
    /// probablemente tampoco funcione.
    ///
    /// No expone nada: la vista solo muestra el identificador de la solicitud, nunca el
    /// mensaje de la excepción ni su traza.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorVm { IdSolicitud = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
