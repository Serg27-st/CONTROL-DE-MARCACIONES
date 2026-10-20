using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.MarcacionesProvisionales;

/// <summary>
/// Registro de una carga de archivo en el módulo provisional.
/// </summary>
/// <remarks>
/// Módulo temporal — eliminar junto con las tablas <c>prov_*</c> cuando el motor
/// principal cubra este flujo.
/// </remarks>
public class ImportacionProvisional : EntidadBase
{
    public string NombreArchivo { get; set; } = null!;

    public long EquipoId { get; set; }

    public Catalogos.EquipoMarcador? Equipo { get; set; }

    public string HashContenido { get; set; } = null!;

    public DateTime FechaCarga { get; set; }

    public string CargadoPorUsuario { get; set; } = null!;

    public int FilasLeidas { get; set; }

    public int FilasInsertadas { get; set; }

    public int FilasRechazadas { get; set; }

    public ICollection<ErrorImportacionProvisional> Errores { get; set; } = [];
}
