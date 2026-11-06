using ClosedXML.Excel;
using System.Globalization;
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
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Programacion;

#pragma warning disable IDE0011 // Bucles compactos del generador de libros de prueba.

public sealed class ImportadorProgramacionLaboralTests : IDisposable
{
    private static readonly DateOnly Inicio = new(2026, 11, 2);
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly MarcacionDbContext contexto;

    public ImportadorProgramacionLaboralTests()
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
    public void CATTURN_PLANTILLA_es_un_xlsx_real_con_columnas_y_validaciones()
    {
        var bytes = Servicio().GenerarPlantillaExcel();
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheet("Programacion");

        hoja.Cell("A1").GetString().ShouldBe("Tipo de documento");
        hoja.Cell("N1").GetString().ShouldBe("Tipo de día");
        hoja.Cell("A2").GetString().ShouldBe("DNI");
        hoja.Cell("N3").GetString().ShouldBe("DESCANSO");
        hoja.DataValidations.Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task CATTURN_01_02_09_10_13_18_turnos_intervalo_nocturno_y_preview_sin_escrituras()
    {
        var trabajador = await CrearTrabajadorAsync("81000001", conTurno: false);
        var existente = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var antes = await ConteosAsync();
        using var excel = Libro(
            Fila("81000001", Inicio, Inicio.AddDays(1), null, existente.Codigo, existente.Nombre,
                existente.HoraInicio, existente.HoraFin, existente.MinutosRefrigerio, "TRABAJO"),
            Fila("81000001", null, null, Inicio.AddDays(2), "NUEVONOC", "Nocturno QA",
                new TimeOnly(19, 0), new TimeOnly(5, 0), 0, "TRABAJO", salidaSiguiente: "SI"));

        var vista = await Servicio().PrevisualizarAsync(excel, "programacion.xlsx", null);

        vista.PuedeConfirmar.ShouldBeTrue();
        vista.FechasExpandidas.ShouldBe(3);
        vista.Nocturnos.ShouldBe(1);
        vista.Turnos.ShouldContain(t => t.Codigo == existente.Codigo && t.Estado == EstadoFilaProgramacion.Existente);
        vista.Turnos.ShouldContain(t => t.Codigo == "NUEVONOC" && t.Estado == EstadoFilaProgramacion.NuevoPropuesto);
        (await ConteosAsync()).ShouldBe(antes);
        trabajador.AsignacionesTurno.ShouldBeEmpty();
    }

    [Fact]
    public async Task CATTURN_03_turno_homonimo_o_codigo_incompatible_no_se_reutiliza()
    {
        await CrearTrabajadorAsync("81000002", conTurno: false);
        var existente = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(Fila("81000002", null, null, Inicio, existente.Codigo, existente.Nombre,
            existente.HoraInicio.AddMinutes(30), existente.HoraFin, existente.MinutosRefrigerio, "TRABAJO"));

        var vista = await Servicio().PrevisualizarAsync(excel, "incompatible.xlsx", null);

        vista.Conflictos.ShouldBe(1);
        vista.PuedeConfirmar.ShouldBeFalse();
        (vista.Filas.Single().Motivo ?? string.Empty).ShouldContain("no se sobrescribirá");
    }

    [Fact]
    public async Task CATTURN_04_05_07_08_duplicados_inexistentes_y_alcance_fail_closed_se_bloquean()
    {
        await CrearTrabajadorAsync("81000003", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var fila = Fila("81000003", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO");
        using var duplicado = Libro(fila, fila);
        var vistaDuplicada = await Servicio().PrevisualizarAsync(duplicado, "duplicado.xlsx", null);
        vistaDuplicada.Conflictos.ShouldBe(2);

        using var inexistente = Libro(fila with { Documento = "99999999" });
        var vistaInexistente = await Servicio().PrevisualizarAsync(inexistente, "inexistente.xlsx", null);
        vistaInexistente.Incompletos.ShouldBe(1);

        using var sinAlcance = Libro(fila);
        var vistaSinAlcance = await Servicio().PrevisualizarAsync(sinAlcance, "alcance.xlsx", []);
        vistaSinAlcance.Conflictos.ShouldBe(1);
        (vistaSinAlcance.Filas.Single().Motivo ?? string.Empty).ShouldContain("alcance autorizado");
    }

    [Fact]
    public async Task CATTURN_06_reingreso_se_resuelve_por_vinculo_historico()
    {
        var trabajador = await CrearTrabajadorAsync("81000004", conTurno: false, fechaCese: Inicio.AddDays(2));
        var segundoInicio = Inicio.AddDays(10);
        var segundo = new PeriodoLaboral { EmpresaId = trabajador.EmpresaId, FechaIngreso = segundoInicio };
        segundo.AsignacionesPlanta.Add(new AsignacionPlanta { PlantaId = trabajador.PlantaId, FechaInicio = segundoInicio });
        trabajador.PeriodosLaborales.Add(segundo);
        await contexto.SaveChangesAsync();
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(Fila("81000004", null, null, segundoInicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));

        var vista = await Servicio().PrevisualizarAsync(excel, "reingreso.xlsx", null);

        vista.PuedeConfirmar.ShouldBeTrue();
        vista.Filas.Single().TrabajadorId.ShouldBe(trabajador.Id);
    }

    [Fact]
    public async Task CATTURN_11_12_14_25_26_27_30_confirma_trabajo_descanso_historia_y_readiness_sin_tareo()
    {
        var trabajador = await CrearTrabajadorAsync("81000005", conTurno: false, conMarcador: true);
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "2026-11", Nombre = "Noviembre 2026", FechaInicio = Inicio,
            FechaFin = Inicio.AddDays(30), Estado = EstadoPeriodo.Abierto
        });
        await contexto.SaveChangesAsync();
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(
            Fila("81000005", null, null, Inicio, turno.Codigo, turno.Nombre,
                turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"),
            Fila("81000005", null, null, Inicio.AddDays(1), null, null, null, null, null, "DESCANSO"),
            Fila("81000005", null, null, Inicio.AddDays(2), "NOCQA", "Noche QA",
                new TimeOnly(19, 0), new TimeOnly(5, 0), 0, "TRABAJO", salidaSiguiente: "SI"),
            Fila("81000005", null, null, Inicio.AddDays(6), turno.Codigo, turno.Nombre,
                turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        var bytes = excel.ToArray();
        var antesMarcas = await contexto.MarcacionesCrudas.CountAsync();
        var antesTareo = await contexto.TareosDiarios.CountAsync();
        var vista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "integral.xlsx", null);

        var confirmado = await Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "integral.xlsx", vista.Huella, "qa", null);

        confirmado.TrabajadoresProgramados.ShouldBe(1);
        confirmado.FechasProgramadas.ShouldBe(4);
        confirmado.DescansosProgramados.ShouldBe(1);
        confirmado.TurnosCreados.ShouldBe(1);
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(antesMarcas);
        (await contexto.TareosDiarios.CountAsync()).ShouldBe(antesTareo);
        var asignacion = await contexto.AsignacionesTurno.AsNoTracking()
            .Include(a => a.Cuadrilla)!.ThenInclude(c => c!.Programaciones).ThenInclude(p => p.Tramos)
            .SingleAsync(a => a.TrabajadorId == trabajador.Id && a.FechaInicio == Inicio);
        var programacion = asignacion.Cuadrilla!.Programaciones.Single(p => p.FechaInicio == Inicio);
        programacion.ResolverEn(Inicio.AddDays(1)).Estado.ShouldBe(EstadoProgramacionDiaria.DescansoProgramado);
        programacion.ResolverEn(Inicio.AddDays(2)).TurnoId.ShouldNotBeNull();
        (await contexto.AsignacionesTurno.AsNoTracking()
            .Include(a => a.Cuadrilla)!.ThenInclude(c => c!.Programaciones).ThenInclude(p => p.Tramos)
            .SingleAsync(a => a.TrabajadorId == trabajador.Id && a.FechaInicio == Inicio.AddDays(6)))
            .Cuadrilla!.Programaciones.Single(p => p.FechaInicio == Inicio.AddDays(6))
            .ResolverEn(Inicio.AddDays(6)).Estado.ShouldBe(EstadoProgramacionDiaria.Trabajo);
        asignacion.CreadoPor.ShouldBe("qa");
        var preparacion = await new ValidadorPreparacionTareo(contexto, new Alcance(null))
            .EvaluarIntervaloAsync(trabajador.Id, Inicio, Inicio.AddDays(2));
        preparacion.Estado.ShouldBe(EstadoPreparacionTareo.Listo);
    }

    [Fact]
    public async Task CATTURN_15_16_17_solapamiento_aprobado_y_periodo_cerrado_no_se_reescriben()
    {
        var trabajador = await CrearTrabajadorAsync("81000006", conTurno: true);
        var otro = await contexto.Turnos.AsNoTracking().Skip(1).FirstAsync(t => t.Activo);
        using var solapado = Libro(Fila("81000006", null, null, Inicio, otro.Codigo, otro.Nombre,
            otro.HoraInicio, otro.HoraFin, otro.MinutosRefrigerio, "TRABAJO"));
        var conflicto = await Servicio().PrevisualizarAsync(solapado, "solapado.xlsx", null);
        conflicto.Conflictos.ShouldBe(1);

        var asignacionOriginal = await contexto.AsignacionesTurno.SingleAsync(a => a.TrabajadorId == trabajador.Id);
        asignacionOriginal.FechaFin = Inicio.AddDays(-1);
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "CERRADO", Nombre = "Cerrado", FechaInicio = Inicio, FechaFin = Inicio.AddDays(5),
            Estado = EstadoPeriodo.Cerrado
        });
        await contexto.SaveChangesAsync();
        using var cerrado = Libro(Fila("81000006", null, null, Inicio, otro.Codigo, otro.Nombre,
            otro.HoraInicio, otro.HoraFin, otro.MinutosRefrigerio, "TRABAJO"));
        var vistaCerrada = await Servicio().PrevisualizarAsync(cerrado, "cerrado.xlsx", null);
        vistaCerrada.Conflictos.ShouldBe(1);
        (vistaCerrada.Filas.Single().Motivo ?? string.Empty).ShouldContain("cerrado");
    }

    [Fact]
    public async Task CATTURN_19_20_21_cancelacion_token_valido_y_token_obsoleto()
    {
        await CrearTrabajadorAsync("81000007", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var fila = Fila("81000007", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO");
        using var excel = Libro(fila);
        var bytes = excel.ToArray();
        var vista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "token.xlsx", null);
        (await contexto.AsignacionesTurno.CountAsync()).ShouldBe(0); // cancelar equivale a no confirmar

        contexto.Turnos.First().ModificadoPor = "concurrente";
        await contexto.SaveChangesAsync();
        await Should.ThrowAsync<ReglaNegocioException>(() => Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "token.xlsx", vista.Huella, "qa", null));
        (await contexto.AsignacionesTurno.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CATTURN_22_23_reimportacion_es_idempotente_y_token_serializa_confirmacion()
    {
        await CrearTrabajadorAsync("81000008", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var fila = Fila("81000008", Inicio, Inicio.AddDays(1), null, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO");
        using var excel = Libro(fila);
        var bytes = excel.ToArray();
        var primeraVista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "idem.xlsx", null);
        await Servicio().ConfirmarAsync(new MemoryStream(bytes), "idem.xlsx", primeraVista.Huella, "qa", null);
        var asignaciones = await contexto.AsignacionesTurno.CountAsync();
        var segundaVista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "idem.xlsx", null);
        var segunda = await Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "idem.xlsx", segundaVista.Huella, "qa", null);

        segunda.RegistrosIdempotentes.ShouldBe(2);
        segunda.FechasProgramadas.ShouldBe(0);
        (await contexto.AsignacionesTurno.CountAsync()).ShouldBe(asignaciones);
    }

    [Fact]
    public async Task CATTURN_24_28_29_rollback_conflicto_y_ausencia_de_marca_no_crea_descanso()
    {
        await CrearTrabajadorAsync("81000009", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(
            Fila("81000009", null, null, Inicio, turno.Codigo, turno.Nombre,
                turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"),
            Fila("81000009", null, null, Inicio.AddDays(1), turno.Codigo, turno.Nombre,
                turno.HoraInicio.AddMinutes(10), turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        var vista = await Servicio().PrevisualizarAsync(excel, "rollback.xlsx", null);

        vista.Conflictos.ShouldBe(1);
        vista.Descansos.ShouldBe(0);
        (await contexto.AsignacionesTurno.CountAsync()).ShouldBe(0);
        (await contexto.ProgramacionesCuadrilla.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task HEQA_06_09_turno_nuevo_conserva_duracion_efectiva_aprobada_en_turno_y_version()
    {
        await CrearTrabajadorAsync("81990001", conTurno: false, conMarcador: true);
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "HEQA-2026-11", Nombre = "HEQA noviembre", FechaInicio = Inicio,
            FechaFin = Inicio.AddDays(5), Estado = EstadoPeriodo.Abierto
        });
        await contexto.SaveChangesAsync();
        using var excel = Libro(Fila(
            "81990001", null, null, Inicio, "DIU-HEQA", "Diurno HEQA",
            new TimeOnly(7, 0), new TimeOnly(18, 22), 60, "TRABAJO"));
        var bytes = excel.ToArray();
        var vista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "heqa.xlsx", null);

        await Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "heqa.xlsx", vista.Huella, "qa-heqa", null);

        var turno = await contexto.Turnos.AsNoTracking()
            .Include(t => t.Versiones)
            .SingleAsync(t => t.Codigo == "DIU-HEQA");
        turno.DuracionBrutaMinutos.ShouldBe(682);
        turno.MinutosRefrigerio.ShouldBe(60);
        turno.MinutosJornadaOrdinaria.ShouldBe(622);
        turno.Versiones.Single().MinutosJornadaOrdinaria.ShouldBe(622);
        turno.Versiones.Single().FechaInicio.ShouldBe(Inicio);
    }

    [Fact]
    public async Task CATTURN_INTEGRAL_cinco_trabajadores_preview_confirmacion_readiness_y_reimportacion()
    {
        string[] documentos = ["82000001", "82000002", "82000003", "82000004", "82000005"];
        foreach (var documento in documentos)
        {
            await CrearTrabajadorAsync(documento, conTurno: false, conMarcador: true);
        }
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "INT-2026-11", Nombre = "Integral noviembre", FechaInicio = Inicio,
            FechaFin = Inicio.AddDays(15), Estado = EstadoPeriodo.Abierto
        });
        await contexto.SaveChangesAsync();
        var diu = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var filasValidas = new[]
        {
            Fila(documentos[0], Inicio, Inicio.AddDays(2), null, diu.Codigo, diu.Nombre,
                diu.HoraInicio, diu.HoraFin, diu.MinutosRefrigerio, "TRABAJO"),
            Fila(documentos[1], null, null, Inicio, diu.Codigo, diu.Nombre,
                diu.HoraInicio, diu.HoraFin, diu.MinutosRefrigerio, "TRABAJO"),
            Fila(documentos[1], null, null, Inicio.AddDays(1), null, null, null, null, null, "DESCANSO"),
            Fila(documentos[1], null, null, Inicio.AddDays(2), "NOCINT", "Nocturno integral",
                new TimeOnly(19, 0), new TimeOnly(5, 0), 0, "TRABAJO", "SI"),
            Fila(documentos[2], Inicio, Inicio.AddDays(2), null, "NOCINT", "Nocturno integral",
                new TimeOnly(19, 0), new TimeOnly(5, 0), 0, "TRABAJO", "SI"),
            Fila(documentos[3], null, null, Inicio, diu.Codigo, diu.Nombre,
                diu.HoraInicio, diu.HoraFin, diu.MinutosRefrigerio, "TRABAJO"),
            Fila(documentos[3], null, null, Inicio.AddDays(1), "NOCINT", "Nocturno integral",
                new TimeOnly(19, 0), new TimeOnly(5, 0), 0, "TRABAJO", "SI"),
            Fila(documentos[4], null, null, Inicio, diu.Codigo, diu.Nombre,
                diu.HoraInicio, diu.HoraFin, diu.MinutosRefrigerio, "TRABAJO")
        };
        using var contradictorio = Libro([.. filasValidas, filasValidas[^1] with { CodigoTurno = "NOCINT", NombreTurno = "Nocturno integral", HoraInicio = new TimeOnly(19, 0), HoraFin = new TimeOnly(5, 0), SalidaSiguiente = "SI" }]);
        (await Servicio().PrevisualizarAsync(contradictorio, "integral-conflicto.xlsx", null))
            .Conflictos.ShouldBe(2);

        using var correcto = Libro(filasValidas);
        var bytes = correcto.ToArray();
        var vista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "integral-cinco.xlsx", null);
        vista.TrabajadoresIdentificados.ShouldBe(5);
        vista.PuedeConfirmar.ShouldBeTrue();
        var confirmado = await Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "integral-cinco.xlsx", vista.Huella, "qa-integral", null);
        confirmado.TrabajadoresProgramados.ShouldBe(5);
        confirmado.DescansosProgramados.ShouldBe(1);

        var repeticion = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "integral-cinco.xlsx", null);
        var idempotente = await Servicio().ConfirmarAsync(
            new MemoryStream(bytes), "integral-cinco.xlsx", repeticion.Huella, "qa-integral", null);
        idempotente.FechasProgramadas.ShouldBe(0);
        idempotente.RegistrosIdempotentes.ShouldBe(vista.FechasExpandidas);
    }

    [Fact]
    public async Task CATDATE_01_fecha_nativa_Excel()
    {
        await CrearTrabajadorAsync("83000001", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var baseLibro = Libro(Fila("83000001", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", new DateTime(2026, 11, 2));

        var vista = await Servicio().PrevisualizarAsync(excel, "fecha-nativa.xlsx", null);

        vista.Filas.Single().Fecha.ShouldBe(Inicio);
    }

    [Fact]
    public async Task CATDATE_02_ISO_yyyy_MM_dd()
    {
        await CrearTrabajadorAsync("83000002", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var baseLibro = Libro(Fila("83000002", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "2026-11-02");

        (await Servicio().PrevisualizarAsync(excel, "fecha-iso.xlsx", null))
            .Filas.Single().Fecha.ShouldBe(Inicio);
    }

    [Fact]
    public async Task CATDATE_03_dd_MM_yyyy_admitido()
    {
        await CrearTrabajadorAsync("83000003", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(Fila("83000003", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));

        (await Servicio().PrevisualizarAsync(excel, "fecha-latina.xlsx", null))
            .Filas.Single().Fecha.ShouldBe(Inicio);
    }

    [Fact]
    public async Task CATDATE_04_celda_vacia_opcional()
    {
        await CrearTrabajadorAsync("83000004", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(Fila("83000004", Inicio, Inicio.AddDays(1), null, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));

        (await Servicio().PrevisualizarAsync(excel, "fecha-opcional.xlsx", null))
            .FechasExpandidas.ShouldBe(2);
    }

    [Fact]
    public async Task CATDATE_05_fecha_invalida_informa_fila_columna_valor_y_formatos()
    {
        await CrearTrabajadorAsync("83000005", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var baseLibro = Libro(Fila("83000005", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "31/02/2026");

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio().PrevisualizarAsync(excel, "fecha-invalida.xlsx", null));
        error.Message.ShouldContain("Fila 2");
        error.Message.ShouldContain("Fecha específica");
        error.Message.ShouldContain("31/02/2026");
        error.Message.ShouldContain("yyyy-MM-dd o dd/MM/yyyy");
    }

    [Fact]
    public async Task CATDATE_06_valor_ambiguo_rechazado()
    {
        await CrearTrabajadorAsync("83000006", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var baseLibro = Libro(Fila("83000006", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "01/02/03");

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio().PrevisualizarAsync(excel, "fecha-ambigua.xlsx", null));
    }

    [Fact]
    public async Task CATDATE_07_intervalo_final_anterior()
    {
        await CrearTrabajadorAsync("83000007", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        using var excel = Libro(Fila("83000007", Inicio, Inicio.AddDays(-1), null, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio().PrevisualizarAsync(excel, "intervalo-invalido.xlsx", null));
        error.Message.ShouldContain("fecha final es anterior");
    }

    [Fact]
    public void CATDATE_08_conversion_conserva_dia_calendario()
    {
        using var archivo = File.OpenRead(RutaLibroIntegral());

        var filas = LectorExcelProgramacion.Leer(archivo);

        filas[1][4].ShouldBe("2026-11-02");
        filas[1][5].ShouldBe("2026-11-06");
        filas[2][6].ShouldBe("2026-11-02");
    }

    [Fact]
    public async Task CATDATE_09_previsualizacion_no_persiste()
    {
        await CrearTrabajadorAsync("83000009", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var antes = await ConteosAsync();
        using var baseLibro = Libro(Fila("83000009", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "2026-11-02");

        await Servicio().PrevisualizarAsync(excel, "preview-fecha.xlsx", null);

        (await ConteosAsync()).ShouldBe(antes);
    }

    [Fact]
    public async Task CATDATE_10_confirmacion_persiste_fecha_correcta()
    {
        var trabajador = await CrearTrabajadorAsync("83000010", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "CATDATE-10", Nombre = "CATDATE", FechaInicio = Inicio,
            FechaFin = Inicio.AddDays(2), Estado = EstadoPeriodo.Abierto
        });
        await contexto.SaveChangesAsync();
        using var baseLibro = Libro(Fila("83000010", null, null, Inicio, turno.Codigo, turno.Nombre,
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "2026-11-02");
        var bytes = excel.ToArray();
        var vista = await Servicio().PrevisualizarAsync(new MemoryStream(bytes), "confirmar-fecha.xlsx", null);

        await Servicio().ConfirmarAsync(new MemoryStream(bytes), "confirmar-fecha.xlsx", vista.Huella, "qa", null);

        (await contexto.AsignacionesTurno.SingleAsync(a => a.TrabajadorId == trabajador.Id))
            .FechaInicio.ShouldBe(Inicio);
    }

    [Fact]
    public async Task CATDATE_11_compatibilidad_archivo_original()
    {
        await PrepararTrabajadoresLibroOriginalAsync();
        using var archivo = File.OpenRead(RutaLibroIntegral());

        var vista = await Servicio().PrevisualizarAsync(archivo, "programacion-integral.xlsx", null);

        vista.FilasLeidas.ShouldBe(8);
        vista.FechasExpandidas.ShouldBe(14);
        vista.Conflictos.ShouldBe(0);
    }

    [Fact]
    public async Task CATDATE_12_error_fecha_no_crea_huerfanos()
    {
        await CrearTrabajadorAsync("83000012", conTurno: false);
        var turno = await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo);
        var antes = await ConteosAsync();
        using var baseLibro = Libro(Fila("83000012", null, null, Inicio, "NUEVOCATDATE", "Nuevo",
            turno.HoraInicio, turno.HoraFin, turno.MinutosRefrigerio, "TRABAJO"));
        using var excel = CambiarCelda(baseLibro, "G2", "2026-11-02T10:30:00Z");

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio().PrevisualizarAsync(excel, "fecha-con-hora.xlsx", null));
        (await ConteosAsync()).ShouldBe(antes);
    }

    private ImportadorProgramacionLaboral Servicio(IReadOnlyCollection<long>? alcance = null) =>
        new(new ConciliadorProgramacion(contexto, new ValidadorPreparacionTareo(contexto, new Alcance(alcance))));

    private static MemoryStream CambiarCelda(MemoryStream origen, string direccion, object valor)
    {
        origen.Position = 0;
        using var libro = new XLWorkbook(origen);
        libro.Worksheet(1).Cell(direccion).Value = XLCellValue.FromObject(valor, CultureInfo.InvariantCulture);
        var salida = new MemoryStream();
        libro.SaveAs(salida);
        salida.Position = 0;
        return salida;
    }

    private static string RutaLibroIntegral() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "artifacts", "CAT-DYN-004", "programacion-integral.xlsx"));

    private async Task PrepararTrabajadoresLibroOriginalAsync()
    {
        var cliente = await contexto.Clientes.FirstAsync(c => c.Activo);
        var empresa = new Empresa
        {
            ClienteId = cliente.Id, Codigo = "EMPRESA QA", RazonSocial = "EMPRESA QA", Ruc = "20999999991"
        };
        var unidadA = new Planta { Empresa = empresa, Codigo = "UQA", Nombre = "UNIDAD A" };
        var unidadB = new Planta { Empresa = empresa, Codigo = "UQB", Nombre = "UNIDAD B" };
        contexto.AddRange(empresa, unidadA, unidadB);
        await contexto.SaveChangesAsync();
        var tipo = await contexto.TiposDocumento.FirstAsync(t => t.Codigo == "DNI");
        var puesto = await contexto.Puestos.FirstAsync(p => p.Activo);
        for (var i = 1; i <= 5; i++)
        {
            var planta = i == 5 ? unidadB : unidadA;
            var trabajador = new Trabajador
            {
                TipoDocumentoId = tipo.Id, NumeroDocumento = $"9000000{i}", ApellidoPaterno = "QA",
                ApellidoMaterno = "CATDATE", Nombres = $"TRABAJADOR {i}", Sexo = Sexo.Femenino,
                EmpresaId = empresa.Id, PlantaId = planta.Id, TipoPlanilla = TipoPlanilla.Empleado,
                PuestoId = puesto.Id, FechaIngreso = new DateOnly(2026, 11, 1)
            };
            trabajador.AbrirVinculo("qa");
            contexto.Trabajadores.Add(trabajador);
        }
        contexto.Periodos.Add(new Periodo
        {
            Codigo = "CATDATE-11", Nombre = "Noviembre CATDATE", FechaInicio = new DateOnly(2026, 11, 1),
            FechaFin = new DateOnly(2026, 11, 30), Estado = EstadoPeriodo.Abierto
        });
        await contexto.SaveChangesAsync();
    }

    private async Task<Trabajador> CrearTrabajadorAsync(
        string documento, bool conTurno, bool conMarcador = false, DateOnly? fechaCese = null)
    {
        var empresa = await contexto.Empresas.AsNoTracking().FirstAsync(e => e.Activo);
        var planta = await contexto.Plantas.AsNoTracking().FirstAsync(p => p.EmpresaId == empresa.Id && p.Activo);
        var tipo = await contexto.TiposDocumento.AsNoTracking().FirstAsync(t => t.Codigo == "DNI");
        var puesto = await contexto.Puestos.AsNoTracking().FirstAsync(p => p.Activo);
        var trabajador = new Trabajador
        {
            TipoDocumentoId = tipo.Id, NumeroDocumento = documento, ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "CATTURN", Nombres = documento, Sexo = Sexo.Femenino,
            EmpresaId = empresa.Id, PlantaId = planta.Id, TipoPlanilla = TipoPlanilla.Empleado,
            PuestoId = puesto.Id, FechaIngreso = Inicio, FechaCese = fechaCese
        };
        var vinculo = new PeriodoLaboral { EmpresaId = empresa.Id, FechaIngreso = Inicio, FechaCese = fechaCese };
        vinculo.AsignacionesPlanta.Add(new AsignacionPlanta
        {
            PlantaId = planta.Id, FechaInicio = Inicio, FechaFin = fechaCese
        });
        trabajador.PeriodosLaborales.Add(vinculo);
        if (conTurno)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = (await contexto.Turnos.AsNoTracking().FirstAsync(t => t.Activo)).Id,
                FechaInicio = Inicio, FechaFin = fechaCese
            });
        }
        if (conMarcador)
        {
            var equipo = await contexto.EquiposMarcador.FirstOrDefaultAsync(e => e.PlantaId == planta.Id);
            if (equipo is null)
            {
                equipo = new EquipoMarcador { Codigo = $"EQ-{documento}", Nombre = "Equipo QA", PlantaId = planta.Id };
                contexto.EquiposMarcador.Add(equipo);
                await contexto.SaveChangesAsync();
            }
            var marcador = new Marcador { Equipo = equipo, PlantaId = planta.Id, Codigo = documento };
            contexto.Marcadores.Add(marcador);
            trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
            {
                Equipo = equipo, IdMarcador = documento, FechaInicio = Inicio, FechaFin = fechaCese
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

    private static MemoryStream Libro(params FilaExcel[] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Programacion");
        string[] cabeceras =
        [
            "Tipo de documento", "Número de documento", "Empresa", "Unidad", "Fecha de inicio", "Fecha de fin",
            "Fecha específica", "Código de turno", "Nombre del turno", "Hora de entrada", "Hora de salida",
            "Salida al día siguiente", "Minutos de refrigerio", "Tipo de día", "Modalidad", "Observaciones",
            "Referencia de aprobación"
        ];
        for (var c = 0; c < cabeceras.Length; c++) hoja.Cell(1, c + 1).Value = cabeceras[c];
        for (var i = 0; i < filas.Length; i++)
        {
            var f = filas[i];
            string?[] valores =
            [
                "DNI", f.Documento, f.Empresa, f.Unidad, f.Desde?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                f.Hasta?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), f.Fecha?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), f.CodigoTurno,
                f.NombreTurno, f.HoraInicio?.ToString("HH:mm", CultureInfo.InvariantCulture), f.HoraFin?.ToString("HH:mm", CultureInfo.InvariantCulture),
                f.SalidaSiguiente, f.Refrigerio?.ToString(CultureInfo.InvariantCulture), f.TipoDia, "ROTATIVO", "QA", "QA-AUT"
            ];
            for (var c = 0; c < valores.Length; c++) hoja.Cell(i + 2, c + 1).Value = valores[c] ?? string.Empty;
        }
        var salida = new MemoryStream();
        libro.SaveAs(salida);
        salida.Position = 0;
        return salida;
    }

    private FilaExcel Fila(
        string documento, DateOnly? desde, DateOnly? hasta, DateOnly? fecha,
        string? codigo, string? nombre, TimeOnly? inicio, TimeOnly? fin, int? refrigerio,
        string tipoDia, string salidaSiguiente = "NO")
    {
        var empresa = contexto.Empresas.AsNoTracking().First(e => e.Activo);
        var planta = contexto.Plantas.AsNoTracking().First(p => p.EmpresaId == empresa.Id && p.Activo);
        return new(documento, empresa.Codigo, planta.Codigo, desde, hasta, fecha, codigo, nombre,
            inicio, fin, refrigerio, tipoDia, salidaSiguiente);
    }

    private sealed record FilaExcel(
        string Documento, string Empresa, string Unidad, DateOnly? Desde, DateOnly? Hasta,
        DateOnly? Fecha, string? CodigoTurno, string? NombreTurno, TimeOnly? HoraInicio,
        TimeOnly? HoraFin, int? Refrigerio, string TipoDia, string SalidaSiguiente);

    private sealed class Alcance(IReadOnlyCollection<long>? plantas) : IProveedorAlcancePlantas
    {
        public Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(plantas);
    }
}

#pragma warning restore IDE0011
