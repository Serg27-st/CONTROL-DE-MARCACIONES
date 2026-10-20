using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>
/// Filtros del listado de Personal, sobre SQLite real: son subconsultas sobre las
/// asignaciones vigentes, y el proveedor InMemory no prueba su traducción a SQL.
/// </summary>
public sealed class FiltrosListadoPersonalTests : IDisposable
{
    private const long PlantaIntracan = 2;
    private const long TurnoDiurno = 1;

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ConsultaTrabajadores consulta;
    private readonly long aparato;
    private readonly long conNumero;
    private readonly long sinNumero;

    public FiltrosListadoPersonalTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador { Codigo = "P20M-01", Nombre = "Planta 20 mil", PlantaId = PlantaIntracan, Activo = true };
        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        aparato = equipo.Id;

        var agip = Trabajador("70000001", "AGIP", new DateOnly(2024, 1, 3));
        agip.AsignacionesMarcador.Add(new AsignacionMarcador { EquipoId = aparato, IdMarcador = "23", FechaInicio = new DateOnly(2024, 1, 3) });
        agip.AsignacionesTurno.Add(new AsignacionTurno { TurnoId = TurnoDiurno, FechaInicio = new DateOnly(2024, 1, 3) });

        var blujefe = Trabajador("70000002", "BLUJEFE", new DateOnly(2026, 9, 8));

        contexto.Trabajadores.AddRange(agip, blujefe);
        contexto.SaveChanges();

        conNumero = agip.Id;
        sinNumero = blujefe.Id;

        consulta = new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task Sin_numero_trae_a_quien_falta_asignar()
    {
        (await Ids(new FiltroTrabajadores { ConNumeroMarcador = false })).ShouldBe([sinNumero]);
        (await Ids(new FiltroTrabajadores { ConNumeroMarcador = true })).ShouldBe([conNumero]);
    }

    [Fact]
    public async Task Filtra_por_aparato_del_numero_vigente()
    {
        (await Ids(new FiltroTrabajadores { EquipoId = aparato })).ShouldBe([conNumero]);
    }

    /// <summary>El número se compara exacto: «2» no trae al 23.</summary>
    [Fact]
    public async Task El_filtro_trabajador_encuentra_por_numero_de_marcador_exacto()
    {
        (await Ids(new FiltroTrabajadores { Busqueda = "23" })).ShouldBe([conNumero]);
        (await Ids(new FiltroTrabajadores { Busqueda = "blujefe" })).ShouldBe([sinNumero]);
        (await Ids(new FiltroTrabajadores { Busqueda = "9" })).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sin_turno_y_rango_de_ingreso()
    {
        (await Ids(new FiltroTrabajadores { TurnoId = FiltroTrabajadores.SinTurno })).ShouldBe([sinNumero]);
        (await Ids(new FiltroTrabajadores { TurnoId = TurnoDiurno })).ShouldBe([conNumero]);
        (await Ids(new FiltroTrabajadores { IngresoDesde = new DateOnly(2026, 1, 1) })).ShouldBe([sinNumero]);
        (await Ids(new FiltroTrabajadores { IngresoHasta = new DateOnly(2025, 12, 31) })).ShouldBe([conNumero]);
    }

    /// <summary>
    /// Buscar por turno trae también a quien integra una cuadrilla que hoy está en ese turno.
    /// </summary>
    /// <remarks>
    /// El ciclo es de un solo tramo de un día de nocturno: está de noche cualquier día en que
    /// corra la prueba, que es lo que permite no depender del reloj.
    /// </remarks>
    [Fact]
    public async Task El_filtro_por_turno_incluye_a_la_cuadrilla_que_hoy_esta_en_ese_turno()
    {
        const long TurnoNocturno = 2;

        var cuadrilla = new Cuadrilla { Codigo = "A", Nombre = "Cuadrilla A", PlantaId = PlantaIntracan };
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var programacion = new ProgramacionCuadrilla
        {
            Cuadrilla = cuadrilla,
            FechaInicio = hoy,
            FechaFin = hoy,
            FechaAncla = hoy
        };
        programacion.Tramos.Add(new TramoRotacion { Orden = 1, TurnoId = TurnoNocturno, Dias = 1 });

        var collao = Trabajador("70000003", "COLLAO", new DateOnly(2024, 1, 3));
        collao.AsignacionesTurno.Add(new AsignacionTurno { Cuadrilla = cuadrilla, FechaInicio = new DateOnly(2024, 1, 3) });

        contexto.ProgramacionesCuadrilla.Add(programacion);
        contexto.Trabajadores.Add(collao);
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await Ids(new FiltroTrabajadores { TurnoId = TurnoNocturno })).ShouldBe([collao.Id]);
        (await Ids(new FiltroTrabajadores { TurnoId = TurnoDiurno })).ShouldBe([conNumero]);
        (await Ids(new FiltroTrabajadores { TurnoId = FiltroTrabajadores.SinTurno })).ShouldBe([sinNumero]);

        var fila = (await consulta.ListarAsync(new FiltroTrabajadores { Busqueda = "collao" }, CancellationToken.None))
            .Elementos.ShouldHaveSingleItem();
        fila.TurnoVigente.ShouldBe("Cuadrilla A");
    }

    private async Task<long[]> Ids(FiltroTrabajadores filtro) =>
        [.. (await consulta.ListarAsync(filtro, CancellationToken.None)).Elementos.Select(t => t.Id)];

    private static Trabajador Trabajador(string documento, string apellido, DateOnly ingreso) => new()
    {
        TipoDocumentoId = 1,
        NumeroDocumento = documento,
        ApellidoPaterno = apellido,
        ApellidoMaterno = "PRUEBA",
        Nombres = "FILTRO",
        Sexo = Sexo.Masculino,
        EmpresaId = 1,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = PlantaIntracan,
        PuestoId = 1,
        FechaIngreso = ingreso
    };
}
