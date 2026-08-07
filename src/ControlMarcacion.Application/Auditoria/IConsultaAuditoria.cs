using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Auditoria;

/// <summary>
/// Lectura de la bitácora de auditoría.
/// </summary>
/// <remarks>
/// Solo lectura, a propósito: no hay servicio de escritura porque nadie escribe en la
/// bitácora desde un caso de uso. Las filas las pone el interceptor de EF Core al
/// guardar, y nadie las edita ni las borra.
/// </remarks>
public interface IConsultaAuditoria
{
    Task<ResultadoPaginado<RegistroAuditoriaDto>> ListarAsync(
        FiltroAuditoria filtro,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Bitácora de un registro concreto, del más reciente al más antiguo. La usa la
    /// pantalla de historial del trabajador.
    /// </summary>
    Task<IReadOnlyList<RegistroAuditoriaDto>> DeRegistroAsync(
        string tabla,
        string claveRegistro,
        int maximo = 100,
        CancellationToken cancelacion = default);

    /// <summary>Tablas que ya tienen movimientos, para poblar el desplegable del filtro.</summary>
    Task<IReadOnlyList<string>> TablasAsync(CancellationToken cancelacion = default);
}
