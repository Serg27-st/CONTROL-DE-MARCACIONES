using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>Contratos PL3C-TAREO-01..05 de vigencia histórica durante el recálculo.</summary>
public sealed class PeriodoLaboralTareoFase3CTests : IDisposable
{
    private static readonly DateOnly Inicio = new(2026, 7, 1);
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;

    public PeriodoLaboralTareoFase3CTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options;
        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        servicio = new ServicioCalculoTareo(
            contexto,
            new AlcanceSinRestriccion(),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task PL3C_TAREO_01_preingreso_no_genera_ausencia()
    {
        var id = await CrearAsync(Periodo(new DateOnly(2026, 7, 10), null, plantaId: 3));

        await RecalcularAsync();

        var dia = await DiaAsync(id, new DateOnly(2026, 7, 9));
        AssertSinVinculo(dia);
    }

    [Fact]
    public async Task PL3C_TAREO_02_postcese_no_genera_ausencia()
    {
        var id = await CrearAsync(Periodo(Inicio, new DateOnly(2026, 7, 20), plantaId: 3));

        await RecalcularAsync();

        var dia = await DiaAsync(id, new DateOnly(2026, 7, 21));
        AssertSinVinculo(dia);
    }

    [Fact]
    public async Task PL3C_TAREO_03_reingreso_conserva_el_hueco_sin_vinculo()
    {
        var id = await CrearAsync(
            Periodo(Inicio, new DateOnly(2026, 7, 5), plantaId: 3),
            Periodo(new DateOnly(2026, 7, 10), null, plantaId: 4));

        await RecalcularAsync();

        (await DiaAsync(id, new DateOnly(2026, 7, 5))).Estado.ShouldNotBe(EstadoTareo.SinVinculo);
        AssertSinVinculo(await DiaAsync(id, new DateOnly(2026, 7, 6)));
        (await DiaAsync(id, new DateOnly(2026, 7, 10))).Estado.ShouldNotBe(EstadoTareo.SinVinculo);
    }

    [Fact]
    public async Task PL3C_TAREO_04_feriado_de_planta_usa_la_planta_historica()
    {
        var id = await CrearAsync(Periodo(Inicio, null, plantaId: 3), plantaActual: 4);
        contexto.Feriados.Add(new Feriado
        {
            Fecha = Inicio,
            Descripcion = "Feriado de la planta histórica",
            PlantaId = 3,
            Activo = true
        });
        await contexto.SaveChangesAsync();

        await RecalcularAsync();

        (await DiaAsync(id, Inicio)).Estado.ShouldBe(EstadoTareo.Feriado);
    }

    [Fact]
    public async Task PL3C_TAREO_05_justificacion_fuera_del_vinculo_se_conserva_sin_reinterpretar()
    {
        var id = await CrearAsync(Periodo(new DateOnly(2026, 7, 10), null, plantaId: 3));
        contexto.Justificaciones.Add(new Justificacion
        {
            TrabajadorId = id,
            PeriodoId = 1,
            Fecha = new DateOnly(2026, 7, 5),
            Origen = OrigenJustificacion.Manual,
            TipoAusenciaId = 1,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Evidencia histórica",
            RegistradoPorUsuario = "prueba",
            RegistradoEn = new DateTime(2026, 7, 6, 8, 0, 0)
        });
        await contexto.SaveChangesAsync();

        await RecalcularAsync();

        var dia = await DiaAsync(id, new DateOnly(2026, 7, 5));
        AssertSinVinculo(dia);
        dia.JustificacionId.ShouldBeNull();
        (await contexto.Justificaciones.CountAsync(j => j.TrabajadorId == id)).ShouldBe(1);
    }

    [Fact]
    public async Task PL3C_TAREO_06_alcance_sql_usa_planta_historica_y_no_la_actual()
    {
        await CrearAsync(Periodo(Inicio, null, plantaId: 3), plantaActual: 4);
        await RecalcularAsync();

        var dePlantaHistorica = new ServicioCalculoTareo(
            contexto,
            new AlcanceDePlantas(3),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
        var dePlantaActual = new ServicioCalculoTareo(
            contexto,
            new AlcanceDePlantas(4),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);

        (await dePlantaHistorica.ResumirAsync(new FiltroTareo { PeriodoId = 1 })).Dias.ShouldBe(31);
        (await dePlantaActual.ResumirAsync(new FiltroTareo { PeriodoId = 1 })).Dias.ShouldBe(0);
    }

    private async Task<long> CrearAsync(
        PeriodoLaboral primerPeriodo,
        PeriodoLaboral? segundoPeriodo = null,
        long plantaActual = 3)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = Guid.NewGuid().ToString("N"),
            ApellidoPaterno = "HISTORIA",
            ApellidoMaterno = "TAREO",
            Nombres = "PRUEBA",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = plantaActual,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };
        trabajador.PeriodosLaborales.Add(primerPeriodo);
        if (segundoPeriodo is not null)
        {
            trabajador.PeriodosLaborales.Add(segundoPeriodo);
        }

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = 1,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador.Id;
    }

    private static PeriodoLaboral Periodo(DateOnly inicio, DateOnly? fin, long plantaId)
    {
        var periodo = new PeriodoLaboral
        {
            EmpresaId = 2,
            FechaIngreso = inicio,
            FechaCese = fin,
            CreadoPor = "prueba"
        };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = plantaId,
            FechaInicio = inicio,
            FechaFin = fin,
            CreadoPor = "prueba"
        });
        return periodo;
    }

    private Task<ResultadoRecalculo> RecalcularAsync() =>
        servicio.RecalcularPeriodoAsync(1, "prueba", CancellationToken.None);

    private Task<TareoDiario> DiaAsync(long trabajadorId, DateOnly fecha) =>
        contexto.TareosDiarios.AsNoTracking()
            .SingleAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == fecha);

    private static void AssertSinVinculo(TareoDiario dia)
    {
        dia.Estado.ShouldBe(EstadoTareo.SinVinculo);
        dia.MinutosTrabajados.ShouldBe(0);
        dia.MinutosOrdinarios.ShouldBe(0);
        dia.MinutosExtra25.ShouldBe(0);
        dia.MinutosExtra35.ShouldBe(0);
        dia.MinutosTardanza.ShouldBe(0);
        dia.MinutosSalidaAnticipada.ShouldBe(0);
    }
}
