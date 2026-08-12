namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Sincronización manual del catálogo de feriados contra un proveedor externo.
/// </summary>
/// <remarks>
/// No es mantenimiento (alta/edición/baja manual): es sincronización bajo demanda, un botón
/// que trae los feriados nacionales del año y agrega los que falten. El catálogo
/// <c>Feriado</c> ya admite carga manual de feriados locales de planta desde antes; esta
/// sincronización nunca borra ni pisa lo que ya esté cargado, sea de acá o de un alta manual.
/// </remarks>
public interface IServicioFeriados
{
    /// <summary>Feriados cargados para un año, para revisar qué hay antes o después de sincronizar.</summary>
    Task<IReadOnlyList<FeriadoDto>> ListarAsync(int anio, CancellationToken cancelacion = default);

    /// <summary>
    /// Trae los feriados del proveedor externo para <paramref name="anio"/> e inserta
    /// <b>solo los que falten</b> (comparando por fecha). Nunca borra ni actualiza un
    /// feriado ya cargado.
    /// </summary>
    /// <exception cref="ControlMarcacion.Domain.Comun.ReglaNegocioException">
    /// Si no se pudo contactar al proveedor externo (sin internet, tiempo de espera
    /// agotado, respuesta inválida) o el año es inválido.
    /// </exception>
    Task<ResultadoSincronizacionFeriados> SincronizarAsync(
        int anio, string usuario, CancellationToken cancelacion = default);
}

/// <summary>Un feriado del catálogo local, con el nombre de planta ya resuelto si aplica.</summary>
public sealed record FeriadoDto(
    long Id,
    DateOnly Fecha,
    string Descripcion,
    long? PlantaId,
    string? PlantaNombre,
    bool Activo);

/// <summary>Resultado de sincronizar un año contra el proveedor externo.</summary>
public sealed record ResultadoSincronizacionFeriados(int Anio, int Agregados, int YaExistian);
