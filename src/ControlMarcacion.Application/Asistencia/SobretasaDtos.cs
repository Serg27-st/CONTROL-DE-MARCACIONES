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
/// <see cref="MinutosEquivalentes"/> es esa aplicación expresada en tiempo: 120 minutos
/// al 25 % equivalen a 150 minutos de jornada ordinaria. Es la forma habitual de entregar
/// sobretiempo a planilla sin que este sistema tenga que tocar sueldos.
/// </remarks>
public sealed record ConceptoLiquidado(
    ConceptoSobretasa Concepto,
    string Etiqueta,
    int Minutos,
    decimal Porcentaje,
    string? BaseLegal,
    bool TieneTasaVigente)
{
    /// <summary>Minutos convertidos a equivalente de jornada ordinaria.</summary>
    public decimal MinutosEquivalentes =>
        Math.Round(Minutos * (1 + (Porcentaje / 100m)), 2);

    public decimal HorasEquivalentes => Math.Round(MinutosEquivalentes / 60m, 2);

    public decimal Horas => Math.Round(Minutos / 60m, 2);
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
    IReadOnlyList<ConceptoLiquidado> Conceptos)
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

    /// <summary>Total en horas equivalentes de jornada ordinaria, incluidas las ordinarias.</summary>
    public decimal TotalHorasEquivalentes =>
        Math.Round(HorasOrdinarias + Conceptos.Sum(c => c.HorasEquivalentes), 2);
}
