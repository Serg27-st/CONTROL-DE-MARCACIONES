using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Cuadrillas;

public sealed partial class ProgramacionCuadrillaMvcTests
{
    [Fact]
    public void Appsettings_distribuido_no_contiene_credenciales_de_bootstrap()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
        var seguridad = documento.RootElement.GetProperty("Seguridad");

        seguridad.GetProperty("AdminCorreo").GetString().ShouldBe(string.Empty);
        seguridad.GetProperty("AdminClave").GetString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("1", "1", "Un descanso no puede tener turno.")]
    [InlineData(null, "2", "Un descanso debe durar exactamente un día.")]
    public async Task Post_rechaza_descanso_invalido_sin_persistir(
        string? turnoId,
        string dias,
        string mensajeEsperado)
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionAsync().ConfigureAwait(true);

        var antes = await escenario.CantidadProgramacionesAsync().ConfigureAwait(true);
        var formulario = await escenario.Cliente
            .GetAsync($"/Cuadrillas/Programar/{escenario.CuadrillaId}")
            .ConfigureAwait(true);
        var token = ExtraerAntiforgery(await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

        var respuesta = await escenario.Cliente.PostAsync(
                $"/Cuadrillas/Programar/{escenario.CuadrillaId}",
                ContenidoProgramacion(
                    token,
                    escenario.CuadrillaId,
                    [("Descanso", turnoId, dias)]))
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true);
        WebUtility.HtmlDecode(html).ShouldContain(mensajeEsperado);
        (await escenario.CantidadProgramacionesAsync().ConfigureAwait(true)).ShouldBe(antes);
    }

    [Fact]
    public async Task Post_valido_persiste_ciclo_exacto_y_redirige_al_detalle()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionAsync().ConfigureAwait(true);

        var formulario = await escenario.Cliente
            .GetAsync($"/Cuadrillas/Programar/{escenario.CuadrillaId}")
            .ConfigureAwait(true);
        var token = ExtraerAntiforgery(await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

        var respuesta = await escenario.Cliente.PostAsync(
                $"/Cuadrillas/Programar/{escenario.CuadrillaId}",
                ContenidoProgramacion(
                    token,
                    escenario.CuadrillaId,
                    [
                        ("Trabajo", "1", "2"),
                        ("Descanso", null, "1"),
                        ("Trabajo", "2", "2")
                    ]))
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        respuesta.Headers.Location.ShouldNotBeNull();
        respuesta.Headers.Location!.OriginalString.ShouldBe($"/Cuadrillas/Detalle/{escenario.CuadrillaId}");

        var tramos = await escenario.TramosPersistidosAsync().ConfigureAwait(true);
        tramos.Count.ShouldBe(3);
        tramos[0].ShouldBe((1, TipoTramoRotacion.Trabajo, 1L, 2));
        tramos[1].ShouldBe((2, TipoTramoRotacion.Descanso, null, 1));
        tramos[2].ShouldBe((3, TipoTramoRotacion.Trabajo, 2L, 2));

        var detalle = await escenario.Cliente.GetAsync(respuesta.Headers.Location).ConfigureAwait(true);
        detalle.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await detalle.Content.ReadAsStringAsync().ConfigureAwait(true));
        html.ShouldContain("DIU 2 d → DESCANSO PROGRAMADO → NOC 2 d");
    }

    [Fact]
    public async Task Get_programar_conserva_csp_estricta_y_carga_script_externo()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionAsync().ConfigureAwait(true);

        var respuesta = await escenario.Cliente
            .GetAsync($"/Cuadrillas/Programar/{escenario.CuadrillaId}")
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var csp = respuesta.Headers.GetValues("Content-Security-Policy").Single();
        var directivaScript = csp.Split(';', StringSplitOptions.TrimEntries)
            .Single(directiva => directiva.StartsWith("script-src", StringComparison.Ordinal));
        directivaScript.ShouldBe("script-src 'self'");

        var html = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true);
        html.ShouldContain("/js/programacion-cuadrilla.js?v=");
        html.ShouldNotContain("function actualizar(tipo)");
    }

    private static FormUrlEncodedContent ContenidoProgramacion(
        string token,
        long cuadrillaId,
        IReadOnlyList<(string Tipo, string? TurnoId, string? Dias)> tramos)
    {
        var valores = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Datos.CuadrillaId", cuadrillaId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Datos.Desde", "2026-10-12")
        };

        for (var indice = 0; indice < 6; indice++)
        {
            var tramo = indice < tramos.Count ? tramos[indice] : ("Trabajo", null, null);
            valores.Add(new($"Datos.Tramos[{indice}].Tipo", tramo.Tipo));

            if (tramo.TurnoId is not null)
            {
                valores.Add(new($"Datos.Tramos[{indice}].TurnoId", tramo.TurnoId));
            }

            if (tramo.Dias is not null)
            {
                valores.Add(new($"Datos.Tramos[{indice}].Dias", tramo.Dias));
            }
        }

        return new FormUrlEncodedContent(valores);
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
        private const string Correo = "coordinador.mvc@gca.local";
        private const string Clave = "WebMvc2026";

        private readonly AplicacionWebFactory factory;

        private EscenarioMvc(AplicacionWebFactory factory, HttpClient cliente, long cuadrillaId)
        {
            this.factory = factory;
            Cliente = cliente;
            CuadrillaId = cuadrillaId;
        }

        public HttpClient Cliente { get; }

        public long CuadrillaId { get; }

        public static async Task<EscenarioMvc> CrearAsync()
        {
            var factory = new AplicacionWebFactory();
            var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            using var alcance = factory.Services.CreateScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            Path.GetFullPath(contexto.Database.GetDbConnection().DataSource)
                .ShouldBe(Path.GetFullPath(factory.RutaBase));
            var servicio = alcance.ServiceProvider.GetRequiredService<IServicioCuadrillas>();
            var id = await servicio.GuardarAsync(
                    new GuardarCuadrillaRequest
                    {
                        Codigo = "MVC-ROT",
                        Nombre = "Prueba MVC descanso rotativo",
                        PlantaId = 1,
                        Activo = true
                    },
                    Correo)
                .ConfigureAwait(true);

            return new EscenarioMvc(factory, cliente, id);
        }

        public async Task IniciarSesionAsync()
        {
            var formulario = await Cliente.GetAsync("/Cuenta/Ingresar").ConfigureAwait(true);
            formulario.StatusCode.ShouldBe(HttpStatusCode.OK);
            var token = ExtraerAntiforgery(await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

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

        public async Task<int> CantidadProgramacionesAsync()
        {
            using var alcance = factory.Services.CreateScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            return await contexto.ProgramacionesCuadrilla
                .CountAsync(p => p.CuadrillaId == CuadrillaId)
                .ConfigureAwait(true);
        }

        public async Task<List<(int Orden, TipoTramoRotacion Tipo, long? TurnoId, int Dias)>>
            TramosPersistidosAsync()
        {
            using var alcance = factory.Services.CreateScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            return await contexto.TramosRotacion
                .AsNoTracking()
                .Where(t => t.Programacion!.CuadrillaId == CuadrillaId)
                .OrderBy(t => t.Orden)
                .Select(t => new ValueTuple<int, TipoTramoRotacion, long?, int>(
                    t.Orden,
                    t.Tipo,
                    t.TurnoId,
                    t.Dias))
                .ToListAsync()
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

        public string RutaBase => rutaBase
            ?? throw new InvalidOperationException("La base temporal todavía no fue configurada.");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            rutaBase ??= Path.Combine(
                Path.GetTempPath(),
                $"control-marcacion-web-{Guid.NewGuid():N}.db");

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Seguridad:AdminCorreo"] = "coordinador.mvc@gca.local",
                    ["Seguridad:AdminClave"] = "WebMvc2026",
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
