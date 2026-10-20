using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>
/// Contratos de escritura de P1-DATA-PERIODOLABORAL Fase 3D sobre el motor SQLite real.
/// </summary>
public sealed class PeriodoLaboralFase3DEscriturasSqliteTests : IDisposable
{
    private const long EmpresaInicialId = 1;
    private const long PlantaInicialId = 2;
    private const long EmpresaReingresoId = 2;
    private const long PlantaReingresoId = 3;
    private const long PuestoId = 1;
    private const long TipoDocumentoId = 1;

    private static readonly DateOnly IngresoInicial = new(2026, 3, 1);
    private static readonly DateOnly CeseInicial = new(2026, 9, 30);
    private static readonly DateOnly Reingreso = new(2027, 2, 1);

    private readonly SqliteConnection conexion;

    public PeriodoLaboralFase3DEscriturasSqliteTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        using var comando = conexion.CreateCommand();
        comando.CommandText = "PRAGMA foreign_keys = ON";
        comando.ExecuteNonQuery();

        using var contexto = CrearContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => conexion.Dispose();

    [Fact]
    public async Task PL3D_WRITE_01_alta_cese_y_reingreso_preservan_historia_y_proyeccion_actual()
    {
        var id = await CrearYCesarAsync("41300301");

        await ReingresarAsync(id);

        await using var verificacion = CrearContexto();
        var trabajador = await verificacion.Trabajadores
            .AsNoTracking()
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .SingleAsync(t => t.Id == id);

        var periodos = trabajador.PeriodosLaborales.OrderBy(p => p.FechaIngreso).ToList();
        periodos.Count.ShouldBe(2);

        periodos[0].EmpresaId.ShouldBe(EmpresaInicialId);
        periodos[0].FechaIngreso.ShouldBe(IngresoInicial);
        periodos[0].FechaCese.ShouldBe(CeseInicial);
        periodos[0].AsignacionesPlanta.ShouldHaveSingleItem().PlantaId.ShouldBe(PlantaInicialId);

        periodos[1].EmpresaId.ShouldBe(EmpresaReingresoId);
        periodos[1].FechaIngreso.ShouldBe(Reingreso);
        periodos[1].FechaCese.ShouldBeNull();
        periodos[1].AsignacionesPlanta.ShouldHaveSingleItem().PlantaId.ShouldBe(PlantaReingresoId);

        trabajador.EmpresaId.ShouldBe(EmpresaReingresoId);
        trabajador.PlantaId.ShouldBe(PlantaReingresoId);
        trabajador.FechaIngreso.ShouldBe(Reingreso);
        trabajador.FechaCese.ShouldBeNull();

        await VerificarIntegridadSqliteAsync();
    }

    [Fact]
    public async Task PL3D_WRITE_02_editar_inicio_abierto_no_puede_solaparlo_con_historia_cerrada()
    {
        var id = await CrearYCesarAsync("41300302");
        await ReingresarAsync(id);

        await using (var contexto = CrearContexto())
        {
            var solicitud = Ficha("41300302", EmpresaReingresoId, PlantaReingresoId, CeseInicial);
            var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
                CrearServicio(contexto).ActualizarAsync(solicitud with { Id = id }, "tester"));

            error.Message.ShouldContain("solapados", Case.Insensitive);
        }

        await using var verificacion = CrearContexto();
        var periodos = await verificacion.PeriodosLaborales
            .AsNoTracking()
            .Where(p => p.TrabajadorId == id)
            .OrderBy(p => p.FechaIngreso)
            .ToListAsync();

        periodos.Count.ShouldBe(2);
        periodos[0].FechaIngreso.ShouldBe(IngresoInicial);
        periodos[0].FechaCese.ShouldBe(CeseInicial);
        periodos[1].FechaIngreso.ShouldBe(Reingreso);
        periodos[1].FechaCese.ShouldBeNull();
    }

    [Fact]
    public async Task PL3D_WRITE_03_editar_ficha_actual_no_reescribe_periodo_historico_cerrado()
    {
        var id = await CrearYCesarAsync("41300303");
        await ReingresarAsync(id);

        PeriodoSnapshot anterior;
        await using (var lectura = CrearContexto())
        {
            anterior = await SnapshotCerradoAsync(lectura, id);
        }

        await using (var contexto = CrearContexto())
        {
            var solicitud = Ficha("41300303", EmpresaReingresoId, PlantaReingresoId, Reingreso);
            await CrearServicio(contexto).ActualizarAsync(
                solicitud with { Id = id, Telefono = "999-333" }, "editor");
        }

        await using var verificacion = CrearContexto();
        var posterior = await SnapshotCerradoAsync(verificacion, id);
        posterior.ShouldBe(anterior);

        var trabajador = await verificacion.Trabajadores.AsNoTracking().SingleAsync(t => t.Id == id);
        trabajador.Telefono.ShouldBe("999-333");
    }

    [Fact]
    public async Task PL3D_WRITE_04_fallo_de_fk_revierte_cierre_y_reingreso_completos()
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha("41300304"), "tester");
        }

        await using (var escritura = CrearContexto())
        {
            var trabajador = await escritura.Trabajadores
                .Include(t => t.AsignacionesMarcador)
                .Include(t => t.AsignacionesTurno)
                .Include(t => t.AsignacionesSueldo)
                .Include(t => t.PeriodosLaborales)
                    .ThenInclude(p => p.AsignacionesPlanta)
                .SingleAsync(t => t.Id == id);

            trabajador.DarDeBaja(CeseInicial, MotivoCese.RenunciaVoluntaria, null, "tester");
            trabajador.Reingresar(Reingreso, 999_999, PlantaReingresoId, "tester");

            await Should.ThrowAsync<DbUpdateException>(() => escritura.SaveChangesAsync());
        }

        await using var verificacion = CrearContexto();
        var persistido = await verificacion.Trabajadores
            .AsNoTracking()
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .SingleAsync(t => t.Id == id);

        persistido.PeriodosLaborales.ShouldHaveSingleItem().FechaCese.ShouldBeNull();
        persistido.PeriodosLaborales.Single().AsignacionesPlanta.ShouldHaveSingleItem().FechaFin.ShouldBeNull();
        persistido.EmpresaId.ShouldBe(EmpresaInicialId);
        persistido.PlantaId.ShouldBe(PlantaInicialId);
        persistido.FechaIngreso.ShouldBe(IngresoInicial);
        persistido.FechaCese.ShouldBeNull();
    }

    private async Task<long> CrearYCesarAsync(string documento)
    {
        long id;
        await using (var contexto = CrearContexto())
        {
            id = await CrearServicio(contexto).CrearAsync(Ficha(documento), "tester");
        }

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto).DarDeBajaAsync(new BajaTrabajadorRequest
            {
                TrabajadorId = id,
                FechaCese = CeseInicial,
                Motivo = MotivoCese.RenunciaVoluntaria
            }, "tester");
        }

        return id;
    }

    private async Task ReingresarAsync(long id)
    {
        await using var contexto = CrearContexto();
        var trabajador = await contexto.Trabajadores
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .SingleAsync(t => t.Id == id);

        trabajador.Reingresar(Reingreso, EmpresaReingresoId, PlantaReingresoId, "tester");
        trabajador.ValidarPeriodosLaborales();
        await contexto.SaveChangesAsync();
    }

    private async Task VerificarIntegridadSqliteAsync()
    {
        await using var integridad = conexion.CreateCommand();
        integridad.CommandText = "PRAGMA integrity_check";
        (await integridad.ExecuteScalarAsync()).ShouldBe("ok");

        await using var clavesForaneas = conexion.CreateCommand();
        clavesForaneas.CommandText = "PRAGMA foreign_key_check";
        await using var resultados = await clavesForaneas.ExecuteReaderAsync();
        resultados.HasRows.ShouldBeFalse();
    }

    private static async Task<PeriodoSnapshot> SnapshotCerradoAsync(MarcacionDbContext contexto, long id)
    {
        var periodo = await contexto.PeriodosLaborales
            .AsNoTracking()
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == id && p.FechaCese != null);
        var planta = periodo.AsignacionesPlanta.ShouldHaveSingleItem();

        return new PeriodoSnapshot(
            periodo.EmpresaId,
            periodo.FechaIngreso,
            periodo.FechaCese,
            periodo.MotivoCese,
            periodo.ModificadoEn,
            periodo.ModificadoPor,
            planta.PlantaId,
            planta.FechaInicio,
            planta.FechaFin,
            planta.ModificadoEn,
            planta.ModificadoPor);
    }

    private MarcacionDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);

    private static ServicioTrabajadores CrearServicio(MarcacionDbContext contexto)
    {
        var alcance = new AlcanceSinRestriccion();
        return new ServicioTrabajadores(
            new TrabajadorRepository(contexto),
            new ConsultaTrabajadores(contexto, alcance),
            new ConsultaCatalogos(contexto, alcance),
            alcance,
            new ServicioMarcadores(contexto),
            new ServicioPeriodos(contexto));
    }

    private static GuardarTrabajadorRequest Ficha(
        string documento,
        long empresaId = EmpresaInicialId,
        long plantaId = PlantaInicialId,
        DateOnly? ingreso = null) => new()
    {
        TipoDocumentoId = TipoDocumentoId,
        NumeroDocumento = documento,
        ApellidoPaterno = "PRUEBA",
        ApellidoMaterno = "FASE3D",
        Nombres = "ESCRITURAS",
        Sexo = Sexo.Femenino,
        EmpresaId = empresaId,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = plantaId,
        PuestoId = PuestoId,
        FechaIngreso = ingreso ?? IngresoInicial
    };

    private sealed record PeriodoSnapshot(
        long EmpresaId,
        DateOnly FechaIngreso,
        DateOnly? FechaCese,
        MotivoCese? MotivoCese,
        DateTime? ModificadoEn,
        string? ModificadoPor,
        long PlantaId,
        DateOnly FechaInicioPlanta,
        DateOnly? FechaFinPlanta,
        DateTime? PlantaModificadoEn,
        string? PlantaModificadoPor);
}
