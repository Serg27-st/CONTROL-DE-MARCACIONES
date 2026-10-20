using System.Globalization;
using System.Text;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
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
/// Que un archivo salió del aparato que se declaró al cargarlo.
/// </summary>
/// <remarks>
/// <para>
/// Todos los ZKTeco numeran a su gente desde el 1, así que declarar el aparato equivocado
/// no produce ningún error: cada marcación encuentra dueño —el equivocado— y ni siquiera
/// aparece entre los números sin asignar. En Planta Detergentes, con un aparato A-L y otro
/// M-Z, es el error más probable y el más caro.
/// </para>
/// <para>
/// Sobre <b>SQLite real</b> y no InMemory: la comparación con los demás aparatos es una
/// consulta con subconsulta y rango de fechas, y la serie se compara sin distinguir
/// mayúsculas, dos cosas en las que el proveedor en memoria no se parece a la base.
/// </para>
/// </remarks>
public sealed class OrigenDeLaCargaTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const string SerieAL = "AEH2200760069";
    private const string SerieMZ = "AEWD193260084";

    private static readonly DateOnly PrimerDia = new(2026, 7, 6);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;
    private readonly long aparatoAL;
    private readonly long aparatoMZ;

    public OrigenDeLaCargaTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        // Sin serie: así están hoy los seis equipos reales, porque nadie puede leerla en el
        // aparato. Cada prueba registra la que necesita.
        var al = new EquipoMarcador { Codigo = "DET-AL", Nombre = "Detergentes A-L", PlantaId = PlantaDetergentes, Activo = true };
        var mz = new EquipoMarcador { Codigo = "DET-MZ", Nombre = "Detergentes M-Z", PlantaId = PlantaDetergentes, Activo = true };
        contexto.EquiposMarcador.AddRange(al, mz);
        contexto.SaveChanges();
        aparatoAL = al.Id;
        aparatoMZ = mz.Id;

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });
        servicio = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    // ---------------------------------------------------------------- Serie ----

    [Fact]
    public async Task Un_attlog_con_la_serie_del_equipo_en_el_nombre_queda_verificado()
    {
        await RegistrarSerieAsync(aparatoAL, SerieAL);

        var resultado = await CargarAsync(aparatoAL, $"{SerieAL}_attlog.dat", Jornadas(1, "14"));

        resultado.OrigenVerificado.ShouldBe(OrigenCarga.PorNombre);
        resultado.SerieArchivo.ShouldBe(SerieAL);

        // Y queda en el historial, que es donde se busca meses después.
        var historial = await servicio.HistorialAsync(cancelacion: CancellationToken.None);
        historial.Single().OrigenVerificado.ShouldBe(OrigenCarga.PorNombre);
    }

    /// <summary>
    /// El caso que motivó todo: el archivo del A-L declarado como M-Z.
    /// </summary>
    [Fact]
    public async Task Un_attlog_con_la_serie_de_otro_equipo_se_rechaza_nombrandolo()
    {
        await RegistrarSerieAsync(aparatoAL, SerieAL);
        await RegistrarSerieAsync(aparatoMZ, SerieMZ);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CargarAsync(aparatoMZ, $"{SerieAL}_attlog.dat", Jornadas(1, "14")));

        error.Message.ShouldContain("Detergentes A-L");
        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task El_primer_archivo_con_serie_la_registra_en_su_equipo()
    {
        var resultado = await CargarAsync(aparatoAL, $"{SerieAL}_attlog.dat", Jornadas(1, "14"));

        resultado.OrigenVerificado.ShouldBe(OrigenCarga.SerieAprendida);
        (await SerieDeAsync(aparatoAL)).ShouldBe(SerieAL);
    }

    /// <summary>
    /// El cruce que justifica aprender: una vez que el A-L tiene su serie, su archivo ya no
    /// entra por el M-Z aunque el M-Z no tenga ninguna.
    /// </summary>
    [Fact]
    public async Task Una_serie_aprendida_rechaza_su_archivo_declarado_con_otro_equipo()
    {
        await CargarAsync(aparatoAL, $"{SerieAL}_attlog.dat", Jornadas(1, "14"));

        // Otra descarga del mismo aparato: otro contenido, así que el hash no la detecta.
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CargarAsync(aparatoMZ, $"{SerieAL}_attlog (1).dat", Jornadas(2, "14")));

        error.Message.ShouldContain("Detergentes A-L");
        (await SerieDeAsync(aparatoMZ)).ShouldBeNull();
    }

    [Fact]
    public async Task Un_archivo_renombrado_se_acepta_sin_verificar_y_no_ensena_nada()
    {
        var resultado = await CargarAsync(aparatoAL, "03-03-2025.txt", Jornadas(1, "14"));

        resultado.OrigenVerificado.ShouldBe(OrigenCarga.NoVerificado);
        resultado.SerieArchivo.ShouldBeNull();
        resultado.FilasInsertadas.ShouldBe(2);
        (await SerieDeAsync(aparatoAL)).ShouldBeNull();
    }

    [Fact]
    public async Task Un_respaldo_renombrado_con_la_serie_de_otro_aparato_se_rechaza()
    {
        var respaldo = RespaldoDePrueba.Archivo(
            "VGU6251600671",
            new RespaldoDePrueba.Marca(14, PrimerDia.ToDateTime(new TimeOnly(6, 48))));

        using var archivo = new MemoryStream(respaldo);

        await Should.ThrowAsync<ReglaNegocioException>(() => servicio.ImportarAsync(
            archivo, $"{SerieAL}_attlog.dat", PlantaDetergentes, aparatoAL, "coordinador", CancellationToken.None));

        (await contexto.MarcacionesCrudas.CountAsync()).ShouldBe(0);
    }

    // --------------------------------------------------- Otro aparato, sin serie ----

    /// <summary>
    /// Un archivo renombrado —sin serie— que es una descarga posterior del A-L, declarada
    /// como M-Z. Solo lo delatan sus propias marcaciones.
    /// </summary>
    [Fact]
    public async Task Un_archivo_cuyas_marcaciones_ya_estan_en_otro_equipo_se_rechaza()
    {
        await CargarAsync(aparatoAL, "03-03-2025.txt", Jornadas(3, "1", "2", "3", "4"));

        // Los tres primeros días repiten los del A-L —el attlog acumula historial— y trae
        // dos nuevos.
        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CargarAsync(aparatoMZ, "NOV-POR TERMINAR.txt", Jornadas(5, "1", "2", "3", "4")));

        error.Message.ShouldContain("Detergentes A-L");
        error.Message.ShouldContain("24 de las 24");
        (await contexto.MarcacionesCrudas.CountAsync(m => m.EquipoId == aparatoMZ)).ShouldBe(0);
    }

    /// <summary>
    /// En el cambio de turno, el mismo número en dos aparatos puede marcar en el mismo
    /// segundo por azar. Unas pocas coincidencias no son un archivo ajeno.
    /// </summary>
    [Fact]
    public async Task Unas_pocas_coincidencias_con_otro_equipo_no_bloquean_la_carga()
    {
        var ids = Enumerable.Range(1, 10).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray();
        var delAL = Jornadas(5, ids);
        await CargarAsync(aparatoAL, "03-03-2025.txt", delAL);

        // Las mismas personas del M-Z, a otros segundos, salvo seis marcas idénticas: más
        // que el mínimo, pero 6 de 100 no es un archivo del A-L.
        var delMZ = Jornadas(5, segundo: 17, ids);
        for (var i = 0; i < 6; i++)
        {
            delMZ[i] = delAL[i];
        }

        var resultado = await CargarAsync(aparatoMZ, "NOV-POR TERMINAR.txt", delMZ);

        resultado.FilasInsertadas.ShouldBe(100);
    }

    /// <summary>
    /// Si el archivo se rechaza, su serie no se aprende: quedaría asociada al equipo que
    /// justamente no era.
    /// </summary>
    [Fact]
    public async Task Un_archivo_rechazado_no_deja_su_serie_aprendida()
    {
        await CargarAsync(aparatoAL, "03-03-2025.txt", Jornadas(3, "1", "2", "3", "4"));

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            CargarAsync(aparatoMZ, $"{SerieAL}_attlog.dat", Jornadas(5, "1", "2", "3", "4")));

        (await SerieDeAsync(aparatoMZ)).ShouldBeNull();
    }

    /// <summary>
    /// Anular la carga equivocada, cargar el archivo en el aparato correcto y después
    /// reactivar la primera dejaría las mismas marcaciones vigentes en dos aparatos. Los
    /// archivos son distintos, así que la comparación por hash no lo ve.
    /// </summary>
    [Fact]
    public async Task No_se_reactiva_una_carga_cuyas_marcaciones_estan_vigentes_en_otro_equipo()
    {
        var equivocada = await CargarAsync(aparatoMZ, "03-03-2025.txt", Jornadas(3, "1", "2", "3", "4"));
        await servicio.AnularAsync(equivocada.ImportacionId, "Era del A-L", "coordinador", CancellationToken.None);

        await CargarAsync(aparatoAL, "NOV-POR TERMINAR.txt", Jornadas(5, "1", "2", "3", "4"));

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.ReactivarAsync(equivocada.ImportacionId, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("Detergentes A-L");
        (await contexto.Importaciones.AsNoTracking().SingleAsync(i => i.Id == equivocada.ImportacionId))
            .Anulada.ShouldBeTrue();
    }

    // ------------------------------------------------------------------

    private async Task<ResultadoImportacion> CargarAsync(
        long equipoId,
        string nombreArchivo,
        IEnumerable<(string Id, DateTime Momento)> marcas)
    {
        var texto = string.Concat(marcas.Select(m =>
            $"{m.Id,10}\t{m.Momento.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\t1\t0\t1\t0\n"));

        using var archivo = new MemoryStream(Encoding.UTF8.GetBytes(texto));

        return await servicio.ImportarAsync(
            archivo, nombreArchivo, PlantaDetergentes, equipoId, "coordinador", CancellationToken.None);
    }

    /// <summary>
    /// Entrada y salida de cada número durante <paramref name="dias"/> días desde el 06/07,
    /// separados un minuto entre personas para que ninguna doble fichada los junte.
    /// </summary>
    private static List<(string Id, DateTime Momento)> Jornadas(int dias, params string[] ids) =>
        Jornadas(dias, segundo: 0, ids);

    private static List<(string Id, DateTime Momento)> Jornadas(int dias, int segundo, params string[] ids)
    {
        var marcas = new List<(string, DateTime)>();

        for (var d = 0; d < dias; d++)
        {
            var dia = PrimerDia.AddDays(d).ToDateTime(TimeOnly.MinValue);

            for (var i = 0; i < ids.Length; i++)
            {
                marcas.Add((ids[i], dia.AddHours(6).AddMinutes(40 + i).AddSeconds(segundo)));
                marcas.Add((ids[i], dia.AddHours(19).AddMinutes(5 + i).AddSeconds(segundo)));
            }
        }

        return marcas;
    }

    private async Task RegistrarSerieAsync(long equipoId, string serie) =>
        await new ServicioEquipos(contexto).RegistrarSerieAsync(equipoId, serie, "test", CancellationToken.None);

    private Task<string?> SerieDeAsync(long equipoId) =>
        contexto.EquiposMarcador.AsNoTracking().Where(e => e.Id == equipoId).Select(e => e.Serie).SingleAsync();
}
