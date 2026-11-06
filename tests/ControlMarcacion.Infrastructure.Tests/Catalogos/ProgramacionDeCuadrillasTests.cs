using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

/// <summary>
/// Programar el ciclo de una cuadrilla, reemplazarlo y correr su rotación.
/// </summary>
/// <remarks>
/// Sobre SQLite y no sobre InMemory: la baja lógica y el cierre de programaciones tienen que
/// convivir con los índices y las claves reales del esquema. Turnos sembrados: DIU (1),
/// NOC (2) y SAB (3, habilitado solo en Planta 1). El periodo 1 es julio de 2026.
/// </remarks>
public sealed class ProgramacionDeCuadrillasTests : IDisposable
{
    private const long Diurno = 1;
    private const long Nocturno = 2;
    private const long Sabatino = 3;
    private const long PlantaDetergentes = 1;
    private const long Planta1 = 3;

    private static readonly DateOnly Lunes6 = new(2026, 7, 6);
    private static readonly DateOnly Lunes20 = new(2026, 7, 20);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCuadrillas servicio;

    public ProgramacionDeCuadrillasTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioCuadrillas(contexto);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task Programar_deja_el_ciclo_vigente_y_anuncia_las_proximas_rotaciones()
    {
        var id = await CrearCuadrillaAsync();

        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));

        var cuadrilla = await servicio.ObtenerAsync(id, Lunes20, CancellationToken.None);

        cuadrilla.ShouldNotBeNull();
        cuadrilla.ProgramacionVigente!.Ciclo.ShouldBe("DIU 14 d → NOC 14 d");
        cuadrilla.TurnoHoy.ShouldStartWith("NOC");
        cuadrilla.ProximosCambios.ShouldBeEmpty();
    }

    [Fact]
    public async Task Programar_desde_una_fecha_posterior_cierra_la_anterior_el_dia_previo()
    {
        var id = await CrearCuadrillaAsync();

        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));
        await ProgramarAsync(id, new DateOnly(2026, 8, 3), (Nocturno, 7), (Diurno, 7));

        var programaciones = await servicio.ProgramacionesAsync(id, CancellationToken.None);

        programaciones.Count.ShouldBe(2);
        programaciones[1].FechaFin.ShouldBe(new DateOnly(2026, 8, 2));
        programaciones[1].Activo.ShouldBeTrue();
        programaciones[0].FechaFin.ShouldBe(new DateOnly(2026, 8, 16));
        programaciones[0].Ciclo.ShouldBe("NOC 7 d → DIU 7 d");
    }

    /// <summary>
    /// Reprogramar desde la misma fecha reemplaza entera a la anterior: queda de baja, en el
    /// historial, y deja de contar. Nada se borra.
    /// </summary>
    [Fact]
    public async Task Programar_desde_la_misma_fecha_da_de_baja_a_la_anterior()
    {
        var id = await CrearCuadrillaAsync();

        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));
        await ProgramarAsync(id, Lunes6, (Nocturno, 14), (Diurno, 14));

        var programaciones = await servicio.ProgramacionesAsync(id, CancellationToken.None);

        programaciones.Count.ShouldBe(2);
        programaciones.Count(p => p.Activo).ShouldBe(1);

        var cuadrilla = await servicio.ObtenerAsync(id, Lunes6, CancellationToken.None);
        cuadrilla!.TurnoHoy.ShouldStartWith("NOC");
    }

    /// <summary>
    /// La cuadrilla rotó un día antes: desde ese día el ciclo se corre, y lo anterior no cambia.
    /// </summary>
    [Fact]
    public async Task Correr_la_rotacion_un_dia_antes_adelanta_el_cambio_y_respeta_lo_anterior()
    {
        var id = await CrearCuadrillaAsync();
        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));

        await servicio.CorrerRotacionAsync(
            new CorrerRotacionRequest { CuadrillaId = id, Desde = new DateOnly(2026, 7, 18), Dias = -1 },
            "coordinador",
            CancellationToken.None);

        (await TurnoHoyAsync(id, new DateOnly(2026, 7, 17))).ShouldStartWith("DIU");
        (await TurnoHoyAsync(id, new DateOnly(2026, 7, 18))).ShouldStartWith("DIU");
        (await TurnoHoyAsync(id, new DateOnly(2026, 7, 19))).ShouldStartWith("NOC");

        var programaciones = await servicio.ProgramacionesAsync(id, CancellationToken.None);
        programaciones[0].FechaAncla.ShouldBe(new DateOnly(2026, 7, 5));
        programaciones[1].FechaFin.ShouldBe(new DateOnly(2026, 7, 17));
    }

    [Fact]
    public async Task Correr_una_rotacion_sin_ciclo_programado_se_rechaza()
    {
        var id = await CrearCuadrillaAsync();

        var error = await Should.ThrowAsync<ReglaNegocioException>(() => servicio.CorrerRotacionAsync(
            new CorrerRotacionRequest { CuadrillaId = id, Desde = Lunes20, Dias = 1 },
            "coordinador",
            CancellationToken.None));

        error.Message.ShouldContain("no tiene ciclo programado");
    }

    [Fact]
    public async Task Un_periodo_cerrado_impide_programar_sobre_sus_dias()
    {
        var id = await CrearCuadrillaAsync();

        var julio = await contexto.Periodos.SingleAsync(p => p.Codigo == "2026-07");
        julio.Estado = EstadoPeriodo.Cerrado;
        await contexto.SaveChangesAsync();

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            ProgramarAsync(id, Lunes20, (Diurno, 14), (Nocturno, 14)));

        error.Message.ShouldContain("2026-07");
        error.Message.ShouldContain("posterior al 31/07/2026");

        // Agosto sí se puede.
        await ProgramarAsync(id, new DateOnly(2026, 8, 3), (Diurno, 14), (Nocturno, 14));
        (await servicio.ProgramacionesAsync(id, CancellationToken.None)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Un_turno_inactivo_se_rechaza()
    {
        var id = await CrearCuadrillaAsync(PlantaDetergentes);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            ProgramarAsync(id, Lunes6, (Diurno, 5), (Sabatino, 1)));

        error.Message.ShouldContain("dado de baja");
    }

    [Fact]
    public async Task Un_tramo_a_medio_llenar_se_rechaza_y_las_filas_vacias_se_ignoran()
    {
        var id = await CrearCuadrillaAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() => servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos = [new TramoProgramadoRequest { TurnoId = Diurno }]
            },
            "coordinador",
            CancellationToken.None));

        await servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos =
                [
                    new TramoProgramadoRequest { TurnoId = Diurno, Dias = 7 },
                    new TramoProgramadoRequest(),
                    new TramoProgramadoRequest { TurnoId = Nocturno, Dias = 7 }
                ]
            },
            "coordinador",
            CancellationToken.None);

        (await servicio.ProgramacionesAsync(id, CancellationToken.None))[0].Ciclo.ShouldBe("DIU 7 d → NOC 7 d");
    }

    [Fact]
    public async Task ROT_DESC_01_la_aplicacion_valida_tipo_turno_y_un_dia()
    {
        var id = await CrearCuadrillaAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() => servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos = [new TramoProgramadoRequest
                    { Tipo = TipoTramoRotacion.Trabajo, Dias = 1 }]
            }, "test", CancellationToken.None));

        await Should.ThrowAsync<ReglaNegocioException>(() => servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos = [new TramoProgramadoRequest
                    { Tipo = TipoTramoRotacion.Descanso, TurnoId = Diurno, Dias = 1 }]
            }, "test", CancellationToken.None));

        await Should.ThrowAsync<ReglaNegocioException>(() => servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos = [new TramoProgramadoRequest
                    { Tipo = TipoTramoRotacion.Descanso, Dias = 2 }]
            }, "test", CancellationToken.None));

        await servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = id,
                Desde = Lunes6,
                Tramos =
                [
                    new TramoProgramadoRequest
                        { Tipo = TipoTramoRotacion.Trabajo, TurnoId = Diurno, Dias = 1 },
                    new TramoProgramadoRequest
                        { Tipo = TipoTramoRotacion.Descanso, Dias = 1 },
                    new TramoProgramadoRequest
                        { Tipo = TipoTramoRotacion.Trabajo, TurnoId = Nocturno, Dias = 1 }
                ]
            }, "test", CancellationToken.None);

        var programacion = (await servicio.ProgramacionesAsync(id, CancellationToken.None))[0];
        programacion.FechaFin.ShouldBe(Lunes6.AddDays(2));
        programacion.Ciclo.ShouldBe("DIU 1 d → DESCANSO PROGRAMADO → NOC 1 d");
    }

    [Fact]
    public async Task No_se_da_de_baja_una_cuadrilla_con_integrantes_vigentes()
    {
        var id = await CrearCuadrillaAsync();
        await SumarIntegranteAsync(id);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.CambiarEstadoAsync(id, activo: false, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("1 integrante");
    }

    /// <summary>
    /// La casilla «Activa» del formulario de edición es la misma baja que el botón del
    /// listado, y pasa por la misma regla.
    /// </summary>
    /// <remarks>
    /// Desmarcarla la saltaba: la cuadrilla quedaba fuera del listado y sin poder
    /// reprogramarse —<c>CuadrillaActivaAsync</c> la rechaza— mientras su ciclo seguía
    /// decidiendo el turno de sus integrantes en cada recálculo.
    /// </remarks>
    [Fact]
    public async Task Editar_con_la_casilla_desmarcada_tampoco_da_de_baja_una_cuadrilla_con_integrantes()
    {
        var id = await CrearCuadrillaAsync();
        await SumarIntegranteAsync(id);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.GuardarAsync(
                new GuardarCuadrillaRequest
                {
                    Id = id,
                    Codigo = "A",
                    Nombre = "Cuadrilla A",
                    PlantaId = Planta1,
                    Activo = false
                },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("1 integrante");

        var cuadrilla = await contexto.Cuadrillas.AsNoTracking().FirstAsync(c => c.Id == id);
        cuadrilla.Activo.ShouldBeTrue();
    }

    /// <summary>
    /// Correr el ancla un ciclo entero la deja donde estaba: ni un día cambia de turno.
    /// Antes se aceptaba, dejaba una programación más en el historial y el tareo salía igual.
    /// </summary>
    [Fact]
    public async Task Correr_la_rotacion_un_ciclo_entero_se_rechaza_porque_no_cambiaria_nada()
    {
        var id = await CrearCuadrillaAsync();
        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.CorrerRotacionAsync(
                new CorrerRotacionRequest { CuadrillaId = id, Desde = Lunes20, Dias = 28 },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("secuencia finita");

        // Y el historial queda como estaba: una sola programación.
        (await servicio.ProgramacionesAsync(id, CancellationToken.None)).Count.ShouldBe(1);
    }

    /// <summary>Medio ciclo sí rota: es el caso legítimo que no debe quedar bloqueado.</summary>
    [Fact]
    public async Task Correr_la_rotacion_medio_ciclo_intercambia_los_turnos()
    {
        var id = await CrearCuadrillaAsync();
        await ProgramarAsync(id, Lunes6, (Diurno, 14), (Nocturno, 14));

        (await TurnoHoyAsync(id, Lunes20)).ShouldStartWith("NOC");

        await servicio.CorrerRotacionAsync(
            new CorrerRotacionRequest { CuadrillaId = id, Desde = Lunes20, Dias = 14 },
            "coordinador",
            CancellationToken.None);

        (await TurnoHoyAsync(id, Lunes20)).ShouldStartWith("DIU");
    }

    /// <summary>La restricción de la base: turno fijo o cuadrilla, nunca los dos ni ninguno.</summary>
    [Fact]
    public async Task La_base_rechaza_una_asignacion_con_turno_y_cuadrilla_a_la_vez()
    {
        var id = await CrearCuadrillaAsync();

        await Should.ThrowAsync<DbUpdateException>(() => SumarIntegranteAsync(id, turnoId: Diurno));
    }

    [Fact]
    public async Task ROT_03_una_programacion_futura_distinta_no_cambia_la_resolucion_historica()
    {
        var id = await CrearCuadrillaAsync();
        await ProgramarAsync(id, Lunes6, (Diurno, 7), (Nocturno, 7));

        var fechaHistorica = new DateOnly(2026, 7, 13);
        (await TurnoHoyAsync(id, fechaHistorica)).ShouldStartWith("NOC");

        await ProgramarAsync(id, new DateOnly(2026, 8, 3), (Diurno, 10), (Nocturno, 5));

        (await TurnoHoyAsync(id, fechaHistorica)).ShouldStartWith("NOC");
        (await TurnoHoyAsync(id, new DateOnly(2026, 8, 12))).ShouldStartWith("DIU");
        (await TurnoHoyAsync(id, new DateOnly(2026, 8, 13))).ShouldStartWith("NOC");
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearCuadrillaAsync(long plantaId = Planta1) =>
        await servicio.GuardarAsync(
            new GuardarCuadrillaRequest { Codigo = "A", Nombre = "Cuadrilla A", PlantaId = plantaId },
            "coordinador",
            CancellationToken.None);

    private Task ProgramarAsync(long cuadrillaId, DateOnly desde, params (long Turno, int Dias)[] tramos) =>
        servicio.ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = cuadrillaId,
                Desde = desde,
                Tramos = [.. tramos.Select(t => new TramoProgramadoRequest { TurnoId = t.Turno, Dias = t.Dias })]
            },
            "coordinador",
            CancellationToken.None);

    private async Task<string?> TurnoHoyAsync(long cuadrillaId, DateOnly fecha) =>
        (await servicio.ObtenerAsync(cuadrillaId, fecha, CancellationToken.None))!.TurnoHoy;

    private async Task SumarIntegranteAsync(long cuadrillaId, long? turnoId = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = "45678901",
            ApellidoPaterno = "CUADRILLA",
            ApellidoMaterno = "PRUEBA",
            Nombres = "OBRERO",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = Planta1,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            CuadrillaId = cuadrillaId,
            TurnoId = turnoId,
            FechaInicio = Lunes6
        });

        await contexto.Trabajadores.AddAsync(trabajador);
        await contexto.SaveChangesAsync();
    }
}
