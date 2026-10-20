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
    /// Cuadrillas activas de todas las plantas, para el desplegable de la ficha. Son pocas:
    /// la ficha las agrupa por planta y el servidor comprueba que coincida con la del
    /// trabajador.
    /// </summary>
    Task<IReadOnlyList<CuadrillaOpcionDto>> CuadrillasActivasAsync(CancellationToken cancelacion = default);

    /// <summary>Una cuadrilla, activa o no. Nulo si no existe.</summary>
    Task<CuadrillaOpcionDto?> CuadrillaPorIdAsync(long id, CancellationToken cancelacion = default);

    /// <summary>Equipos activos, para los desplegables de carga y de personal.</summary>
    /// <remarks>
    /// Devuelve todos con su planta dentro: son un puñado de aparatos y la pantalla de
    /// carga filtra por planta del lado del cliente, sin otra vuelta al servidor.
    /// </remarks>
    Task<IReadOnlyList<EquipoOpcionDto>> EquiposActivosAsync(
        CancellationToken cancelacion = default);

    /// <summary>
    /// Marcadores de una planta que llevan ese código.
    /// </summary>
    /// <remarks>
    /// La usa Personal para comprobar que el ID que se le asigna a un trabajador existe y
    /// es de su planta. Personal consulta el catálogo por este contrato público y no por
    /// la tabla: es la regla de este repositorio para hablar entre módulos.
    ///
    /// Devuelve una <b>lista</b> y no una ficha porque el código dejó de ser único: con dos
    /// aparatos en la misma planta, "14" existe en los dos y son dos personas distintas.
    /// Quien llama decide — con uno solo no hay nada que preguntar, con varios hay que
    /// exigir el equipo en vez de elegir por él.
    /// </remarks>
    Task<IReadOnlyList<MarcadorCatalogoDto>> MarcadoresPorCodigoAsync(
        string codigo,
        long plantaId,
        CancellationToken cancelacion = default);
}
