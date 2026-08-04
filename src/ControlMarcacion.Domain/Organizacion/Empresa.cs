using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Razón social empleadora. En GCA hoy existen dos: Consulting Advisers y CA Outsourcing.
/// </summary>
/// <remarks>
/// La empresa NO es una etiqueta decorativa: son personas jurídicas distintas, con
/// planillas separadas. Toda consulta de personal, marcaciones y tareos debe filtrar
/// por empresa. Un listado que mezcla trabajadores de ambas razones sociales es un defecto.
/// </remarks>
public class Empresa : EntidadBase
{
    /// <summary>Cliente al que presta servicio esta razón social (hoy: Intradevco).</summary>
    public long ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public string Codigo { get; set; } = null!;

    public string RazonSocial { get; set; } = null!;

    /// <summary>RUC de 11 dígitos. Texto, no numérico.</summary>
    public string Ruc { get; set; } = null!;

    public ICollection<Planta> Plantas { get; set; } = [];
}
