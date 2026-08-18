using System.Security.Claims;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Web.Seguridad;

/// <summary>
/// Resuelve las plantas visibles desde el rol y las asignaciones de la sesión.
/// </summary>
/// <remarks>
/// Solo el <c>ADMINISTRADOR</c> queda acotado a las plantas que gestiona (<c>UsuarioPlanta</c>).
/// COORDINADOR, GERENCIA y TRABAJADOR_SOCIAL no tienen restricción; sin sesión tampoco, que es
/// el arranque y no expone datos a nadie.
///
/// El resultado se calcula una sola vez por petición (el proveedor es Scoped): Personal,
/// Solicitudes, Justificaciones, Tareo y Reportes lo consultan varias veces en la misma
/// petición y no tiene sentido volver a la base cada vez.
/// </remarks>
public sealed class ProveedorAlcancePlantasHttp(
    IHttpContextAccessor acceso,
    MarcacionDbContext contexto) : IProveedorAlcancePlantas
{
    private IReadOnlyCollection<long>? _plantas;
    private bool _calculado;

    public async Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default)
    {
        if (_calculado)
        {
            return _plantas;
        }

        _plantas = await CalcularAsync(cancelacion).ConfigureAwait(false);
        _calculado = true;

        return _plantas;
    }

    private async Task<IReadOnlyCollection<long>?> CalcularAsync(CancellationToken cancelacion)
    {
        var usuario = acceso.HttpContext?.User;

        // Sin sesión (arranque, siembra) o sin ser un ADMINISTRADOR: sin restricción.
        // COORDINADOR gana siempre —aunque por diseño nadie tiene dos roles, que mande el más
        // amplio evita un cierre accidental si algún día alguien acumula roles—.
        if (usuario?.Identity?.IsAuthenticated != true
            || !usuario.IsInRole(RolesSistema.Administrador)
            || usuario.IsInRole(RolesSistema.Coordinador))
        {
            return null;
        }

        var id = usuario.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(id))
        {
            // Autenticado como ADMINISTRADOR pero sin id legible en la cookie: fail-closed,
            // no ve nada, en vez de caer en la rama «sin restricción» y verlo todo.
            return [];
        }

        return await contexto.UsuariosPlanta
            .AsNoTracking()
            .Where(up => up.UsuarioId == id)
            .Select(up => up.PlantaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }
}
