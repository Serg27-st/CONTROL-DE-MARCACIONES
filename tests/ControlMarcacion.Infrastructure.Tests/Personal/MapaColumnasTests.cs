using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Personal;

/// <summary>
/// Reconocimiento del encabezado del CSV de carga masiva.
/// </summary>
public class MapaColumnasTests
{
    private static readonly string[] Minimo =
    [
        "tipo_documento", "numero_documento", "apellido_paterno", "apellido_materno",
        "nombres", "sexo", "empresa", "unidad", "tipo_planilla", "puesto", "fecha_ingreso"
    ];

    [Fact]
    public void Reconoce_el_encabezado_canonico()
    {
        var mapa = MapaColumnas.Construir([.. MapaColumnas.EncabezadoCanonico]);

        mapa.Valor(["DNI", "09876543"], Columna.TipoDocumento).ShouldBe("DNI");
    }

    [Fact]
    public void El_orden_de_las_columnas_no_importa()
    {
        // Se buscan por nombre: un archivo con una columna insertada al medio seguiría
        // cargando "bien" si se leyera por posición, con los apellidos en la columna del
        // teléfono, y nadie lo notaría hasta ver el padrón.
        string[] encabezado = ["nombres", "numero_documento", .. Minimo];

        var mapa = MapaColumnas.Construir(encabezado);

        mapa.Valor(["JUAN", "09876543"], Columna.Nombres).ShouldBe("JUAN");
        mapa.Valor(["JUAN", "09876543"], Columna.NumeroDocumento).ShouldBe("09876543");
    }

    [Theory]
    [InlineData("dni")]
    [InlineData("documento")]
    [InlineData("Nro Documento")]
    [InlineData("NÚMERO DE DOCUMENTO")]
    public void Acepta_los_sinonimos_del_numero_de_documento(string titulo)
    {
        string[] encabezado = [titulo, .. Minimo.Where(c => c != "numero_documento")];

        var mapa = MapaColumnas.Construir(encabezado);

        mapa.Valor(["09876543"], Columna.NumeroDocumento).ShouldBe("09876543");
    }

    [Fact]
    public void Planta_es_sinonimo_de_unidad()
    {
        // GCA llama "unidad" a lo que el sistema llama planta. Las dos palabras llegan.
        string[] encabezado = ["planta", .. Minimo.Where(c => c != "unidad")];

        var mapa = MapaColumnas.Construir(encabezado);

        mapa.Valor(["DETERGENTES"], Columna.Unidad).ShouldBe("DETERGENTES");
    }

    [Fact]
    public void Rechaza_el_archivo_si_falta_una_columna_obligatoria()
    {
        var error = Should.Throw<ReglaNegocioException>(() =>
            MapaColumnas.Construir([.. Minimo.Where(c => c != "fecha_ingreso")]));

        error.Message.ShouldContain("fecha_ingreso");
    }

    [Fact]
    public void El_mensaje_enumera_todas_las_columnas_que_faltan_de_una_vez()
    {
        // De a una obligaría al usuario a reintentar el archivo tantas veces como
        // columnas le falten.
        var error = Should.Throw<ReglaNegocioException>(() =>
            MapaColumnas.Construir(["nombres", "sexo"]));

        error.Message.ShouldContain("tipo_documento");
        error.Message.ShouldContain("numero_documento");
        error.Message.ShouldContain("fecha_ingreso");
    }

    [Fact]
    public void Las_columnas_opcionales_pueden_faltar()
    {
        var mapa = MapaColumnas.Construir(Minimo);

        mapa.Valor(["DNI"], Columna.SueldoBasico).ShouldBeNull();
        mapa.Valor(["DNI"], Columna.IdMarcador).ShouldBeNull();
    }

    [Fact]
    public void Una_fila_mas_corta_que_el_encabezado_no_revienta()
    {
        // Excel recorta las columnas finales vacías al exportar.
        var mapa = MapaColumnas.Construir([.. MapaColumnas.EncabezadoCanonico]);

        mapa.Valor(["DNI", "09876543"], Columna.SueldoBasico).ShouldBeNull();
    }

    [Fact]
    public void Un_campo_vacio_se_lee_como_nulo_y_no_como_cadena_vacia()
    {
        var mapa = MapaColumnas.Construir(Minimo);

        mapa.Valor(["DNI", "   "], Columna.NumeroDocumento).ShouldBeNull();
    }
}
