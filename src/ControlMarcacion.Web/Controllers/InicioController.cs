using System.Diagnostics;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

public sealed class InicioController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorVm { IdSolicitud = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
