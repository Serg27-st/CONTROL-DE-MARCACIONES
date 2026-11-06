using System.Net;
using System.Text.RegularExpressions;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Seguridad;
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

namespace ControlMarcacion.Web.Tests.Trabajadores;

public sealed partial class ReingresoTrabajadorMvcTests
{
    private const long EmpresaReingresoId = 2;
    private const long PlantaReingresoId = 3;

    [Fact]
    public async Task PL3EC_WEB_01_get_formulario_permitido_para_trabajador_cesado()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionCoordinadorAsync().ConfigureAwait(true);

        var historial = await escenario.Cliente
            .GetAsync($"/Trabajadores/Historial/{escenario.TrabajadorId}")
            .ConfigureAwait(true);
        var htmlHistorial = await historial.Content.ReadAsStringAsync().ConfigureAwait(true);
        htmlHistorial.ShouldContain($"/Trabajadores/Reingresar/{escenario.TrabajadorId}");

        var respuesta = await escenario.Cliente
            .GetAsync($"/Trabajadores/Reingresar/{escenario.TrabajadorId}")
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true));
        html.ShouldContain("Reingresar trabajador");
        html.ShouldContain("Nueva fecha de ingreso");
        html.ShouldContain("Empresa");
        html.ShouldContain("Unidad inicial");
        html.ShouldNotContain("Turno rige desde");
    }

    [Fact]
    public async Task PL3EC_WEB_02_post_valido_redirige_al_historial()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionCoordinadorAsync().ConfigureAwait(true);
        var token = await escenario.TokenReingresoAsync().ConfigureAwait(true);

        var respuesta = await escenario.Cliente.PostAsync(
                "/Trabajadores/RegistrarReingreso",
                ContenidoReingreso(token, escenario.TrabajadorId, "2026-02-01", "2", "3"))
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        respuesta.Headers.Location?.OriginalString.ShouldBe(
            $"/Trabajadores/Historial/{escenario.TrabajadorId}");
        (await escenario.PeriodosAsync().ConfigureAwait(true)).Count.ShouldBe(2);

        var auditoria = await escenario.AuditoriaDelReingresoAsync().ConfigureAwait(true);
        auditoria.Select(a => a.Tabla).ShouldContain("trabajador");
        auditoria.Select(a => a.Tabla).ShouldContain("periodo_laboral");
        auditoria.Select(a => a.Tabla).ShouldContain("asignacion_planta");
        string.Join(' ', auditoria.Select(a => a.Detalle)).ShouldContain("EmpresaId");
        string.Join(' ', auditoria.Select(a => a.Detalle)).ShouldContain("PlantaId");
    }

    [Fact]
    public async Task PL3EC_WEB_03_post_trabajador_activo_muestra_error_controlado()
    {
        await using var escenario = await EscenarioMvc.CrearAsync(trabajadorActivo: true).ConfigureAwait(true);
        await escenario.IniciarSesionCoordinadorAsync().ConfigureAwait(true);
        var token = await escenario.TokenDeFormularioAsync("/Trabajadores/Crear").ConfigureAwait(true);

        var respuesta = await escenario.Cliente.PostAsync(
                "/Trabajadores/RegistrarReingreso",
                ContenidoReingreso(token, escenario.TrabajadorId, "2026-02-01", "2", "3"))
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true));
        html.ShouldContain("periodo laboral abierto", Case.Insensitive);
        (await escenario.PeriodosAsync().ConfigureAwait(true)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PL3EC_WEB_04_usuario_sin_rol_de_personal_es_bloqueado()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionGerenciaAsync().ConfigureAwait(true);

        var respuesta = await escenario.Cliente
            .GetAsync($"/Trabajadores/Reingresar/{escenario.TrabajadorId}")
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        respuesta.Headers.Location.ShouldNotBeNull();
        respuesta.Headers.Location!.OriginalString.ShouldContain("/Cuenta/AccesoDenegado");
    }

    [Fact]
    public async Task PL3EC_WEB_05_campos_invalidos_conservan_modelstate_y_no_persisten()
    {
        await using var escenario = await EscenarioMvc.CrearAsync().ConfigureAwait(true);
        await escenario.IniciarSesionCoordinadorAsync().ConfigureAwait(true);
        var token = await escenario.TokenReingresoAsync().ConfigureAwait(true);

        var respuesta = await escenario.Cliente.PostAsync(
                "/Trabajadores/RegistrarReingreso",
                ContenidoReingreso(token, escenario.TrabajadorId, string.Empty, "0", "0"))
            .ConfigureAwait(true);

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync().ConfigureAwait(true));
        html.ShouldContain("No se pudo registrar el reingreso");
        html.ShouldContain("Seleccione la empresa del nuevo vínculo");
        html.ShouldContain("Seleccione la unidad inicial del nuevo vínculo");
        (await escenario.PeriodosAsync().ConfigureAwait(true)).ShouldHaveSingleItem();
    }

    private static FormUrlEncodedContent ContenidoReingreso(
        string token,
        long trabajadorId,
        string fecha,
        string empresaId,
        string plantaId) =>
        new(
        [
            new("__RequestVerificationToken", token),
            new("Datos.TrabajadorId", trabajadorId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Datos.FechaIngreso", fecha),
            new("Datos.EmpresaId", empresaId),
            new("Datos.PlantaId", plantaId)
        ]);

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
        private const string CorreoCoordinador = "coordinador.reingreso@gca.local";
        private const string ClaveCoordinador = "WebMvc2026";
        private const string CorreoGerencia = "gerencia.reingreso@gca.local";
        private const string ClaveGerencia = "Gerencia2026";

        private readonly AplicacionWebFactory factory;

        private EscenarioMvc(AplicacionWebFactory factory, HttpClient cliente, long trabajadorId)
        {
            this.factory = factory;
            Cliente = cliente;
            TrabajadorId = trabajadorId;
        }

        public HttpClient Cliente { get; }

        public long TrabajadorId { get; }

        public static async Task<EscenarioMvc> CrearAsync(bool trabajadorActivo = false)
        {
            var factory = new AplicacionWebFactory();
            var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            await using var alcance = factory.Services.CreateAsyncScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>();
            var trabajador = new Trabajador
            {
                TipoDocumentoId = 1,
                NumeroDocumento = trabajadorActivo ? "41300322" : "41300321",
                ApellidoPaterno = "PRUEBA",
                ApellidoMaterno = "WEB",
                Nombres = "REINGRESO",
                Sexo = Sexo.Femenino,
                EmpresaId = 1,
                TipoPlanilla = TipoPlanilla.Obrero,
                PlantaId = 2,
                PuestoId = 1,
                FechaIngreso = new DateOnly(2025, 1, 1),
                CreadoPor = CorreoCoordinador
            };
            trabajador.AbrirVinculo(CorreoCoordinador);
            if (!trabajadorActivo)
            {
                trabajador.DarDeBaja(
                    new DateOnly(2025, 12, 31),
                    MotivoCese.RenunciaVoluntaria,
                    null,
                    CorreoCoordinador);
            }

            contexto.Trabajadores.Add(trabajador);
            await contexto.SaveChangesAsync().ConfigureAwait(true);

            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var gerencia = new ApplicationUser
            {
                UserName = CorreoGerencia,
                Email = CorreoGerencia,
                EmailConfirmed = true,
                NombreCompleto = "Gerencia sin acceso a Personal"
            };
            (await usuarios.CreateAsync(gerencia, ClaveGerencia).ConfigureAwait(true))
                .Succeeded.ShouldBeTrue();
            (await usuarios.AddToRoleAsync(gerencia, RolesSistema.Gerencia).ConfigureAwait(true))
                .Succeeded.ShouldBeTrue();

            return new EscenarioMvc(factory, cliente, trabajador.Id);
        }

        public Task IniciarSesionCoordinadorAsync() =>
            IniciarSesionAsync(CorreoCoordinador, ClaveCoordinador);

        public Task IniciarSesionGerenciaAsync() =>
            IniciarSesionAsync(CorreoGerencia, ClaveGerencia);

        public async Task<string> TokenReingresoAsync() =>
            await TokenDeFormularioAsync($"/Trabajadores/Reingresar/{TrabajadorId}")
                .ConfigureAwait(true);

        public async Task<string> TokenDeFormularioAsync(string ruta)
        {
            var formulario = await Cliente.GetAsync(ruta).ConfigureAwait(true);
            formulario.StatusCode.ShouldBe(HttpStatusCode.OK);
            return ExtraerAntiforgery(await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));
        }

        public async Task<List<PeriodoLaboral>> PeriodosAsync()
        {
            await using var alcance = factory.Services.CreateAsyncScope();
            return await alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>()
                .PeriodosLaborales
                .AsNoTracking()
                .Where(p => p.TrabajadorId == TrabajadorId)
                .OrderBy(p => p.FechaIngreso)
                .ToListAsync()
                .ConfigureAwait(true);
        }

        public async Task<List<AuditoriaReingreso>> AuditoriaDelReingresoAsync()
        {
            await using var alcance = factory.Services.CreateAsyncScope();
            return await alcance.ServiceProvider.GetRequiredService<MarcacionDbContext>()
                .RegistrosAuditoria
                .AsNoTracking()
                .Where(a => a.Usuario == CorreoCoordinador)
                .Select(a => new AuditoriaReingreso(a.Tabla, a.Detalle))
                .ToListAsync()
                .ConfigureAwait(true);
        }

        private async Task IniciarSesionAsync(string correo, string clave)
        {
            var formulario = await Cliente.GetAsync("/Cuenta/Ingresar").ConfigureAwait(true);
            formulario.StatusCode.ShouldBe(HttpStatusCode.OK);
            var token = ExtraerAntiforgery(await formulario.Content.ReadAsStringAsync().ConfigureAwait(true));

            var respuesta = await Cliente.PostAsync(
                    "/Cuenta/Ingresar",
                    new FormUrlEncodedContent(
                    [
                        new("__RequestVerificationToken", token),
                        new("Correo", correo),
                        new("Clave", clave),
                        new("Recordarme", "false"),
                        new("ReturnUrl", string.Empty)
                    ]))
                .ConfigureAwait(true);

            respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await factory.DisposeAsync().ConfigureAwait(true);
        }
    }

    private sealed record AuditoriaReingreso(string Tabla, string Detalle);

    private sealed class AplicacionWebFactory : WebApplicationFactory<Program>
    {
        private string? rutaBase;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            rutaBase ??= Path.Combine(
                Path.GetTempPath(),
                $"control-marcacion-reingreso-{Guid.NewGuid():N}.db");

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Seguridad:AdminCorreo"] = "coordinador.reingreso@gca.local",
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
