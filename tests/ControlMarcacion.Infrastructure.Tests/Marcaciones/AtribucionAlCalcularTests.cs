using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// La marcación se atribuye al calcular, no al cargar el archivo.
/// </summary>
/// <remarks>
/// Antes el dueño se decidía al subir el archivo y quedaba grabado en la fila: un número
/// sin asignar ese día se descartaba, y uno asignado mal quedaba para siempre a nombre de
/// otra persona. La única salida era volver a subir archivos. Estas pruebas fijan que
/// basta con asignar o corregir el ID y recalcular.
/// </remarks>
public sealed class AtribucionAlCalcularTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;
    private const long Aparato = 7;

    private static readonly DateOnly Dia = new(2026, 7, 1);

    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo calculo;
    private readonly ServicioImportacion importacion;

    public AtribucionAlCalcularTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        contexto.EquiposMarcador.Add(new EquipoMarcador
        {
            Id = Aparato,
            Codigo = "P01-AL",
            Nombre = "Planta 01 A-L",
            PlantaId = PlantaP01,
            Activo = true
        });
        contexto.SaveChanges();

        calculo = new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(), NullLogger<ServicioCalculoTareo>.Instance);

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        importacion = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task Un_ID_asignado_despues_de_cargar_el_archivo_cuenta_al_recalcular()
    {
        var trabajador = await CrearTrabajadorAsync("44444451");
        await CargarJornadaAsync("25");

        (await MinutosDelDiaAsync(trabajador)).ShouldBe(0);

        await AsignarAsync(trabajador, "25", desde: new DateOnly(2020, 1, 1));

        (await MinutosDelDiaAsync(trabajador)).ShouldBe(720);
    }

    /// <summary>
    /// El caso que antes no tenía arreglo: el ID se asignó mal y las marcaciones ya
    /// estaban cargadas a nombre de quien no era.
    /// </summary>
    [Fact]
    public async Task Corregir_un_ID_mal_asignado_mueve_sus_marcaciones_al_recalcular()
    {
        var trabajador = await CrearTrabajadorAsync("44444452");
        var asignacion = await AsignarAsync(trabajador, "52", desde: new DateOnly(2020, 1, 1));

        // El aparato lo tiene como 25, no como 52.
        await CargarJornadaAsync("25");
        (await MinutosDelDiaAsync(trabajador)).ShouldBe(0);

        asignacion.IdMarcador = "25";
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await MinutosDelDiaAsync(trabajador)).ShouldBe(720);
    }

    [Fact]
    public async Task La_lista_de_numeros_sin_asignar_se_vacia_al_asignarlos()
    {
        var trabajador = await CrearTrabajadorAsync("44444453");
        await CargarJornadaAsync("25");

        var pendientes = await importacion.NumerosSinAsignarAsync(Dia, Dia, CancellationToken.None);

        pendientes.Count.ShouldBe(1);
        pendientes[0].IdMarcador.ShouldBe("25");
        pendientes[0].Equipo.ShouldBe("Planta 01 A-L");
        pendientes[0].Marcaciones.ShouldBe(2);
        pendientes[0].Dias.ShouldBe(1);

        await AsignarAsync(trabajador, "25", desde: new DateOnly(2020, 1, 1));

        (await importacion.NumerosSinAsignarAsync(Dia, Dia, CancellationToken.None)).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "ATRIBUCION",
            Nombres = "CALCULO",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = TurnoDiurno,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task<AsignacionMarcador> AsignarAsync(long trabajadorId, string numero, DateOnly desde)
    {
        var asignacion = new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = Aparato,
            IdMarcador = numero,
            FechaInicio = desde
        };

        await contexto.AsignacionesMarcador.AddAsync(asignacion, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return asignacion;
    }

    /// <summary>Miércoles 1 de julio, de 07:00 a 19:00, marcada con el número indicado.</summary>
    private async Task CargarJornadaAsync(string numero)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "jornada.txt",
            EquipoId = Aparato,
            HashContenido = new string('c', 64),
            FechaCarga = new DateTime(2026, 7, 2, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 2,
            FilasInsertadas = 2
        };

        await contexto.Importaciones.AddAsync(carga, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        foreach (var hora in new[] { new TimeOnly(7, 0), new TimeOnly(19, 0) })
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                EquipoId = Aparato,
                IdMarcador = numero,
                FechaHora = Dia.ToDateTime(hora),
                ImportacionId = carga.Id
            }, CancellationToken.None);
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<int> MinutosDelDiaAsync(long trabajadorId)
    {
        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var tareo = await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == Dia, CancellationToken.None);

        return tareo.MinutosTrabajados;
    }
}
