using System.Data.Common;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>Contratos Application del flujo explícito de reingreso de la Fase 3E-C.</summary>
public sealed class PeriodoLaboralFase3ECReingresoTests : IDisposable
{
    private const long EmpresaInicialId = 1;
    private const long PlantaInicialId = 2;
    private const long EmpresaReingresoId = 2;
    private const long PlantaReingresoId = 3;
    private const long PuestoId = 1;
    private const long TipoDocumentoId = 1;

    private static readonly DateOnly IngresoInicial = new(2025, 1, 1);
    private static readonly DateOnly CeseInicial = new(2025, 12, 31);
    private static readonly DateOnly Reingreso = new(2026, 2, 1);

    private readonly SqliteConnection conexion;

    public PeriodoLaboralFase3ECReingresoTests()
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
    public async Task PL3EC_APP_01_trabajador_cesado_admite_reingreso_valido()
    {
        var id = await CrearYCesarAsync("41300311");

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto).ReingresarAsync(Solicitud(id), "coordinador");
        }

        await using var verificacion = CrearContexto();
        var nuevo = await verificacion.PeriodosLaborales
            .AsNoTracking()
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == id && p.FechaCese == null);

        nuevo.EmpresaId.ShouldBe(EmpresaReingresoId);
        nuevo.FechaIngreso.ShouldBe(Reingreso);
        var planta = nuevo.AsignacionesPlanta.ShouldHaveSingleItem();
        planta.PlantaId.ShouldBe(PlantaReingresoId);
        planta.FechaInicio.ShouldBe(Reingreso);
        planta.FechaFin.ShouldBeNull();
    }

    [Fact]
    public async Task PL3EC_APP_02_trabajador_activo_es_rechazado()
    {
        var id = await CrearAsync("41300312");

        await using var contexto = CrearContexto();
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CrearServicio(contexto).ReingresarAsync(Solicitud(id), "coordinador"));

        error.Message.ShouldContain("periodo laboral abierto", Case.Insensitive);
        (await contexto.PeriodosLaborales.CountAsync(p => p.TrabajadorId == id)).ShouldBe(1);
    }

    [Fact]
    public async Task PL3EC_APP_03_fecha_que_solapa_historia_es_rechazada()
    {
        var id = await CrearYCesarAsync("41300313");

        await using var contexto = CrearContexto();
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CrearServicio(contexto).ReingresarAsync(
                Solicitud(id) with { FechaIngreso = new DateOnly(2025, 10, 1) },
                "coordinador"));

        error.Message.ShouldContain("solapados", Case.Insensitive);
        (await contexto.PeriodosLaborales.CountAsync(p => p.TrabajadorId == id)).ShouldBe(1);
    }

    [Fact]
    public async Task PL3EC_APP_04_periodo_historico_y_su_planta_permanecen_intactos()
    {
        var id = await CrearYCesarAsync("41300314");
        PeriodoSnapshot antes;

        await using (var lectura = CrearContexto())
        {
            antes = await SnapshotCerradoAsync(lectura, id);
        }

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto).ReingresarAsync(Solicitud(id), "coordinador");
        }

        await using var verificacion = CrearContexto();
        (await SnapshotCerradoAsync(verificacion, id)).ShouldBe(antes);
    }

    [Fact]
    public async Task PL3EC_APP_05_proyeccion_actual_queda_sincronizada()
    {
        var id = await CrearYCesarAsync("41300315");

        await using (var contexto = CrearContexto())
        {
            await CrearServicio(contexto).ReingresarAsync(Solicitud(id), "coordinador");
        }

        await using var verificacion = CrearContexto();
        var trabajador = await verificacion.Trabajadores.AsNoTracking().SingleAsync(t => t.Id == id);
        trabajador.FechaIngreso.ShouldBe(Reingreso);
        trabajador.FechaCese.ShouldBeNull();
        trabajador.EmpresaId.ShouldBe(EmpresaReingresoId);
        trabajador.PlantaId.ShouldBe(PlantaReingresoId);
        trabajador.MotivoCese.ShouldBeNull();
        trabajador.ObservacionCese.ShouldBeNull();
    }

    [Fact]
    public async Task PL3EC_APP_06_fallo_al_insertar_planta_revierte_todo_el_reingreso()
    {
        var id = await CrearYCesarAsync("41300316");
        var interceptor = new FalloAlInsertarPlantaInterceptor();

        await using (var contexto = CrearContexto(interceptor))
        {
            await Should.ThrowAsync<DbUpdateException>(() =>
                CrearServicio(contexto).ReingresarAsync(Solicitud(id), "coordinador"));
        }

        await using var verificacion = CrearContexto();
        var trabajador = await verificacion.Trabajadores
            .AsNoTracking()
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .SingleAsync(t => t.Id == id);

        trabajador.PeriodosLaborales.ShouldHaveSingleItem().FechaCese.ShouldBe(CeseInicial);
        trabajador.PeriodosLaborales.Single().AsignacionesPlanta
            .ShouldHaveSingleItem().FechaFin.ShouldBe(CeseInicial);
        trabajador.EmpresaId.ShouldBe(EmpresaInicialId);
        trabajador.PlantaId.ShouldBe(PlantaInicialId);
        trabajador.FechaIngreso.ShouldBe(IngresoInicial);
        trabajador.FechaCese.ShouldBe(CeseInicial);
    }

    [Fact]
    public async Task PL3EC_APP_07_doble_envio_concurrente_devuelve_error_controlado()
    {
        var id = await CrearYCesarAsync("41300317");

        await using var contextoObsoleto = CrearContexto();
        var repositorioObsoleto = new TrabajadorRepository(contextoObsoleto);
        var copiaObsoleta = await repositorioObsoleto.ObtenerPorIdAsync(id);
        copiaObsoleta.ShouldNotBeNull();

        await using (var contextoGanador = CrearContexto())
        {
            await CrearServicio(contextoGanador).ReingresarAsync(Solicitud(id), "primero");
        }

        copiaObsoleta!.Reingresar(
            Reingreso.AddDays(1),
            EmpresaReingresoId,
            PlantaReingresoId,
            "segundo");
        repositorioObsoleto.Actualizar(copiaObsoleta);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            repositorioObsoleto.GuardarReingresoAsync());
        error.Message.ShouldContain("datos laborales cambiaron", Case.Insensitive);

        await using var verificacion = CrearContexto();
        (await verificacion.PeriodosLaborales.CountAsync(p => p.TrabajadorId == id)).ShouldBe(2);
        (await verificacion.PeriodosLaborales
            .SingleAsync(p => p.TrabajadorId == id && p.FechaCese == null))
            .FechaIngreso.ShouldBe(Reingreso);
    }

    private async Task<long> CrearYCesarAsync(string documento)
    {
        var id = await CrearAsync(documento);

        await using var contexto = CrearContexto();
        await CrearServicio(contexto).DarDeBajaAsync(
            new BajaTrabajadorRequest
            {
                TrabajadorId = id,
                FechaCese = CeseInicial,
                Motivo = MotivoCese.RenunciaVoluntaria
            },
            "coordinador");

        return id;
    }

    private async Task<long> CrearAsync(string documento)
    {
        await using var contexto = CrearContexto();
        return await CrearServicio(contexto).CrearAsync(Ficha(documento), "coordinador");
    }

    private static ReingresarTrabajadorRequest Solicitud(long trabajadorId) => new()
    {
        TrabajadorId = trabajadorId,
        FechaIngreso = Reingreso,
        EmpresaId = EmpresaReingresoId,
        PlantaId = PlantaReingresoId
    };

    private static GuardarTrabajadorRequest Ficha(string documento) => new()
    {
        TipoDocumentoId = TipoDocumentoId,
        NumeroDocumento = documento,
        ApellidoPaterno = "PRUEBA",
        ApellidoMaterno = "FASE3EC",
        Nombres = "REINGRESO",
        Sexo = Sexo.Femenino,
        EmpresaId = EmpresaInicialId,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = PlantaInicialId,
        PuestoId = PuestoId,
        FechaIngreso = IngresoInicial
    };

    private static async Task<PeriodoSnapshot> SnapshotCerradoAsync(
        MarcacionDbContext contexto,
        long trabajadorId)
    {
        var periodo = await contexto.PeriodosLaborales
            .AsNoTracking()
            .Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == trabajadorId && p.FechaCese != null);
        var planta = periodo.AsignacionesPlanta.ShouldHaveSingleItem();

        return new PeriodoSnapshot(
            periodo.EmpresaId,
            periodo.FechaIngreso,
            periodo.FechaCese,
            periodo.MotivoCese,
            periodo.ObservacionCese,
            periodo.ModificadoEn,
            periodo.ModificadoPor,
            planta.PlantaId,
            planta.FechaInicio,
            planta.FechaFin,
            planta.ModificadoEn,
            planta.ModificadoPor);
    }

    private MarcacionDbContext CrearContexto(IInterceptor? interceptor = null)
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion);
        if (interceptor is not null)
        {
            opciones.AddInterceptors(interceptor);
        }

        return new MarcacionDbContext(opciones.Options);
    }

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

    private sealed class FalloAlInsertarPlantaInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            FallarSiEsInsercionDePlanta(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            FallarSiEsInsercionDePlanta(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void FallarSiEsInsercionDePlanta(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("asignacion_planta", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Fallo de persistencia simulado.");
            }
        }
    }

    private sealed record PeriodoSnapshot(
        long EmpresaId,
        DateOnly FechaIngreso,
        DateOnly? FechaCese,
        MotivoCese? MotivoCese,
        string? ObservacionCese,
        DateTime? ModificadoEn,
        string? ModificadoPor,
        long PlantaId,
        DateOnly FechaInicioPlanta,
        DateOnly? FechaFinPlanta,
        DateTime? PlantaModificadoEn,
        string? PlantaModificadoPor);
}
