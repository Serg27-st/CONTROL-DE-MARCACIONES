using ControlMarcacion.Domain.Marcaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Marcaciones;

/// <summary>
/// Si un día tiene cargado lo que debía, aparato por aparato.
/// </summary>
/// <remarks>
/// El caso que la motiva: Planta Detergentes con un aparato A-L y otro M-Z. Se cargó el
/// archivo del A-L y se olvidó el del M-Z; el día tiene marcaciones y, contado en total,
/// parece cubierto. La mitad de la planta saldría en falta al calcular.
/// </remarks>
public class CoberturaDeCargaTests
{
    private const long AL = 1;
    private const long MZ = 2;

    private static readonly DateOnly Hoy = new(2026, 7, 20);
    private static readonly DateOnly Dia = new(2026, 7, 6);

    private static readonly AparatoEsperado[] DosAparatos =
    [
        new(AL, Activo: true, Desde: new DateOnly(2026, 7, 1)),
        new(MZ, Activo: true, Desde: new DateOnly(2026, 7, 1))
    ];

    [Fact]
    public void Con_los_dos_aparatos_cargados_el_dia_esta_completo() =>
        CoberturaDeCarga.Evaluar(Dia, Hoy, DosAparatos, [AL, MZ]).ShouldBe(EstadoCobertura.Completo);

    [Fact]
    public void Con_uno_solo_el_dia_esta_incompleto_y_dice_cual_falta()
    {
        CoberturaDeCarga.Evaluar(Dia, Hoy, DosAparatos, [AL]).ShouldBe(EstadoCobertura.Incompleto);
        CoberturaDeCarga.Faltantes(Dia, Hoy, DosAparatos, [AL]).ShouldBe([MZ]);
    }

    [Fact]
    public void Sin_ninguna_marcacion_el_dia_esta_vacio_y_faltan_los_dos()
    {
        CoberturaDeCarga.Evaluar(Dia, Hoy, DosAparatos, []).ShouldBe(EstadoCobertura.SinMarcaciones);
        CoberturaDeCarga.Faltantes(Dia, Hoy, DosAparatos, []).ShouldBe([AL, MZ], ignoreOrder: true);
    }

    /// <summary>Un día que todavía no ocurre no puede tener nada faltante.</summary>
    [Fact]
    public void Un_dia_futuro_no_es_hueco()
    {
        var manana = Hoy.AddDays(1);

        CoberturaDeCarga.Evaluar(manana, Hoy, DosAparatos, []).ShouldBe(EstadoCobertura.Futuro);
        CoberturaDeCarga.Faltantes(manana, Hoy, DosAparatos, []).ShouldBeEmpty();
    }

    /// <summary>
    /// Antes de su primera marcación el aparato no se usaba: esos días no le faltan.
    /// </summary>
    [Fact]
    public void Un_aparato_no_se_espera_antes_de_que_empezara_a_usarse()
    {
        AparatoEsperado[] aparatos =
        [
            new(AL, Activo: true, Desde: new DateOnly(2026, 7, 1)),
            new(MZ, Activo: true, Desde: new DateOnly(2026, 7, 10))
        ];

        CoberturaDeCarga.Evaluar(Dia, Hoy, aparatos, [AL]).ShouldBe(EstadoCobertura.Completo);
        CoberturaDeCarga.Evaluar(new DateOnly(2026, 7, 10), Hoy, aparatos, [AL])
            .ShouldBe(EstadoCobertura.Incompleto);
    }

    [Fact]
    public void Un_aparato_dado_de_baja_no_se_espera_nunca()
    {
        AparatoEsperado[] aparatos =
        [
            new(AL, Activo: true, Desde: new DateOnly(2026, 7, 1)),
            new(MZ, Activo: false, Desde: new DateOnly(2026, 7, 1))
        ];

        CoberturaDeCarga.Evaluar(Dia, Hoy, aparatos, [AL]).ShouldBe(EstadoCobertura.Completo);
    }

    /// <summary>
    /// El primer archivo trae toda la memoria del equipo: los días anteriores a su primera
    /// marcación no son un archivo olvidado.
    /// </summary>
    [Fact]
    public void Se_espera_desde_la_primera_marcacion_aunque_se_diera_de_alta_antes() =>
        AparatoEsperado.Para(MZ, activo: true, primeraMarcacion: new DateOnly(2026, 7, 10), alta: new DateOnly(2026, 6, 1))
            .Desde.ShouldBe(new DateOnly(2026, 7, 10));

    /// <summary>
    /// Un aparato registrado del que nunca se cargó nada es justo el olvido que hay que
    /// mostrar: se espera desde su alta.
    /// </summary>
    [Fact]
    public void Sin_ninguna_marcacion_se_espera_desde_su_alta() =>
        AparatoEsperado.Para(MZ, activo: true, primeraMarcacion: null, alta: new DateOnly(2026, 7, 1))
            .Desde.ShouldBe(new DateOnly(2026, 7, 1));
}
