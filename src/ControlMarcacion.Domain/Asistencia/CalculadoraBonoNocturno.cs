namespace ControlMarcacion.Domain.Asistencia;

/// <summary>
/// Bonificación por trabajo nocturno del D.S. 007-2002-TR, art. 8.
/// </summary>
/// <remarks>
/// El trabajador que labora en la franja nocturna (22:00–06:00) no puede percibir menos
/// que la <b>RMV incrementada con la sobretasa nocturna</b>. Es un <i>piso</i>, no un
/// adicional: si su sueldo básico ya supera ese mínimo, la ley está cumplida y no hay
/// bono que pagar.
///
/// Por qué vive aparte de <see cref="CalculadoraTareo"/> y no dentro:
/// <list type="number">
///   <item>Es un cálculo <b>aditivo</b>. No cambia cómo se reparten ordinarias y extras,
///   ni cuántos minutos caen en franja nocturna: eso ya lo resuelve el motor del tareo y
///   está validado. Meterlo ahí obligaría a tocar el cálculo que sustenta planillas ya
///   emitidas para agregar algo que no las afecta.</item>
///   <item>Es <b>dinero</b>, y el tareo son <b>minutos</b>. El resto del sistema entrega
///   minutos y porcentajes; este es uno de los pocos conceptos que GCA pidió resuelto en
///   soles, y conviene que se vea que lo es.</item>
///   <item>Su unidad natural es el <b>mes</b> (piso mensual repartido entre los días),
///   no el día trabajado. El motor del tareo calcula un día a la vez y no sabe cuántos
///   días tiene el mes ni cuánto gana el trabajador.</item>
/// </list>
///
/// Es una función <b>pura</b>: cuatro números entran, un número sale. No conoce al
/// trabajador, no sabe qué días tuvieron franja nocturna y no consulta nada. Quien
/// orquesta —la capa de reportes— resuelve la RMV, la sobretasa y el sueldo vigentes a
/// la fecha que se está liquidando y multiplica por los días que correspondan. Ese
/// reparto de responsabilidades es lo que permite recalcular un periodo de hace seis
/// meses con los valores de entonces.
/// </remarks>
public static class CalculadoraBonoNocturno
{
    /// <summary>
    /// Bono nocturno que corresponde a <b>un día</b> trabajado en franja nocturna.
    /// </summary>
    /// <param name="remuneracionMinimaVital">
    /// RMV vigente en la fecha liquidada, en soles. Sale de <c>ParametroLegal</c>, no de
    /// una constante: cambia por decreto y un recálculo antiguo debe usar la de entonces.
    /// </param>
    /// <param name="porcentajeSobretasaNocturna">
    /// Sobretasa nocturna vigente, expresada como porcentaje entero (35 para el 35 %).
    /// Sale de <c>TasaSobretasa</c> con el concepto <c>TrabajoNocturno</c>.
    /// </param>
    /// <param name="sueldoBasicoMensual">
    /// Sueldo básico mensual del trabajador vigente en la fecha liquidada, en soles.
    /// Sale de <c>AsignacionSueldo</c>.
    /// </param>
    /// <param name="diasDelMes">
    /// Días del mes que se liquida: 28, 29, 30 o 31 según el calendario real. El piso es
    /// mensual y se prorratea entre ellos; asumir 30 fijo pagaría de menos en enero y de
    /// más en febrero.
    /// </param>
    /// <returns>
    /// Importe diario en soles, redondeado a céntimos. Cero cuando el sueldo básico ya
    /// alcanza o supera el piso legal: <b>el bono nunca es negativo</b>, porque este
    /// concepto suma a la planilla y jamás descuenta de ella.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="diasDelMes"/> no es positivo. No es un caso de negocio sino un
    /// error de quien llama, y debe fallar a la vista en vez de devolver cero.
    /// </exception>
    /// <example>
    /// El caso que planteó GCA: RMV 1130, sobretasa 35 %, sueldo básico 1200, mes de 30
    /// días. El piso mensual es 1130 × 1,35 = 1525,50; la diferencia contra el sueldo es
    /// 1525,50 − 1200 = 325,50; y repartida entre los 30 días da <b>10,85 soles diarios</b>.
    /// </example>
    public static decimal BonoDiario(
        decimal remuneracionMinimaVital,
        decimal porcentajeSobretasaNocturna,
        decimal sueldoBasicoMensual,
        int diasDelMes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(diasDelMes, 0);

        var pisoMensual = remuneracionMinimaVital * (1 + porcentajeSobretasaNocturna / 100m);

        // El piso es un mínimo, no un adicional: sobre él no se paga nada.
        var diferencia = Math.Max(0, pisoMensual - sueldoBasicoMensual);

        // Se redondea al alza en el medio céntimo, a favor del trabajador. La regla queda
        // explícita porque el redondeo por omisión de .NET es el bancario (al par), y una
        // diferencia de un céntimo por día multiplicada por la planta es una discrepancia
        // que planilla detecta al cuadrar.
        return Math.Round(diferencia / diasDelMes, 2, MidpointRounding.AwayFromZero);
    }
}
