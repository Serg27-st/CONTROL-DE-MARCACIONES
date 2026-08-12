using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Normativa;

/// <summary>
/// Valor legal absoluto —un monto o un porcentaje suelto— <b>con vigencia por fecha</b>.
/// </summary>
/// <remarks>
/// Es el hermano de <see cref="TasaSobretasa"/> para lo que no es una sobretasa sobre
/// horas trabajadas. La RMV son 1130 soles, no un porcentaje aplicable a una jornada; la
/// asignación familiar es un 10 % <i>de la RMV</i>, no de un sueldo por hora; y la
/// movilidad diaria es un monto plano por día asistido.
///
/// Rige la misma regla que en <see cref="TasaSobretasa"/>: <b>son datos, nunca constantes
/// en el código</b>. La RMV peruana ha cambiado varias veces y volverá a cambiar. Si el
/// valor estuviera escrito en C#, recalcular un periodo cerrado del año pasado lo
/// liquidaría con la RMV de hoy y reescribiría una planilla ya pagada. Con vigencia por
/// fecha, subir la RMV es cerrar una fila e insertar otra.
///
/// No todo lo que vive aquí es de origen legal: la movilidad diaria es política interna
/// de GCA. Usa el mismo mecanismo a propósito, porque el problema es idéntico —cambia con
/// el tiempo y el pasado no debe moverse— y no amerita una segunda tabla que haga lo mismo.
/// </remarks>
public class ParametroLegal : EntidadBase
{
    public TipoParametroLegal Tipo { get; set; }

    /// <summary>
    /// Valor del parámetro. Soles para los montos (1130.0000) y porcentaje entero para
    /// los que son proporción (10.0000 para el 10 %); lo dice <see cref="Tipo"/>.
    /// </summary>
    public decimal Valor { get; set; }

    public DateOnly VigenteDesde { get; set; }

    /// <summary>Nulo mientras el valor siga vigente.</summary>
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Norma, decreto o política interna que sustenta el valor.</summary>
    public string? BaseLegal { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta.Value);
}

/// <summary>
/// Qué representa el valor de un <see cref="ParametroLegal"/>.
/// </summary>
public enum TipoParametroLegal
{
    /// <summary>
    /// Remuneración Mínima Vital, en soles. Base del bono nocturno del D.S. 007-2002-TR
    /// art. 8 y de la asignación familiar de la Ley 25129.
    /// </summary>
    RemuneracionMinimaVital = 1,

    /// <summary>
    /// Asignación familiar como porcentaje de la RMV. Ley 25129: 10 % mensual para el
    /// trabajador con hijos menores de edad, o mayores cursando estudios superiores.
    /// Es un monto fijo mensual, no se prorratea por días asistidos.
    /// </summary>
    PorcentajeAsignacionFamiliar = 2,

    /// <summary>
    /// Movilidad por día efectivamente asistido, en soles. No es un concepto legal sino
    /// política interna de GCA: reembolso de traslado, así que solo corresponde por los
    /// días en que el trabajador se trasladó de verdad.
    /// </summary>
    ValorMovilidadDiaria = 3
}
