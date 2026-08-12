using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Normativa;

/// <summary>
/// Tasa de sobretasa aplicable a un concepto, <b>con vigencia por fecha</b>.
/// </summary>
/// <remarks>
/// Estas tasas son <b>datos, nunca constantes en el código</b>. Si en alguna parte del
/// sistema aparece un <c>* 1.25</c> escrito a mano, está mal.
///
/// Dos razones concretas:
/// <list type="number">
///   <item>Recalcular un periodo de hace seis meses debe aplicar las tasas vigentes
///   <i>entonces</i>, no las de hoy. Sin vigencia por fecha, un recálculo reescribiría
///   la historia de una planilla ya pagada.</item>
///   <item>Un convenio colectivo o una política interna de GCA puede mejorar los mínimos
///   legales. Eso es un UPDATE, no un despliegue.</item>
/// </list>
///
/// Base normativa: D.S. 007-2002-TR (TUO de la Ley de Jornada de Trabajo) y D.S. 008-2002-TR.
/// Los valores cargados como semilla son los <b>mínimos legales</b>; deben validarse contra
/// el convenio vigente de GCA.
/// </remarks>
public class TasaSobretasa : EntidadBase
{
    public ConceptoSobretasa Concepto { get; set; }

    /// <summary>Porcentaje de sobretasa. Por ejemplo 25.0000 para el 25 %.</summary>
    public decimal Porcentaje { get; set; }

    public DateOnly VigenteDesde { get; set; }

    /// <summary>Nulo mientras la tasa siga vigente.</summary>
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Referencia normativa o del convenio que sustenta la tasa.</summary>
    public string? BaseLegal { get; set; }

    /// <summary>Empresa a la que aplica. Nulo si aplica a todas.</summary>
    public long? EmpresaId { get; set; }

    public bool EstaVigenteEn(DateOnly fecha) =>
        fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta.Value);
}

public enum ConceptoSobretasa
{
    /// <summary>Horas extras, las 2 primeras del día. Mínimo legal: +25 %.</summary>
    HoraExtraPrimerasDos = 1,

    /// <summary>Horas extras, a partir de la tercera. Mínimo legal: +35 %.</summary>
    HoraExtraAdicionales = 2,

    /// <summary>Trabajo nocturno entre 22:00 y 06:00. Mínimo legal: +35 % sobre la RMV.</summary>
    TrabajoNocturno = 3,

    /// <summary>Descanso semanal trabajado sin sustitutorio. Mínimo legal: +100 %.</summary>
    DescansoSemanalTrabajado = 4,

    /// <summary>Feriado trabajado sin descanso sustitutorio. Mínimo legal: +100 %.</summary>
    FeriadoTrabajado = 5
}
