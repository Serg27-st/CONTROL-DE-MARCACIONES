using System.Security.Cryptography;
using System.Text;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// El mismo archivo no puede quedar vigente en dos aparatos a la vez.
/// </summary>
/// <remarks>
/// La deduplicación de la ingesta es por (equipo, número, instante), así que declarar otro
/// aparato al volver a subir un archivo lo insertaba entero de nuevo: el 14 de M-Z recibía
/// las jornadas del 14 de A-L. La pantalla lo permitía con una casilla de confirmación, y
/// su aviso afirmaba que "volver a cargarlo no duplica nada".
///
/// Se prueba con un TXT sin serie en el nombre, que es el caso real: el lector del TXT no
/// devuelve serie, así que aunque los equipos la tengan registrada, la comparación de
/// serie no llega a ejecutarse y el contenido del archivo es lo único que lo delata.
/// </remarks>
public sealed class ArchivoCargadoEnOtroEquipoTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const long EquipoAL = 1;
    private const long EquipoMZ = 2;

    /// <summary>Dos marcaciones del número 14, en el formato del attlog del G3.</summary>
    private static readonly byte[] ArchivoAttlog = Encoding.UTF8.GetBytes(
        "        14\t2026-07-06 06:48:00\t1\t0\t1\t0\n" +
        "        14\t2026-07-06 19:05:00\t1\t0\t1\t0\n");

    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;

    public ArchivoCargadoEnOtroEquipoTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        contexto.EquiposMarcador.AddRange(
            new EquipoMarcador
            {
                Id = EquipoAL,
                Codigo = "DET-AL",
                Nombre = "Detergentes A-L",
                Serie = "VGU6251600671",
                PlantaId = PlantaDetergentes
            },
            new EquipoMarcador
            {
                Id = EquipoMZ,
                Codigo = "DET-MZ",
                Nombre = "Detergentes M-Z",
                Serie = "VGU6251600672",
                PlantaId = PlantaDetergentes
            });

        contexto.SaveChanges();

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });

        servicio = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task El_archivo_vigente_en_un_equipo_no_se_carga_declarando_otro()
    {
        var primera = await ImportarAsync(EquipoAL);
        primera.FilasInsertadas.ShouldBe(2);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() => ImportarAsync(EquipoMZ));

        // Nombra dónde está cargado, que es lo que el operador necesita para decidir.
        error.Message.ShouldContain("Detergentes A-L");

        // Y no dejó nada a medias: ni marcaciones a nombre del M-Z ni la carga registrada.
        var crudas = await contexto.MarcacionesCrudas.AsNoTracking().ToListAsync(CancellationToken.None);
        crudas.Count.ShouldBe(2);
        crudas.ShouldAllBe(m => m.EquipoId == EquipoAL);
        (await contexto.Importaciones.CountAsync(CancellationToken.None)).ShouldBe(1);
    }

    /// <summary>
    /// Repetirlo en el mismo aparato sigue permitido: ahí la deduplicación sí lo absorbe.
    /// </summary>
    [Fact]
    public async Task Repetir_el_archivo_en_el_mismo_equipo_no_se_bloquea()
    {
        await ImportarAsync(EquipoAL);

        var repetida = await ImportarAsync(EquipoAL);

        repetida.FilasInsertadas.ShouldBe(0);
        repetida.FilasDuplicadas.ShouldBe(2);
        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(2);
    }

    /// <summary>
    /// La salida al "lo cargué con el equipo equivocado": anular y volver a cargar.
    /// </summary>
    [Fact]
    public async Task Anulada_la_carga_equivocada_el_archivo_entra_en_el_equipo_correcto()
    {
        var equivocada = await ImportarAsync(EquipoMZ);
        await servicio.AnularAsync(equivocada.ImportacionId, "Se declaró el M-Z", "coordinador", CancellationToken.None);

        var correcta = await ImportarAsync(EquipoAL);

        correcta.FilasInsertadas.ShouldBe(2);
        (await contexto.MarcacionesCrudas.CountAsync(m => m.EquipoId == EquipoAL, CancellationToken.None))
            .ShouldBe(2);
    }

    /// <summary>
    /// Reactivar la carga anulada es la otra puerta para dejar el archivo en dos aparatos.
    /// </summary>
    [Fact]
    public async Task No_se_reactiva_una_carga_si_el_mismo_archivo_esta_vigente_en_otro_equipo()
    {
        var equivocada = await ImportarAsync(EquipoMZ);
        await servicio.AnularAsync(equivocada.ImportacionId, "Se declaró el M-Z", "coordinador", CancellationToken.None);
        await ImportarAsync(EquipoAL);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.ReactivarAsync(equivocada.ImportacionId, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("Detergentes A-L");

        var carga = await contexto.Importaciones
            .AsNoTracking()
            .SingleAsync(i => i.Id == equivocada.ImportacionId, CancellationToken.None);
        carga.Anulada.ShouldBeTrue();
    }

    /// <summary>
    /// El aviso previo informa la carga vigente antes que una anulada más reciente.
    /// </summary>
    /// <remarks>
    /// La pantalla decide con este dato si ofrece confirmar o deja que el servicio rechace:
    /// si mostrara la anulada, pediría confirmar algo que después se va a rechazar.
    /// Las dos cargas se escriben a mano porque son datos de antes de esta regla: hoy la
    /// segunda ya no se podría hacer.
    /// </remarks>
    [Fact]
    public async Task El_aviso_previo_muestra_donde_esta_vigente_el_archivo()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(ArchivoAttlog));

        contexto.Importaciones.AddRange(
            new ImportacionMarcador
            {
                NombreArchivo = "attlog.txt",
                EquipoId = EquipoAL,
                HashContenido = hash,
                FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
                CargadoPorUsuario = "coordinador",
                FilasInsertadas = 2
            },
            new ImportacionMarcador
            {
                NombreArchivo = "attlog.txt",
                EquipoId = EquipoMZ,
                HashContenido = hash,
                FechaCarga = new DateTime(2026, 8, 5, 9, 0, 0),
                CargadoPorUsuario = "coordinador",
                FilasInsertadas = 2,
                Anulada = true,
                MotivoAnulacion = "Se declaró el M-Z"
            });
        await contexto.SaveChangesAsync(CancellationToken.None);

        using var archivo = new MemoryStream(ArchivoAttlog);
        var previa = await servicio.BuscarImportacionPreviaAsync(archivo, CancellationToken.None);

        previa.ShouldNotBeNull();
        previa.EquipoId.ShouldBe(EquipoAL);
        previa.Anulada.ShouldBeFalse();
    }

    private async Task<ResultadoImportacion> ImportarAsync(long equipoId)
    {
        using var archivo = new MemoryStream(ArchivoAttlog);

        return await servicio.ImportarAsync(
            archivo,
            "attlog.txt",
            PlantaDetergentes,
            equipoId,
            "coordinador",
            CancellationToken.None);
    }
}
