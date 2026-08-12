using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>
/// Pruebas del formato y la longitud del número de documento de identidad.
/// </summary>
/// <remarks>
/// Los tipos se construyen igual que están sembrados en <c>DatosSemilla</c>: DNI con
/// longitud exacta de 8 y solo dígitos; carné de extranjería, pasaporte y PTP con techo
/// de 10 y sin restricción de formato. Si el seed cambia, estos tests dejan de reflejar
/// la realidad y hay que actualizarlos a propósito, no por accidente.
/// </remarks>
public class ValidadorDocumentoIdentidadTests
{
    private static TipoDocumento Dni() => new()
    {
        Id = 1,
        Codigo = "DNI",
        Nombre = "Documento Nacional de Identidad",
        LongitudExacta = 8,
        SoloDigitos = true
    };

    private static TipoDocumento CarneExtranjeria() => new()
    {
        Id = 2,
        Codigo = "CE",
        Nombre = "Carné de Extranjería",
        LongitudMaxima = 10,
        SoloDigitos = false
    };

    [Fact]
    public void Dni_de_ocho_digitos_es_valido()
    {
        ValidadorDocumentoIdentidad.Validar(Dni(), "45678912").ShouldBeNull();
    }

    [Fact]
    public void Dni_conserva_los_ceros_a_la_izquierda()
    {
        // Si en algún punto el documento se tratara como número, "00123456" pasaría a ser
        // 123456 y dejaría de cruzar con el marcador y con la planilla.
        ValidadorDocumentoIdentidad.Validar(Dni(), "00123456").ShouldBeNull();
    }

    [Theory]
    [InlineData("4567891")]    // siete: uno de menos
    [InlineData("456789123")]  // nueve: uno de más
    [InlineData("1")]
    public void Dni_con_longitud_distinta_de_ocho_se_rechaza(string numero)
    {
        var problema = ValidadorDocumentoIdentidad.Validar(Dni(), numero);

        problema.ShouldNotBeNull();
        problema.ShouldContain("exactamente 8");
    }

    [Fact]
    public void Dni_con_letras_se_rechaza_por_formato_antes_que_por_longitud()
    {
        var problema = ValidadorDocumentoIdentidad.Validar(Dni(), "4567891A");

        problema.ShouldNotBeNull();
        problema.ShouldContain("solo dígitos");
    }

    [Fact]
    public void Dni_con_digitos_de_otro_alfabeto_se_rechaza()
    {
        // Ocho caracteres que Unicode considera dígitos (árabe-índicos). Se ven como un
        // número y entran de un copiar y pegar, pero no cruzan con nada.
        var problema = ValidadorDocumentoIdentidad.Validar(Dni(), "٤٥٦٧٨٩١٢");

        problema.ShouldNotBeNull();
        problema.ShouldContain("solo dígitos");
    }

    [Fact]
    public void Carne_de_extranjeria_de_diez_caracteres_es_valido()
    {
        ValidadorDocumentoIdentidad.Validar(CarneExtranjeria(), "AB12345678").ShouldBeNull();
    }

    [Fact]
    public void Carne_de_extranjeria_mas_corto_es_valido()
    {
        // El techo es máximo, no exacto: un documento de nueve caracteres es legítimo.
        ValidadorDocumentoIdentidad.Validar(CarneExtranjeria(), "AB1234567").ShouldBeNull();
    }

    [Fact]
    public void Carne_de_extranjeria_de_once_caracteres_se_rechaza()
    {
        var problema = ValidadorDocumentoIdentidad.Validar(CarneExtranjeria(), "AB123456789");

        problema.ShouldNotBeNull();
        problema.ShouldContain("máximo 10");
    }

    [Fact]
    public void Documento_vacio_se_rechaza()
    {
        ValidadorDocumentoIdentidad.Validar(Dni(), "   ").ShouldNotBeNull();
        ValidadorDocumentoIdentidad.Validar(Dni(), null).ShouldNotBeNull();
    }

    [Fact]
    public void Los_espacios_alrededor_no_cuentan_para_la_longitud()
    {
        ValidadorDocumentoIdentidad.Validar(Dni(), "  45678912  ").ShouldBeNull();
    }

    [Fact]
    public void Un_tipo_sin_reglas_admite_cualquier_cosa()
    {
        // Un tipo dado de alta desde Catálogos sin longitudes: no se le inventa un límite.
        var libre = new TipoDocumento { Id = 9, Codigo = "OTRO", Nombre = "Otro documento" };

        ValidadorDocumentoIdentidad.Validar(libre, "X-1").ShouldBeNull();
    }
}
