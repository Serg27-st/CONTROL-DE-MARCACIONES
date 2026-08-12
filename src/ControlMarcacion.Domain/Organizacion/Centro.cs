using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Centro SAP: dimensión propia de costos de Intradevco, independiente de la
/// jerarquía Planta → Área → Sección. Cuelga de <see cref="Empresa"/>, igual que
/// <see cref="Planta"/>, pero no se corresponde 1 a 1 con ninguna planta sembrada
/// (5 centros SAP contra 4 plantas del sistema).
/// </summary>
/// <remarks>
/// En el tareo final, el código de este catálogo llena la columna "Centro" y
/// <see cref="Descripcion"/> la columna "Texto Centro".
/// </remarks>
public class Centro : EntidadBase
{
    public long EmpresaId { get; set; }

    public Empresa? Empresa { get; set; }

    public string Codigo { get; set; } = null!;

    /// <summary>
    /// Nombre del centro ("PLANTA INTRACLORO", "CENTRO LURÍN"…).
    /// </summary>
    /// <remarks>
    /// El archivo de SAP (<c>CENTROS DE COSTO POR EMPRESA.xlsx</c>) trae solo el código:
    /// estas descripciones las entregó GCA aparte. Es anulable porque un centro nuevo
    /// puede darse de alta antes de que alguien le ponga nombre — el tareo escribe
    /// "Texto Centro" vacío en ese caso, no un texto inventado.
    /// </remarks>
    public string? Descripcion { get; set; }

    public ICollection<CentroCosto> CentrosCosto { get; set; } = [];
}
