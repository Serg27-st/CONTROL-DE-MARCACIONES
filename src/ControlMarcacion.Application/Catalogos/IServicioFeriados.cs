using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Catálogo de feriados: sincronización con la fuente oficial y carga manual.
/// </summary>
/// <remarks>
/// Son dos caminos con propósitos distintos y ninguno reemplaza al otro:
/// <list type="bullet">
///   <item><b>Sincronizar</b> trae los feriados <i>nacionales</i> del año desde un
///   proveedor público y agrega los que falten. Es lo que evita teclear doce fechas a mano
///   cada enero.</item>
///   <item><b>Carga manual</b> cubre lo que ninguna fuente pública sabe: el aniversario de
///   una planta, un feriado regional, o un nacional decretado que el proveedor todavía no
///   publicó.</item>
/// </list>
///
/// El alta manual <b>existe desde aquí y solo desde aquí</b>. La documentación anterior
/// afirmaba que el catálogo se cargaba a mano "desde otras rutas del sistema" —el
/// controlador, esta interfaz y el estado vacío de la pantalla lo repetían—, y no era
/// cierto: no había ninguna. Un feriado local de planta no se podía registrar, y sin
/// registrar el motor trata ese día como laborable: falta para quien no fue, y sin el
/// 100 % para quien sí.
/// </remarks>
public interface IServicioFeriados
{
    /// <summary>Feriados cargados para un año, para revisar qué hay antes o después de sincronizar.</summary>
    Task<IReadOnlyList<FeriadoDto>> ListarAsync(int anio, CancellationToken cancelacion = default);

    /// <summary>Un feriado por su identificador, para el formulario de edición.</summary>
    Task<FeriadoDto?> ObtenerAsync(long id, CancellationToken cancelacion = default);

    /// <summary>
    /// Da de alta o modifica un feriado cargado a mano.
    /// </summary>
    /// <remarks>
    /// No toca ningún tareo ya calculado: el feriado entra en el cálculo en el
    /// <b>próximo recálculo</b> del periodo, igual que la sincronización.
    /// </remarks>
    /// <exception cref="Domain.Comun.ReglaNegocioException">
    /// Si la fecha o la descripción faltan, si ya hay un feriado con el mismo alcance en
    /// esa fecha, o si se intenta crear uno de planta sobre una fecha que ya es feriado
    /// nacional —ese día ya no se trabaja en ninguna planta, y la fila sobraría—.
    /// </exception>
    Task<long> GuardarAsync(
        GuardarFeriadoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Activa o desactiva un feriado. <b>Baja lógica</b>: nunca se borra la fila.
    /// </summary>
    /// <remarks>
    /// Un feriado desactivado deja de contar en el próximo recálculo, pero los tareos que
    /// ya lo aplicaron siguen explicándose: la fila que los sustenta sigue ahí. Borrarla
    /// dejaría un tareo con sobretasa del 100 % y nada que la justifique.
    /// </remarks>
    Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default);

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
    bool Activo)
{
    /// <summary>Aplica a todas las plantas. Es lo que trae la sincronización nacional.</summary>
    public bool EsNacional => PlantaId is null;
}

/// <summary>Alta y edición manual de un feriado.</summary>
public sealed record GuardarFeriadoRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "Indique la fecha del feriado.")]
    public DateOnly? Fecha { get; init; }

    [Required(ErrorMessage = "Escriba de qué feriado se trata.")]
    [StringLength(150, MinimumLength = 3,
        ErrorMessage = "La descripción debe tener entre 3 y 150 caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>
    /// Planta a la que aplica, o <c>null</c> si es nacional.
    /// </summary>
    /// <remarks>
    /// Es la diferencia entre "el 28 de julio no se trabaja en el país" y "el 12 de
    /// noviembre no se trabaja en Planta 01 porque es su aniversario". El motor ya
    /// distingue las dos: consulta los feriados con su <c>PlantaId</c>.
    /// </remarks>
    public long? PlantaId { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>Resultado de sincronizar un año contra el proveedor externo.</summary>
public sealed record ResultadoSincronizacionFeriados(int Anio, int Agregados, int YaExistian);
