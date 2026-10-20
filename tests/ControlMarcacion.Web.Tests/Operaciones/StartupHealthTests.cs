using System.Net;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Seguridad;
using ControlMarcacion.Web.Operaciones;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Operaciones;

public sealed class StartupHealthTests
{
    [Fact]
    public void Hosting_local_usa_puerto_preferido_fallback_consecutivo_y_loopback_ipv4()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Despliegue:PuertoPreferido"] = "5000"
            })
            .Build();

        var preferido = HostingLocalWindows.ResolverPuertoPreferido(configuracion);

        preferido.ShouldBe(5000);
        HostingLocalWindows.SiguientePuerto(preferido).ShouldBe(5001);
        HostingLocalWindows.CrearUrl(5001).ShouldBe("http://127.0.0.1:5001");
    }

    [Fact]
    public void Hosting_local_reconoce_AddressInUse_anidado_de_Kestrel()
    {
        var excepcion = new IOException(
            "Fallo de bind",
            new AddressInUseException("El puerto ya está en uso"));

        HostingLocalWindows.EsDireccionEnUso(excepcion).ShouldBeTrue();
        HostingLocalWindows.EsDireccionEnUso(new IOException("Otro fallo")).ShouldBeFalse();
    }

    [Fact]
    public async Task Hosting_local_no_declara_listo_si_estado_responde_503()
    {
        using var cliente = new HttpClient(
            new RespuestasInicioHandler(HttpStatusCode.ServiceUnavailable));

        var resultado = await HostingLocalWindows
            .SondearAsync(cliente, "http://127.0.0.1:5000", CancellationToken.None)
            .ConfigureAwait(true);

        resultado.Vivo.ShouldBeTrue();
        resultado.Listo.ShouldBeFalse();
        resultado.EstadoHttp.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Hosting_local_solo_declara_listo_con_marca_completa()
    {
        using var cliente = new HttpClient(new RespuestasInicioHandler(HttpStatusCode.OK));

        var resultado = await HostingLocalWindows
            .SondearAsync(cliente, "http://127.0.0.1:5000", CancellationToken.None)
            .ConfigureAwait(true);

        resultado.Vivo.ShouldBeTrue();
        resultado.Listo.ShouldBeTrue();
        resultado.Detalle.ShouldBe("LISTO");
    }

    [Fact]
    public async Task Hosting_local_reconoce_configuracion_inicial_sin_esperar_timeout()
    {
        using var cliente = new HttpClient(new RespuestasInicioHandler(
            HttpStatusCode.ServiceUnavailable,
            configuracionInicial: true));

        var resultado = await HostingLocalWindows
            .SondearAsync(cliente, "http://127.0.0.1:5000", CancellationToken.None)
            .ConfigureAwait(true);

        resultado.Vivo.ShouldBeTrue();
        resultado.Listo.ShouldBeFalse();
        resultado.RequiereConfiguracion.ShouldBeTrue();
        resultado.Estado.ShouldBe(EstadoInicioLocal.SetupRequired);
    }

    [Fact]
    public void Instancia_local_registra_pid_puerto_y_timestamp_y_rechaza_pid_inexistente()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), $"cm-instancia-{Guid.NewGuid():N}");
        var ruta = Path.Combine(carpeta, "instancia.json");

        try
        {
            InstanciaLocalWindows.Registrar(ruta, 5007);
            var estado = InstanciaLocalWindows.Leer(ruta);

            estado.ShouldNotBeNull();
            estado.Pid.ShouldBe(Environment.ProcessId);
            estado.Puerto.ShouldBe(5007);
            estado.IniciadaUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
            InstanciaLocalWindows.ProcesoExiste(estado).ShouldBeTrue();
            InstanciaLocalWindows.ProcesoExiste(
                new EstadoInstanciaLocal(int.MaxValue, 5007, DateTimeOffset.UtcNow)).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(carpeta))
            {
                Directory.Delete(carpeta, true);
            }
        }
    }

    [Fact]
    public void Logs_locales_rotan_por_dia_y_tamanio_con_sufijo_secuencial()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), $"cm-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        var fecha = new DateOnly(2026, 10, 12);
        var baseLog = Path.Combine(carpeta, "ControlMarcacion-20261012.log");
        var primerRollover = Path.Combine(carpeta, "ControlMarcacion-20261012-001.log");

        try
        {
            File.WriteAllText(baseLog, "12345");
            RegistroArchivoLocal.ResolverArchivoActual(carpeta, fecha, 5)
                .ShouldBe(primerRollover);

            File.WriteAllText(primerRollover, "12345");
            RegistroArchivoLocal.ResolverArchivoActual(carpeta, fecha, 5)
                .ShouldBe(Path.Combine(carpeta, "ControlMarcacion-20261012-002.log"));
            RegistroArchivoLocal.ResolverArchivoActual(
                    carpeta,
                    fecha.AddDays(1),
                    RegistroArchivoLocal.TamanioMaximoBytes)
                .ShouldBe(Path.Combine(carpeta, "ControlMarcacion-20261013.log"));
        }
        finally
        {
            Directory.Delete(carpeta, true);
        }
    }

    [Fact]
    public void Logs_locales_eliminan_solo_logs_propios_mayores_a_30_dias()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), $"cm-retencion-{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        var antiguo = Path.Combine(carpeta, "ControlMarcacion-20260801.log");
        var reciente = Path.Combine(carpeta, "ControlMarcacion-20261012.log");
        var backup = Path.Combine(carpeta, "ControlMarcacion.db.bak");

        try
        {
            File.WriteAllText(antiguo, "antiguo");
            File.WriteAllText(reciente, "reciente");
            File.WriteAllText(backup, "no tocar");
            File.SetLastWriteTimeUtc(antiguo, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(reciente, new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(backup, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

            RegistroArchivoLocal.LimpiarExpirados(
                    carpeta,
                    new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc))
                .ShouldBe(1);

            File.Exists(antiguo).ShouldBeFalse();
            File.Exists(reciente).ShouldBeTrue();
            File.Exists(backup).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(carpeta, true);
        }
    }

    [Fact]
    public async Task Fallo_de_preparacion_deja_proceso_vivo_pero_readiness_en_503()
    {
        await using var factory = new AplicacionConMigracionFallidaFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var vivo = await cliente.GetAsync("/vivo").ConfigureAwait(true);
        var estado = await cliente.GetAsync("/estado").ConfigureAwait(true);
        var funcional = await cliente.GetAsync("/").ConfigureAwait(true);

        vivo.StatusCode.ShouldBe(HttpStatusCode.OK);
        vivo.Headers.CacheControl.ShouldNotBeNull();
        vivo.Headers.CacheControl!.NoStore.ShouldBeTrue();
        (await vivo.Content.ReadAsStringAsync().ConfigureAwait(true)).ShouldContain(Marcas.Sistema);
        estado.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        estado.Headers.CacheControl.ShouldNotBeNull();
        estado.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var cuerpo = await estado.Content.ReadAsStringAsync().ConfigureAwait(true);
        cuerpo.ShouldContain("ERROR");
        cuerpo.ShouldContain("MIGRACIONES_PENDIENTES");
        cuerpo.ShouldNotContain(PreparadorFallido.MensajeInterno);
        funcional.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await funcional.Content.ReadAsStringAsync().ConfigureAwait(true))
            .ShouldContain("INICIALIZACION_NO_COMPLETA");
    }

    [Fact]
    public async Task Readiness_se_recupera_sin_reiniciar_cuando_la_verificacion_vuelve_a_lista()
    {
        await using var factory = new AplicacionConMigracionFallidaFactory();
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        (await cliente.GetAsync("/estado").ConfigureAwait(true))
            .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        factory.Verificador.Listo = true;

        var recuperado = await cliente.GetAsync("/estado").ConfigureAwait(true);
        recuperado.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await recuperado.Content.ReadAsStringAsync().ConfigureAwait(true)).ShouldContain("LISTO");

        var funcional = await cliente.GetAsync("/").ConfigureAwait(true);
        funcional.StatusCode.ShouldNotBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Migracion_pendiente_pasa_a_setup_required_al_aplicarla_sin_usuarios()
    {
        await using var escenario = await EscenarioSqlite.CrearAsync().ConfigureAwait(true);
        var migraciones = escenario.Contexto.Database.GetMigrations().ToList();
        migraciones.Count.ShouldBeGreaterThan(1);

        var migrador = escenario.Contexto.GetService<IMigrator>();
        await migrador
            .MigrateAsync(migraciones[^2], CancellationToken.None)
            .ConfigureAwait(true);

        var pendiente = await escenario.Verificador
            .VerificarAsync(CancellationToken.None)
            .ConfigureAwait(true);
        pendiente.Listo.ShouldBeFalse();
        pendiente.Codigo.ShouldBe("MIGRACIONES_PENDIENTES");

        await migrador.MigrateAsync(null, CancellationToken.None).ConfigureAwait(true);

        var recuperado = await escenario.Verificador
            .VerificarAsync(CancellationToken.None)
            .ConfigureAwait(true);
        recuperado.Listo.ShouldBeFalse();
        recuperado.RequiereConfiguracion.ShouldBeTrue();
        recuperado.Codigo.ShouldBe("CONFIGURACION_INICIAL_REQUERIDA");
    }

    [Fact]
    public async Task Esquema_compatible_con_usuario_esta_listo()
    {
        await using var escenario = await EscenarioSqlite.CrearAsync().ConfigureAwait(true);
        await escenario.Contexto.Database.MigrateAsync().ConfigureAwait(true);
        escenario.Contexto.Set<ApplicationUser>().Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "coordinador@example.invalid",
            NormalizedUserName = "COORDINADOR@EXAMPLE.INVALID",
            Email = "coordinador@example.invalid",
            NormalizedEmail = "COORDINADOR@EXAMPLE.INVALID",
            NombreCompleto = "Coordinador",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            Activo = true
        });
        await escenario.Contexto.SaveChangesAsync().ConfigureAwait(true);

        var resultado = await escenario.Verificador
            .VerificarAsync(CancellationToken.None)
            .ConfigureAwait(true);

        resultado.Listo.ShouldBeTrue();
        resultado.Estado.ShouldBe(EstadoOperativo.Ready);
        resultado.Codigo.ShouldBe("LISTO");
    }

    [Fact]
    public async Task Historial_completo_con_tabla_requerida_ausente_no_esta_listo()
    {
        await using var escenario = await EscenarioSqlite.CrearAsync().ConfigureAwait(true);
        await escenario.Contexto.Database.MigrateAsync().ConfigureAwait(true);
        await escenario.Contexto.Database
            .ExecuteSqlRawAsync("ALTER TABLE trabajador RENAME TO trabajador_ausente;")
            .ConfigureAwait(true);

        var resultado = await escenario.Verificador
            .VerificarAsync(CancellationToken.None)
            .ConfigureAwait(true);

        resultado.Listo.ShouldBeFalse();
        resultado.Codigo.ShouldBe("ESQUEMA_INCOMPATIBLE");
    }

    [Fact]
    public void Error_visual_indica_ruta_de_logs_sin_stack_trace()
    {
        var mensaje = DialogoInicioWindows.CrearMensaje(@"C:\ProgramData\ControlMarcacion\logs");

        mensaje.ShouldContain(@"C:\ProgramData\ControlMarcacion\logs");
        mensaje.ShouldContain("no pudo iniciarse");
        mensaje.ShouldNotContain("System.");
    }

    private sealed class RespuestasInicioHandler(
        HttpStatusCode estadoReadiness,
        bool configuracionInicial = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var respuesta = request.RequestUri?.AbsolutePath == "/vivo"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Marcas.Sistema)
                }
                : new HttpResponseMessage(estadoReadiness)
                {
                    Content = new StringContent(
                        estadoReadiness == HttpStatusCode.OK
                            ? $"{Marcas.Sistema};LISTO"
                            : configuracionInicial
                                ? "{\"sistema\":\"ControlMarcaciones-OK\",\"estado\":\"CONFIGURACION_INICIAL_REQUERIDA\"}"
                                : "{\"sistema\":\"ControlMarcaciones-OK\",\"estado\":\"ERROR\"}")
                };
            return Task.FromResult(respuesta);
        }
    }

    private sealed class AplicacionConMigracionFallidaFactory : WebApplicationFactory<Program>
    {
        private readonly string rutaBase = Path.Combine(
            Path.GetTempPath(),
            $"control-marcacion-startup-health-{Guid.NewGuid():N}.db");

        public VerificadorControlado Verificador { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuracion) =>
            {
                configuracion.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BaseDatos"] = $"Data Source={rutaBase}",
                    ["Despliegue:HostingLocal"] = "false",
                    ["Despliegue:AbrirNavegador"] = "false",
                    ["Urls"] = "http://127.0.0.1:0",
                    ["Logging:LogLevel:Default"] = "Critical"
                });
            });
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<IPreparadorAplicacion>();
                servicios.AddScoped<IPreparadorAplicacion, PreparadorFallido>();
                servicios.RemoveAll<IVerificadorEstadoOperativo>();
                servicios.AddSingleton<IVerificadorEstadoOperativo>(Verificador);
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(true);
            SqliteConnection.ClearAllPools();

            foreach (var ruta in new[] { rutaBase, $"{rutaBase}-wal", $"{rutaBase}-shm" })
            {
                if (File.Exists(ruta))
                {
                    File.Delete(ruta);
                }
            }
        }
    }

    private sealed class PreparadorFallido : IPreparadorAplicacion
    {
        public const string MensajeInterno = "detalle interno simulado que no debe exponerse";

        public Task PrepararAsync(CancellationToken cancelacion) =>
            Task.FromException(new InvalidOperationException(MensajeInterno));
    }

    internal sealed class VerificadorControlado : IVerificadorEstadoOperativo
    {
        public bool Listo { get; set; }

        public Task<ResultadoEstadoOperativo> VerificarAsync(CancellationToken cancelacion) =>
            Task.FromResult(
                Listo
                    ? ResultadoEstadoOperativo.Preparado()
                    : ResultadoEstadoOperativo.NoPreparado("MIGRACIONES_PENDIENTES"));
    }

    private sealed class EscenarioSqlite : IAsyncDisposable
    {
        private EscenarioSqlite(SqliteConnection conexion, MarcacionDbContext contexto)
        {
            Conexion = conexion;
            Contexto = contexto;
            Verificador = new VerificadorEstadoOperativo(contexto);
        }

        private SqliteConnection Conexion { get; }

        public MarcacionDbContext Contexto { get; }

        public VerificadorEstadoOperativo Verificador { get; }

        public static async Task<EscenarioSqlite> CrearAsync()
        {
            var conexion = new SqliteConnection("Data Source=:memory:");
            await conexion.OpenAsync().ConfigureAwait(true);
            var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
                .UseSqlite(conexion)
                .Options;
            return new EscenarioSqlite(conexion, new MarcacionDbContext(opciones));
        }

        public async ValueTask DisposeAsync()
        {
            await Contexto.DisposeAsync().ConfigureAwait(true);
            await Conexion.DisposeAsync().ConfigureAwait(true);
        }
    }
}
