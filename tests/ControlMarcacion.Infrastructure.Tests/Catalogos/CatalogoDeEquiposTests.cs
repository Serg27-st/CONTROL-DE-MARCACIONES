using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

/// <summary>
/// Alta de equipos y de sus códigos, con las reglas que hacen posible el caso real.
/// </summary>
/// <remarks>
/// El caso es Planta Detergentes con dos aparatos, A-L y M-Z, ambos numerando del 1 al 100.
/// El modelo lo soportaba desde que existe <c>EquipoMarcador</c>, pero el catálogo seguía
/// exigiendo que el código fuera único en TODO el sistema: la pantalla rechazaba el segundo
/// "14" y no había forma de registrar el segundo aparato desde la interfaz.
///
/// Estas pruebas fijan las dos mitades: que el código sea único por equipo y no más allá, y
/// que la planta del marcador se herede del aparato en vez de capturarse aparte —si se
/// capturaran las dos, podrían guardarse en desacuerdo—.
/// </remarks>
public sealed class CatalogoDeEquiposTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const long PlantaVeinteMil = 2;

    private const string CodigoCompartido = "14";

    private readonly MarcacionDbContext contexto;
    private readonly ServicioEquipos equipos;
    private readonly ServicioMarcadores marcadores;

    public CatalogoDeEquiposTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        // Empresa y plantas llegan de DatosSemilla, que EnsureCreated aplica también en
        // InMemory: sembrarlas aquí chocaría con esas mismas claves.
        equipos = new ServicioEquipos(contexto);
        marcadores = new ServicioMarcadores(contexto);
    }

    public void Dispose() => contexto.Dispose();

    /// <summary>
    /// La prueba que desbloquea el caso de GCA.
    /// </summary>
    [Fact]
    public async Task El_mismo_codigo_se_registra_en_los_dos_equipos_de_una_planta()
    {
        var al = await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);
        var mz = await CrearEquipoAsync("DET-MZ", "Detergentes M-Z", PlantaDetergentes);

        var enAl = await CrearMarcadorAsync(al, CodigoCompartido);
        var enMz = await CrearMarcadorAsync(mz, CodigoCompartido);

        enAl.ShouldNotBe(enMz);

        var registrados = await contexto.Marcadores
            .AsNoTracking()
            .Where(m => m.Codigo == CodigoCompartido)
            .ToListAsync(CancellationToken.None);

        registrados.Count.ShouldBe(2);
        registrados.Select(m => m.EquipoId).ShouldBe([al, mz], ignoreOrder: true);
    }

    [Fact]
    public async Task Repetir_el_codigo_dentro_del_mismo_equipo_se_rechaza()
    {
        var al = await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);

        await CrearMarcadorAsync(al, CodigoCompartido);

        // Dentro de un aparato el código sí identifica a una sola persona.
        var error = await Should.ThrowAsync<ReglaNegocioException>(
            () => CrearMarcadorAsync(al, CodigoCompartido));

        error.Message.ShouldContain(CodigoCompartido);
    }

    /// <summary>
    /// <c>Marcador.PlantaId</c> es una copia derivada, no un dato que se capture.
    /// </summary>
    [Fact]
    public async Task El_marcador_hereda_la_planta_de_su_equipo()
    {
        var enVeinteMil = await CrearEquipoAsync("20M-01", "20 mil principal", PlantaVeinteMil);

        var id = await CrearMarcadorAsync(enVeinteMil, "7");

        var marcador = await contexto.Marcadores
            .AsNoTracking()
            .FirstAsync(m => m.Id == id, CancellationToken.None);

        marcador.PlantaId.ShouldBe(PlantaVeinteMil);
    }

    [Fact]
    public async Task Registrar_un_marcador_en_un_equipo_de_baja_se_rechaza()
    {
        var al = await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);

        await equipos.CambiarEstadoAsync(al, activo: false, "tester", CancellationToken.None);

        await Should.ThrowAsync<ReglaNegocioException>(() => CrearMarcadorAsync(al, "3"));
    }

    /// <summary>
    /// La serie es la huella del aparato: duplicarla dejaría sin sentido la comprobación
    /// que hace la carga al leer un archivo.
    /// </summary>
    [Fact]
    public async Task Dos_equipos_no_pueden_declarar_la_misma_serie()
    {
        await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes, "VGU6251600671");

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            () => CrearEquipoAsync("DET-MZ", "Detergentes M-Z", PlantaDetergentes, "VGU6251600671"));

        error.Message.ShouldContain("VGU6251600671");
    }

    /// <summary>
    /// Un equipo sin serie no choca con otro sin serie: la serie es opcional porque el
    /// aparato se da de alta antes de haber visto un archivo suyo.
    /// </summary>
    [Fact]
    public async Task Varios_equipos_pueden_quedarse_sin_serie_declarada()
    {
        await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);
        await CrearEquipoAsync("DET-MZ", "Detergentes M-Z", PlantaDetergentes);

        var sinSerie = await contexto.EquiposMarcador
            .AsNoTracking()
            .CountAsync(e => e.Serie == null, CancellationToken.None);

        sinSerie.ShouldBe(2);
    }

    [Fact]
    public async Task No_se_puede_mudar_de_planta_un_equipo_que_ya_emitio_codigos()
    {
        var al = await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);
        await CrearMarcadorAsync(al, CodigoCompartido);

        // Mudarlo arrastraría a otra planta a todo su personal: Personal exige que el
        // marcador sea de la planta del trabajador, así que esas fichas quedarían
        // imposibles de guardar sin tocarlas una por una.
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            equipos.GuardarAsync(
                new GuardarEquipoRequest
                {
                    Id = al,
                    Codigo = "DET-AL",
                    Nombre = "Detergentes A-L",
                    PlantaId = PlantaVeinteMil
                },
                "tester",
                CancellationToken.None));

        error.Message.ShouldContain("marcador(es)");
    }

    [Fact]
    public async Task No_se_puede_dar_de_baja_un_equipo_con_marcadores_activos()
    {
        var al = await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);
        await CrearMarcadorAsync(al, CodigoCompartido);

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            equipos.CambiarEstadoAsync(al, activo: false, "tester", CancellationToken.None));
    }

    [Fact]
    public async Task El_codigo_del_equipo_es_unico_en_todo_el_sistema()
    {
        await CrearEquipoAsync("DET-AL", "Detergentes A-L", PlantaDetergentes);

        // A diferencia del código de marcador: este identifica al aparato, no a una persona
        // dentro de él, así que repetirlo dejaría la carga sin saber cuál se eligió.
        await Should.ThrowAsync<ReglaNegocioException>(
            () => CrearEquipoAsync("DET-AL", "Otro aparato", PlantaVeinteMil));
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearEquipoAsync(
        string codigo,
        string nombre,
        long plantaId,
        string? serie = null) =>
        await equipos.GuardarAsync(
            new GuardarEquipoRequest
            {
                Codigo = codigo,
                Nombre = nombre,
                Serie = serie,
                PlantaId = plantaId
            },
            "tester",
            CancellationToken.None);

    private async Task<long> CrearMarcadorAsync(long equipoId, string codigo) =>
        await marcadores.GuardarAsync(
            new GuardarMarcadorRequest { Codigo = codigo, EquipoId = equipoId },
            "tester",
            CancellationToken.None);
}
