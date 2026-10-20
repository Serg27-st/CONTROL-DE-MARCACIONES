using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Web.Operaciones;

/// <summary>
/// Ejecuta la preparación con efectos de escritura que precede al servicio HTTP normal.
/// </summary>
internal interface IPreparadorAplicacion
{
    Task PrepararAsync(CancellationToken cancelacion);
}

internal sealed class PreparadorAplicacion(
    MarcacionDbContext contexto,
    IServiceProvider servicios,
    IConfiguration configuracion) : IPreparadorAplicacion
{
    public async Task PrepararAsync(CancellationToken cancelacion)
    {
        await contexto.Database.MigrateAsync(cancelacion).ConfigureAwait(false);

        var correo = configuracion["Seguridad:AdminCorreo"];
        var clave = configuracion["Seguridad:AdminClave"];

        // Compatibilidad operativa para despliegues automatizados: si ambas credenciales
        // externas existen se conserva el bootstrap anterior. En una instalación local
        // normal, la ausencia de ambas ya no es fatal: /estado anuncia SetupRequired y el
        // usuario completa /setup sin que el secreto pase por Inno Setup ni por archivos.
        if (!string.IsNullOrWhiteSpace(correo) || !string.IsNullOrWhiteSpace(clave))
        {
            await InicializadorSeguridad
                .InicializarAsync(servicios, configuracion)
                .ConfigureAwait(false);
        }
    }
}
