using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Infrastructure.Persistencia.Auditoria;

/// <summary>
/// Respaldo cuando no hay una petición con sesión detrás del cambio.
/// </summary>
/// <remarks>
/// Lo usan el arranque (siembra de catálogos, creación del primer coordinador) y las
/// herramientas de línea de comandos de EF. La capa Web registra el suyo, que lee la
/// identidad de la petición, y sustituye a este.
///
/// Existe para que la auditoría nunca sea el motivo de que algo no arranque: sin este
/// respaldo, olvidar el registro en la capa que hospeda tumbaría toda escritura a la base.
/// </remarks>
internal sealed class ProveedorUsuarioSistema : IProveedorUsuarioActual
{
    public string Usuario => "sistema";
}
