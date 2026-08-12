using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Organizacion;

/// <summary>
/// Centro de costo real de SAP: código y descripción tal como los administra
/// Intradevco. En el tareo final llena las columnas "Ceco" / "Descrip. Ceco".
/// </summary>
/// <remarks>
/// <see cref="Codigo"/> NO es único a nivel global: el mismo código de centro de
/// costo se repite bajo distintos <see cref="Centro"/> (verificado sobre el
/// catálogo real entregado por GCA). La unicidad, si acaso, es por la combinación
/// con <see cref="CentroId"/>.
/// </remarks>
public class CentroCosto : EntidadBase
{
    public long CentroId { get; set; }

    public Centro? Centro { get; set; }

    public string Codigo { get; set; } = null!;

    public string Descripcion { get; set; } = null!;
}
