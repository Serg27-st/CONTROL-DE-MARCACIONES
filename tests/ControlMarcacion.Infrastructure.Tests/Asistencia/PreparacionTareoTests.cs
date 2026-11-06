using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>Contratos CATREADY-01..20 sobre SQLite real.</summary>
public sealed class PreparacionTareoTests : IDisposable
{
    private static readonly DateOnly Inicio = new(2026, 7, 1);
    private static readonly DateOnly Fin = new(2026, 7, 31);
    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;

    public PreparacionTareoTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion)
            .Options);
        contexto.Database.EnsureCreated();
        SembrarCatalogos();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task CATREADY_01_trabajador_completamente_configurado()
    {
        var trabajador = await CrearTrabajadorAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        resultado.Dias.Single().Requisitos.Any(r =>
            r.Estado != EstadoRequisitoPreparacion.Cumplido &&
            r.Estado != EstadoRequisitoPreparacion.NoAplica).ShouldBeFalse();
    }

    [Fact]
    public async Task CATREADY_02_sin_relacion_laboral_es_incompleto()
    {
        var trabajador = await CrearTrabajadorAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio.AddDays(-1), periodoId: null);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-01");
    }

    [Fact]
    public async Task CATREADY_03_sin_unidad_es_incompleto()
    {
        var trabajador = await CrearTrabajadorAsync();
        var periodo = await contexto.PeriodosLaborales.Include(p => p.AsignacionesPlanta)
            .SingleAsync(p => p.TrabajadorId == trabajador.Id);
        contexto.AsignacionesPlanta.RemoveRange(periodo.AsignacionesPlanta);
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-03");
    }

    [Fact]
    public async Task CATREADY_04_sin_marcador_valido_es_incompleto()
    {
        var trabajador = await CrearTrabajadorAsync(conMarcador: false);

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-05");
    }

    [Fact]
    public async Task CATREADY_05_marcadores_solapados_son_conflicto()
    {
        var trabajador = await CrearTrabajadorAsync();
        var marcadorOriginal = await contexto.AsignacionesMarcador.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        marcadorOriginal.FechaFin = Fin;
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajador.Id, EquipoId = 1002, IdMarcador = "200",
            FechaInicio = Inicio, Activo = true
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Conflicto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-09-MARCADOR");
    }

    [Fact]
    public async Task CATREADY_06_sin_turno_es_incompleto()
    {
        var trabajador = await CrearTrabajadorAsync(conTurno: false);

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-06");
    }

    [Fact]
    public async Task HEQA_05_turno_sin_version_economica_vigente_es_incompleto()
    {
        var trabajador = await CrearTrabajadorAsync();
        var versiones = await contexto.VersionesTurno.Where(v => v.TurnoId == 1).ToListAsync();
        contexto.VersionesTurno.RemoveRange(versiones);
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-11");
        resultado.Incidencias.Single(i => i.Codigo == "CATREADY-11").Explicacion
            .ShouldContain("no tiene una versión económica vigente");
    }

    [Fact]
    public async Task CATREADY_07_cuadrilla_sin_programacion_deja_descanso_pendiente()
    {
        var trabajador = await CrearTrabajadorAsync(conTurno: false);
        contexto.Cuadrillas.Add(new Cuadrilla { Id = 1, Codigo = "C1", Nombre = "Cuadrilla 1", PlantaId = 1 });
        contexto.AsignacionesTurno.Add(new AsignacionTurno
        {
            TrabajadorId = trabajador.Id, CuadrillaId = 1, FechaInicio = Inicio
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-07");
    }

    [Fact]
    public async Task CATREADY_08_consulta_historica_acepta_periodo_cerrado()
    {
        var trabajador = await CrearTrabajadorAsync(fechaCese: Fin);
        var periodo = await contexto.Periodos.SingleAsync(p => p.Id == 1);
        periodo.Estado = EstadoPeriodo.Cerrado;
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(
            trabajador.Id, Inicio, 1, OperacionPreparacionTareo.ConsultaHistorica);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
    }

    [Fact]
    public async Task CATREADY_09_cambio_de_equipo_se_resuelve_por_fecha()
    {
        var trabajador = await CrearTrabajadorAsync();
        var primera = await contexto.AsignacionesMarcador.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        primera.FechaFin = Inicio.AddDays(14);
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajador.Id, EquipoId = 1002, IdMarcador = "200",
            FechaInicio = Inicio.AddDays(15)
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarIntervaloAsync(trabajador.Id, Inicio, Fin, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        resultado.Dias[0].EquipoId.ShouldBe(1001);
        resultado.Dias[^1].EquipoId.ShouldBe(1002);
    }

    [Fact]
    public async Task CATREADY_10_cambio_de_turno_se_resuelve_por_fecha()
    {
        var trabajador = await CrearTrabajadorAsync();
        var primera = await contexto.AsignacionesTurno.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        primera.FechaFin = Inicio.AddDays(14);
        contexto.AsignacionesTurno.Add(new AsignacionTurno
        {
            TrabajadorId = trabajador.Id, TurnoId = 2, FechaInicio = Inicio.AddDays(15)
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarIntervaloAsync(trabajador.Id, Inicio, Fin, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        resultado.Dias[0].TurnoId.ShouldBe(1);
        resultado.Dias[^1].TurnoId.ShouldBe(2);
    }

    [Fact]
    public async Task CATREADY_11_cese_conserva_listo_el_historico_y_no_inventa_vinculo_posterior()
    {
        var cese = Inicio.AddDays(10);
        var trabajador = await CrearTrabajadorAsync(fechaCese: cese);

        var historico = await Servicio().EvaluarFechaAsync(trabajador.Id, cese, 1);
        var posterior = await Servicio().EvaluarFechaAsync(trabajador.Id, cese.AddDays(1), 1);

        historico.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        posterior.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        posterior.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-01");
    }

    [Fact]
    public async Task CATREADY_12_reingreso_distingue_dos_vinculos_y_gap()
    {
        var trabajador = await CrearTrabajadorAsync(fechaCese: Inicio.AddDays(4));
        var segundo = new PeriodoLaboral
        {
            TrabajadorId = trabajador.Id, EmpresaId = 1, FechaIngreso = Inicio.AddDays(10)
        };
        segundo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 1, FechaInicio = Inicio.AddDays(10)
        });
        contexto.PeriodosLaborales.Add(segundo);
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajador.Id, EquipoId = 1001, IdMarcador = "101",
            FechaInicio = Inicio.AddDays(10)
        });
        contexto.AsignacionesTurno.Add(new AsignacionTurno
        {
            TrabajadorId = trabajador.Id, TurnoId = 1, FechaInicio = Inicio.AddDays(10)
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarIntervaloAsync(trabajador.Id, Inicio, Inicio.AddDays(12), 1);

        resultado.Dias[0].Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        resultado.Dias[5].Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Dias[^1].Estado.ShouldBe(EstadoPreparacionTareo.Listo);
    }

    [Fact]
    public async Task CATREADY_13_conflicto_tiene_prioridad_sobre_dato_faltante()
    {
        var trabajador = await CrearTrabajadorAsync(conTurno: false);
        var marcadorOriginal = await contexto.AsignacionesMarcador.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        marcadorOriginal.FechaFin = Fin;
        contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajador.Id, EquipoId = 1002, IdMarcador = "200", FechaInicio = Inicio
        });
        await contexto.SaveChangesAsync();

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Conflicto);
        resultado.Incidencias.ShouldContain(i => i.Estado == EstadoRequisitoPreparacion.Conflicto);
        resultado.Incidencias.ShouldContain(i => i.Estado == EstadoRequisitoPreparacion.Pendiente);
    }

    [Fact]
    public async Task CATREADY_14_trabajador_fuera_de_alcance_no_se_expone()
    {
        var trabajador = await CrearTrabajadorAsync();

        await Should.ThrowAsync<RecursoNoEncontradoException>(() =>
            Servicio(new AlcanceDePlantas(2)).EvaluarFechaAsync(trabajador.Id, Inicio, 1));
    }

    [Fact]
    public async Task CATREADY_15_alcance_vacio_no_equivale_a_acceso_global()
    {
        await CrearTrabajadorAsync();

        var resultado = await Servicio(new AlcanceDePlantas()).EvaluarAutorizadosAsync(Filtro());

        resultado.ShouldBeEmpty();
    }

    [Fact]
    public async Task CATREADY_16_consulta_no_escribe_ni_deja_entidades_modificadas()
    {
        var trabajador = await CrearTrabajadorAsync();
        contexto.ChangeTracker.Clear();
        var antes = await ConteosAsync();

        await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        (await ConteosAsync()).ShouldBe(antes);
        contexto.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task CATREADY_17_trabajador_valido_persiste_aunque_aun_no_tenga_turno()
    {
        var trabajador = await CrearTrabajadorAsync(conTurno: false);

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        (await contexto.Trabajadores.AnyAsync(t => t.Id == trabajador.Id)).ShouldBeTrue();
        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        resultado.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-06");
    }

    [Fact]
    public async Task CATREADY_18_no_tener_marcaciones_no_es_configuracion_incompleta()
    {
        var trabajador = await CrearTrabajadorAsync();
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(0);

        var resultado = await Servicio().EvaluarFechaAsync(trabajador.Id, Inicio, 1);

        resultado.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
    }

    [Fact]
    public async Task CATREADY_19_periodos_distintos_producen_resultados_independientes()
    {
        var trabajador = await CrearTrabajadorAsync();
        var turno = await contexto.AsignacionesTurno.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        turno.FechaFin = Fin;
        contexto.Periodos.Add(new Periodo
        {
            Id = 2, Codigo = "2026-08", Nombre = "Agosto", FechaInicio = new(2026, 8, 1), FechaFin = new(2026, 8, 31)
        });
        var vinculo = await contexto.PeriodosLaborales.SingleAsync(p => p.TrabajadorId == trabajador.Id);
        vinculo.AsignacionesPlanta.Single().FechaFin = null;
        await contexto.SaveChangesAsync();

        var julio = await Servicio().EvaluarFechaAsync(trabajador.Id, Fin, 1);
        var agosto = await Servicio().EvaluarFechaAsync(trabajador.Id, new DateOnly(2026, 8, 1), 2);

        julio.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
        agosto.Estado.ShouldBe(EstadoPreparacionTareo.Incompleto);
        agosto.Incidencias.ShouldContain(i => i.Codigo == "CATREADY-06");
    }

    [Fact]
    public async Task CATREADY_20_no_altera_tareo_ni_marcaciones()
    {
        var trabajador = await CrearTrabajadorAsync();
        var tareosAntes = await contexto.TareosDiarios.CountAsync();
        var marcasAntes = await contexto.MarcacionesCrudas.CountAsync();

        await Servicio().EvaluarIntervaloAsync(trabajador.Id, Inicio, Fin, 1);

        (await contexto.TareosDiarios.CountAsync()).ShouldBe(tareosAntes);
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(marcasAntes);
    }

    private ValidadorPreparacionTareo Servicio(IProveedorAlcancePlantas? alcance = null) =>
        new ValidadorPreparacionTareo(contexto, alcance ?? new AlcanceSinRestriccion());

    private static FiltroPreparacionTareo Filtro() => new()
    {
        PeriodoId = 1,
        Desde = Inicio,
        Hasta = Fin
    };

    private async Task<Trabajador> CrearTrabajadorAsync(
        bool conMarcador = true,
        bool conTurno = true,
        DateOnly? fechaCese = null)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = $"{80000000 + await contexto.Trabajadores.CountAsync() + 1}",
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "CATREADY",
            Nombres = "PERSONA",
            Sexo = Sexo.Femenino,
            EmpresaId = 1,
            PlantaId = 1,
            TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = 1,
            FechaIngreso = Inicio,
            FechaCese = fechaCese
        };
        var periodo = new PeriodoLaboral
        {
            EmpresaId = 1,
            FechaIngreso = Inicio,
            FechaCese = fechaCese
        };
        periodo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = 1,
            FechaInicio = Inicio,
            FechaFin = fechaCese
        });
        trabajador.PeriodosLaborales.Add(periodo);

        if (conMarcador)
        {
            trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
            {
                EquipoId = 1001, IdMarcador = "100", FechaInicio = Inicio, FechaFin = fechaCese
            });
        }

        if (conTurno)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = 1, FechaInicio = Inicio, FechaFin = fechaCese
            });
        }

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador;
    }

    private async Task<(int Trabajadores, int Periodos, int Plantas, int Marcadores, int Turnos)> ConteosAsync() =>
        (await contexto.Trabajadores.CountAsync(),
         await contexto.PeriodosLaborales.CountAsync(),
         await contexto.AsignacionesPlanta.CountAsync(),
         await contexto.AsignacionesMarcador.CountAsync(),
         await contexto.AsignacionesTurno.CountAsync());

    private void SembrarCatalogos()
    {
        contexto.AddRange(
            new EquipoMarcador { Id = 1001, Codigo = "EQ-CATREADY-1", Nombre = "Equipo CATREADY 1", PlantaId = 1 },
            new EquipoMarcador { Id = 1002, Codigo = "EQ-CATREADY-2", Nombre = "Equipo CATREADY 2", PlantaId = 1 },
            new Marcador { Id = 1001, EquipoId = 1001, PlantaId = 1, Codigo = "100" },
            new Marcador { Id = 1002, EquipoId = 1002, PlantaId = 1, Codigo = "200" },
            new Marcador { Id = 1003, EquipoId = 1001, PlantaId = 1, Codigo = "101" });
        contexto.SaveChanges();
    }
}
