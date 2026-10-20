using ControlMarcacion.Domain.Normativa;

namespace ControlMarcacion.Application.Asistencia;

/// <summary>
/// Minutos acumulados de un concepto con su sobretasa vigente aplicada.
/// </summary>
/// <remarks>
/// El sistema <b>no calcula dinero</b>: no conoce la remuneración de nadie y no debe
/// conocerla. Lo que entrega es la base auditable para que planilla multiplique — los
/// minutos por concepto y la tasa que regía en esas fechas, con su base legal.
///
/// <see cref="MinutosEquivalentes"/> es el aporte de este concepto al total valorizado,
/// expresado en tiempo. Para un tramo físico exclusivo incluye base y recargo: 120 minutos
/// de HE al 25 % equivalen a 150 minutos ordinarios. Para una dimensión superpuesta como
/// nocturnidad aporta solo la prima: su base física ya está en ordinario/HE/HE100.
/// </remarks>
public sealed record ConceptoLiquidado(
    ConceptoSobretasa Concepto,
    string Etiqueta,
    int Minutos,
    decimal Porcentaje,
    string? BaseLegal,
    bool TieneTasaVigente)
{
    /// <summary>
    /// Base económica expresada en minutos, sin crear minutos físicos adicionales.
    /// </summary>
    /// <remarks>
    /// Para la nocturnidad la base ya pertenece a otro tramo físico y por eso aquí es cero.
    /// Para feriado trabajado conserva exactamente los minutos efectivamente laborados.
    /// </remarks>
    public decimal MinutosBaseEconomica =>
        Concepto == ConceptoSobretasa.TrabajoNocturno ? 0m : Minutos;

    /// <summary>Recargo económico expresado como minutos equivalentes.</summary>
    public decimal MinutosRecargo => Math.Round(Minutos * Porcentaje / 100m, 2);

    /// <summary>Minutos que este concepto aporta al equivalente de jornada ordinaria.</summary>
    public decimal MinutosEquivalentes => MinutosBaseEconomica + MinutosRecargo;

    public decimal HorasEquivalentes => Math.Round(MinutosEquivalentes / 60m, 2);

    public decimal Horas => Math.Round(Minutos / 60m, 2);
}

/// <summary>
/// Derecho a una remuneración diaria ordinaria que no representa tiempo físicamente
/// trabajado. Se mantiene separado de <see cref="ConceptoLiquidado"/> para que un feriado
/// pagado o la coincidencia especial del 1 de mayo no inventen ni dupliquen minutos.
/// </summary>
public sealed record ComponenteRemuneracionDiaria(
    TipoComponenteRemuneracionDiaria Tipo,
    string Etiqueta,
    int CantidadDias,
    bool IncluidoEnRemuneracionBase,
    string BaseLegal);

public enum TipoComponenteRemuneracionDiaria
{
    RemuneracionOrdinariaFeriado = 1,
    RemuneracionDescansoSemanal = 2,
    RemuneracionAdicionalPrimeroDeMayo = 3
}

/// <summary>
/// Liquidación de un periodo: minutos por concepto con la tasa vigente a esas fechas.
/// </summary>
public sealed record LiquidacionPeriodo(
    long PeriodoId,
    string PeriodoCodigo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int MinutosOrdinarios,
    IReadOnlyList<ConceptoLiquidado> Conceptos,
    IReadOnlyList<ComponenteRemuneracionDiaria> ComponentesRemuneracionDiaria)
{
    /// <summary>
    /// Conceptos con minutos acumulados pero sin ninguna tasa vigente en el periodo.
    /// </summary>
    /// <remarks>
    /// Es un aviso, no un detalle: significa que hay trabajo que no se puede valorizar
    /// porque falta cargar la tasa. Callarlo produciría una planilla corta sin que nadie
    /// se entere.
    /// </remarks>
    public IReadOnlyList<ConceptoLiquidado> SinTasa =>
        [.. Conceptos.Where(c => !c.TieneTasaVigente && c.Minutos > 0)];

    public bool HayConceptosSinTasa => SinTasa.Count > 0;

    public decimal HorasOrdinarias => Math.Round(MinutosOrdinarios / 60m, 2);

    public IReadOnlyList<ComponenteRemuneracionDiaria> ComponentesDiariosConDerecho =>
        [.. ComponentesRemuneracionDiaria.Where(c => c.CantidadDias > 0)];

    public bool HayComponentesRemuneracionDiaria => ComponentesDiariosConDerecho.Count > 0;

    /// <summary>
    /// Total en horas equivalentes de jornada ordinaria, incluidas las ordinarias.
    /// No incluye los derechos expresados en días de <see cref="ComponentesRemuneracionDiaria"/>:
    /// convertirlos a horas requeriría inventar una jornada en un día de descanso.
    /// </summary>
    public decimal TotalHorasEquivalentes =>
        Math.Round(HorasOrdinarias + Conceptos.Sum(c => c.HorasEquivalentes), 2);
}
