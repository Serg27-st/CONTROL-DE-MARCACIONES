using ControlMarcacion.Infrastructure.Reportes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Reportes;

/// <summary>
/// Neutralización de inyección de fórmulas en los exports de Excel.
/// </summary>
/// <remarks>
/// Un nombre de trabajador que empieza por <c>= + - @</c> (o tabulador/retorno) se ejecutaría
/// como fórmula al abrir el archivo. Estos valores pueden entrar por la carga masiva de
/// personal desde un CSV/XLSX, así que la salida se sanea.
/// </remarks>
public sealed class SanitizadorExcelTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://malo\",\"clic\")", "'=HYPERLINK(\"http://malo\",\"clic\")")]
    [InlineData("+48+22", "'+48+22")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A9)", "'@SUM(A1:A9)")]
    [InlineData("\tFuga", "'\tFuga")]
    [InlineData("\rReturn", "'\rReturn")]
    [InlineData("\nSalto", "'\nSalto")]
    public void Un_texto_que_abre_formula_se_prefija_con_apostrofo(string entrada, string esperado) =>
        SanitizadorExcel.Neutralizar(entrada).ShouldBe(esperado);

    [Theory]
    [InlineData("PEREZ GOMEZ, JUAN")]
    [InlineData("40404040")]
    [InlineData("Planta Detergentes")]
    [InlineData("2+2 en medio no importa")]
    public void Un_texto_normal_no_se_toca(string entrada) =>
        SanitizadorExcel.Neutralizar(entrada).ShouldBe(entrada);

    [Fact]
    public void Null_y_vacio_pasan_sin_cambios()
    {
        SanitizadorExcel.Neutralizar(null).ShouldBeNull();
        SanitizadorExcel.Neutralizar(string.Empty).ShouldBe(string.Empty);
    }
}
