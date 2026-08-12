using System.Text;
using ControlMarcacion.Infrastructure.Marcaciones;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Pruebas del lector del TXT del ZKTeco G3.
/// </summary>
/// <remarks>
/// Se apoyan en el archivo real de producción guardado en <c>Recursos/</c>: 2509
/// marcaciones de 50 trabajadores durante julio de 2026. Probar contra un archivo
/// inventado no habría revelado los ids con ceros significativos ni las dobles fichadas.
/// </remarks>
public class LectorTxtZktecoTests
{
    private static readonly string RutaArchivoReal =
        Path.Combine(AppContext.BaseDirectory, "Recursos", "AEH2200760069_attlog.txt");

    private static LectorTxtZkteco CrearLector(OpcionesMarcador? opciones = null) =>
        new(Options.Create(opciones ?? new OpcionesMarcador()));

    private static MemoryStream Contenido(string texto) =>
        new MemoryStream(Encoding.UTF8.GetBytes(texto));

    // ------------------------------------------------------------------
    // Formato base
    // ------------------------------------------------------------------

    [Fact]
    public void Lee_una_linea_bien_formada()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido("        2\t2026-07-01 07:00:29\t1\t0\t1\t0\r\n"));

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(1);

        var marcacion = resultado.Marcaciones[0];
        marcacion.IdMarcador.ShouldBe("2");
        marcacion.FechaHora.ShouldBe(new DateTime(2026, 7, 1, 7, 0, 29));
        marcacion.NumeroLinea.ShouldBe(1);
    }

    [Fact]
    public void Recorta_los_espacios_a_la_izquierda_del_identificador()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido("       25\t2026-07-01 07:00:29\t1\t0\t1\t0\r\n"));

        resultado.Marcaciones[0].IdMarcador.ShouldBe("25");
    }

    /// <summary>
    /// El archivo real trae "0014" y "14" como identificadores distintos. Si el lector los
    /// convirtiera a número, ambos serían 14 y las marcaciones de dos personas se
    /// mezclarían en el tareo.
    /// </summary>
    [Fact]
    public void Conserva_los_ceros_significativos_del_identificador()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido(
            "     0014\t2026-07-01 07:00:29\t1\t0\t1\t0\r\n" +
            "       14\t2026-07-01 07:00:30\t1\t0\t1\t0\r\n"));

        resultado.Marcaciones.Select(m => m.IdMarcador).ShouldBe(["0014", "14"]);
    }

    /// <summary>
    /// El equipo reporta siempre 0 en la columna de tipo. El lector lo guarda tal cual y
    /// no intenta deducir si es entrada o salida: eso lo resuelve el motor contra el turno.
    /// </summary>
    [Fact]
    public void Guarda_el_tipo_de_marcacion_sin_interpretarlo()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido("        2\t2026-07-01 19:06:55\t1\t0\t1\t0\r\n"));

        resultado.Marcaciones[0].TipoMarcacion.ShouldBe("0");
        resultado.Marcaciones[0].ModoVerificacion.ShouldBe("1");
    }

    // ------------------------------------------------------------------
    // Líneas problemáticas
    // ------------------------------------------------------------------

    [Fact]
    public void Ignora_las_lineas_en_blanco_sin_contarlas_como_error()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido(
            "        2\t2026-07-01 07:00:29\t1\t0\t1\t0\r\n" +
            "\r\n" +
            "   \r\n"));

        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Rechazadas.ShouldBeEmpty();
    }

    [Fact]
    public void Rechaza_la_linea_con_fecha_ilegible_pero_conserva_las_demas()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido(
            "        2\t2026-07-01 07:00:29\t1\t0\t1\t0\r\n" +
            "        3\tno-es-una-fecha\t1\t0\t1\t0\r\n" +
            "        4\t2026-07-01 07:02:00\t1\t0\t1\t0\r\n"));

        // Una línea corrupta no puede impedir que se carguen las otras.
        resultado.Marcaciones.Count.ShouldBe(2);
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].NumeroLinea.ShouldBe(2);
        resultado.Rechazadas[0].Motivo.ShouldContain("no-es-una-fecha");
    }

    [Fact]
    public void Rechaza_la_linea_sin_columnas_suficientes()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido("        2\r\n"));

        resultado.Marcaciones.ShouldBeEmpty();
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("columnas");
    }

    [Fact]
    public void Rechaza_la_linea_con_identificador_vacio()
    {
        var lector = CrearLector();

        var resultado = lector.Leer(Contenido("   \t2026-07-01 07:00:29\t1\t0\t1\t0\r\n"));

        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("identificador");
    }

    /// <summary>
    /// Un marcador con la fecha mal configurada reportaría marcaciones de hace años.
    /// Cargarlas en silencio contaminaría periodos ya cerrados.
    /// </summary>
    [Fact]
    public void Rechaza_las_marcaciones_demasiado_antiguas()
    {
        var lector = CrearLector(new OpcionesMarcador { DiasAntiguedadMaxima = 30 });

        var antigua = DateTime.Now.AddDays(-90).ToString("yyyy-MM-dd HH:mm:ss", null);
        var reciente = DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd HH:mm:ss", null);

        var resultado = lector.Leer(Contenido(
            $"        2\t{antigua}\t1\t0\t1\t0\r\n" +
            $"        2\t{reciente}\t1\t0\t1\t0\r\n"));

        resultado.Marcaciones.Count.ShouldBe(1);
        resultado.Rechazadas.Count.ShouldBe(1);
        resultado.Rechazadas[0].Motivo.ShouldContain("antigüedad");
    }

    // ------------------------------------------------------------------
    // Archivo real de producción
    // ------------------------------------------------------------------

    [Fact]
    public void Lee_el_archivo_real_completo_sin_rechazar_ninguna_linea()
    {
        var lector = CrearLector();

        using var archivo = File.OpenRead(RutaArchivoReal);
        var resultado = lector.Leer(archivo);

        resultado.Rechazadas.ShouldBeEmpty();
        resultado.Marcaciones.Count.ShouldBe(2509);
    }

    [Fact]
    public void El_archivo_real_cubre_julio_de_2026()
    {
        var lector = CrearLector();

        using var archivo = File.OpenRead(RutaArchivoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones.Min(m => m.FechaHora).Date.ShouldBe(new DateTime(2026, 7, 1));
        resultado.Marcaciones.Max(m => m.FechaHora).Date.ShouldBe(new DateTime(2026, 7, 31));
    }

    [Fact]
    public void El_archivo_real_trae_cincuenta_identificadores_distintos()
    {
        var lector = CrearLector();

        using var archivo = File.OpenRead(RutaArchivoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones
            .Select(m => m.IdMarcador)
            .Distinct(StringComparer.Ordinal)
            .Count()
            .ShouldBe(50);
    }

    [Fact]
    public void El_archivo_real_contiene_identificadores_con_ceros_a_la_izquierda()
    {
        var lector = CrearLector();

        using var archivo = File.OpenRead(RutaArchivoReal);
        var resultado = lector.Leer(archivo);

        var conCeros = resultado.Marcaciones
            .Select(m => m.IdMarcador)
            .Distinct(StringComparer.Ordinal)
            .Where(id => id.StartsWith('0'))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        conCeros.ShouldBe(["0014", "0024", "0044", "0064"]);
    }

    /// <summary>
    /// Documenta el hallazgo que condiciona todo el motor de cálculo: el equipo nunca
    /// informa si la marca es entrada o salida.
    /// </summary>
    [Fact]
    public void En_el_archivo_real_el_tipo_de_marcacion_siempre_es_cero()
    {
        var lector = CrearLector();

        using var archivo = File.OpenRead(RutaArchivoReal);
        var resultado = lector.Leer(archivo);

        resultado.Marcaciones
            .Select(m => m.TipoMarcacion)
            .Distinct(StringComparer.Ordinal)
            .ShouldBe(["0"]);
    }
}
