using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Seguridad;

public sealed class PreparacionTareoAutorizacionHttpTests
{
    private const string UsuarioId = "coord-catready-restringido";
    private const string Visible = "VISIBLE UNIDAD UNO";
    private const string Ajeno = "SECRETO UNIDAD DOS";
    private const string DocumentoAjeno = "93999992";

    [Fact]
    public async Task SEC_CATREADY_11_html_no_expone_identificadores_ni_diagnosticos_ajenos()
    {
        await using var factory = new AplicacionWebFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SembrarEscenarioAsync();

        var respuesta = await cliente.GetAsync("/PreparacionTareo?periodoId=9901");
        var html = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain(Visible);
        html.ShouldNotContain(Ajeno);
        html.ShouldNotContain(DocumentoAjeno);
    }

    [Fact]
    public async Task SEC_CATREADY_14_filtro_no_autorizado_responde_vacio_sin_revelar_datos()
    {
        await using var factory = new AplicacionWebFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await factory.SembrarEscenarioAsync();

        var respuesta = await cliente.GetAsync(
            "/PreparacionTareo?periodoId=9901&plantaId=2&estado=0&operacion=0");
        var html = await respuesta.Content.ReadAsStringAsync();

        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldNotContain(Ajeno);
        html.ShouldNotContain(DocumentoAjeno);
        html.ShouldNotContain(Visible);
        html.ShouldContain("0");
    }

    private sealed class AplicacionWebFactory : WebApplicationFactory<Program>
    {
        private string? rutaBase;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            rutaBase ??= Path.Combine(
                Path.GetTempPath(),
                $"control-marcacion-catready-sec-{Guid.NewGuid():N}.db");

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Seguridad:AdminCorreo"] = "bootstrap.catready@prueba.local",
                    ["Seguridad:AdminClave"] = "Bootstrap2026",
                    ["Despliegue:HostingLocal"] = "false",
                    ["Despliegue:AbrirNavegador"] = "false",
                    ["Urls"] = "http://127.0.0.1:0",
                    ["Logging:LogLevel:Default"] = "Warning"
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
            builder.ConfigureTestServices(servicios =>
            {
                servicios.AddAuthentication(opciones =>
                    {
                        opciones.DefaultAuthenticateScheme = AutenticacionPruebaHandler.Esquema;
                        opciones.DefaultChallengeScheme = AutenticacionPruebaHandler.Esquema;
                    })
                    .AddScheme<AuthenticationSchemeOptions, AutenticacionPruebaHandler>(
                        AutenticacionPruebaHandler.Esquema,
                        _ => { });
            });
        }

        public async Task SembrarEscenarioAsync()
        {
            using var scope = Services.CreateScope();
            var contexto = scope.ServiceProvider.GetRequiredService<MarcacionDbContext>();

            contexto.Users.Add(new ApplicationUser
            {
                Id = UsuarioId,
                UserName = "coord.catready@prueba.local",
                NormalizedUserName = "COORD.CATREADY@PRUEBA.LOCAL",
                Email = "coord.catready@prueba.local",
                NormalizedEmail = "COORD.CATREADY@PRUEBA.LOCAL",
                NombreCompleto = "COORDINADOR CATREADY",
                Activo = true
            });
            contexto.UsuariosPlanta.Add(new UsuarioPlanta
            {
                UsuarioId = UsuarioId,
                PlantaId = 1
            });
            contexto.Periodos.Add(new Periodo
            {
                Id = 9901,
                Codigo = "SEC-2026-07",
                Nombre = "Seguridad julio 2026",
                FechaInicio = new DateOnly(2026, 7, 1),
                FechaFin = new DateOnly(2026, 7, 31)
            });
            contexto.Trabajadores.AddRange(
                TrabajadorDePrueba(1, "93999991", Visible),
                TrabajadorDePrueba(2, DocumentoAjeno, Ajeno));
            await contexto.SaveChangesAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
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

        private static Trabajador TrabajadorDePrueba(long plantaId, string documento, string apellido)
        {
            var trabajador = new Trabajador
            {
                TipoDocumentoId = 1,
                NumeroDocumento = documento,
                ApellidoPaterno = apellido,
                ApellidoMaterno = "CATREADY",
                Nombres = "PRUEBA",
                Sexo = Sexo.Femenino,
                EmpresaId = 1,
                PlantaId = plantaId,
                TipoPlanilla = TipoPlanilla.Empleado,
                PuestoId = 1,
                FechaIngreso = new DateOnly(2026, 7, 1)
            };
            var periodo = new PeriodoLaboral
            {
                EmpresaId = 1,
                FechaIngreso = new DateOnly(2026, 7, 1)
            };
            periodo.AsignacionesPlanta.Add(new AsignacionPlanta
            {
                PlantaId = plantaId,
                FechaInicio = new DateOnly(2026, 7, 1)
            });
            trabajador.PeriodosLaborales.Add(periodo);
            return trabajador;
        }
    }

    private sealed class AutenticacionPruebaHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
    {
        public const string Esquema = "CATREADY-PRUEBA";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UsuarioId),
                new Claim(ClaimTypes.Name, "coord.catready@prueba.local"),
                new Claim(ClaimTypes.Role, RolesSistema.Coordinador)
            ], Esquema);
            var principal = new ClaimsPrincipal(identidad);
            var ticket = new AuthenticationTicket(principal, Esquema);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
