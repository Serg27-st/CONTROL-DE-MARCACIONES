using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Una carga anulada conserva sus marcaciones pero el motor deja de contarlas.
/// </summary>
/// <remarks>
/// Es la prueba que de verdad importa de la anulación: marcar el flag no sirve de nada si
/// el cálculo sigue leyendo esas filas. Se comprueba sobre la base, no sobre el dominio,
/// porque el filtro vive en la consulta.
/// </remarks>
public sealed class CargaAnuladaNoCuentaTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;

    public CargaAnuladaNoCuentaTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioCalculoTareo(contexto);
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task Las_marcaciones_de_una_carga_anulada_no_producen_jornada()
    {
        var trabajadorId = await CrearTrabajadorAsync();
        var cargaId = await CrearCargaAsync(anulada: false);

        // Miércoles 1 de julio, jornada de 07:00 a 19:00.
        await CrearMarcacionesAsync(trabajadorId, cargaId,
            new DateTime(2026, 7, 1, 7, 0, 0), new DateTime(2026, 7, 1, 19, 0, 0));

        var conCarga = await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);
        conCarga.ShouldNotBeNull();

        var diaCalculado = await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == new DateOnly(2026, 7, 1));

        diaCalculado.MinutosTrabajados.ShouldBe(720);

        // Se anula la carga y se recalcula: la marcación sigue en la base, pero el día
        // deja de tener jornada.
        var carga = await contexto.Importaciones.FirstAsync(i => i.Id == cargaId);
        carga.Anular("Archivo equivocado", "coordinador", new DateTime(2026, 8, 11, 12, 0, 0));
        await contexto.SaveChangesAsync(CancellationToken.None);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var trasAnular = await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == new DateOnly(2026, 7, 1));

        trasAnular.MinutosTrabajados.ShouldBe(0);

        // Y lo esencial: NO se borró nada.
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Reactivar_la_carga_devuelve_la_jornada()
    {
        var trabajadorId = await CrearTrabajadorAsync();
        var cargaId = await CrearCargaAsync(anulada: true);

        await CrearMarcacionesAsync(trabajadorId, cargaId,
            new DateTime(2026, 7, 1, 7, 0, 0), new DateTime(2026, 7, 1, 19, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var anulada = await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == new DateOnly(2026, 7, 1));

        anulada.MinutosTrabajados.ShouldBe(0);

        var carga = await contexto.Importaciones.FirstAsync(i => i.Id == cargaId);
        carga.Reactivar("coordinador");
        await contexto.SaveChangesAsync(CancellationToken.None);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var reactivada = await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == new DateOnly(2026, 7, 1));

        reactivada.MinutosTrabajados.ShouldBe(720);
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearTrabajadorAsync()
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = "44444441",
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "TEST",
            Nombres = "ANULACION",
            Sexo = Sexo.Masculino,
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

        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task<long> CrearCargaAsync(bool anulada)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "prueba.txt",
            HashContenido = new string('b', 64),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 2,
            FilasInsertadas = 2,
            Anulada = anulada,
            MotivoAnulacion = anulada ? "Carga de prueba" : null
        };

        await contexto.Importaciones.AddAsync(carga, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return carga.Id;
    }

    private async Task CrearMarcacionesAsync(long trabajadorId, long cargaId, params DateTime[] momentos)
    {
        foreach (var momento in momentos)
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                TrabajadorId = trabajadorId,
                IdMarcador = "1",
                FechaHora = momento,
                ImportacionId = cargaId
            }, CancellationToken.None);
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
