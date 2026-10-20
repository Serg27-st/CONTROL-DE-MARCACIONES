using ControlMarcacion.Application.MarcacionesProvisionales.Dto;

namespace ControlMarcacion.Application.MarcacionesProvisionales;

// ──────────────────────────────────────────────────────────────────────────────
// Módulo provisional — eliminar cuando el motor principal cubra este flujo.
// ──────────────────────────────────────────────────────────────────────────────

public interface IServicioImportacionProvisional
{
    /// <summary>
    /// Carga un archivo del marcador en las tablas provisionales.
    /// </summary>
    Task<ResultadoCargaProvisional> ImportarAsync(
        Stream contenido,
        string nombreArchivo,
        long equipoId,
        string usuario);

    /// <summary>
    /// Devuelve el historial de importaciones provisionales del equipo indicado,
    /// o de todos si <paramref name="equipoId"/> es nulo.
    /// </summary>
    Task<IReadOnlyList<ImportacionProvisionalListaDto>> ListarImportacionesAsync(
        long? equipoId = null);
}
