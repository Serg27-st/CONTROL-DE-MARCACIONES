using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

public sealed class DescansoSustitutorioPersistenceTests : IDisposable
{
    private static readonly DateOnly Feriado = new(2026, 7, 28);
    private static readonly DateOnly Descanso = new(2026, 7, 30);
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private long trabajadorId;

    public DescansoSustitutorioPersistenceTests()
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
        PrepararEscenarioAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task FER003_01_sin_sustitutorio_conserva_base_y_recargo()
    {
        var liquidacion = await CrearServicioCalculo(contexto).LiquidarPeriodoAsync(1);

        var concepto = liquidacion!.Conceptos.Single(c =>
            c.Concepto == Domain.Normativa.ConceptoSobretasa.FeriadoTrabajado);
        concepto.Minutos.ShouldBe(480);
        concepto.MinutosBaseEconomica.ShouldBe(480m);
        concepto.MinutosRecargo.ShouldBe(480m);
    }

    [Fact]
    public async Task FER003_02_otorgamiento_valido_suprime_adicional_sin_borrar_minutos_fisicos()
    {
        var servicio = CrearServicio(contexto);
        await servicio.OtorgarAsync(Solicitud(), "coordinador");

        var tareo = await contexto.TareosDiarios.AsNoTracking().SingleAsync(t =>
            t.TrabajadorId == trabajadorId && t.Fecha == Feriado);
        tareo.MinutosTrabajados.ShouldBe(480);

        var liquidacion = await CrearServicioCalculo(contexto).LiquidarPeriodoAsync(1);
        liquidacion!.Conceptos.Single(c =>
                c.Concepto == Domain.Normativa.ConceptoSobretasa.FeriadoTrabajado)
            .Minutos.ShouldBe(0);

        var listado = await CrearServicioCalculo(contexto).ListarPorTrabajadorAsync(
            new FiltroTareo { PeriodoId = 1, TrabajadorId = trabajadorId });
        var fila = listado.Elementos.Single().Dias.Single(d => d.Fecha == Feriado);
        fila.TieneDescansoSustitutorio.ShouldBeTrue();
        fila.FechaDescansoSustitutorio.ShouldBe(Descanso);
    }

    [Fact]
    public async Task FER003_05_duplicado_activo_es_rechazado_por_servicio_y_base()
    {
        var servicio = CrearServicio(contexto);
        await servicio.OtorgarAsync(Solicitud(), "coordinador");

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.OtorgarAsync(Solicitud() with { FechaDescanso = Descanso.AddDays(1) }, "otro"));

        contexto.ChangeTracker.Clear();
        contexto.DescansosSustitutorios.Add(DescansoSustitutorio.Otorgar(
            trabajadorId, Feriado, Descanso.AddDays(2), "concurrente", DateTime.Now));
        await Should.ThrowAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
    }

    [Fact]
    public async Task FER003_06_trabajador_sin_vinculo_en_origen_es_rechazado()
    {
        var sinVinculo = await CrearTrabajadorAsync("87654321", crearVinculo: false);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CrearServicio(contexto).OtorgarAsync(
                Solicitud() with { TrabajadorId = sinVinculo }, "coordinador"));

        error.Message.ShouldContain("vínculo laboral vigente");
        await contexto.DescansosSustitutorios.AsNoTracking()
            .CountAsync(d => d.TrabajadorId == sinVinculo)
            .ShouldBeAsync(0);
    }

    [Fact]
    public async Task FER003_07_anulacion_restaura_economia_y_conserva_fila()
    {
        var servicio = CrearServicio(contexto);
        var id = await servicio.OtorgarAsync(Solicitud(), "coordinador");
        var version = (await servicio.ListarAsync()).Single(d => d.Id == id).Version;

        await servicio.AnularAsync(new AnularDescansoSustitutorioRequest
        {
            Id = id,
            Version = version,
            Motivo = "Fecha concedida por error"
        }, "administrador");

        var fila = await contexto.DescansosSustitutorios.AsNoTracking().SingleAsync(d => d.Id == id);
        fila.Estado.ShouldBe(EstadoDescansoSustitutorio.Anulado);
        fila.Activo.ShouldBeFalse();

        var liquidacion = await CrearServicioCalculo(contexto).LiquidarPeriodoAsync(1);
        liquidacion!.Conceptos.Single(c =>
                c.Concepto == Domain.Normativa.ConceptoSobretasa.FeriadoTrabajado)
            .Minutos.ShouldBe(480);
    }

    [Fact]
    public async Task fk_invalida_revierte_todo_el_guardado()
    {
        contexto.ChangeTracker.Clear();
        contexto.DescansosSustitutorios.Add(DescansoSustitutorio.Otorgar(
            999_999, Feriado, Descanso, "coordinador", DateTime.Now));

        await Should.ThrowAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        contexto.ChangeTracker.Clear();
        (await contexto.DescansosSustitutorios.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task periodo_cerrado_no_deja_escritura_parcial()
    {
        var periodo = await contexto.Periodos.SingleAsync(p => p.Id == 1);
        periodo.Estado = Domain.Catalogos.EstadoPeriodo.Cerrado;
        await contexto.SaveChangesAsync();
        contexto.ChangeTracker.Clear();

        await Should.ThrowAsync<PeriodoCerradoException>(() =>
            CrearServicio(contexto).OtorgarAsync(Solicitud(), "coordinador"));

        (await contexto.DescansosSustitutorios.AsNoTracking().CountAsync()).ShouldBe(0);
    }

    private RegistrarDescansoSustitutorioRequest Solicitud() => new()
    {
        TrabajadorId = trabajadorId,
        FechaFeriadoOrigen = Feriado,
        FechaDescanso = Descanso
    };

    private async Task PrepararEscenarioAsync()
    {
        trabajadorId = await CrearTrabajadorAsync("12345678", crearVinculo: true);
        contexto.TareosDiarios.Add(new TareoDiario
        {
            PeriodoId = 1,
            TrabajadorId = trabajadorId,
            Fecha = Feriado,
            MinutosTrabajados = 480,
            TipoDiaNoLaborable = TipoDiaNoLaborable.Feriado,
            Estado = EstadoTareo.Feriado,
            CalculadoEn = DateTime.Now
        });
        await contexto.SaveChangesAsync();
    }

    private async Task<long> CrearTrabajadorAsync(string documento, bool crearVinculo)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "FERIADO",
            Nombres = documento,
            Sexo = Sexo.Masculino,
            EmpresaId = 1,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 1,
            FechaIngreso = new DateOnly(2026, 1, 1),
            PuestoId = 1,
            Activo = true
        };

        if (crearVinculo)
        {
            trabajador.PeriodosLaborales.Add(new PeriodoLaboral
            {
                EmpresaId = 1,
                FechaIngreso = new DateOnly(2026, 1, 1),
                AsignacionesPlanta =
                [
                    new AsignacionPlanta
                    {
                        PlantaId = 1,
                        FechaInicio = new DateOnly(2026, 1, 1)
                    }
                ]
            });
        }

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador.Id;
    }

    private MarcacionDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);

    private static ServicioDescansosSustitutorios CrearServicio(MarcacionDbContext db) =>
        new(db, new AlcanceSinRestriccion());

    private static ServicioCalculoTareo CrearServicioCalculo(MarcacionDbContext db) =>
        new(db, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
}

internal static class ShouldlyTaskExtensions
{
    public static async Task ShouldBeAsync<T>(this Task<T> actual, T esperado)
    {
        (await actual).ShouldBe(esperado);
    }
}
