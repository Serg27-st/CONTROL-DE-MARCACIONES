using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

public sealed class PeriodoLaboralFase3EBCuadrillasTests : IDisposable
{
    private const long EmpresaId = 1;
    private const long PlantaUno = 2;
    private const long PlantaDos = 3;
    private const long PuestoId = 1;
    private static readonly DateOnly Inicio = new(2026, 1, 1);
    private static readonly DateOnly AntesDelCambio = new(2026, 6, 15);
    private static readonly DateOnly Cambio = new(2026, 7, 1);
    private static readonly DateOnly DespuesDelCambio = new(2026, 8, 15);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly long cuadrillaId;

    public PeriodoLaboralFase3EBCuadrillasTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var cuadrilla = new Cuadrilla
        {
            Codigo = "3EB-CUA",
            Nombre = "Cuadrilla histórica 3E-B",
            PlantaId = PlantaUno
        };
        contexto.Cuadrillas.Add(cuadrilla);
        contexto.SaveChanges();
        cuadrillaId = cuadrilla.Id;
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task PL3EB_INT_01_traslado_posterior_no_oculta_integrante_de_planta_historica()
    {
        var trabajador = CrearTrabajador("41300311", Inicio, PlantaUno);
        trabajador.CambiarPlanta(PlantaDos, Cambio, "tester");
        AsignarCuadrilla(trabajador);
        await contexto.SaveChangesAsync();

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaUno));
        var integrantes = await consulta.IntegrantesDeCuadrillaAsync(cuadrillaId, AntesDelCambio);

        integrantes.Select(i => i.TrabajadorId).ShouldBe([trabajador.Id]);
    }

    [Fact]
    public async Task PL3EB_INT_02_despues_del_traslado_el_alcance_corresponde_a_planta_nueva()
    {
        var trabajador = CrearTrabajador("41300312", Inicio, PlantaUno);
        trabajador.CambiarPlanta(PlantaDos, Cambio, "tester");
        AsignarCuadrilla(trabajador);
        await contexto.SaveChangesAsync();

        var consultaNueva = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaDos));
        var consultaAntigua = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaUno));

        (await consultaNueva.IntegrantesDeCuadrillaAsync(cuadrillaId, DespuesDelCambio))
            .Select(i => i.TrabajadorId).ShouldBe([trabajador.Id]);
        (await consultaAntigua.IntegrantesDeCuadrillaAsync(cuadrillaId, DespuesDelCambio))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task PL3EB_CAN_01_cesado_actual_es_candidato_cuando_tenia_vinculo_en_fecha()
    {
        var trabajador = CrearTrabajador("41300313", Inicio, PlantaUno);
        trabajador.DarDeBaja(
            new DateOnly(2026, 12, 31), MotivoCese.RenunciaVoluntaria, null, "tester");
        await contexto.SaveChangesAsync();

        var candidatos = await new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion())
            .CandidatosParaCuadrillaAsync(cuadrillaId, PlantaUno, AntesDelCambio);

        candidatos.Select(c => c.TrabajadorId).ShouldContain(trabajador.Id);
    }

    [Fact]
    public async Task PL3EB_CAN_02_activo_actual_no_es_candidato_antes_de_su_ingreso()
    {
        var trabajador = CrearTrabajador(
            "41300314", new DateOnly(2026, 7, 1), PlantaUno);
        await contexto.SaveChangesAsync();

        var candidatos = await new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion())
            .CandidatosParaCuadrillaAsync(cuadrillaId, PlantaUno, AntesDelCambio);

        candidatos.Select(c => c.TrabajadorId).ShouldNotContain(trabajador.Id);
    }

    [Fact]
    public async Task PL3EB_CAN_03_traslado_posterior_conserva_candidatura_en_planta_historica()
    {
        var trabajador = CrearTrabajador("41300315", Inicio, PlantaUno);
        trabajador.CambiarPlanta(PlantaDos, Cambio, "tester");
        await contexto.SaveChangesAsync();

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion());
        var antiguos = await consulta.CandidatosParaCuadrillaAsync(
            cuadrillaId, PlantaUno, AntesDelCambio);
        var actualesEnPlantaAntigua = await consulta.CandidatosParaCuadrillaAsync(
            cuadrillaId, PlantaUno, DespuesDelCambio);

        antiguos.Select(c => c.TrabajadorId).ShouldContain(trabajador.Id);
        actualesEnPlantaAntigua.Select(c => c.TrabajadorId).ShouldNotContain(trabajador.Id);
    }

    private Trabajador CrearTrabajador(string documento, DateOnly ingreso, long plantaId)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = documento,
            ApellidoPaterno = "HISTORICO",
            ApellidoMaterno = "CUADRILLA",
            Nombres = documento,
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaId,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = plantaId,
            PuestoId = PuestoId,
            FechaIngreso = ingreso,
            CreadoPor = "tester"
        };
        trabajador.AbrirVinculo("tester");
        contexto.Trabajadores.Add(trabajador);
        return trabajador;
    }

    private void AsignarCuadrilla(Trabajador trabajador) =>
        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            CuadrillaId = cuadrillaId,
            FechaInicio = Inicio,
            CreadoPor = "tester"
        });
}
