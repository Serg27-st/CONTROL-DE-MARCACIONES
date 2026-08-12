using ControlMarcacion.Application.Auditoria;
using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlMarcacion.Web.Controllers;

/// <summary>
/// Bitácora de auditoría: qué cambió en cada tabla, quién y cuándo.
/// </summary>
/// <remarks>
/// <b>Solo COORDINADOR.</b> La bitácora deja ver quién tocó qué, incluidos los cambios
/// que hizo un administrador sobre el padrón o sobre un tareo. Que la audite el mismo rol
/// que ejecuta la operación del día a día quitaría buena parte de su valor.
///
/// Solo lectura, sin excepción: no hay acción de edición ni de borrado en este
/// controlador, y no debe agregarse ninguna.
/// </remarks>
[Authorize(Roles = RolesSistema.Coordinador)]
public sealed class AuditoriaController(IConsultaAuditoria consulta) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroAuditoria filtro, CancellationToken cancelacion) =>
        View(new ListaAuditoriaVm
        {
            Filtro = filtro,
            Resultado = await consulta.ListarAsync(filtro, cancelacion).ConfigureAwait(false),
            Tablas = await consulta.TablasAsync(cancelacion).ConfigureAwait(false)
        });
}
