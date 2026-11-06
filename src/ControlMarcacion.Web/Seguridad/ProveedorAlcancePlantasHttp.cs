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
/// <c>ADMINISTRADOR</c> queda siempre acotado a las plantas que gestiona
/// (<c>UsuarioPlanta</c>). Un <c>COORDINADOR</c> con asignaciones también queda acotado a ellas;
/// solo el coordinador sin asignaciones conserva el alcance global que corresponde al rol
/// superior. GERENCIA y TRABAJADOR_SOCIAL no usan este alcance operativo; sin sesión tampoco,
/// que es el arranque y no expone datos a nadie.
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

        // Sin sesión: procesos internos de arranque/siembra, sin una petición que exponga datos.
        if (usuario?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var esAdministrador = usuario.IsInRole(RolesSistema.Administrador);
        var esCoordinador = usuario.IsInRole(RolesSistema.Coordinador);

        // Estos roles no operan la preparación ni administran datos por planta.
        if (!esAdministrador && !esCoordinador)
        {
            return null;
        }

        var id = usuario.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(id))
        {
            // Autenticado con un rol operativo pero sin id legible en la cookie: fail-closed,
            // no ve nada, en vez de caer en la rama «sin restricción» y verlo todo.
            return [];
        }

        var plantas = await contexto.UsuariosPlanta
            .AsNoTracking()
            .Where(up => up.UsuarioId == id)
            .Select(up => up.PlantaId)
            .Distinct()
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // ADMINISTRADOR sin filas es alcance vacío (fail-closed). COORDINADOR sin filas es el
        // único caso HTTP autorizado de alcance global. Si el coordinador tiene una o más
        // asignaciones, esas filas son deliberadamente restrictivas y nunca se descartan.
        return esCoordinador && plantas.Count == 0
            ? null
            : plantas;
    }
}
