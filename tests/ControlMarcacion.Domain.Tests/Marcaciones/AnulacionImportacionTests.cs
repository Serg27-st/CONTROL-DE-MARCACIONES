using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Marcaciones;

/// <summary>
/// Anulación de una carga de marcaciones.
/// </summary>
/// <remarks>
/// Es la alternativa a borrar, y existe porque borrar está prohibido: las marcaciones
/// crudas son inmutables. Anular no toca ninguna fila — solo marca la carga para que el
/// motor deje de contarla.
/// </remarks>
public class AnulacionImportacionTests
{
    private static readonly DateTime Momento = new(2026, 8, 11, 10, 30, 0);

    private static ImportacionMarcador Carga() => new()
    {
        Id = 1,
        NombreArchivo = "AEH2200760069_attlog.txt",
        HashContenido = new string('a', 64),
        FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
        CargadoPorUsuario = "admin",
        FilasLeidas = 100,
        FilasInsertadas = 90
    };

    [Fact]
    public void Anular_deja_constancia_de_quien_cuando_y_por_que()
    {
        var carga = Carga();

        carga.Anular("Se subió el archivo de otra planta", "coordinador", Momento);

        carga.Anulada.ShouldBeTrue();
        carga.MotivoAnulacion.ShouldBe("Se subió el archivo de otra planta");
        carga.AnuladaEn.ShouldBe(Momento);
        carga.AnuladaPorUsuario.ShouldBe("coordinador");
    }

    [Fact]
    public void Anular_no_altera_el_recuento_de_filas()
    {
        // Lo que se anula es el efecto, no el dato: las cifras de la carga siguen contando
        // lo que de verdad ocurrió, y sus marcaciones siguen en la base.
        var carga = Carga();

        carga.Anular("Prueba", "coordinador", Momento);

        carga.FilasLeidas.ShouldBe(100);
        carga.FilasInsertadas.ShouldBe(90);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Anular_sin_motivo_se_rechaza(string? motivo)
    {
        var carga = Carga();

        Should.Throw<ReglaNegocioException>(() => carga.Anular(motivo!, "coordinador", Momento));

        carga.Anulada.ShouldBeFalse();
    }

    [Fact]
    public void Anular_dos_veces_se_rechaza()
    {
        var carga = Carga();
        carga.Anular("Primera", "coordinador", Momento);

        var error = Should.Throw<ReglaNegocioException>(
            () => carga.Anular("Segunda", "coordinador", Momento));

        error.Message.ShouldContain("ya se anuló");
        carga.MotivoAnulacion.ShouldBe("Primera");
    }

    [Fact]
    public void Reactivar_devuelve_la_carga_al_calculo_y_limpia_el_rastro_de_anulacion()
    {
        var carga = Carga();
        carga.Anular("Me equivoqué de archivo", "coordinador", Momento);

        carga.Reactivar("coordinador");

        carga.Anulada.ShouldBeFalse();
        carga.MotivoAnulacion.ShouldBeNull();
        carga.AnuladaEn.ShouldBeNull();
        carga.AnuladaPorUsuario.ShouldBeNull();
    }

    [Fact]
    public void Reactivar_una_carga_vigente_se_rechaza()
    {
        Should.Throw<ReglaNegocioException>(() => Carga().Reactivar("coordinador"));
    }
}
