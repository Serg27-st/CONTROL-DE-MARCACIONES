using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Web.Seguridad;

/// <summary>
/// Resuelve el usuario de la auditoría desde la identidad de la petición.
/// </summary>
/// <remarks>
/// Vive en Web porque es la única capa que conoce <c>HttpContext</c>. Devuelve "sistema"
/// cuando no hay petición o no hay sesión: es el caso del arranque, donde se siembran
/// catálogos y se crea el primer coordinador.
/// </remarks>
public sealed class ProveedorUsuarioActualHttp(IHttpContextAccessor acceso) : IProveedorUsuarioActual
{
    public string Usuario
    {
        get
        {
            var nombre = acceso.HttpContext?.User.Identity?.Name;

            return string.IsNullOrWhiteSpace(nombre) ? "sistema" : nombre;
        }
    }
}
