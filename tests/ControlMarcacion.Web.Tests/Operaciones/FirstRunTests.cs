using System.Net;
using System.Text.RegularExpressions;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Operaciones;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Operaciones;

public sealed partial class FirstRunTests
{
    [Fact]
    public async Task FirstRun01_y_02_sin_db_ni_variables_mantiene_vivo_y_expone_setup()
    {
        var ruta = RutaTemporal();
        try
        {
            await using var factory = new FirstRunFactory(ruta);
            using var cliente = CrearCliente(factory);

            var vivo = await cliente.GetAsync("/vivo").ConfigureAwait(true);
            var estado = await cliente.GetAsync("/estado").ConfigureAwait(true);
            var setup = await cliente.GetAsync("/setup").ConfigureAwait(true);

            vivo.StatusCode.ShouldBe(HttpStatusCode.OK);
            estado.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await estado.Content.ReadAsStringAsync().ConfigureAwait(true))
                .ShouldContain("CONFIGURACION_INICIAL_REQUERIDA");
            setup.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await setup.Content.ReadAsStringAsync().ConfigureAwait(true))
                .ShouldContain("Crear coordinador");
        }
        finally
        {
            Limpiar(ruta);
        }
    }

    [Fact]
    public async Task FirstRun03_crea_un_solo_coordinador_con_hash_y_pasa_a_listo()
    {
        var ruta = RutaTemporal();
        const string clave = "PrimeraClave2026";
        try
        {
            await using var factory = new FirstRunFactory(ruta);
            using var cliente = CrearCliente(factory);
            await using (var diagnostico = factory.Services.CreateAsyncScope())
            {
                var contexto = diagnostico.ServiceProvider.GetRequiredService<MarcacionDbContext>();
                (await contexto.Set<ApplicationUser>().CountAsync().ConfigureAwait(true))
                    .ShouldBe(0, contexto.Database.GetConnectionString());
            }
            var respuesta = await EnviarSetupAsync(cliente, clave).ConfigureAwait(true);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            respuesta.Headers.Location?.OriginalString.ShouldBe("/Cuenta/Ingresar");

            var estado = await cliente.GetAsync("/estado").ConfigureAwait(true);
            estado.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await estado.Content.ReadAsStringAsync().ConfigureAwait(true)).ShouldContain("LISTO");

            await using var alcance = factory.Services.CreateAsyncScope();
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var coordinador = await usuarios.Users.SingleAsync().ConfigureAwait(true);
            coordinador.PasswordHash.ShouldNotBeNullOrWhiteSpace();
            coordinador.PasswordHash.ShouldNotContain(clave);
            (await usuarios.CheckPasswordAsync(coordinador, clave).ConfigureAwait(true)).ShouldBeTrue();
            (await usuarios.IsInRoleAsync(coordinador, RolesSistema.Coordinador)
                    .ConfigureAwait(true))
                .ShouldBeTrue();
        }
        finally
        {
            Limpiar(ruta);
        }
    }

    [Fact]
    public async Task FirstRun04_y_05_segundo_arranque_reutiliza_db_y_setup_redirige_seguro()
    {
        var ruta = RutaTemporal();
        try
        {
            await using (var primera = new FirstRunFactory(ruta))
            {
                using var cliente = CrearCliente(primera);
                (await EnviarSetupAsync(cliente, "PrimeraClave2026").ConfigureAwait(true))
                    .StatusCode.ShouldBe(HttpStatusCode.Redirect);
            }

            SqliteConnection.ClearAllPools();

            await using var segunda = new FirstRunFactory(ruta);
            using var clienteSegundo = CrearCliente(segunda);
            var estado = await clienteSegundo.GetAsync("/estado").ConfigureAwait(true);
            var setup = await clienteSegundo.GetAsync("/setup").ConfigureAwait(true);

            estado.StatusCode.ShouldBe(HttpStatusCode.OK);
            setup.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            setup.Headers.Location?.OriginalString.ShouldBe("/Cuenta/Ingresar");
        }
        finally
        {
            Limpiar(ruta);
        }
    }

    [Fact]
    public async Task FirstRun06_error_tecnico_no_habilita_setup()
    {
        await using var factory = new TechnicalFailureFactory();
        using var cliente = CrearCliente(factory);

        var estado = await cliente.GetAsync("/estado").ConfigureAwait(true);
        var setup = await cliente.GetAsync("/setup").ConfigureAwait(true);

        estado.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await estado.Content.ReadAsStringAsync().ConfigureAwait(true)).ShouldContain("ERROR");
        setup.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await setup.Content.ReadAsStringAsync().ConfigureAwait(true))
            .ShouldContain("INICIALIZACION_NO_COMPLETA");
    }

    [Fact]
    public async Task FirstRun07_dos_posts_concurrentes_crean_un_solo_coordinador()
    {
        var ruta = RutaTemporal();
        try
        {
            await using var factory = new FirstRunFactory(ruta);
            using var primerCliente = CrearCliente(factory);
            using var segundoCliente = CrearCliente(factory);
            var primerPost = await CrearPostSetupAsync(
                    primerCliente,
                    "Coordinador uno",
                    "coordinador-uno@example.invalid",
                    "PrimeraClave2026")
                .ConfigureAwait(true);
            var segundoPost = await CrearPostSetupAsync(
                    segundoCliente,
                    "Coordinador dos",
                    "coordinador-dos@example.invalid",
                    "SegundaClave2026")
                .ConfigureAwait(true);

            var respuestas = await Task.WhenAll(
                    primerCliente.SendAsync(primerPost),
                    segundoCliente.SendAsync(segundoPost))
                .ConfigureAwait(true);

            foreach (var respuesta in respuestas)
            {
                respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
                respuesta.Headers.Location?.OriginalString.ShouldBe("/Cuenta/Ingresar");
            }

            await using var alcance = factory.Services.CreateAsyncScope();
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var coordinador = await usuarios.Users.SingleAsync().ConfigureAwait(true);
            (await usuarios.IsInRoleAsync(coordinador, RolesSistema.Coordinador)
                    .ConfigureAwait(true))
                .ShouldBeTrue();
        }
        finally
        {
            Limpiar(ruta);
        }
    }

    [Fact]
    public async Task FirstRun08_clave_no_persiste_en_texto_plano_ni_en_configuracion()
    {
        var ruta = RutaTemporal();
        var clave = $"ClaveTemporal{Guid.NewGuid():N}Aa9";
        try
        {
            await using var factory = new FirstRunFactory(ruta);
            using var cliente = CrearCliente(factory);
            var respuesta = await EnviarSetupAsync(cliente, clave).ConfigureAwait(true);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            (await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true))
                .ShouldNotContain(clave);

            var configuracion = factory.Services.GetRequiredService<IConfiguration>();
            configuracion["Seguridad:AdminCorreo"].ShouldBeNullOrEmpty();
            configuracion["Seguridad:AdminClave"].ShouldBeNullOrEmpty();

            SqliteConnection.ClearAllPools();
            var contenido = await File.ReadAllBytesAsync(ruta).ConfigureAwait(true);
            Contiene(contenido, System.Text.Encoding.UTF8.GetBytes(clave)).ShouldBeFalse();
        }
        finally
        {
            Limpiar(ruta);
        }
    }

    private static HttpClient CrearCliente(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<HttpResponseMessage> EnviarSetupAsync(
        HttpClient cliente,
        string clave)
    {
        using var solicitud = await CrearPostSetupAsync(
                cliente,
                "Coordinador de prueba",
                "coordinador@example.invalid",
                clave)
            .ConfigureAwait(true);
        return await cliente.SendAsync(solicitud).ConfigureAwait(true);
    }

    private static async Task<HttpRequestMessage> CrearPostSetupAsync(
        HttpClient cliente,
        string nombre,
        string correo,
        string clave)
    {
        var respuestaPagina = await cliente.GetAsync("/setup").ConfigureAwait(true);
        respuestaPagina.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"/setup redirigió a {respuestaPagina.Headers.Location}");
        var pagina = await respuestaPagina.Content.ReadAsStringAsync().ConfigureAwait(true);
        var coincidencia = TokenAntiforgery().Match(pagina);
        coincidencia.Success.ShouldBeTrue("El wizard debe emitir un token antiforgery.");

        return new HttpRequestMessage(HttpMethod.Post, "/setup")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(coincidencia.Groups[1].Value),
                ["NombreCompleto"] = nombre,
                ["Correo"] = correo,
                ["Clave"] = clave,
                ["ConfirmacionClave"] = clave
            })
        };
    }

    private static bool Contiene(ReadOnlySpan<byte> contenido, ReadOnlySpan<byte> buscado) =>
        contenido.IndexOf(buscado) >= 0;

    private static string RutaTemporal() =>
        Path.Combine(Path.GetTempPath(), $"control-marcacion-first-run-{Guid.NewGuid():N}.db");

    private static void Limpiar(string ruta)
    {
        SqliteConnection.ClearAllPools();
        foreach (var archivo in new[] { ruta, $"{ruta}-wal", $"{ruta}-shm" })
        {
            if (File.Exists(archivo))
            {
                File.Delete(archivo);
            }
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    private sealed class FirstRunFactory(string rutaBase) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Seguridad:AdminCorreo"] = string.Empty,
                    ["Seguridad:AdminClave"] = string.Empty,
                    ["Despliegue:HostingLocal"] = "false",
                    ["Despliegue:AbrirNavegador"] = "false",
                    ["Urls"] = "http://127.0.0.1:0",
                    ["Logging:LogLevel:Default"] = "Critical"
                });
            });
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<DbContextOptions<MarcacionDbContext>>();
                servicios.AddDbContext<MarcacionDbContext>((proveedor, opciones) =>
                    opciones
                        .UseSqlite($"Data Source={rutaBase}")
                        .AddInterceptors(
                            proveedor.GetRequiredService<InterceptorAuditoria>()));
            });
        }
    }

    private sealed class TechnicalFailureFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = "Data Source=:memory:",
                    ["Despliegue:HostingLocal"] = "false",
                    ["Despliegue:AbrirNavegador"] = "false",
                    ["Logging:LogLevel:Default"] = "Critical"
                });
            });
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<IPreparadorAplicacion>();
                servicios.AddScoped<IPreparadorAplicacion, PreparadorFallido>();
                servicios.RemoveAll<IVerificadorEstadoOperativo>();
                servicios.AddSingleton<IVerificadorEstadoOperativo>(new VerificadorFallido());
            });
        }
    }

    private sealed class PreparadorFallido : IPreparadorAplicacion
    {
        public Task PrepararAsync(CancellationToken cancelacion) =>
            Task.FromException(new InvalidOperationException("fallo técnico simulado"));
    }

    private sealed class VerificadorFallido : IVerificadorEstadoOperativo
    {
        public Task<ResultadoEstadoOperativo> VerificarAsync(CancellationToken cancelacion) =>
            Task.FromResult(ResultadoEstadoOperativo.NoPreparado("MIGRACIONES_PENDIENTES"));
    }
}
