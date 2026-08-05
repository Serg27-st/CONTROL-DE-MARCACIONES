using ControlMarcacion.Application.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Crea el primer administrador cuando la base todavía no tiene ningún usuario.
/// </summary>
/// <remarks>
/// Los roles se siembran por migración porque son nombres, no secretos. Una cuenta con
/// contraseña es distinto: <b>nunca se hornea en una migración ni en código fuente</b>
/// (viola la regla del proyecto de no versionar credenciales). Este inicializador lee
/// <c>Seguridad:AdminCorreo</c> / <c>Seguridad:AdminClave</c> desde configuración —
/// user-secrets en desarrollo, variables de entorno en producción — y solo actúa la
/// primera vez: si ya existe algún usuario, no hace nada, aunque las variables sigan
/// configuradas.
/// </remarks>
public static partial class InicializadorSeguridad
{
    public static async Task InicializarAsync(IServiceProvider servicios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var logger = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorSeguridad));
        var userManager = servicios.GetRequiredService<UserManager<ApplicationUser>>();

        var yaHayUsuarios = await userManager.Users.AnyAsync().ConfigureAwait(false);

        if (yaHayUsuarios)
        {
            return;
        }

        var correo = configuracion["Seguridad:AdminCorreo"];
        var clave = configuracion["Seguridad:AdminClave"];

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(clave))
        {
            RegistrarFaltanCredenciales(logger);
            return;
        }

        var administrador = new ApplicationUser
        {
            UserName = correo,
            Email = correo,
            NombreCompleto = "Administrador inicial",
            EmailConfirmed = true,
            Activo = true
        };

        var resultado = await userManager.CreateAsync(administrador, clave).ConfigureAwait(false);

        if (!resultado.Succeeded)
        {
            RegistrarErrorCreacion(logger, string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(administrador, RolesSistema.Administrador).ConfigureAwait(false);

        RegistrarAdminCreado(logger, correo);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No hay ningún usuario registrado y Seguridad:AdminCorreo / Seguridad:AdminClave no están " +
                   "configurados. Defínalos (user-secrets en desarrollo, variables de entorno " +
                   "Seguridad__AdminCorreo / Seguridad__AdminClave en producción) y reinicie la aplicación " +
                   "para crear el primer administrador.")]
    private static partial void RegistrarFaltanCredenciales(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo crear el administrador inicial: {Errores}")]
    private static partial void RegistrarErrorCreacion(ILogger logger, string errores);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Se creó el administrador inicial '{Correo}'. Cambie la contraseña desde su primer inicio " +
                   "de sesión y retire Seguridad:AdminClave de la configuración.")]
    private static partial void RegistrarAdminCreado(ILogger logger, string correo);
}
