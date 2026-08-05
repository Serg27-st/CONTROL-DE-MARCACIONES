using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Application.Seguridad.Dto;
using ControlMarcacion.Domain.Comun;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Alta y baja lógica de cuentas, sobre <c>UserManager</c> y <c>RoleManager</c>.
/// </summary>
internal sealed class ServicioUsuarios(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IServicioUsuarios
{
    public async Task<IReadOnlyList<UsuarioListaDto>> ListarAsync(CancellationToken cancelacion = default)
    {
        var usuarios = await userManager.Users
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var resultado = new List<UsuarioListaDto>(usuarios.Count);

        foreach (var usuario in usuarios)
        {
            var roles = await userManager.GetRolesAsync(usuario).ConfigureAwait(false);

            resultado.Add(new UsuarioListaDto(
                usuario.Id,
                usuario.Email ?? usuario.UserName ?? "-",
                usuario.NombreCompleto,
                roles.Count > 0 ? roles[0] : "(sin rol)",
                usuario.Activo));
        }

        return resultado;
    }

    public async Task CrearAsync(CrearUsuarioRequest solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var correo = solicitud.Correo.Trim();

        var existente = await userManager.FindByEmailAsync(correo).ConfigureAwait(false);

        if (existente is not null)
        {
            throw new ReglaNegocioException($"Ya existe un usuario con el correo '{correo}'.");
        }

        if (!await roleManager.RoleExistsAsync(solicitud.Rol).ConfigureAwait(false))
        {
            throw new ReglaNegocioException($"El rol '{solicitud.Rol}' no existe.");
        }

        var usuario = new ApplicationUser
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = solicitud.NombreCompleto.Trim(),
            // No hay servidor de correo configurado para un enlace de confirmación: el
            // ADMINISTRADOR que crea la cuenta ya validó el correo por su cuenta.
            EmailConfirmed = true,
            Activo = true
        };

        var resultado = await userManager.CreateAsync(usuario, solicitud.Clave).ConfigureAwait(false);

        if (!resultado.Succeeded)
        {
            throw new ReglaNegocioException(
                string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(usuario, solicitud.Rol).ConfigureAwait(false);
    }

    public async Task CambiarEstadoAsync(string id, bool activo, CancellationToken cancelacion = default)
    {
        var usuario = await userManager.FindByIdAsync(id).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el usuario", id);

        usuario.Activo = activo;

        // El flag Activo es lo que se ve en la pantalla; el bloqueo de Identity es lo que
        // realmente impide iniciar sesión. Sin esto, desactivar a alguien en la lista no
        // le negaría el acceso hasta quién sabe cuándo.
        await userManager
            .SetLockoutEndDateAsync(usuario, activo ? null : DateTimeOffset.MaxValue)
            .ConfigureAwait(false);

        await userManager.UpdateAsync(usuario).ConfigureAwait(false);
    }

    public IReadOnlyList<string> RolesDisponibles() => RolesSistema.Todos;
}
