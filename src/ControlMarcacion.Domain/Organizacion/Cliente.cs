using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Cliente al que GCA presta el servicio. Hoy el único es Intradevco.
/// </summary>
/// <remarks>
/// Se modela desde el inicio aunque haya un solo cliente: el costo es marginal y
/// evita una migración dolorosa si GCA incorpora otro. Ver ADR-001.
/// </remarks>
public class Cliente : EntidadBase
{
    public string Codigo { get; set; } = null!;

    public string RazonSocial { get; set; } = null!;

    public string? Ruc { get; set; }

    public ICollection<Empresa> Empresas { get; set; } = [];
}
