using System.Diagnostics;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

public sealed class InicioController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

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
