using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Programacion;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Programacion;

public sealed class ProgramacionMasivaTests : IDisposable
{
    private static readonly DateOnly FechaBase = new(2026, 8, 1);
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");
    private readonly MarcacionDbContext _contexto;
    private readonly IProveedorAlcancePlantas _alcance = new AlcanceSinRestriccion();
    private readonly Application.Catalogos.IServicioPeriodos _periodos;
    private readonly long _turnoIdDiu;
    private readonly long _turnoIdNoc;

    public ProgramacionMasivaTests()
    {
        _conexion.Open();
        _contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(_conexion).Options);
        _contexto.Database.EnsureCreated();

        _turnoIdDiu = _contexto.Turnos.First(t => t.Codigo == "DIU").Id;
        _turnoIdNoc = _contexto.Turnos.First(t => t.Codigo == "NOC").Id;

        _periodos = Substitute.For<Application.Catalogos.IServicioPeriodos>();
        _periodos.ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PeriodoListaDto>>([]));
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _conexion.Dispose();
    }

    private ServicioProgramacionMasiva CrearServicio(IProveedorAlcancePlantas? alcance = null) =>
        new(_contexto, alcance ?? _alcance, _periodos);

    // ─────────────── Helpers ───────────────

    private static async Task<string> ObtenerHashAsync(ServicioProgramacionMasiva svc, SolicitudProgramacionMasiva sol) =>
        (await svc.PrevisualizarAsync(sol)).Hash;

    private async Task<Trabajador> CrearTrabajadorAsync(
        string documento,
        long? turnoId = null,
        long? cuadrillaId = null,
        DateOnly? fechaInicio = null,
        DateOnly? fechaCese = null)
    {
        var t = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = documento,
            ApellidoPaterno = "PM",
            ApellidoMaterno = $"TEST{documento}",
            Nombres = "MASIVO",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3,
            PuestoId = 1,
            FechaIngreso = fechaInicio ?? new DateOnly(2020, 1, 1),
            FechaCese = fechaCese
        };

        if (turnoId is not null || cuadrillaId is not null)
        {
            t.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                CuadrillaId = cuadrillaId,
                FechaInicio = fechaInicio ?? new DateOnly(2020, 1, 1)
            });
        }

        t.AbrirVinculo("test");
        _contexto.Trabajadores.Add(t);
        await _contexto.SaveChangesAsync();
        return t;
    }

    private static SolicitudProgramacionMasiva Solicitud(long turnoId, DateOnly fecha, params long[] ids) =>
        new(PlantaId: 3, TurnoId: turnoId, FechaEfectiva: fecha, TrabajadorIds: ids);

    // ═══════════════ PREVISUALIZACIÓN (PREV_01 a PREV_06) ═══════════════

    [Fact]
    public async Task PREV_01_trabajador_sin_turno_muestra_cambio_primera_asignacion()
    {
        var t = await CrearTrabajadorAsync("70000001");
        var svc = CrearServicio();

        var prev = await svc.PrevisualizarAsync(Solicitud(_turnoIdDiu, FechaBase, t.Id));

        prev.Cambios.ShouldBe(1);
        prev.Filas.ShouldHaveSingleItem();
        var fila = prev.Filas[0];
        fila.Estado.ShouldBe(EstadoCambio.Cambio);
        fila.Observacion.ShouldNotBeNull();
        fila.Observacion.ShouldContain("Primera asignación");
    }

    [Fact]
    public async Task PREV_02_trabajador_con_mismo_turno_muestra_sin_cambio()
    {
        var t = await CrearTrabajadorAsync("70000002", turnoId: _turnoIdDiu);
        var svc = CrearServicio();

        var prev = await svc.PrevisualizarAsync(Solicitud(_turnoIdDiu, FechaBase, t.Id));

        prev.SinCambio.ShouldBe(1);
        prev.Cambios.ShouldBe(0);
        prev.Filas[0].Estado.ShouldBe(EstadoCambio.SinCambio);
    }

    [Fact]
    public async Task PREV_03_trabajador_con_turno_distinto_muestra_cambio()
    {
        var t = await CrearTrabajadorAsync("70000003", turnoId: _turnoIdDiu);
        var svc = CrearServicio();

        var prev = await svc.PrevisualizarAsync(Solicitud(_turnoIdNoc, FechaBase, t.Id));

        prev.Cambios.ShouldBe(1);
        prev.Filas[0].Estado.ShouldBe(EstadoCambio.Cambio);
        prev.Filas[0].TurnoActual.ShouldNotBeNull();
    }

    [Fact]
    public async Task PREV_04_trabajador_cesado_muestra_sin_cambio()
    {
        var t = await CrearTrabajadorAsync("70000004", turnoId: _turnoIdDiu, fechaCese: FechaBase.AddDays(-10));
        var svc = CrearServicio();

        var prev = await svc.PrevisualizarAsync(Solicitud(_turnoIdNoc, FechaBase, t.Id));

        prev.SinCambio.ShouldBe(1);
        prev.Filas[0].Estado.ShouldBe(EstadoCambio.SinCambio);
        prev.Filas[0].Observacion.ShouldNotBeNull();
        prev.Filas[0].Observacion!.ShouldContain("Cesado");
    }

    [Fact]
    public async Task PREV_05_trabajador_con_cuadrilla_muestra_advertencia()
    {
        var cuadrilla = new Cuadrilla
        {
            Codigo = "CUA-T",
            Nombre = "CUADRILLA TEST",
            PlantaId = 3,
            Activo = true
        };
        _contexto.Cuadrillas.Add(cuadrilla);
        await _contexto.SaveChangesAsync();

        var t = await CrearTrabajadorAsync("70000005", cuadrillaId: cuadrilla.Id);
        var svc = CrearServicio();

        var prev = await svc.PrevisualizarAsync(Solicitud(_turnoIdDiu, FechaBase, t.Id));

        prev.Advertencias.ShouldBe(1);
        prev.Filas[0].Estado.ShouldBe(EstadoCambio.Advertencia);
        prev.Filas[0].CuadrillaActual.ShouldNotBeNull();
    }

    [Fact]
    public async Task PREV_06_hash_es_deterministico()
    {
        var t1 = await CrearTrabajadorAsync("70000061");
        var t2 = await CrearTrabajadorAsync("70000062");

        var sol = Solicitud(_turnoIdDiu, FechaBase, t1.Id, t2.Id);
        var hash1 = ServicioProgramacionMasiva.CalcularHash(sol);
        var hash2 = ServicioProgramacionMasiva.CalcularHash(sol);
        hash1.ShouldBe(hash2);

        var solInvertida = Solicitud(_turnoIdDiu, FechaBase, t2.Id, t1.Id);
        var hashInv = ServicioProgramacionMasiva.CalcularHash(solInvertida);
        hashInv.ShouldBe(hash1);
    }

    // ═══════════════ CONFIRMACIÓN (CONF_01 a CONF_06) ═══════════════

    [Fact]
    public async Task CONF_01_asigna_turno_a_varios_trabajadores()
    {
        var t1 = await CrearTrabajadorAsync("70000101", turnoId: _turnoIdDiu);
        var t2 = await CrearTrabajadorAsync("70000102", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t1.Id, t2.Id);
        var svc = CrearServicio();
        var hash = await ObtenerHashAsync(svc, sol);

        var res = await svc.ConfirmarAsync(sol, hash, "coord", CancellationToken.None);

        res.Procesados.ShouldBe(2);
        res.Errores.ShouldBe(0);

        var a1 = await _contexto.AsignacionesTurno.AsNoTracking()
            .Where(a => a.TrabajadorId == t1.Id && a.FechaFin == null).SingleAsync();
        a1.TurnoId.ShouldBe(_turnoIdNoc);
        a1.CuadrillaId.ShouldBeNull();
    }

    [Fact]
    public async Task CONF_02_hash_distinto_rechaza_confirmacion()
    {
        var t = await CrearTrabajadorAsync("70000201", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t.Id);
        var svc = CrearServicio();

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => svc.ConfirmarAsync(sol, "hash-falso", "coord", CancellationToken.None));

        ex.Message.ShouldContain("previsualización");
    }

    [Fact]
    public async Task CONF_03_mismo_turno_no_genera_cambio()
    {
        var t = await CrearTrabajadorAsync("70000301", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdDiu, FechaBase, t.Id);
        var svc = CrearServicio();
        var hash = await ObtenerHashAsync(svc, sol);

        var res = await svc.ConfirmarAsync(sol, hash, "coord", CancellationToken.None);

        res.SinCambio.ShouldBe(1);
        res.Procesados.ShouldBe(0);
    }

    [Fact]
    public async Task CONF_04_periodo_cerrado_rechaza_confirmacion()
    {
        var t = await CrearTrabajadorAsync("70000401", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t.Id);
        var svc = CrearServicio();
        var hash = await ObtenerHashAsync(svc, sol);

        _periodos.ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PeriodoListaDto>>(
            [
                new PeriodoListaDto(1, "JUL-2026", "Julio 2026",
                    new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 15),
                    EstadoPeriodo.Cerrado, DateTime.Now, "admin", null, null, null, true, 0)
            ]));

        var svc2 = CrearServicio();

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => svc2.ConfirmarAsync(sol, hash, "coord", CancellationToken.None));

        ex.Message.ShouldContain("cerrado");
    }

    [Fact]
    public async Task CONF_05_trabajador_de_otra_planta_rechaza()
    {
        var t = await CrearTrabajadorAsync("70000501", turnoId: _turnoIdDiu);
        var sol = new SolicitudProgramacionMasiva(
            PlantaId: 999, TurnoId: _turnoIdNoc, FechaEfectiva: FechaBase, TrabajadorIds: [t.Id]);
        var svc = CrearServicio();

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => svc.ConfirmarAsync(sol, "irrelevante", "coord", CancellationToken.None));

        ex.Message.ShouldContain("planta");
    }

    [Fact]
    public async Task CONF_06_alcance_sin_acceso_a_planta_rechaza()
    {
        var t = await CrearTrabajadorAsync("70000601", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t.Id);
        var svc = CrearServicio(alcance: new AlcanceDePlantas(999));

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => svc.ConfirmarAsync(sol, "irrelevante", "coord", CancellationToken.None));

        ex.Message.ShouldContain("acceso");
    }

    // ═══════════════ MOTORES / COEXISTENCIA (MOT_01 a MOT_02) ═══════════════

    [Fact]
    public async Task MOT_01_masiva_cierra_vigente_y_abre_nueva_sin_perder_historial()
    {
        var t = await CrearTrabajadorAsync("70000701", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t.Id);
        var svc = CrearServicio();
        var hash = await ObtenerHashAsync(svc, sol);

        await svc.ConfirmarAsync(sol, hash, "coord", CancellationToken.None);

        var asignaciones = await _contexto.AsignacionesTurno.AsNoTracking()
            .Where(a => a.TrabajadorId == t.Id)
            .OrderBy(a => a.FechaInicio)
            .ToListAsync();

        asignaciones.Count.ShouldBe(2);

        var cerrada = asignaciones[0];
        cerrada.TurnoId.ShouldBe(_turnoIdDiu);
        cerrada.FechaFin.ShouldBe(FechaBase.AddDays(-1));

        var nueva = asignaciones[1];
        nueva.TurnoId.ShouldBe(_turnoIdNoc);
        nueva.FechaFin.ShouldBeNull();
        nueva.FechaInicio.ShouldBe(FechaBase);
    }

    [Fact]
    public async Task MOT_02_todo_o_nada_si_uno_falla_nadie_se_modifica()
    {
        var t1 = await CrearTrabajadorAsync("70000801", turnoId: _turnoIdDiu);

        var t2 = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = "70000802",
            ApellidoPaterno = "PM",
            ApellidoMaterno = "TEST802",
            Nombres = "MASIVO",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };
        t2.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = _turnoIdDiu,
            FechaInicio = FechaBase.AddDays(-3),
            FechaFin = FechaBase.AddDays(-1)
        });
        t2.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = _turnoIdNoc,
            FechaInicio = FechaBase
        });
        t2.AbrirVinculo("test");
        _contexto.Trabajadores.Add(t2);
        await _contexto.SaveChangesAsync();

        var fechaAnterior = FechaBase.AddDays(-5);
        var sol = Solicitud(_turnoIdDiu, fechaAnterior, t1.Id, t2.Id);
        var svc = CrearServicio();
        var hash = await ObtenerHashAsync(svc, sol);

        var res = await svc.ConfirmarAsync(sol, hash, "coord", CancellationToken.None);

        res.Errores.ShouldBeGreaterThan(0);
        res.Procesados.ShouldBe(0);

        var a1 = await _contexto.AsignacionesTurno.AsNoTracking()
            .Where(a => a.TrabajadorId == t1.Id && a.FechaFin == null).SingleAsync();
        a1.TurnoId.ShouldBe(_turnoIdDiu);
    }

    // ═══════════════ CONCURRENCIA (CONC_01) ═══════════════

    [Fact]
    public async Task CONC_01_cambio_externo_entre_preview_y_confirm_rechaza()
    {
        var t = await CrearTrabajadorAsync("70000901", turnoId: _turnoIdDiu);
        var sol = Solicitud(_turnoIdNoc, FechaBase, t.Id);
        var svc = CrearServicio();

        var hash = await ObtenerHashAsync(svc, sol);

        var vigente = await _contexto.AsignacionesTurno
            .SingleAsync(a => a.TrabajadorId == t.Id && a.FechaFin == null);
        vigente.FechaFin = FechaBase.AddDays(-1);
        _contexto.AsignacionesTurno.Add(new AsignacionTurno
        {
            TrabajadorId = t.Id,
            TurnoId = _turnoIdNoc,
            FechaInicio = FechaBase
        });
        await _contexto.SaveChangesAsync();

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => svc.ConfirmarAsync(sol, hash, "coord", CancellationToken.None));

        ex.Message.ShouldContain("previsualización");
    }
}
