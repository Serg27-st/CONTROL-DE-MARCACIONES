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

    /// <summary>
    /// Longitud máxima admitida cuando no hay una exacta. Nulo si no hay tope propio.
    /// </summary>
    /// <remarks>
    /// Se separa de <see cref="LongitudExacta"/> porque son reglas distintas: el DNI
    /// tiene ocho dígitos siempre —siete no es un DNI corto, es un DNI mal escrito—,
    /// mientras que un carné de extranjería o un pasaporte varían de país a país y solo
    /// admiten un techo. Las dos son <b>dato del catálogo</b>, no constantes: si mañana
    /// migraciones amplía el carné a doce caracteres, es un UPDATE y no un despliegue.
    /// </remarks>
    public int? LongitudMaxima { get; set; }

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

    /// <summary>
    /// Empresa a la que pertenece este puesto. Nulo si aplica a cualquier empresa
    /// — mismo patrón que <see cref="Feriado.PlantaId"/> y
    /// <c>TasaSobretasa.EmpresaId</c>. Los 4 puestos sembrados (OPER, SUPV, AUX,
    /// ANAL) quedan universales; si GCA entrega puestos propios de una empresa se
    /// agregan como filas nuevas con este campo asignado.
    /// </summary>
    public long? EmpresaId { get; set; }
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
