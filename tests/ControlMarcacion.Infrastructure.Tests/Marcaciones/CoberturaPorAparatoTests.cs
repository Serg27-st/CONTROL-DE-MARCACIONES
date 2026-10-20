using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// La cobertura de marcaciones, mirada aparato por aparato.
/// </summary>
/// <remarks>
/// <para>
/// El calendario contaba un día como cubierto si tenía marcaciones de cualquier aparato. En
/// Planta Detergentes, con un aparato A-L y otro M-Z, cargar el archivo del A-L y olvidar el
/// del M-Z dejaba el mes entero «cubierto», la portada decía que no faltaba nada, y la mitad
/// de la planta salía en falta al calcular.
/// </para>
/// <para>
/// Sobre SQLite real: el agrupado por fecha y aparato, y el mínimo por aparato sobre las
/// cargas vigentes, son consultas que InMemory no traduce.
/// </para>
/// </remarks>
public sealed class CoberturaPorAparatoTests : IAsyncLifetime, IDisposable
{
    private const long PlantaDetergentes = 1;

    /// <summary>Hasta dónde se miran los días: después no hay datos y todo sería hueco.</summary>
    private static readonly DateOnly Hoy = new(2026, 7, 8);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;
    private long aparatoAL;
    private long aparatoMZ;

    public CoberturaPorAparatoTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        servicio = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    /// <remarks>
    /// Los equipos se crean con <c>SaveChangesAsync</c>, como en producción: es el que estampa
    /// <c>CreadoEn</c>, y el alta decide desde cuándo se espera un aparato sin marcaciones.
    /// </remarks>
    public async Task InitializeAsync()
    {
        aparatoAL = await CrearEquipoAsync("DET-AL", "Detergentes A-L");
        aparatoMZ = await CrearEquipoAsync("DET-MZ", "Detergentes M-Z");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// El caso de Detergentes: el A-L cargado del 6 al 8 de julio y el M-Z solo el 6. Los
    /// tres días tienen marcaciones; dos están a medias.
    /// </summary>
    [Fact]
    public async Task Un_dia_con_el_A_L_cargado_y_sin_el_M_Z_queda_incompleto()
    {
        await CargarAsync(aparatoAL, desdeDia: 6, dias: 3);
        await CargarAsync(aparatoMZ, desdeDia: 6, dias: 1, segundo: 17);

        var cobertura = await servicio.CoberturaAsync(2026, 7, CancellationToken.None);

        cobertura.EstadoDe(DiaDe(cobertura, 6), Hoy).ShouldBe(EstadoCobertura.Completo);
        cobertura.EstadoDe(DiaDe(cobertura, 7), Hoy).ShouldBe(EstadoCobertura.Incompleto);
        cobertura.EstadoDe(DiaDe(cobertura, 8), Hoy).ShouldBe(EstadoCobertura.Incompleto);

        cobertura.DiasIncompletos(Hoy).ShouldBe(2);
        // Del 1 al 5 no hay nada de nadie: son días sin marcaciones, no incompletos.
        cobertura.DiasSinMarcacion(Hoy).ShouldBe(5);
        cobertura.FaltantesEn(DiaDe(cobertura, 7), Hoy).Select(e => e.Nombre).ShouldBe(["Detergentes M-Z"]);

        var faltantes = cobertura.FaltantesPorEquipo(Hoy);
        faltantes.Count.ShouldBe(1);
        faltantes[0].Equipo.EquipoId.ShouldBe(aparatoMZ);
        faltantes[0].Dias.ShouldBe(2);

        // El desglose por aparato del día: el 7 solo trae personas del A-L.
        DiaDe(cobertura, 7).PorEquipo.Select(e => e.EquipoId).ShouldBe([aparatoAL]);
        DiaDe(cobertura, 7).De(aparatoAL)!.Personas.ShouldBe(4);

        // Y la pastilla del mes lo avisa sin tener que abrirlo.
        cobertura.Meses.Single(m => m.Mes == 7).DiasIncompletos.ShouldBe(2);
    }

    /// <summary>
    /// La portada usa la misma regla: no puede decir «todo cargado» sobre un mes que el
    /// calendario marca incompleto.
    /// </summary>
    [Fact]
    public async Task La_portada_cuenta_los_dias_incompletos_y_nombra_el_aparato()
    {
        await CargarAsync(aparatoAL, desdeDia: 6, dias: 3);
        await CargarAsync(aparatoMZ, desdeDia: 6, dias: 1, segundo: 17);

        var rango = await servicio.CoberturaDeRangoAsync(
            new DateOnly(2026, 7, 6), Hoy, CancellationToken.None);

        rango.DiasSinMarcacion.ShouldBe(0);
        rango.DiasIncompletos.ShouldBe(2);
        rango.HayHuecos.ShouldBeTrue();
        rango.AparatosConFaltantes.ShouldBe(["Detergentes M-Z"]);
    }

    /// <summary>
    /// Un aparato registrado del que nunca se cargó nada es el olvido más completo: se
    /// espera desde su alta. El M-Z, dado de alta hoy, todavía no se espera en julio.
    /// </summary>
    [Fact]
    public async Task Un_aparato_del_que_nunca_se_cargo_nada_se_espera_desde_su_alta()
    {
        var lavaVajillas = await CrearEquipoAsync("DET-LV", "Lava vajillas");

        // CreadoEn lo estampa el contexto al guardar; para simular un alta antigua se
        // escribe directo en la tabla.
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE equipo_marcador SET creado_en = {new DateTime(2026, 7, 1)} WHERE id = {lavaVajillas}");

        await CargarAsync(aparatoAL, desdeDia: 6, dias: 1);

        var cobertura = await servicio.CoberturaAsync(2026, 7, CancellationToken.None);

        cobertura.EstadoDe(DiaDe(cobertura, 6), Hoy).ShouldBe(EstadoCobertura.Incompleto);
        cobertura.FaltantesEn(DiaDe(cobertura, 6), Hoy).Select(e => e.Nombre).ShouldBe(["Lava vajillas"]);
    }

    /// <summary>
    /// Una carga anulada no cubre nada, igual que para el motor: si el único archivo del
    /// M-Z del día 7 se anuló, al día 7 le falta el M-Z.
    /// </summary>
    [Fact]
    public async Task Una_carga_anulada_no_cubre_el_dia()
    {
        await CargarAsync(aparatoAL, desdeDia: 6, dias: 2);
        await CargarAsync(aparatoMZ, desdeDia: 6, dias: 1, segundo: 17);
        var delSiete = await CargarAsync(aparatoMZ, desdeDia: 7, dias: 1, segundo: 17);

        await servicio.AnularAsync(delSiete, "Archivo equivocado", "coordinador", CancellationToken.None);

        var cobertura = await servicio.CoberturaAsync(2026, 7, CancellationToken.None);

        cobertura.EstadoDe(DiaDe(cobertura, 7), Hoy).ShouldBe(EstadoCobertura.Incompleto);
    }

    // ------------------------------------------------------------------

    private async Task<long> CrearEquipoAsync(string codigo, string nombre)
    {
        var equipo = new EquipoMarcador { Codigo = codigo, Nombre = nombre, PlantaId = PlantaDetergentes, Activo = true };
        contexto.EquiposMarcador.Add(equipo);
        await contexto.SaveChangesAsync();
        return equipo.Id;
    }

    private static DiaConMarcaciones DiaDe(CoberturaMarcaciones cobertura, int dia) =>
        cobertura.Dias.Single(d => d.Fecha == new DateOnly(2026, 7, dia));

    /// <summary>
    /// Carga un archivo renombrado —sin serie— con entrada y salida de los números 1 a 4 en
    /// cada día.
    /// </summary>
    private async Task<long> CargarAsync(long equipoId, int desdeDia, int dias, int segundo = 0)
    {
        var texto = new StringBuilder();

        for (var d = 0; d < dias; d++)
        {
            var dia = new DateTime(2026, 7, desdeDia + d);

            for (var n = 1; n <= 4; n++)
            {
                foreach (var hora in new[] { dia.AddHours(6).AddMinutes(40 + n), dia.AddHours(19).AddMinutes(5 + n) })
                {
                    texto.Append(CultureInfo.InvariantCulture,
                        $"{n,10}\t{hora.AddSeconds(segundo):yyyy-MM-dd HH:mm:ss}\t1\t0\t1\t0\n");
                }
            }
        }

        using var archivo = new MemoryStream(Encoding.UTF8.GetBytes(texto.ToString()));

        var resultado = await servicio.ImportarAsync(
            archivo, $"carga-{equipoId}-{desdeDia}.txt", PlantaDetergentes, equipoId, "coordinador", CancellationToken.None);

        return resultado.ImportacionId;
    }
}
