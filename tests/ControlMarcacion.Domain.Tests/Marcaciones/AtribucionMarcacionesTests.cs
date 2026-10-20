using ControlMarcacion.Domain.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Marcaciones;

/// <summary>
/// De quién es cada marcación: de quien tenía ese número en ese aparato en esa fecha.
/// </summary>
public class AtribucionMarcacionesTests
{
    private const long AparatoAL = 1;
    private const long AparatoMZ = 2;

    private const long Flores = 10;
    private const long Quispe = 20;
    private const long Reemplazo = 30;

    [Fact]
    public void El_mismo_numero_en_dos_aparatos_son_dos_personas()
    {
        var atribucion = new AtribucionMarcaciones(
        [
            new VigenciaIdMarcador(Flores, AparatoAL, "14", new DateOnly(2026, 1, 1), null),
            new VigenciaIdMarcador(Quispe, AparatoMZ, "14", new DateOnly(2026, 1, 1), null)
        ]);

        var dia = new DateTime(2026, 7, 6, 7, 0, 0);

        atribucion.Resolver(AparatoAL, "14", dia).ShouldBe(Flores);
        atribucion.Resolver(AparatoMZ, "14", dia).ShouldBe(Quispe);
    }

    /// <summary>
    /// El número se reasigna cuando alguien cesa: lo anterior al cambio sigue siendo de
    /// quien se fue. Resolverlo por la asignación de hoy se lo daría al reemplazo.
    /// </summary>
    [Fact]
    public void Un_numero_reasignado_atribuye_cada_fecha_a_quien_lo_tenia_entonces()
    {
        var atribucion = new AtribucionMarcaciones(
        [
            new VigenciaIdMarcador(Flores, AparatoAL, "14", new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 15)),
            new VigenciaIdMarcador(Reemplazo, AparatoAL, "14", new DateOnly(2026, 7, 16), null)
        ]);

        atribucion.Resolver(AparatoAL, "14", new DateTime(2026, 7, 15, 18, 59, 0)).ShouldBe(Flores);
        atribucion.Resolver(AparatoAL, "14", new DateTime(2026, 7, 16, 6, 55, 0)).ShouldBe(Reemplazo);
    }

    [Fact]
    public void Fuera_de_toda_vigencia_la_marcacion_no_es_de_nadie()
    {
        var atribucion = new AtribucionMarcaciones(
        [
            new VigenciaIdMarcador(Flores, AparatoAL, "14", new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20))
        ]);

        atribucion.Resolver(AparatoAL, "14", new DateTime(2026, 7, 9, 7, 0, 0)).ShouldBeNull();
        atribucion.Resolver(AparatoAL, "14", new DateTime(2026, 7, 21, 7, 0, 0)).ShouldBeNull();
        atribucion.Resolver(AparatoAL, "15", new DateTime(2026, 7, 15, 7, 0, 0)).ShouldBeNull();
    }

    /// <summary>
    /// <c>0014</c> y <c>14</c> son usuarios distintos del aparato: en el padrón real son
    /// dos personas. Quitar los ceros al comparar las fundiría.
    /// </summary>
    [Fact]
    public void Los_ceros_a_la_izquierda_distinguen_numeros()
    {
        var atribucion = new AtribucionMarcaciones(
        [
            new VigenciaIdMarcador(Flores, AparatoAL, "14", new DateOnly(2026, 1, 1), null),
            new VigenciaIdMarcador(Quispe, AparatoAL, "0014", new DateOnly(2026, 1, 1), null)
        ]);

        var dia = new DateTime(2026, 7, 6, 7, 0, 0);

        atribucion.Resolver(AparatoAL, "14", dia).ShouldBe(Flores);
        atribucion.Resolver(AparatoAL, "0014", dia).ShouldBe(Quispe);
    }
}
