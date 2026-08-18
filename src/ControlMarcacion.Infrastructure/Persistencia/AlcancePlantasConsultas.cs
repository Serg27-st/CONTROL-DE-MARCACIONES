using ControlMarcacion.Application.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Atajos de consulta para aplicar el aislamiento por planta sobre entidades que se relacionan
/// con el trabajador solo por <c>TrabajadorId</c> (sin navegación).
/// </summary>
/// <remarks>
/// Solicitudes, Justificaciones, Tareo, Reportes y Reintegros comparten la misma necesidad:
/// «¿este trabajador está entre las plantas que puede ver el usuario?». Vive aquí, en un solo
/// sitio, para que la regla no se reimplemente —y se desvíe— en cada servicio.
/// </remarks>
internal static class AlcancePlantasConsultas
{
    /// <summary>
    /// ¿El usuario actual puede ver a este trabajador, según la planta a la que pertenece?
    /// </summary>
    /// <remarks>
    /// Es el control del acceso por id directo en los servicios que parten de un
    /// <c>TrabajadorId</c> (aprobar una solicitud, tocar una justificación, un reintegro): sin
    /// restricción devuelve <c>true</c>; con restricción, resuelve la planta del trabajador y
    /// la compara. Un trabajador inexistente da <c>false</c> — quien no está, no se ve.
    /// </remarks>
    public static async Task<bool> PuedeVerTrabajadorAsync(
        this IProveedorAlcancePlantas alcance,
        MarcacionDbContext contexto,
        long trabajadorId,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        ArgumentNullException.ThrowIfNull(contexto);

        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantas is null)
        {
            return true;
        }

        var plantaId = await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == trabajadorId)
            .Select(t => (long?)t.PlantaId)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        return plantaId is { } planta && plantas.Contains(planta);
    }
}
