using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Application.Seguridad.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Alta y baja lógica de cuentas, sobre <c>UserManager</c> y <c>RoleManager</c>.
/// </summary>
/// <remarks>
/// El <c>DbContext</c> es el mismo que usa Identity por dentro (se registra con
/// <c>AddEntityFrameworkStores&lt;MarcacionDbContext&gt;</c>), así que la transacción del
/// alta cubre también lo que escribe <c>UserManager</c>: o queda la cuenta con su rol y sus
/// plantas, o no queda nada. Una cuenta a medio crear es peor que ninguna.
/// </remarks>
internal sealed class ServicioUsuarios(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    MarcacionDbContext contexto) : IServicioUsuarios
{
    public async Task<IReadOnlyList<UsuarioListaDto>> ListarAsync(CancellationToken cancelacion = default)
    {
        var usuarios = await userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var plantasPorUsuario = await contexto.UsuariosPlanta
            .AsNoTracking()
            .Join(
                contexto.Plantas.AsNoTracking(),
                up => up.PlantaId,
                p => p.Id,
                (up, p) => new { up.UsuarioId, p.Nombre })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var nombresDePlanta = plantasPorUsuario
            .GroupBy(x => x.UsuarioId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(x => x.Nombre).Order()),
                StringComparer.Ordinal);

        var resultado = new List<UsuarioListaDto>(usuarios.Count);

        foreach (var usuario in usuarios)
        {
            var roles = await userManager.GetRolesAsync(usuario).ConfigureAwait(false);

            resultado.Add(new UsuarioListaDto(
                usuario.Id,
                usuario.Email ?? usuario.UserName ?? "-",
                usuario.NombreCompleto,
                usuario.NumeroDocumento,
                roles.Count > 0 ? roles[0] : "(sin rol)",
                nombresDePlanta.TryGetValue(usuario.Id, out var plantas) ? plantas : string.Empty,
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

        // El rol llega por POST: se comprueba contra la lista cerrada del sistema y no
        // solo contra la tabla de roles, para que un rol huérfano de una migración vieja
        // no vuelva a ser asignable desde el formulario.
        if (!RolesSistema.Todos.Contains(solicitud.Rol, StringComparer.Ordinal) ||
            !await roleManager.RoleExistsAsync(solicitud.Rol).ConfigureAwait(false))
        {
            throw new ReglaNegocioException($"El rol '{solicitud.Rol}' no existe.");
        }

        var tipoDocumentoId = solicitud.TipoDocumentoId
            ?? throw new ReglaNegocioException("Seleccione el tipo de documento.");

        var tipoDocumento = await contexto.TiposDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tipoDocumentoId && t.Activo, cancelacion)
            .ConfigureAwait(false);

        if (tipoDocumento is null)
        {
            throw new ReglaNegocioException("El tipo de documento seleccionado no existe o está inactivo.");
        }

        // Mismo validador que Personal: el documento de un operador se cruza a ojo con el
        // de su ficha, así que si aquí se admitiera un DNI de siete dígitos tendríamos dos
        // reglas distintas para el mismo dato.
        var documentoInvalido = ValidadorDocumentoIdentidad.Validar(
            tipoDocumento, solicitud.NumeroDocumento);

        if (documentoInvalido is not null)
        {
            throw new ReglaNegocioException(documentoInvalido);
        }

        // Las plantas solo significan algo para ADMINISTRADOR. Para el resto se descartan
        // aunque el POST las traiga: nadie debe terminar con un alcance registrado que su
        // rol no contempla.
        var esAdministrador = string.Equals(solicitud.Rol, RolesSistema.Administrador, StringComparison.Ordinal);

        var plantaIds = esAdministrador
            ? solicitud.PlantaIds.Distinct().ToList()
            : [];

        // Fail-closed: el aislamiento por planta trata «sin plantas» como «no ve ninguna», así
        // que un ADMINISTRADOR sin plantas no vería a ningún trabajador. Crear la cuenta así la
        // dejaría inservible desde el primer día — se exige al menos una planta. Es la
        // contraparte, en el alta, de que el filtro de consulta sea estricto.
        if (esAdministrador && plantaIds.Count == 0)
        {
            throw new ReglaNegocioException(
                "Un administrador gestiona el tareo de plantas concretas: seleccione al menos " +
                "una planta. Sin plantas asignadas no vería a ningún trabajador.");
        }

        if (plantaIds.Count > 0)
        {
            var existentes = await contexto.Plantas
                .AsNoTracking()
                .Where(p => plantaIds.Contains(p.Id) && p.Activo)
                .Select(p => p.Id)
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            if (existentes.Count != plantaIds.Count)
            {
                throw new ReglaNegocioException(
                    "Una de las plantas seleccionadas no existe o está inactiva.");
            }
        }

        var usuario = new ApplicationUser
        {
            UserName = correo,
            Email = correo,
            ApellidoPaterno = solicitud.ApellidoPaterno.Trim(),
            ApellidoMaterno = solicitud.ApellidoMaterno.Trim(),
            Nombres = solicitud.Nombres.Trim(),
            NombreCompleto = solicitud.NombreCompleto,
            TipoDocumentoId = tipoDocumentoId,
            NumeroDocumento = solicitud.NumeroDocumento.Trim(),
            // No hay servidor de correo configurado para un enlace de confirmación: el
            // COORDINADOR que crea la cuenta ya validó el correo por su cuenta.
            EmailConfirmed = true,
            Activo = true
        };

        // BeginTransactionAsync a secas no es compatible con la estrategia de reintentos de
        // SQL Server (EnableRetryOnFailure, configurada en Program.cs): si una operación
        // dentro de una transacción manual dispara un reintento, EF Core lanza
        // InvalidOperationException en vez de reintentar. CreateExecutionStrategy().ExecuteAsync
        // es el patrón documentado para que ambas cosas convivan — todo el bloque se reintenta
        // como unidad si hace falta, no a medias.
        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database
                .BeginTransactionAsync(cancelacion)
                .ConfigureAwait(false);

            var resultado = await userManager.CreateAsync(usuario, solicitud.Clave).ConfigureAwait(false);

            if (!resultado.Succeeded)
            {
                throw new ReglaNegocioException(
                    string.Join(" ", resultado.Errors.Select(e => e.Description)));
            }

            var asignacionRol = await userManager.AddToRoleAsync(usuario, solicitud.Rol).ConfigureAwait(false);

            if (!asignacionRol.Succeeded)
            {
                throw new ReglaNegocioException(
                    string.Join(" ", asignacionRol.Errors.Select(e => e.Description)));
            }

            foreach (var plantaId in plantaIds)
            {
                contexto.UsuariosPlanta.Add(new UsuarioPlanta
                {
                    UsuarioId = usuario.Id,
                    PlantaId = plantaId
                });
            }

            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
        }).ConfigureAwait(false);
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
