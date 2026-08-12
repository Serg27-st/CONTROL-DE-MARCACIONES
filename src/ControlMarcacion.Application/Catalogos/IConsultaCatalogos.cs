using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Application.Catalogos;

/// <summary>
/// Listas de catálogo para poblar los desplegables de los formularios.
/// </summary>
public interface IConsultaCatalogos
{
    /// <summary>
    /// Tipos de documento activos, con la longitud y el formato que admite cada uno.
    /// </summary>
    Task<IReadOnlyList<TipoDocumentoOpcionDto>> TiposDocumentoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Reglas de formato de un tipo de documento. Nulo si el tipo no existe o está inactivo.
    /// </summary>
    /// <remarks>
    /// La usan los servicios que dan de alta personas —Personal, Usuarios y la carga
    /// masiva— para validar el número contra el catálogo antes de guardarlo, sin tocar
    /// la tabla directamente.
    /// </remarks>
    Task<TipoDocumentoOpcionDto?> TipoDocumentoPorIdAsync(
        long id,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> EmpresasAsync(CancellationToken cancelacion = default);

    /// <summary>Plantas de una empresa. Sin empresa, devuelve todas.</summary>
    Task<IReadOnlyList<OpcionDto>> PlantasAsync(long? empresaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Empresa dueña de una planta, para resolver del lado del servidor la cascada
    /// Planta → Centro sin pedirle al cliente que recuerde la empresa. Nulo si la
    /// planta no existe o está inactiva.
    /// </summary>
    Task<long?> EmpresaDePlantaAsync(long plantaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> AreasAsync(long? plantaId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<OpcionDto>> SeccionesAsync(long? areaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Centros SAP de una empresa (catálogo Centro → Centro de costo, ajeno a la
    /// jerarquía Planta/Área/Sección). Sin empresa, devuelve todos.
    /// </summary>
    Task<IReadOnlyList<OpcionDto>> CentrosAsync(long? empresaId, CancellationToken cancelacion = default);

    /// <summary>Centros de costo de un centro SAP. Sin centro, devuelve todos.</summary>
    Task<IReadOnlyList<OpcionDto>> CentrosCostoAsync(long? centroId, CancellationToken cancelacion = default);

    /// <summary>
    /// Puestos disponibles. Si se indica la empresa, se incluyen los universales
    /// (<c>EmpresaId</c> nulo) más los propios de esa empresa. Sin empresa,
    /// devuelve todos.
    /// </summary>
    Task<IReadOnlyList<OpcionDto>> PuestosAsync(long? empresaId = null, CancellationToken cancelacion = default);

    /// <summary>
    /// Turnos disponibles. Si se indica la planta, se excluyen los que no están
    /// habilitados en ella: el turno especial de sábado solo existe en Planta 01.
    /// </summary>
    Task<IReadOnlyList<OpcionDto>> TurnosAsync(long? plantaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Marcadores activos de una planta, para el desplegable de ID de marcador. Sin
    /// planta, devuelve todos.
    /// </summary>
    /// <param name="incluirCodigo">
    /// Código que debe aparecer en la lista aunque esté inactivo o pertenezca a otra
    /// planta. Se usa al editar: si el trabajador ya tenía asignado un ID que hoy no
    /// cumple el filtro, omitirlo haría que el desplegable se abriera vacío y que
    /// guardar sin tocar nada le borrara la asignación en silencio.
    /// </param>
    Task<IReadOnlyList<OpcionTextoDto>> MarcadoresAsync(
        long? plantaId,
        string? incluirCodigo = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Ficha de catálogo de un código de marcador. Nulo si el código no está registrado.
    /// </summary>
    /// <remarks>
    /// La usa Personal para comprobar que el ID que se le asigna a un trabajador existe
    /// y pertenece a su planta. Personal consulta el catálogo por este contrato público
    /// y no por la tabla: es la regla de este repositorio para hablar entre módulos.
    /// </remarks>
    Task<MarcadorCatalogoDto?> MarcadorPorCodigoAsync(
        string codigo,
        CancellationToken cancelacion = default);
}
