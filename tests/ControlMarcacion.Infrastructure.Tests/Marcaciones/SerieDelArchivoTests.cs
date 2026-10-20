using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// La serie del aparato, sacada del nombre del attlog o del contenido del respaldo.
/// </summary>
/// <remarks>
/// Los nombres son los que aparecieron en el share de producción: 19 de 22 archivos traían
/// la serie en el nombre, algunos con sufijos agregados a mano, y 3 se renombraron del todo.
/// </remarks>
public class SerieDelArchivoTests
{
    [Theory]
    [InlineData("AEWD193260084_attlog.dat", "AEWD193260084")]
    [InlineData("AEWD193260084_attlog PAT.dat", "AEWD193260084")]
    [InlineData("AEH2200760069_attlog_prueba.txt", "AEH2200760069")]
    [InlineData("AEWD193260084_attlog (1).dat", "AEWD193260084")]
    [InlineData("aewd193260084_ATTLOG.dat", "AEWD193260084")]
    [InlineData(@"C:\fakepath\AEWD193260084_attlog.dat", "AEWD193260084")]
    public void Lee_la_serie_del_nombre_del_attlog(string nombre, string esperada) =>
        SerieDelArchivo.DesdeNombre(nombre).ShouldBe(esperada);

    [Theory]
    [InlineData("03-03-2025.txt")]
    [InlineData("NOV-POR TERMINAR.txt")]
    [InlineData("BKtransaction.dat")]
    [InlineData("attlog.txt")]
    [InlineData("")]
    public void Un_nombre_sin_esa_forma_no_trae_serie(string nombre) =>
        SerieDelArchivo.DesdeNombre(nombre).ShouldBeNull();

    [Fact]
    public void La_serie_del_contenido_manda_sobre_un_nombre_que_no_la_trae()
    {
        var (serie, delContenido) = SerieDelArchivo.Resolver("VGU6251600671", "BKtransaction.dat");

        serie.ShouldBe("VGU6251600671");
        delContenido.ShouldBeTrue();
    }

    [Fact]
    public void Sin_serie_en_el_contenido_se_usa_la_del_nombre()
    {
        var (serie, delContenido) = SerieDelArchivo.Resolver(null, "AEWD193260084_attlog.dat");

        serie.ShouldBe("AEWD193260084");
        delContenido.ShouldBeFalse();
    }

    [Fact]
    public void Contenido_y_nombre_con_la_misma_serie_no_es_conflicto() =>
        SerieDelArchivo.Resolver("vgu6251600671", "VGU6251600671_attlog.dat").Serie.ShouldBe("VGU6251600671");

    /// <summary>
    /// Un archivo renombrado con la serie de otro aparato: no hay forma de saber cuál dice
    /// la verdad, así que no se carga.
    /// </summary>
    [Fact]
    public void Contenido_y_nombre_con_series_distintas_se_rechaza()
    {
        var error = Should.Throw<ReglaNegocioException>(() =>
            SerieDelArchivo.Resolver("VGU6251600671", "AEWD193260084_attlog.dat"));

        error.Message.ShouldContain("VGU6251600671");
        error.Message.ShouldContain("AEWD193260084");
    }

    [Fact]
    public void Un_archivo_sin_serie_en_ningun_lado_no_trae_serie() =>
        SerieDelArchivo.Resolver(null, "03-03-2025.txt").Serie.ShouldBeNull();
}
