using System.Net;
using System.Text.RegularExpressions;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Turnos;

public sealed partial class PlantasHabilitadasMvcTests
{
    [Fact]
    public async Task Rige_desde_solo_se_solicita_al_editar_un_turno_existente()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionAsync().ConfigureAwait(true);

        var alta = await escenario.LeerFormularioAsync().ConfigureAwait(true);
        alta.ShouldNotContain("name=\"Datos.RigeDesde\"");
        alta.ShouldContain("vigencia histórica desde el origen");

        var turnoId = await escenario.GuardarTurnoAsync("WEBVIG", []).ConfigureAwait(true);
        var edicion = await escenario.LeerFormularioAsync(turnoId).ConfigureAwait(true);
        edicion.ShouldContain("name=\"Datos.RigeDesde\"");
    }

    [Fact]
    public async Task Alta_y_edicion_persisten_exactamente_las_plantas_marcadas()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionAsync().ConfigureAwait(true);

        var turnoId = await escenario.GuardarTurnoAsync(
                codigo: "WEBPL",
                plantas: [1, 2])
            .ConfigureAwait(true);

        (await escenario.PlantasDelTurnoAsync(turnoId).ConfigureAwait(true))
            .ShouldBe([1, 2]);

        await escenario.GuardarTurnoAsync(
                codigo: "WEBPL",
                plantas: [2],
                turnoId: turnoId)
            .ConfigureAwait(true);

        (await escenario.PlantasDelTurnoAsync(turnoId).ConfigureAwait(true))
            .ShouldBe([2]);

        await escenario.GuardarTurnoAsync(
                codigo: "WEBPL",
                plantas: [],
                turnoId: turnoId)
            .ConfigureAwait(true);

        (await escenario.PlantasDelTurnoAsync(turnoId).ConfigureAwait(true))
            .ShouldBeEmpty();
    }

    private static string ExtraerAntiforgery(string html)
    {
        var coincidencia = AntiforgeryRegex().Match(html);
        coincidencia.Success.ShouldBeTrue("El formulario real debe emitir un token antiforgery.");
        return WebUtility.HtmlDecode(coincidencia.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();

    private sealed class EscenarioMvc : IAsyncDisposable
    {
        private const string Correo = "coordinador.turnos@gca.local";
        private const string Clave = "WebTurnos2026";
        private readonly AplicacionWebFactory factory;

        private EscenarioMvc(AplicacionWebFactory factory, HttpClient cliente)
        {
            this.factory = factory;
            Cliente = cliente;
        }

        private HttpClient Cliente { get; }

        public static Task<EscenarioMvc> CrearAsync()
        {
            var factory = new AplicacionWebFactory();
            var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            return Task.FromResult(new EscenarioMvc(factory, cliente));
        }

        public async Task IniciarSesionAsync()
        {
            var formulario = await Cliente.GetAsync("/Cuenta/Ingresar").ConfigureAwait(true);
            formulario.StatusCode.ShouldBe(HttpStatusCode.OK);
            var token = ExtraerAntiforgery(
                await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

            var respuesta = await Cliente.PostAsync(
                    "/Cuenta/Ingresar",
                    new FormUrlEncodedContent(
                    [
                        new("__RequestVerificationToken", token),
                        new("Correo", Correo),
                        new("Clave", Clave),
                        new("Recordarme", "false"),
                        new("ReturnUrl", string.Empty)
                    ]))
                .ConfigureAwait(true);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        public async Task<long> GuardarTurnoAsync(
            string codigo,
            IReadOnlyList<long> plantas,
            long? turnoId = null)
        {
            var ruta = turnoId is null ? "/Turnos/Editar" : $"/Turnos/Editar/{turnoId}";
            var formulario = await Cliente.GetAsync(ruta).ConfigureAwait(true);
            formulario.StatusCode.ShouldBe(HttpStatusCode.OK);
            var token = ExtraerAntiforgery(
                await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

            var valores = new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", token),
                new("Datos.Codigo", codigo),
                new("Datos.Nombre", "Turno plantas MVC"),
                new("Datos.HoraInicio", "07:00"),
                new("Datos.HoraFin", "16:00"),
                new("Datos.MinutosRefrigerio", "60"),
                new("Datos.MinutosTolerancia", "10"),
                new("Datos.MinutosAnticipacionEntrada", "15"),
                new("Datos.MinutosAnticipacionSalida", "5"),
                new("Datos.MinutosGraciaSalida", "0"),
                new("Datos.MinutosJornadaOrdinaria", "480"),
                new("Datos.DiasAplicables", "1"),
                new("Datos.Activo", "true")
            };

            if (turnoId is { } id)
            {
                valores.Add(new("Datos.Id", id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            valores.AddRange(plantas.Select(planta =>
                new KeyValuePair<string, string>(
                    "Datos.PlantasHabilitadas",
                    planta.ToString(System.Globalization.CultureInfo.InvariantCulture))));

            var respuesta = await Cliente.PostAsync(
                    "/Turnos/Guardar",
                    new FormUrlEncodedContent(valores))
                .ConfigureAwait(true);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            respuesta.Headers.Location.ShouldNotBeNull();
            respuesta.Headers.Location!.OriginalString.ShouldBe("/Turnos");

            if (turnoId is not null)
            {
                return turnoId.Value;
            }

            using var alcance = factory.Services.CreateScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            return await contexto.Turnos
                .Where(turno => turno.Codigo == codigo)
                .Select(turno => turno.Id)
                .SingleAsync()
                .ConfigureAwait(true);
        }

        public async Task<string> LeerFormularioAsync(long? turnoId = null)
        {
            var ruta = turnoId is null ? "/Turnos/Editar" : $"/Turnos/Editar/{turnoId}";
            var respuesta = await Cliente.GetAsync(ruta).ConfigureAwait(true);
            respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
            return await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true);
        }

        public async Task<long[]> PlantasDelTurnoAsync(long turnoId)
        {
            using var alcance = factory.Services.CreateScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            return await contexto.TurnosPlanta
                .AsNoTracking()
                .Where(relacion => relacion.TurnoId == turnoId)
                .OrderBy(relacion => relacion.PlantaId)
                .Select(relacion => relacion.PlantaId)
                .ToArrayAsync()
                .ConfigureAwait(true);
        }

        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await factory.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed class AplicacionWebFactory : WebApplicationFactory<Program>
    {
        private string? rutaBase;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            rutaBase ??= Path.Combine(
                Path.GetTempPath(),
                $"control-marcacion-turnos-{Guid.NewGuid():N}.db");

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Seguridad:AdminCorreo"] = "coordinador.turnos@gca.local",
                    ["Seguridad:AdminClave"] = "WebTurnos2026",
                    ["Despliegue:HostingLocal"] = "false",
                    ["Despliegue:AbrirNavegador"] = "false",
                    ["Urls"] = "http://127.0.0.1:0",
                    ["Logging:LogLevel:Default"] = "Warning",
                    ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
                });
            });
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<DbContextOptions<MarcacionDbContext>>();
                servicios.AddDbContext<MarcacionDbContext>((proveedor, opciones) =>
                    opciones
                        .UseSqlite($"Data Source={rutaBase}")
                        .AddInterceptors(proveedor.GetRequiredService<InterceptorAuditoria>()));
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(true);
            SqliteConnection.ClearAllPools();

            if (rutaBase is null)
            {
                return;
            }

            foreach (var ruta in new[] { rutaBase, $"{rutaBase}-wal", $"{rutaBase}-shm" })
            {
                if (File.Exists(ruta))
                {
                    File.Delete(ruta);
                }
            }
        }
    }
}
