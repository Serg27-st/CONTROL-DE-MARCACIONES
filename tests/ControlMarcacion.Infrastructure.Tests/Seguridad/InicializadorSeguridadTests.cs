using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Seguridad;

public sealed class InicializadorSeguridadTests
{
    [Fact]
    public async Task Base_sin_usuarios_y_sin_bootstrap_externo_falla_explicito()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync().ConfigureAwait(true);

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<MarcacionDbContext>(opciones => opciones.UseSqlite(conexion));
        servicios
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<MarcacionDbContext>();

        await using var proveedor = servicios.BuildServiceProvider();
        await using var alcance = proveedor.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
        await contexto.Database.EnsureCreatedAsync().ConfigureAwait(true);

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seguridad:AdminCorreo"] = string.Empty,
                ["Seguridad:AdminClave"] = string.Empty
            })
            .Build();

        await Should.ThrowAsync<CredencialesBootstrapFaltantesException>(() =>
                InicializadorSeguridad.InicializarAsync(alcance.ServiceProvider, configuracion))
            .ConfigureAwait(true);
    }
}
