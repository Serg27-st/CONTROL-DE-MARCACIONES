using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Tipo de documento de identidad: DNI, carné de extranjería, pasaporte, PTP.
/// </summary>
public class TipoDocumento : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>Longitud exacta esperada, si aplica (DNI = 8). Nulo si es variable.</summary>
    public int? LongitudExacta { get; set; }

    /// <summary>El número admite solo dígitos.</summary>
    public bool SoloDigitos { get; set; }
}

/// <summary>
/// Puesto o cargo que ocupa el trabajador. Es una de las columnas del tareo final.
/// </summary>
public class Puesto : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;
}

/// <summary>
/// Día no laborable a nivel nacional o de planta.
/// </summary>
/// <remarks>
/// Necesario para el cálculo: trabajar un feriado sin descanso sustitutorio genera
/// una sobretasa del 100 %. Se modela con planta opcional para admitir feriados
/// locales o paradas de planta además de los nacionales.
/// </remarks>
public class Feriado : EntidadBase
{
    public DateOnly Fecha { get; set; }

    public string Descripcion { get; set; } = null!;

    /// <summary>Nulo si el feriado aplica a todas las plantas.</summary>
    public long? PlantaId { get; set; }
}
