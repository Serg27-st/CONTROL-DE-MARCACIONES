using ClosedXML.Excel;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.MarcacionesProvisionales;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.MarcacionesProvisionales;

public sealed class ImportacionProvisionalTests : IDisposable
{
    private static readonly DateTime Marca1 = new(2026, 7, 15, 7, 0, 0);
    private static readonly DateTime Marca2 = new(2026, 7, 15, 19, 0, 0);

    private readonly SqliteConnection _conexion;
    private readonly MarcacionDbContext _contexto;
    private readonly IFuenteDeMarcaciones _fuente;
    private readonly ServicioImportacionProvisional _servicio;
    private readonly long _equipoId;

    public ImportacionProvisionalTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        _contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(_conexion).Options);
        _contexto.Database.EnsureCreated();

        var plantaId = _contexto.Plantas.First().Id;

        var equipo = new EquipoMarcador
        {
            Codigo = "DET-AL",
            Nombre = "Detergentes A-L",
            PlantaId = plantaId,
            Activo = true
        };
        _contexto.EquiposMarcador.Add(equipo);
        _contexto.SaveChanges();
        _equipoId = equipo.Id;

        _fuente = Substitute.For<IFuenteDeMarcaciones>();
        _servicio = new ServicioImportacionProvisional(_contexto, _fuente, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public async Task Importar_inserta_marcaciones_y_devuelve_conteo()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca2, null, null, 2, "linea2"));

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "test.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(2);
        resultado.FilasRechazadas.ShouldBe(0);
        resultado.FechaMinima.ShouldBe(new DateOnly(2026, 7, 15));
        resultado.FechaMaxima.ShouldBe(new DateOnly(2026, 7, 15));

        var enBase = await _contexto.MarcacionesProvisionales.CountAsync();
        enBase.ShouldBe(2);
    }

    [Fact]
    public async Task Archivo_duplicado_por_hash_se_rechaza()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"));

        await _servicio.ImportarAsync(FlujoVacio(), "test.dat", _equipoId, "admin");

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => _servicio.ImportarAsync(FlujoVacio(), "test.dat", _equipoId, "admin"));

        ex.Message.ShouldContain("ya se cargó");
    }

    [Fact]
    public async Task Marcaciones_duplicadas_en_base_no_se_reinsertan()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"));

        await _servicio.ImportarAsync(FlujoVacio(), "archivo1.dat", _equipoId, "admin");

        ConfigurarFuente([
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca2, null, null, 2, "linea2")
        ], hashDistinto: true);

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "archivo2.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(1);
        resultado.FilasRechazadas.ShouldBe(1);

        var enBase = await _contexto.MarcacionesProvisionales.CountAsync();
        enBase.ShouldBe(2);
    }

    [Fact]
    public async Task Equipo_invalido_lanza_excepcion()
    {
        ConfigurarFuente();

        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => _servicio.ImportarAsync(FlujoVacio(), "test.dat", 999, "admin"));

        ex.Message.ShouldContain("no existe");
    }

    [Fact]
    public async Task Nombre_archivo_vacio_lanza_excepcion()
    {
        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => _servicio.ImportarAsync(FlujoVacio(), "", _equipoId, "admin"));

        ex.Message.ShouldContain("nombre");
    }

    [Fact]
    public async Task Usuario_vacio_lanza_excepcion()
    {
        var ex = await Should.ThrowAsync<ReglaNegocioException>(
            () => _servicio.ImportarAsync(FlujoVacio(), "test.dat", _equipoId, ""));

        ex.Message.ShouldContain("usuario");
    }

    [Fact]
    public async Task Listar_importaciones_devuelve_historial()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"));

        await _servicio.ImportarAsync(FlujoVacio(), "test.dat", _equipoId, "admin");

        var lista = await _servicio.ListarImportacionesAsync();

        lista.Count.ShouldBe(1);
        lista[0].NombreArchivo.ShouldBe("test.dat");
        lista[0].CargadoPorUsuario.ShouldBe("admin");
        lista[0].FilasInsertadas.ShouldBe(1);
    }

    [Fact]
    public async Task Lineas_rechazadas_se_registran_como_errores()
    {
        var marcaciones = new List<MarcacionLeida>();
        var rechazadas = new List<LineaRechazada>
        {
            new(5, "linea mala", "formato inválido")
        };
        var resultado = new ResultadoLectura(marcaciones, rechazadas);
        _fuente.Leer(Arg.Any<Stream>()).Returns(resultado);

        var res = await _servicio.ImportarAsync(FlujoVacio(), "test.dat", _equipoId, "admin");

        res.FilasRechazadas.ShouldBe(1);
        res.Errores.Count.ShouldBe(1);
        res.Errores[0].Motivo.ShouldBe("formato inválido");

        var erroresEnBase = await _contexto.Set<ErrorImportacionProvisional>().CountAsync();
        erroresEnBase.ShouldBe(1);
    }

    [Fact]
    public async Task Duplicados_dentro_del_mismo_lote_no_lanzan_excepcion()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca1, null, null, 2, "linea1-dup"));

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "test.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(1);
        resultado.FilasRechazadas.ShouldBe(1);

        var enBase = await _contexto.MarcacionesProvisionales.CountAsync();
        enBase.ShouldBe(1);
    }

    [Fact]
    public async Task Duplicados_en_lote_se_registran_como_errores()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca1, null, null, 2, "linea1-dup"),
            new MarcacionLeida("14", Marca2, null, null, 3, "linea3"));

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "test.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(2);
        resultado.FilasRechazadas.ShouldBe(1);

        var erroresEnBase = await _contexto.Set<ErrorImportacionProvisional>().CountAsync();
        erroresEnBase.ShouldBe(1);

        var error = await _contexto.Set<ErrorImportacionProvisional>().FirstAsync();
        error.Motivo.ShouldContain("Duplicada en el archivo");
    }

    [Fact]
    public async Task Duplicados_en_base_se_registran_como_errores()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"));

        await _servicio.ImportarAsync(FlujoVacio(), "archivo1.dat", _equipoId, "admin");

        ConfigurarFuente([
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca2, null, null, 2, "linea2")
        ], hashDistinto: true);

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "archivo2.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(1);
        resultado.FilasRechazadas.ShouldBe(1);

        var importacion = await _contexto.ImportacionesProvisionales
            .OrderByDescending(i => i.FechaCarga)
            .Include(i => i.Errores)
            .FirstAsync();

        importacion.Errores.Count.ShouldBe(1);
        importacion.Errores.First().Motivo.ShouldContain("ya existe en base");
    }

    [Fact]
    public async Task Archivo_con_duplicados_y_validas_mezcladas_importa_las_validas()
    {
        ConfigurarFuente(
            new MarcacionLeida("14", Marca1, null, null, 1, "linea1"),
            new MarcacionLeida("14", Marca1, null, null, 2, "linea1-dup"),
            new MarcacionLeida("14", Marca2, null, null, 3, "linea3"),
            new MarcacionLeida("15", Marca1, null, null, 4, "linea4"));

        var resultado = await _servicio.ImportarAsync(
            FlujoVacio(), "test.dat", _equipoId, "admin");

        resultado.FilasInsertadas.ShouldBe(3);
        resultado.FilasRechazadas.ShouldBe(1);
        resultado.FilasLeidas.ShouldBe(4);

        var enBase = await _contexto.MarcacionesProvisionales.CountAsync();
        enBase.ShouldBe(3);
    }

    [Fact]
    public async Task PMV004_01_el_flujo_provisional_no_escribe_ni_expone_conceptos_del_motor_oficial()
    {
        var importacionesOficialesAntes = await _contexto.Importaciones.CountAsync();
        var erroresOficialesAntes = await _contexto.ErroresImportacion.CountAsync();
        var marcacionesCrudasAntes = await _contexto.MarcacionesCrudas.CountAsync();
        var tareosAntes = await _contexto.TareosDiarios.CountAsync();
        var periodosLaboralesAntes = await _contexto.PeriodosLaborales.CountAsync();

        ConfigurarFuente(
            new MarcacionLeida("PMV-404", Marca1, null, null, 1, "entrada provisional"),
            new MarcacionLeida("PMV-404", Marca2, null, null, 2, "salida provisional"));

        var carga = await _servicio.ImportarAsync(
            FlujoVacio(),
            "pmv004.dat",
            _equipoId,
            "coordinador");

        var consulta = new ServicioConsultaMarcacionProvisional(
            _contexto,
            new AlcanceSinRestriccion());
        var resultado = await consulta.ConsultarAsync(
            _equipoId,
            DateOnly.FromDateTime(Marca1),
            DateOnly.FromDateTime(Marca1));

        var exportador = new ExportadorMarcacionesProvisionales();
        var archivo = exportador.Exportar(
            resultado.Jornadas,
            "DET-AL - Detergentes A-L",
            DateOnly.FromDateTime(Marca1),
            DateOnly.FromDateTime(Marca1),
            "coordinador");

        carga.FilasInsertadas.ShouldBe(2);
        carga.FilasRechazadas.ShouldBe(0);
        resultado.Jornadas.ShouldHaveSingleItem().IdMarcador.ShouldBe("PMV-404");

        (await _contexto.ImportacionesProvisionales.CountAsync()).ShouldBe(1);
        (await _contexto.MarcacionesProvisionales.CountAsync()).ShouldBe(2);
        (await _contexto.ErroresImportacionProvisional.CountAsync()).ShouldBe(0);

        (await _contexto.Importaciones.CountAsync()).ShouldBe(importacionesOficialesAntes);
        (await _contexto.ErroresImportacion.CountAsync()).ShouldBe(erroresOficialesAntes);
        (await _contexto.MarcacionesCrudas.CountAsync()).ShouldBe(marcacionesCrudasAntes);
        (await _contexto.TareosDiarios.CountAsync()).ShouldBe(tareosAntes);
        (await _contexto.PeriodosLaborales.CountAsync()).ShouldBe(periodosLaboralesAntes);

        using var libro = new XLWorkbook(new MemoryStream(archivo));
        var hoja = libro.Worksheet("Marcaciones");
        var encabezados = Enumerable.Range(1, 13)
            .Select(columna => hoja.Cell(1, columna).GetString())
            .ToList();

        encabezados.ShouldBe([
            "Fecha",
            "ID marcador",
            "DNI",
            "Apellido paterno",
            "Apellido materno",
            "Nombres",
            "Hora ingreso",
            "Hora salida",
            "Turno",
            "Unidad",
            "Área",
            "Centro de costo",
            "Observación"
        ]);
        hoja.Cell(1, 14).IsEmpty().ShouldBeTrue();

        foreach (var conceptoOficial in new[]
                 {
                     "Tardanza", "Salida anticipada", "Horas ordinarias", "HE 25%",
                     "HE 35%", "HE 100%", "Nocturnidad", "Incidencia", "Feriado",
                     "Remuneración", "Pago"
                 })
        {
            encabezados.ShouldNotContain(conceptoOficial);
        }
    }

    private int _llamadaHash;

    private void ConfigurarFuente(params MarcacionLeida[] marcaciones)
    {
        ConfigurarFuenteInterno(marcaciones, false);
    }

    private void ConfigurarFuente(MarcacionLeida[] marcaciones, bool hashDistinto)
    {
        ConfigurarFuenteInterno(marcaciones, hashDistinto);
    }

    private void ConfigurarFuenteInterno(MarcacionLeida[] marcaciones, bool hashDistinto)
    {
        var resultado = new ResultadoLectura(
            marcaciones.ToList(),
            new List<LineaRechazada>());
        _fuente.Leer(Arg.Any<Stream>()).Returns(resultado);

        if (hashDistinto)
        {
            _llamadaHash++;
        }
    }

    private MemoryStream FlujoVacio()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"contenido-{_llamadaHash}");
        return new MemoryStream(bytes);
    }
}
