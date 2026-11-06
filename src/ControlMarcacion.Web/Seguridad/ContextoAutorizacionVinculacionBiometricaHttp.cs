using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Seguridad;

namespace ControlMarcacion.Web.Seguridad;

/// <summary>Autorización backend fail-closed para la operación sensible de vinculación.</summary>
public sealed class ContextoAutorizacionVinculacionBiometricaHttp(IHttpContextAccessor acceso)
    : IContextoAutorizacionVinculacionBiometrica
{
    private System.Security.Claims.ClaimsPrincipal? Principal => acceso.HttpContext?.User;
    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated == true;
    public bool EsCoordinador => EstaAutenticado && Principal!.IsInRole(RolesSistema.Coordinador);
    public string Usuario => EstaAutenticado && !string.IsNullOrWhiteSpace(Principal!.Identity!.Name)
        ? Principal.Identity.Name
        : "sistema";
}
