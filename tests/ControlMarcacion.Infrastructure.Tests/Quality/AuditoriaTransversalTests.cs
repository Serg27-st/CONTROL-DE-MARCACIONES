using System.Globalization;
using System.Text.Json;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Quality;

/// <summary>
/// Contrato transversal AUD-001 sobre SQLite real: qué se incluye en la bitácora y qué
/// dato derivado se resume mediante telemetría en vez de auditarse fila por fila.
/// </summary>
public sealed class AuditoriaTransversalTests : IDisposable
{
    private const string Actor = "auditor-prueba";
    private const string Motivo = "Otorgamiento registrado con fecha equivocada";
    private const long PeriodoJulio2026 = 1;

    private static readonly DateOnly Feriado = new(2026, 7, 28);
    private static readonly DateOnly Descanso = new(2026, 7, 30);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly long trabajadorId;

    public AuditoriaTransversalTests()
    {
        conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();

        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "PRAGMA foreign_keys = ON";
            comando.ExecuteNonQuery();
        }

        contexto = CrearContexto();
        contexto.Database.EnsureCreated();
        trabajadorId = PrepararTrabajadorAsync().GetAwaiter().GetResult();

        // El alta del trabajador sirve solo como precondición del contrato. La prueba
        // comienza con una bitácora vacía para poder atribuir cada fila al acto observado.
        contexto.RegistrosAuditoria.ExecuteDelete();
        contexto.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task AUD001_01_operacion_sensible_persiste_identidad_actor_fecha_y_motivo()
    {
        var desde = DateTime.Now.AddSeconds(-1);
        var descanso = DescansoSustitutorio.Otorgar(
            trabajadorId, Feriado, Descanso, Actor, DateTime.Now);

        contexto.DescansosSustitutorios.Add(descanso);
        await contexto.SaveChangesAsync();

        var alta = await contexto.RegistrosAuditoria
            .AsNoTracking()
            .SingleAsync(r => r.Tabla == "descanso_sustitutorio"
                              && r.ClaveRegistro == descanso.Id.ToString(CultureInfo.InvariantCulture)
                              && r.Operacion == OperacionAuditada.Insercion);

        alta.Usuario.ShouldBe(Actor);
        alta.FechaHora.ShouldBeGreaterThanOrEqualTo(desde);
        alta.FechaHora.ShouldBeLessThanOrEqualTo(DateTime.Now.AddSeconds(1));
        Cambio(alta.Detalle, nameof(DescansoSustitutorio.TrabajadorId))
            .GetProperty("Nuevo").GetString()
            .ShouldBe(trabajadorId.ToString(CultureInfo.InvariantCulture));

        descanso.Anular(Motivo, Actor, DateTime.Now);
        await contexto.SaveChangesAsync();

        var baja = await contexto.RegistrosAuditoria
            .AsNoTracking()
            .SingleAsync(r => r.Tabla == "descanso_sustitutorio"
                              && r.ClaveRegistro == descanso.Id.ToString(CultureInfo.InvariantCulture)
                              && r.Operacion == OperacionAuditada.BajaLogica);

        baja.Usuario.ShouldBe(Actor);
        baja.FechaHora.ShouldBeGreaterThanOrEqualTo(alta.FechaHora);

        var cambioEstado = Cambio(baja.Detalle, nameof(DescansoSustitutorio.Estado));
        cambioEstado.GetProperty("Anterior").GetString().ShouldBe("Otorgado");
        cambioEstado.GetProperty("Nuevo").GetString().ShouldBe("Anulado");

        var cambioMotivo = Cambio(baja.Detalle, nameof(DescansoSustitutorio.MotivoAnulacion));
        cambioMotivo.GetProperty("Anterior").ValueKind.ShouldBe(JsonValueKind.Null);
        cambioMotivo.GetProperty("Nuevo").GetString().ShouldBe(Motivo);
    }

    [Fact]
    public async Task AUD001_02_recalculo_excluye_tareo_por_fila_y_emite_un_log_agregado()
    {
        var logger = new CapturadorLogger<ServicioCalculoTareo>();
        var servicio = new ServicioCalculoTareo(
            contexto,
            new AlcanceSinRestriccion(),
            CoberturaDePrueba.TodoCargado(),
            logger);

        var resultado = await servicio.RecalcularPeriodoAsync(
            PeriodoJulio2026, Actor, CancellationToken.None);

        resultado.DiasCalculados.ShouldBeGreaterThan(0);
        (await contexto.TareosDiarios.AsNoTracking().CountAsync(t => t.TrabajadorId == trabajadorId))
            .ShouldBeGreaterThan(0);

        (await contexto.RegistrosAuditoria.AsNoTracking()
                .CountAsync(r => r.Tabla == "tareo_diario"))
            .ShouldBe(0);

        var agregado = logger.Entradas.Single(e => e.EventId.Id == 4200);
        agregado.Nivel.ShouldBe(LogLevel.Information);
        agregado.Mensaje.ShouldContain(resultado.PeriodoCodigo);
        agregado.Mensaje.ShouldContain(Actor);
        agregado.Mensaje.ShouldContain("1 trabajadores");
        agregado.Mensaje.ShouldContain("días");
        agregado.Mensaje.ShouldContain("pendientes de validar");
    }

    private MarcacionDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .AddInterceptors(new InterceptorAuditoria(new UsuarioActualDePrueba()))
            .Options);

    private async Task<long> PrepararTrabajadorAsync()
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = "70000001",
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "AUDITORIA",
            Nombres = "TRANSVERSAL",
            Sexo = Sexo.Masculino,
            EmpresaId = 1,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 1,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2026, 1, 1),
            CreadoPor = Actor
        };

        trabajador.AbrirVinculo(Actor);
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador.Id;
    }

    private static JsonElement Cambio(string detalle, string campo)
    {
        using var documento = JsonDocument.Parse(detalle);
        return documento.RootElement
            .EnumerateArray()
            .Single(c => c.GetProperty("Campo").GetString() == campo)
            .Clone();
    }

    private sealed class UsuarioActualDePrueba : IProveedorUsuarioActual
    {
        public string Usuario => Actor;
    }

    private sealed class CapturadorLogger<T> : ILogger<T>
    {
        public List<EntradaLog> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entradas.Add(new EntradaLog(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record EntradaLog(LogLevel Nivel, EventId EventId, string Mensaje);
}
