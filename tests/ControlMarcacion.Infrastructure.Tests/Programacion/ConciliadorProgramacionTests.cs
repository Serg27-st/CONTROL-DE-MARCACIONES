using System.Globalization;
using ClosedXML.Excel;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Programacion.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using ControlMarcacion.Infrastructure.Programacion;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Programacion;

public sealed class ConciliadorProgramacionTests : IDisposable
{
    private static readonly DateOnly Inicio = new(2026, 11, 2);
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly MarcacionDbContext contexto;

    public ConciliadorProgramacionTests()
    {
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task PREV01_previsualizar_con_entradas_en_memoria_produce_resultado_valido()
    {
        await CrearTrabajadorAsync("90000001", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000001", Inicio, Inicio.AddDays(4), null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        resultado.ShouldNotBeNull();
        resultado.Filas.Count.ShouldBe(5);
        resultado.Huella.ShouldNotBeNullOrWhiteSpace();
        resultado.PuedeConfirmar.ShouldBeTrue();
    }

    [Fact]
    public async Task PREV02_confirmar_con_entradas_en_memoria_persiste_programacion()
    {
        await CrearTrabajadorAsync("90000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000002", Inicio, Inicio.AddDays(2), null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var preview = await conciliador.PrevisualizarAsync(entradas, "memoria", null);
        var antes = await ConteosAsync();

        var confirmacion = await conciliador.ConfirmarAsync(
            entradas, "memoria", preview.Huella, "qa-test", null);

        confirmacion.TrabajadoresProgramados.ShouldBe(1);
        confirmacion.FechasProgramadas.ShouldBe(3);
        var despues = await ConteosAsync();
        despues.Programaciones.ShouldBeGreaterThan(antes.Programaciones);
        despues.Asignaciones.ShouldBeGreaterThan(antes.Asignaciones);
    }

    [Fact]
    public async Task PREV03_huella_cambia_si_catalogo_cambia_entre_preview_y_confirm()
    {
        await CrearTrabajadorAsync("90000003", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000003", Inicio, Inicio, null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var preview = await conciliador.PrevisualizarAsync(entradas, "memoria", null);

        var turnoReal = await contexto.Turnos.FirstAsync(t => t.Id == turno.Id);
        turnoReal.MinutosTolerancia = 99;
        turnoReal.ModificadoEn = DateTime.UtcNow;
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "memoria", preview.Huella, "qa-test", null));
    }

    [Fact]
    public async Task PREV04_descanso_en_memoria_se_previsualiza_correctamente()
    {
        await CrearTrabajadorAsync("90000004", conTurno: false);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000004", null, null, Inicio,
                null, null, null, null, null, false, TipoDiaProgramacion.Descanso)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        resultado.Filas.Count.ShouldBe(1);
        resultado.Filas[0].TipoDia.ShouldBe("DESCANSO");
        resultado.Descansos.ShouldBe(1);
    }

    [Fact]
    public async Task PREV05_turno_nuevo_en_memoria_se_propone()
    {
        await CrearTrabajadorAsync("90000005", conTurno: false);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000005", Inicio, Inicio, null,
                "MEMTEST", "Turno memoria", new TimeOnly(8, 0), new TimeOnly(16, 0),
                0, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        resultado.PuedeConfirmar.ShouldBeTrue();
        resultado.Turnos.ShouldContain(t => t.Codigo == "MEMTEST"
            && t.Estado == EstadoFilaProgramacion.NuevoPropuesto);
    }

    [Fact]
    public async Task PREV06_duplicado_intralote_en_memoria_produce_conflicto()
    {
        await CrearTrabajadorAsync("90000006", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000006", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo),
            Entrada("90000006", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        resultado.PuedeConfirmar.ShouldBeFalse();
        resultado.Filas.ShouldAllBe(f => f.Estado == EstadoFilaProgramacion.Conflicto);
    }

    [Fact]
    public async Task PREV07_trabajador_inexistente_en_memoria_produce_incompleto()
    {
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("99999999", null, null, Inicio,
                "DIU", "Diurno", new TimeOnly(7, 0), new TimeOnly(19, 0),
                0, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        resultado.PuedeConfirmar.ShouldBeFalse();
        resultado.Filas[0].Estado.ShouldBe(EstadoFilaProgramacion.Incompleto);
    }

    [Fact]
    public async Task PREV08_idempotencia_en_memoria_segunda_vez_no_duplica()
    {
        await CrearTrabajadorAsync("90000008", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("90000008", Inicio, Inicio.AddDays(1), null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var preview1 = await conciliador.PrevisualizarAsync(entradas, "memoria", null);
        await conciliador.ConfirmarAsync(entradas, "memoria", preview1.Huella, "qa-test", null);
        var conteos1 = await ConteosAsync();

        contexto.ChangeTracker.Clear();
        var preview2 = await conciliador.PrevisualizarAsync(entradas, "memoria", null);
        preview2.Filas.ShouldAllBe(f => f.Estado == EstadoFilaProgramacion.Existente);
        await conciliador.ConfirmarAsync(entradas, "memoria", preview2.Huella, "qa-test", null);
        var conteos2 = await ConteosAsync();

        conteos2.Programaciones.ShouldBe(conteos1.Programaciones);
        conteos2.Asignaciones.ShouldBe(conteos1.Asignaciones);
    }

    private ConciliadorProgramacion Conciliador(IReadOnlyCollection<long>? alcance = null) =>
        new(contexto, new ValidadorPreparacionTareo(contexto, new Alcance(alcance)));

    private EntradaProgramacion Entrada(
        string documento, DateOnly? desde, DateOnly? hasta, DateOnly? fecha,
        string? codigo, string? nombre, TimeOnly? horaInicio, TimeOnly? horaFin,
        int? refrigerio, bool nocturno, TipoDiaProgramacion tipo, int fila = 1) =>
        new(fila, "DNI", documento,
            contexto.Empresas.AsNoTracking().First(e => e.Activo).Codigo,
            contexto.Plantas.AsNoTracking().First(p => p.Activo).Codigo,
            desde, hasta, fecha, codigo, nombre, horaInicio, horaFin,
            refrigerio, nocturno, tipo, "FIJO");

    private async Task<Trabajador> CrearTrabajadorAsync(string documento, bool conTurno)
    {
        var empresa = await contexto.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);
        var tipo = await contexto.TiposDocumento.AsNoTracking().FirstAsync(t => t.Codigo == "DNI");
        var puesto = await contexto.Puestos.AsNoTracking().FirstAsync(p => p.Activo);
        var trabajador = new Trabajador
        {
            TipoDocumentoId = tipo.Id, NumeroDocumento = documento, ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "PREV", Nombres = documento, Sexo = Sexo.Femenino,
            EmpresaId = empresa.Id, PlantaId = planta.Id, TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = puesto.Id, FechaIngreso = Inicio
        };
        var vinculo = new PeriodoLaboral { EmpresaId = empresa.Id, FechaIngreso = Inicio };
        vinculo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = planta.Id, FechaInicio = Inicio
        });
        trabajador.PeriodosLaborales.Add(vinculo);
        if (conTurno)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = (await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo)).Id,
                FechaInicio = Inicio
            });
        }
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador;
    }

    private async Task<(int Turnos, int Cuadrillas, int Programaciones, int Tramos, int Asignaciones)> ConteosAsync() =>
        (await contexto.Turnos.CountAsync(), await contexto.Cuadrillas.CountAsync(),
         await contexto.ProgramacionesCuadrilla.CountAsync(), await contexto.TramosRotacion.CountAsync(),
         await contexto.AsignacionesTurno.CountAsync());

    // ── FASE 1.1 § 1 — EQUIVALENCIA EXCEL ↔ MEMORIA ──────────────────────

    [Fact]
    public async Task EQ01_mismas_jornadas_por_excel_y_memoria_producen_resultado_identico()
    {
        await CrearTrabajadorAsync("91000001", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var empresa = await contexto.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);

        using var excel = LibroExcel(
            FilaXl("91000001", empresa.Codigo, planta.Codigo, Inicio, Inicio.AddDays(2), null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"),
            FilaXl("91000001", empresa.Codigo, planta.Codigo, null, null, Inicio.AddDays(3),
                null, null, null, null, null, "DESCANSO"));
        var importador = new ImportadorProgramacionLaboral(Conciliador());
        var vistaExcel = await importador.PrevisualizarAsync(excel, "eq01.xlsx", null);

        var entradas = new List<EntradaProgramacion>
        {
            Entrada("91000001", Inicio, Inicio.AddDays(2), null,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo, fila: 2),
            Entrada("91000001", null, null, Inicio.AddDays(3),
                null, null, null, null, null, false, TipoDiaProgramacion.Descanso, fila: 3)
        };
        contexto.ChangeTracker.Clear();
        var vistaMemoria = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        vistaExcel.FechasExpandidas.ShouldBe(vistaMemoria.FechasExpandidas);
        vistaExcel.TrabajadoresIdentificados.ShouldBe(vistaMemoria.TrabajadoresIdentificados);
        vistaExcel.Descansos.ShouldBe(vistaMemoria.Descansos);
        vistaExcel.Nocturnos.ShouldBe(vistaMemoria.Nocturnos);
        vistaExcel.PuedeConfirmar.ShouldBe(vistaMemoria.PuedeConfirmar);
        vistaExcel.Conflictos.ShouldBe(vistaMemoria.Conflictos);
        vistaExcel.Incompletos.ShouldBe(vistaMemoria.Incompletos);
        vistaExcel.Turnos.Count.ShouldBe(vistaMemoria.Turnos.Count);
        for (var i = 0; i < vistaExcel.Filas.Count; i++)
        {
            vistaExcel.Filas[i].TrabajadorId.ShouldBe(vistaMemoria.Filas[i].TrabajadorId);
            vistaExcel.Filas[i].Fecha.ShouldBe(vistaMemoria.Filas[i].Fecha);
            vistaExcel.Filas[i].TipoDia.ShouldBe(vistaMemoria.Filas[i].TipoDia);
            vistaExcel.Filas[i].CodigoTurno.ShouldBe(vistaMemoria.Filas[i].CodigoTurno);
            vistaExcel.Filas[i].Estado.ShouldBe(vistaMemoria.Filas[i].Estado);
            vistaExcel.Filas[i].Nocturno.ShouldBe(vistaMemoria.Filas[i].Nocturno);
        }
    }

    [Fact]
    public async Task EQ02_huella_SHA_coincide_entre_excel_y_memoria()
    {
        await CrearTrabajadorAsync("91000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var empresa = await contexto.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);

        using var excel = LibroExcel(
            FilaXl("91000002", empresa.Codigo, planta.Codigo, null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        var importador = new ImportadorProgramacionLaboral(Conciliador());
        var vistaExcel = await importador.PrevisualizarAsync(excel, "eq02.xlsx", null);

        var entradas = new List<EntradaProgramacion>
        {
            Entrada("91000002", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo, fila: 2)
        };
        contexto.ChangeTracker.Clear();
        var vistaMemoria = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        vistaExcel.Huella.ShouldBe(vistaMemoria.Huella);
    }

    [Fact]
    public async Task EQ03_turno_nuevo_propuesto_por_ambas_vias_coincide()
    {
        await CrearTrabajadorAsync("91000003", conTurno: false);
        var empresa = await contexto.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);

        using var excel = LibroExcel(
            FilaXl("91000003", empresa.Codigo, planta.Codigo, null, null, Inicio,
                "EQTEST", "Turno equivalencia", new TimeOnly(6, 0), new TimeOnly(14, 0), 30, "TRABAJO"));
        var importador = new ImportadorProgramacionLaboral(Conciliador());
        var vistaExcel = await importador.PrevisualizarAsync(excel, "eq03.xlsx", null);

        var entradas = new List<EntradaProgramacion>
        {
            Entrada("91000003", null, null, Inicio,
                "EQTEST", "Turno equivalencia", new TimeOnly(6, 0), new TimeOnly(14, 0),
                30, false, TipoDiaProgramacion.Trabajo, fila: 2)
        };
        contexto.ChangeTracker.Clear();
        var vistaMemoria = await Conciliador().PrevisualizarAsync(entradas, "memoria", null);

        vistaExcel.Huella.ShouldBe(vistaMemoria.Huella);
        vistaExcel.Turnos.Single().Codigo.ShouldBe(vistaMemoria.Turnos.Single().Codigo);
        vistaExcel.Turnos.Single().Estado.ShouldBe(vistaMemoria.Turnos.Single().Estado);
    }

    // ── FASE 1.1 § 2 — CONTRATOS DE SEGURIDAD ─────────────────────────────

    [Fact]
    public async Task SEC01_planta_fuera_del_alcance_autorizado_se_rechaza()
    {
        await CrearTrabajadorAsync("92000001", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000001", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "sec01", [planta.Id + 999]);

        resultado.Conflictos.ShouldBe(1);
        resultado.PuedeConfirmar.ShouldBeFalse();
        resultado.Filas.Single().Motivo!.ShouldContain("alcance autorizado");
    }

    [Fact]
    public async Task SEC02_alcance_vacio_deniega_todo_fail_closed()
    {
        await CrearTrabajadorAsync("92000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000002", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "sec02", []);

        resultado.Conflictos.ShouldBe(1);
        resultado.PuedeConfirmar.ShouldBeFalse();
    }

    [Fact]
    public async Task SEC03_periodo_cerrado_bloquea_programacion()
    {
        await CrearTrabajadorAsync("92000003", conTurno: false);
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "SEC03", Nombre = "Cerrado sec", FechaInicio = Inicio,
            FechaFin = Inicio.AddDays(10), Estado = EstadoPeriodo.Cerrado
        });
        await contexto.SaveChangesAsync();
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000003", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "sec03", null);

        resultado.Conflictos.ShouldBe(1);
        resultado.PuedeConfirmar.ShouldBeFalse();
        resultado.Filas.Single().Motivo!.ShouldContain("cerrado");
    }

    [Fact]
    public async Task SEC04_conflicto_bloquea_confirmacion()
    {
        await CrearTrabajadorAsync("92000004", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000004", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo),
            Entrada("92000004", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "sec04", null);
        vista.PuedeConfirmar.ShouldBeFalse();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "sec04", vista.Huella, "qa", null));
    }

    [Fact]
    public async Task SEC05_rollback_completo_cuando_huella_no_coincide()
    {
        await CrearTrabajadorAsync("92000005", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000005", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "sec05", null);
        var antes = await ConteosAsync();

        var turnoReal = await contexto.Turnos.FirstAsync(t => t.Id == turno.Id);
        turnoReal.MinutosTolerancia = 77;
        turnoReal.ModificadoEn = DateTime.UtcNow;
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "sec05", vista.Huella, "qa", null));

        var despues = await ConteosAsync();
        despues.Programaciones.ShouldBe(antes.Programaciones);
        despues.Asignaciones.ShouldBe(antes.Asignaciones);
        despues.Cuadrillas.ShouldBe(antes.Cuadrillas);
    }

    [Fact]
    public async Task SEC06_huella_vacia_o_blanca_rechaza_confirmacion()
    {
        await CrearTrabajadorAsync("92000006", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000006", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            Conciliador().ConfirmarAsync(entradas, "sec06", "", "qa", null));
        await Should.ThrowAsync<ReglaNegocioException>(() =>
            Conciliador().ConfirmarAsync(entradas, "sec06", "   ", "qa", null));
    }

    [Fact]
    public async Task SEC07_entradas_vacias_rechaza()
    {
        await Should.ThrowAsync<ReglaNegocioException>(() =>
            Conciliador().PrevisualizarAsync([], "sec07", null));
    }

    [Fact]
    public async Task SEC08_alcance_autorizado_permite_planta_correcta()
    {
        await CrearTrabajadorAsync("92000008", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("92000008", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };

        var resultado = await Conciliador().PrevisualizarAsync(entradas, "sec08", [planta.Id]);

        resultado.PuedeConfirmar.ShouldBeTrue();
        resultado.Conflictos.ShouldBe(0);
    }

    // ── FASE 1.1 § 2b — CONCURRENCIA ──────────────────────────────────────

    [Fact]
    public async Task CONC01_segunda_confirmacion_secuencial_rechaza_por_huella_obsoleta()
    {
        await CrearTrabajadorAsync("93000001", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("93000001", null, null, Inicio,
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "conc01", null);
        await conciliador.ConfirmarAsync(entradas, "conc01", vista.Huella, "qa", null);

        contexto.ChangeTracker.Clear();
        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "conc01-bis", vista.Huella, "qa", null));
    }

    [Fact]
    public async Task CONC02_confirmaciones_simultaneas_se_serializan_sin_duplicar()
    {
        await CrearTrabajadorAsync("93000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("93000002", null, null, Inicio.AddDays(10),
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "conc02", null);

        var tarea1 = conciliador.ConfirmarAsync(entradas, "conc02a", vista.Huella, "qa", null);
        var tarea2 = conciliador.ConfirmarAsync(entradas, "conc02b", vista.Huella, "qa", null);

        var resultados = await Task.WhenAll(
            tarea1.ContinueWith(t => (Exito: t.IsCompletedSuccessfully, Error: t.Exception)),
            tarea2.ContinueWith(t => (Exito: t.IsCompletedSuccessfully, Error: t.Exception)));

        var exitosas = resultados.Count(r => r.Exito);
        var fallidas = resultados.Count(r => !r.Exito);
        (exitosas + fallidas).ShouldBe(2);
        exitosas.ShouldBeGreaterThanOrEqualTo(1);

        contexto.ChangeTracker.Clear();
        var trabajadorId = (await contexto.Trabajadores.FirstAsync(
            t => t.NumeroDocumento == "93000002")).Id;
        var asignaciones = await contexto.AsignacionesTurno
            .CountAsync(a => a.TrabajadorId == trabajadorId);
        asignaciones.ShouldBe(1);
    }

    // ── FASE 1.1 § 3 — ROLLBACK TRANSACCIONAL ──────────────────────────────

    [Fact]
    public async Task ROLL01_fallo_en_transaccion_no_persiste_escrituras_parciales()
    {
        await CrearTrabajadorAsync("94000001", conTurno: false);
        await CrearTrabajadorAsync("94000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("94000001", null, null, Inicio.AddDays(20),
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo),
            Entrada("94000002", null, null, Inicio.AddDays(20),
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "roll01", null);
        var antes = await ConteosAsync();

        var turnoReal = await contexto.Turnos.FirstAsync(t => t.Id == turno.Id);
        turnoReal.MinutosGraciaSalida = 99;
        turnoReal.ModificadoEn = DateTime.UtcNow;
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "roll01", vista.Huella, "qa", null));

        contexto.ChangeTracker.Clear();
        var despues = await ConteosAsync();
        despues.Programaciones.ShouldBe(antes.Programaciones);
        despues.Asignaciones.ShouldBe(antes.Asignaciones);
        despues.Cuadrillas.ShouldBe(antes.Cuadrillas);
        despues.Tramos.ShouldBe(antes.Tramos);
    }

    [Fact]
    public async Task ROLL02_periodo_cerrado_detectado_en_confirmacion_revierte_todo()
    {
        await CrearTrabajadorAsync("94000003", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("94000003", null, null, Inicio.AddDays(25),
                turno.Codigo, turno.Nombre, turno.HoraInicio, turno.HoraFin,
                turno.MinutosRefrigerio, false, TipoDiaProgramacion.Trabajo)
        };
        var conciliador = Conciliador();
        var vista = await conciliador.PrevisualizarAsync(entradas, "roll02", null);
        var antes = await ConteosAsync();

        contexto.Periodos.Add(new Periodo
        {
            Codigo = "ROLL02", Nombre = "Cerrado roll", FechaInicio = Inicio.AddDays(20),
            FechaFin = Inicio.AddDays(30), Estado = EstadoPeriodo.Cerrado
        });
        await contexto.SaveChangesAsync();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            conciliador.ConfirmarAsync(entradas, "roll02", vista.Huella, "qa", null));

        contexto.ChangeTracker.Clear();
        var despues = await ConteosAsync();
        despues.Programaciones.ShouldBe(antes.Programaciones);
        despues.Asignaciones.ShouldBe(antes.Asignaciones);
    }

    // ── FASE 1.1-B § A — ROLLBACK CON FALLO POST-ESCRITURA ────────────────

    [Fact]
    public async Task ROLL03_fallo_despues_de_primera_escritura_revierte_turno_nuevo_y_todo_lo_demas()
    {
        await CrearTrabajadorAsync("94000004", conTurno: false);
        var entradas = new List<EntradaProgramacion>
        {
            Entrada("94000004", Inicio.AddDays(30), Inicio.AddDays(32), null,
                "ROLLNEW", "Turno rollback", new TimeOnly(6, 0), new TimeOnly(14, 0),
                0, false, TipoDiaProgramacion.Trabajo)
        };
        var interceptor = new FalloEnSegundoSave();
        using var conexionRoll = new SqliteConnection("DataSource=:memory:");
        conexionRoll.Open();
        using var ctxRoll = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexionRoll)
            .AddInterceptors(interceptor)
            .Options);
        ctxRoll.Database.EnsureCreated();

        var empresa = await ctxRoll.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await ctxRoll.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);
        var tipo = await ctxRoll.TiposDocumento.AsNoTracking().FirstAsync(t => t.Codigo == "DNI");
        var puesto = await ctxRoll.Puestos.AsNoTracking().FirstAsync(p => p.Activo);
        var trabajador = new Trabajador
        {
            TipoDocumentoId = tipo.Id, NumeroDocumento = "94000004", ApellidoPaterno = "ROLL",
            ApellidoMaterno = "TRES", Nombres = "94000004", Sexo = Sexo.Femenino,
            EmpresaId = empresa.Id, PlantaId = planta.Id, TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = puesto.Id, FechaIngreso = Inicio
        };
        var vinculo = new PeriodoLaboral { EmpresaId = empresa.Id, FechaIngreso = Inicio };
        vinculo.AsignacionesPlanta.Add(new AsignacionPlanta { PlantaId = planta.Id, FechaInicio = Inicio });
        trabajador.PeriodosLaborales.Add(vinculo);
        ctxRoll.Trabajadores.Add(trabajador);
        await ctxRoll.SaveChangesAsync();

        var entradasRoll = new List<EntradaProgramacion>
        {
            new(1, "DNI", "94000004", empresa.Codigo, planta.Codigo,
                Inicio.AddDays(30), Inicio.AddDays(32), null,
                "ROLLNEW", "Turno rollback", new TimeOnly(6, 0), new TimeOnly(14, 0),
                0, false, TipoDiaProgramacion.Trabajo, "FIJO")
        };

        var turnosAntes = await ctxRoll.Turnos.CountAsync();
        var cuadrillasAntes = await ctxRoll.Cuadrillas.CountAsync();
        var programacionesAntes = await ctxRoll.ProgramacionesCuadrilla.CountAsync();
        var asignacionesAntes = await ctxRoll.AsignacionesTurno.CountAsync();
        var tramosAntes = await ctxRoll.TramosRotacion.CountAsync();

        interceptor.Activar();
        var conciliadorRoll = new ConciliadorProgramacion(
            ctxRoll, new ValidadorPreparacionTareo(ctxRoll, new Alcance(null)));
        var vista = await conciliadorRoll.PrevisualizarAsync(entradasRoll, "roll03", null);
        vista.PuedeConfirmar.ShouldBeTrue();
        vista.Turnos.ShouldContain(t => t.Codigo == "ROLLNEW"
            && t.Estado == EstadoFilaProgramacion.NuevoPropuesto);

        await Should.ThrowAsync<Exception>(() =>
            conciliadorRoll.ConfirmarAsync(entradasRoll, "roll03", vista.Huella, "qa", null));

        ctxRoll.ChangeTracker.Clear();
        (await ctxRoll.Turnos.CountAsync()).ShouldBe(turnosAntes,
            "El turno nuevo creado en la primera escritura debió revertirse");
        (await ctxRoll.Cuadrillas.CountAsync()).ShouldBe(cuadrillasAntes);
        (await ctxRoll.ProgramacionesCuadrilla.CountAsync()).ShouldBe(programacionesAntes);
        (await ctxRoll.AsignacionesTurno.CountAsync()).ShouldBe(asignacionesAntes);
        (await ctxRoll.TramosRotacion.CountAsync()).ShouldBe(tramosAntes);
        (await ctxRoll.Turnos.AnyAsync(t => t.Codigo == "ROLLNEW")).ShouldBeFalse(
            "El turno ROLLNEW no debe existir tras el rollback");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MemoryStream LibroExcel(params FilaXlEq[] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Programacion");
        string[] cabeceras =
        [
            "Tipo de documento", "Número de documento", "Empresa", "Unidad",
            "Fecha de inicio", "Fecha de fin", "Fecha específica",
            "Código de turno", "Nombre del turno", "Hora de entrada", "Hora de salida",
            "Salida al día siguiente", "Minutos de refrigerio", "Tipo de día",
            "Modalidad", "Observaciones", "Referencia de aprobación"
        ];
        for (var c = 0; c < cabeceras.Length; c++)
        {
            hoja.Cell(1, c + 1).Value = cabeceras[c];
        }
        for (var i = 0; i < filas.Length; i++)
        {
            var f = filas[i];
            string?[] valores =
            [
                "DNI", f.Documento, f.Empresa, f.Unidad,
                f.Desde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                f.Hasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                f.Fecha?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                f.CodigoTurno, f.NombreTurno,
                f.HoraInicio?.ToString("HH:mm", CultureInfo.InvariantCulture),
                f.HoraFin?.ToString("HH:mm", CultureInfo.InvariantCulture),
                f.HoraInicio is not null && f.HoraFin is not null && f.HoraFin <= f.HoraInicio ? "SI" : "NO",
                f.MinutosRefrigerio?.ToString(CultureInfo.InvariantCulture),
                f.TipoDia, "FIJO", "QA-EQ", "QA-EQ-AUT"
            ];
            for (var c = 0; c < valores.Length; c++)
            {
                hoja.Cell(i + 2, c + 1).Value = valores[c] ?? string.Empty;
            }
        }
        var salida = new MemoryStream();
        libro.SaveAs(salida);
        salida.Position = 0;
        return salida;
    }

    private sealed record FilaXlEq(
        string Documento, string Empresa, string Unidad,
        DateOnly? Desde, DateOnly? Hasta, DateOnly? Fecha,
        string? CodigoTurno, string? NombreTurno,
        TimeOnly? HoraInicio, TimeOnly? HoraFin, int? MinutosRefrigerio,
        string TipoDia);

    private static FilaXlEq FilaXl(
        string documento, string empresa, string unidad,
        DateOnly? desde, DateOnly? hasta, DateOnly? fecha,
        string? codigo, string? nombre, TimeOnly? inicio, TimeOnly? fin,
        int? refrigerio, string tipoDia) =>
        new(documento, empresa, unidad, desde, hasta, fecha, codigo, nombre, inicio, fin, refrigerio, tipoDia);

    private sealed class Alcance(IReadOnlyCollection<long>? plantas) : IProveedorAlcancePlantas
    {
        public Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(plantas);
    }

    private sealed class FalloEnSegundoSave : SaveChangesInterceptor
    {
        private bool _activo;
        private int _llamadas;

        public void Activar() { _activo = true; _llamadas = 0; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_activo && Interlocked.Increment(ref _llamadas) >= 2)
            {
                throw new InvalidOperationException(
                    "Fallo inyectado después de la primera escritura (ROLL03).");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
