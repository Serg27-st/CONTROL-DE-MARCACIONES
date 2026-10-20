using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Domain.Organizacion;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.MarcacionesProvisionales;

public sealed class ConsultaMarcacionProvisionalTests : IDisposable
{
    private const long EmpresaId = 1;
    private const long TipoDocDni = 1;
    private const long PuestoId = 1;

    private static readonly DateOnly Dia = new(2026, 7, 15);

    private readonly SqliteConnection _conexion;
    private readonly MarcacionDbContext _contexto;
    private readonly ServicioConsultaMarcacionProvisional _servicio;
    private readonly long _equipoId;
    private readonly long _plantaId;

    public ConsultaMarcacionProvisionalTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        _contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(_conexion).Options);
        _contexto.Database.EnsureCreated();

        _plantaId = _contexto.Plantas.First().Id;

        var equipo = new EquipoMarcador
        {
            Codigo = "DET-AL",
            Nombre = "Detergentes A-L",
            PlantaId = _plantaId,
            Activo = true
        };
        _contexto.EquiposMarcador.Add(equipo);
        _contexto.SaveChanges();
        _equipoId = equipo.Id;

        _servicio = new ServicioConsultaMarcacionProvisional(_contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public async Task Consulta_de_un_dia_con_dos_marcaciones()
    {
        var trabajadorId = await CrearTrabajadorAsync("41300001");
        await AsignarMarcadorAsync(trabajadorId, "14");
        await AsignarTurnoAsync(trabajadorId, 1);
        await InsertarMarcacionesAsync("14", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        resultado.Jornadas.Count.ShouldBe(1);
        var j = resultado.Jornadas[0];
        j.Fecha.ShouldBe(Dia);
        j.TrabajadorId.ShouldBe(trabajadorId);
        j.NumeroDocumento.ShouldBe("41300001");
        j.HoraIngreso.ShouldBe(new TimeOnly(7, 0));
        j.HoraSalida.ShouldBe(new TimeOnly(19, 0));
        j.CantidadMarcaciones.ShouldBe(2);
        j.Observacion.ShouldBeNull();
    }

    [Fact]
    public async Task Consulta_de_varios_dias()
    {
        var trabajadorId = await CrearTrabajadorAsync("41300002");
        await AsignarMarcadorAsync(trabajadorId, "15");
        await AsignarTurnoAsync(trabajadorId, 1);

        var dia1 = new DateOnly(2026, 7, 15);
        var dia2 = new DateOnly(2026, 7, 16);

        await InsertarMarcacionesAsync("15", dia1, new TimeOnly(7, 0), new TimeOnly(19, 0));
        await InsertarMarcacionesAsync("15", dia2, new TimeOnly(7, 5), new TimeOnly(19, 10));

        var resultado = await _servicio.ConsultarAsync(_equipoId, dia1, dia2);

        resultado.Jornadas.Count.ShouldBe(2);
        resultado.TotalDias.ShouldBe(2);
        resultado.Jornadas[0].Fecha.ShouldBe(dia1);
        resultado.Jornadas[1].Fecha.ShouldBe(dia2);
    }

    [Fact]
    public async Task Trabajador_asignado_por_vigencia()
    {
        var t1 = await CrearTrabajadorAsync("41300010");
        var t2 = await CrearTrabajadorAsync("41300011");

        await AsignarMarcadorAsync(t1, "20", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        await AsignarMarcadorAsync(t2, "20", new DateOnly(2026, 7, 1), null);

        await InsertarMarcacionesAsync("20", new DateOnly(2026, 7, 15), new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(
            _equipoId, new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 15));

        resultado.Jornadas[0].TrabajadorId.ShouldBe(t2);
        resultado.Jornadas[0].NumeroDocumento.ShouldBe("41300011");
    }

    [Fact]
    public async Task Id_marcador_sin_trabajador_asignado()
    {
        await InsertarMarcacionesAsync("99", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        resultado.Jornadas.Count.ShouldBe(1);
        var j = resultado.Jornadas[0];
        j.TrabajadorId.ShouldBeNull();
        j.NumeroDocumento.ShouldBeNull();
        j.ApellidoPaterno.ShouldBeNull();
        j.Observacion.ShouldNotBeNull();
        j.Observacion!.ShouldContain("no asignado");
    }

    [Fact]
    public async Task Una_sola_marcacion_en_el_dia()
    {
        var tid = await CrearTrabajadorAsync("41300003");
        await AsignarMarcadorAsync(tid, "16");
        await InsertarMarcacionesAsync("16", Dia, new TimeOnly(7, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        var j = resultado.Jornadas[0];
        j.HoraIngreso.ShouldBe(new TimeOnly(7, 0));
        j.HoraSalida.ShouldBeNull();
        j.CantidadMarcaciones.ShouldBe(1);
        j.Observacion.ShouldNotBeNull();
        j.Observacion!.ShouldContain("Solo una marcación");
    }

    [Fact]
    public async Task Varias_marcaciones_en_el_dia()
    {
        var tid = await CrearTrabajadorAsync("41300004");
        await AsignarMarcadorAsync(tid, "17");
        await InsertarMarcacionesAsync("17", Dia,
            new TimeOnly(7, 0), new TimeOnly(12, 0), new TimeOnly(13, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        var j = resultado.Jornadas[0];
        j.HoraIngreso.ShouldBe(new TimeOnly(7, 0));
        j.HoraSalida.ShouldBe(new TimeOnly(19, 0));
        j.CantidadMarcaciones.ShouldBe(4);
        j.MarcacionesDelDia.Count.ShouldBe(4);
        j.Observacion.ShouldNotBeNull();
        j.Observacion!.ShouldContain("4 marcaciones");
    }

    [Fact]
    public async Task Duplicados_exactos_genera_observacion()
    {
        var tid = await CrearTrabajadorAsync("41300005");
        await AsignarMarcadorAsync(tid, "18");

        var importacion = await CrearImportacionAsync();
        var fechaHora = Dia.ToDateTime(new TimeOnly(7, 0));

        _contexto.MarcacionesProvisionales.Add(new MarcacionProvisional
        {
            EquipoId = _equipoId,
            IdMarcador = "18",
            FechaHora = fechaHora,
            ImportacionProvisionalId = importacion,
            CreadoPor = "test"
        });
        _contexto.MarcacionesProvisionales.Add(new MarcacionProvisional
        {
            EquipoId = _equipoId,
            IdMarcador = "18",
            FechaHora = fechaHora.AddSeconds(1),
            ImportacionProvisionalId = importacion,
            CreadoPor = "test"
        });
        await _contexto.SaveChangesAsync();

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        var j = resultado.Jornadas[0];
        j.CantidadMarcaciones.ShouldBe(2);
    }

    [Fact]
    public async Task Rango_de_fechas_invalido_lanza_excepcion()
    {
        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => _servicio.ConsultarAsync(_equipoId, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 10)));

        ex.Message.ShouldContain("inicio");
    }

    [Fact]
    public async Task Rango_mayor_a_30_dias_genera_advertencia()
    {
        await InsertarMarcacionesAsync("50", new DateOnly(2026, 7, 1), new TimeOnly(7, 0));

        var resultado = await _servicio.ConsultarAsync(
            _equipoId, new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 5));

        resultado.AdvertenciaRangoExcesivo.ShouldBeTrue();
        resultado.TotalDias.ShouldBe(36);
    }

    [Fact]
    public async Task Rango_mayor_a_40_dias_indica_limite_exportacion()
    {
        await InsertarMarcacionesAsync("51", new DateOnly(2026, 7, 1), new TimeOnly(7, 0));

        var resultado = await _servicio.ConsultarAsync(
            _equipoId, new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 15));

        resultado.RangoExcedeMaximoExportacion.ShouldBeTrue();
    }

    [Fact]
    public async Task La_consulta_no_modifica_ninguna_tabla()
    {
        var tid = await CrearTrabajadorAsync("41300006");
        await AsignarMarcadorAsync(tid, "22");
        await InsertarMarcacionesAsync("22", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var marcacionesAntes = await _contexto.MarcacionesProvisionales.CountAsync();
        var trabajadoresAntes = await _contexto.Trabajadores.CountAsync();
        var asignacionesAntes = await _contexto.AsignacionesMarcador.CountAsync();

        await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        (await _contexto.MarcacionesProvisionales.CountAsync()).ShouldBe(marcacionesAntes);
        (await _contexto.Trabajadores.CountAsync()).ShouldBe(trabajadoresAntes);
        (await _contexto.AsignacionesMarcador.CountAsync()).ShouldBe(asignacionesAntes);
    }

    [Fact]
    public async Task Resuelve_turno_del_trabajador()
    {
        var tid = await CrearTrabajadorAsync("41300007");
        await AsignarMarcadorAsync(tid, "23");
        await AsignarTurnoAsync(tid, 1);
        await InsertarMarcacionesAsync("23", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        resultado.Jornadas[0].TurnoCodigo.ShouldNotBeNull();
        resultado.Jornadas[0].TurnoNombre.ShouldNotBeNull();
    }

    [Fact]
    public async Task Sin_marcaciones_devuelve_lista_vacia()
    {
        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        resultado.Jornadas.ShouldBeEmpty();
        resultado.TotalDias.ShouldBe(1);
    }

    [Fact]
    public async Task PL3EB_PMV_01_muestra_la_planta_historica_de_la_fecha_de_marcacion()
    {
        var trabajadorId = await CrearTrabajadorAsync("41300321");
        var plantaHistorica = await _contexto.Plantas
            .Where(p => p.Id == _plantaId)
            .Select(p => p.Nombre)
            .SingleAsync();
        var plantaActual = await _contexto.Plantas
            .Where(p => p.Id != _plantaId)
            .Select(p => p.Id)
            .FirstAsync();
        var trabajador = await _contexto.Trabajadores
            .Include(t => t.PeriodosLaborales)
                .ThenInclude(p => p.AsignacionesPlanta)
            .SingleAsync(t => t.Id == trabajadorId);
        trabajador.CambiarPlanta(plantaActual, Dia.AddDays(1), "tester");
        await _contexto.SaveChangesAsync();

        await AsignarMarcadorAsync(trabajadorId, "3EB-01");
        await InsertarMarcacionesAsync("3EB-01", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);

        resultado.Jornadas.ShouldHaveSingleItem().Planta.ShouldBe(plantaHistorica);
    }

    [Fact]
    public async Task PL3EB_PMV_02_sin_contexto_historico_no_usa_planta_actual_como_fallback()
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocDni,
            NumeroDocumento = "41300322",
            ApellidoPaterno = "SIN",
            ApellidoMaterno = "HISTORIA",
            Nombres = "LABORAL",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaId,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = _plantaId,
            PuestoId = PuestoId,
            FechaIngreso = new DateOnly(2026, 1, 1)
        };
        _contexto.Trabajadores.Add(trabajador);
        await _contexto.SaveChangesAsync();
        await AsignarMarcadorAsync(trabajador.Id, "3EB-02");
        await InsertarMarcacionesAsync("3EB-02", Dia, new TimeOnly(7, 0), new TimeOnly(19, 0));

        var resultado = await _servicio.ConsultarAsync(_equipoId, Dia, Dia);
        var jornada = resultado.Jornadas.ShouldHaveSingleItem();

        jornada.TrabajadorId.ShouldBe(trabajador.Id);
        jornada.Planta.ShouldBeNull();
        jornada.Observacion.ShouldNotBeNull();
        jornada.Observacion!.ShouldContain("Sin contexto laboral histórico");
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "CONSULTA",
            Nombres = "PROVISIONAL",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaId,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = _plantaId,
            PuestoId = PuestoId,
            FechaIngreso = new DateOnly(2026, 1, 1)
        };

        trabajador.AbrirVinculo("test");
        _contexto.Trabajadores.Add(trabajador);
        await _contexto.SaveChangesAsync();
        return trabajador.Id;
    }

    private async Task AsignarMarcadorAsync(
        long trabajadorId, string idMarcador,
        DateOnly? desde = null, DateOnly? hasta = null)
    {
        _contexto.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = _equipoId,
            IdMarcador = idMarcador,
            FechaInicio = desde ?? new DateOnly(2026, 1, 1),
            FechaFin = hasta
        });
        await _contexto.SaveChangesAsync();
    }

    private async Task AsignarTurnoAsync(long trabajadorId, long turnoId)
    {
        _contexto.AsignacionesTurno.Add(new AsignacionTurno
        {
            TrabajadorId = trabajadorId,
            TurnoId = turnoId,
            FechaInicio = new DateOnly(2026, 1, 1)
        });
        await _contexto.SaveChangesAsync();
    }

    private async Task<long> CrearImportacionAsync()
    {
        var imp = new ImportacionProvisional
        {
            EquipoId = _equipoId,
            NombreArchivo = "test.dat",
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = DateTime.Now,
            CargadoPorUsuario = "test",
            FilasLeidas = 1,
            FilasInsertadas = 1,
            CreadoPor = "test"
        };
        _contexto.ImportacionesProvisionales.Add(imp);
        await _contexto.SaveChangesAsync();
        return imp.Id;
    }

    private async Task InsertarMarcacionesAsync(string idMarcador, DateOnly fecha, params TimeOnly[] horas)
    {
        var importacionId = await CrearImportacionAsync();

        foreach (var hora in horas)
        {
            _contexto.MarcacionesProvisionales.Add(new MarcacionProvisional
            {
                EquipoId = _equipoId,
                IdMarcador = idMarcador,
                FechaHora = fecha.ToDateTime(hora),
                ImportacionProvisionalId = importacionId,
                CreadoPor = "test"
            });
        }

        await _contexto.SaveChangesAsync();
    }
}
