using ControlMarcacion.Domain.Asistencia;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Pruebas del bono nocturno del D.S. 007-2002-TR, art. 8.
/// </summary>
/// <remarks>
/// El cálculo es una función pura sobre cuatro números: no conoce al trabajador, ni qué
/// días fueron nocturnos, ni la base de datos. Por eso todos los casos se prueban con
/// literales y sin fixture.
///
/// Ni la RMV, ni el porcentaje, ni los días del mes viven en el código: entran como
/// parámetro. Varios de estos tests existen justamente para que el día que alguien
/// escriba un <c>1130</c>, un <c>1.35</c> o un <c>30</c> dentro del método, la suite lo
/// delate.
/// </remarks>
public class CalculadoraBonoNocturnoTests
{
    /// <summary>RMV vigente desde 2025-01-01 (D.S. 006-2024-TR), como está sembrada.</summary>
    private const decimal Rmv = 1130m;

    /// <summary>Sobretasa nocturna sembrada como <c>TasaSobretasa</c> Id 3.</summary>
    private const decimal SobretasaNocturna = 35m;

    /// <summary>Piso mensual del trabajador nocturno: 1130 × 1.35.</summary>
    private const decimal PisoMensual = 1525.50m;

    // ------------------------------------------------------------------
    // El ejemplo literal que dio GCA
    // ------------------------------------------------------------------

    [Fact]
    public void Ejemplo_de_GCA_sueldo_1200_en_un_mes_de_30_dias()
    {
        // "1130 * 1.35 = 1525.5 ... 1525.5 - 1200 = 325.5 su bono nocturno por 30 días".
        // 325.50 / 30 = 10.85 exacto, sin redondeo de por medio.
        var bono = CalculadoraBonoNocturno.BonoDiario(
            remuneracionMinimaVital: Rmv,
            porcentajeSobretasaNocturna: SobretasaNocturna,
            sueldoBasicoMensual: 1200m,
            diasDelMes: 30);

        bono.ShouldBe(10.85m);
    }

    [Fact]
    public void El_piso_mensual_es_la_RMV_mas_la_sobretasa()
    {
        // Sueldo cero: el bono cubre el piso entero. 1525.50 / 30 = 50.85.
        // Deja a la vista de qué piso se está hablando, sin tener que deducirlo.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 0m, 30);

        bono.ShouldBe(50.85m);
        (bono * 30).ShouldBe(PisoMensual);
    }

    // ------------------------------------------------------------------
    // El bono nunca es negativo
    // ------------------------------------------------------------------

    [Fact]
    public void Sueldo_por_encima_del_piso_no_genera_bono()
    {
        // 2000 supera los 1525.50 del piso: la ley ya está cumplida por el propio sueldo.
        // Sin el Math.Max esto devolvería un número negativo y planilla descontaría dinero.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 2000m, 30);

        bono.ShouldBe(0m);
    }

    [Fact]
    public void Sueldo_exactamente_igual_al_piso_no_genera_bono()
    {
        // Caso límite: el sueldo iguala 1130 × 1.35. La diferencia es cero, no un céntimo.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, PisoMensual, 30);

        bono.ShouldBe(0m);
    }

    [Fact]
    public void Un_centimo_por_debajo_del_piso_ya_genera_bono()
    {
        // La frontera es estricta: 1525.49 sí está por debajo del mínimo legal.
        // 0.01 / 30 = 0.000333..., que redondeado a céntimos da 0.00 — el bono existe
        // conceptualmente, pero a nivel de céntimos no hay nada que pagar.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1525.49m, 30);

        bono.ShouldBe(0m);
    }

    // ------------------------------------------------------------------
    // Días del mes: reales, nunca un 30 cableado
    // ------------------------------------------------------------------

    /// <summary>
    /// Días del mes y bono esperado, para el sueldo de 1200 del ejemplo de GCA.
    /// </summary>
    /// <remarks>
    /// Se usa <see cref="TheoryData{T1, T2}"/> y no <c>InlineData</c> porque los literales
    /// de un atributo no pueden ser <c>decimal</c>: entrarían como <c>double</c> y se
    /// convertirían por detrás, que es justo el tipo que este sistema no usa para dinero.
    /// </remarks>
    public static TheoryData<int, decimal> DiasDelMesYBono() => new()
    {
        { 28, 11.63m },   // febrero no bisiesto: 325.50 / 28 = 11.625 → 11.63
        { 29, 11.22m },   // febrero bisiesto:    325.50 / 29 = 11.2241... → 11.22
        { 30, 10.85m },   // el caso de GCA:      325.50 / 30 = 10.85 exacto
        { 31, 10.50m }    // mes largo:           325.50 / 31 = 10.50 exacto
    };

    [Theory]
    [MemberData(nameof(DiasDelMesYBono))]
    public void El_bono_se_reparte_entre_los_dias_reales_del_mes(int diasDelMes, decimal esperado)
    {
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, diasDelMes);

        bono.ShouldBe(esperado);
    }

    [Fact]
    public void El_medio_centimo_se_redondea_a_favor_del_trabajador()
    {
        // 325.50 / 28 = 11.625 clavado: es un punto medio.
        // El redondeo bancario por omisión de .NET daría 11.62; aquí se paga 11.63.
        // La regla está fijada a propósito con MidpointRounding.AwayFromZero.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, 28);

        bono.ShouldBe(11.63m);
        bono.ShouldNotBe(11.62m);
    }

    [Fact]
    public void El_resultado_no_arrastra_mas_de_dos_decimales()
    {
        // 325.50 / 29 = 11.224137931... Se corta en céntimos porque es dinero:
        // planilla no puede recibir un número con veinte decimales.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, 29);

        bono.ShouldBe(11.22m);
        decimal.Round(bono, 2).ShouldBe(bono);
    }

    // ------------------------------------------------------------------
    // Ni la RMV ni la sobretasa están en el código
    // ------------------------------------------------------------------

    [Fact]
    public void Una_RMV_distinta_mueve_el_piso()
    {
        // Si el Estado sube la RMV a 1200, el piso pasa a 1620 y el mismo sueldo de 1200
        // genera 420 de diferencia: 420 / 30 = 14.00.
        // Recalcular un periodo antiguo debe usar la RMV de entonces, y por eso entra
        // como parámetro en vez de ser una constante.
        var bono = CalculadoraBonoNocturno.BonoDiario(1200m, SobretasaNocturna, 1200m, 30);

        bono.ShouldBe(14.00m);
    }

    [Fact]
    public void Una_sobretasa_distinta_mueve_el_piso()
    {
        // Con 30 % el piso baja a 1469: 1469 - 1200 = 269, y 269 / 30 = 8.9666... → 8.97.
        // El porcentaje sale de TasaSobretasa con vigencia por fecha; nunca de un * 1.35.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, 30m, 1200m, 30);

        bono.ShouldBe(8.97m);
    }

    [Fact]
    public void Sobretasa_cero_deja_el_piso_en_la_RMV_pelada()
    {
        // Sin sobretasa el piso es la RMV: 1130 - 1000 = 130, y 130 / 30 = 4.3333... → 4.33.
        var bono = CalculadoraBonoNocturno.BonoDiario(Rmv, 0m, 1000m, 30);

        bono.ShouldBe(4.33m);
    }

    // ------------------------------------------------------------------
    // Entradas imposibles
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-30)]
    public void Dias_del_mes_no_positivos_es_un_error_explicito(int diasDelMes)
    {
        // Dividir entre cero o entre un negativo no es un caso de negocio: es un bug de
        // quien llama. Falla fuerte y con el nombre del parámetro, no devolviendo cero.
        var error = Should.Throw<ArgumentOutOfRangeException>(() =>
            CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, diasDelMes));

        error.ParamName.ShouldBe("diasDelMes");
    }

    // ------------------------------------------------------------------
    // Función pura
    // ------------------------------------------------------------------

    [Fact]
    public void Misma_entrada_mismo_resultado()
    {
        // Sin reloj ni estado: recalcular el mismo periodo dos veces da lo mismo.
        var primero = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, 30);
        var segundo = CalculadoraBonoNocturno.BonoDiario(Rmv, SobretasaNocturna, 1200m, 30);

        segundo.ShouldBe(primero);
    }
}
