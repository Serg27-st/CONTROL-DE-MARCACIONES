using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>
/// Modelo de la pantalla de listado de personal.
/// </summary>
public sealed class ListadoTrabajadoresVm
{
    public FiltroTrabajadores Filtro { get; init; } = new();

    public ResultadoPaginado<TrabajadorListaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<TrabajadorListaDto>(25);

    public IReadOnlyList<OpcionDto> Empresas { get; init; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; init; } = [];

    /// <summary>Centro SAP, para el filtro por Centro/Centro de costo (reemplaza el filtro anterior por Área/Sección).</summary>
    public IReadOnlyList<OpcionDto> Centros { get; init; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; init; } = [];
}

/// <summary>
/// Modelo del formulario de alta y edición.
/// </summary>
public sealed class EditarTrabajadorVm
{
    public GuardarTrabajadorRequest Datos { get; set; } = new();

    /// <summary>
    /// Centro SAP elegido en la cascada Planta → Centro → Centro de costo. No es
    /// parte de <see cref="GuardarTrabajadorRequest"/> porque no se guarda en
    /// <c>Trabajador</c>: solo existe para saber con qué centro precargar la lista
    /// de centros de costo, tanto al abrir el formulario como al volver a
    /// mostrarlo si la validación falla.
    /// </summary>
    public long? CentroId { get; set; }

    public bool EsNuevo => Datos.Id is null;

    public IReadOnlyList<OpcionDto> TiposDocumento { get; set; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Puestos { get; set; } = [];

    public IReadOnlyList<OpcionDto> Turnos { get; set; } = [];

    public IReadOnlyList<OpcionDto> Centros { get; set; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; set; } = [];

    /// <summary>
    /// Marcadores del catálogo disponibles para la planta elegida. El valor de cada
    /// opción es el código en texto y no una clave numérica: es lo que se guarda en la
    /// asignación y lo que cruza con el archivo del marcador.
    /// </summary>
    public IReadOnlyList<OpcionTextoDto> Marcadores { get; set; } = [];
}

/// <summary>
/// Modelo del panel flotante lateral de Personal: trae a la vez la ficha resuelta para
/// el modo "ver" (texto plano) y el formulario para el modo "editar" (los mismos
/// desplegables en cascada del formulario de página completa), para no tener que volver
/// a pedir datos al servidor cuando el usuario alterna entre los dos modos.
/// </summary>
public sealed class PanelTrabajadorVm
{
    /// <summary>"ver" o "editar". Cualquier otro valor se trata como "ver".</summary>
    public string Modo { get; set; } = "ver";

    public TrabajadorPanelDto Detalle { get; set; } = null!;

    public GuardarTrabajadorRequest Datos { get; set; } = new();

    /// <summary>Ver <see cref="EditarTrabajadorVm.CentroId"/>: mismo propósito, para el panel.</summary>
    public long? CentroId { get; set; }

    public IReadOnlyList<OpcionDto> TiposDocumento { get; set; } = [];

    public IReadOnlyList<OpcionDto> Empresas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];

    public IReadOnlyList<OpcionDto> Puestos { get; set; } = [];

    public IReadOnlyList<OpcionDto> Turnos { get; set; } = [];

    public IReadOnlyList<OpcionDto> Centros { get; set; } = [];

    public IReadOnlyList<OpcionDto> CentrosCosto { get; set; } = [];

    /// <summary>Ver <see cref="EditarTrabajadorVm.Marcadores"/>: mismo propósito, para el panel.</summary>
    public IReadOnlyList<OpcionTextoDto> Marcadores { get; set; } = [];

    public bool EsEdicion => string.Equals(Modo, "editar", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Modelo de la pantalla de historial de un trabajador.
/// </summary>
public sealed class HistorialTrabajadorVm
{
    public HistorialTrabajadorDto Historial { get; init; } = null!;

    /// <summary>
    /// Bitácora de auditoría de la fila del trabajador. Va junto al historial por rangos
    /// porque responden a la misma pregunta desde dos ángulos: el historial dice qué
    /// estaba vigente cuándo, y la bitácora quién lo cambió.
    /// </summary>
    public IReadOnlyList<RegistroAuditoriaDto> Auditoria { get; init; } = [];

    /// <summary>Solo el coordinador ve la bitácora. Ver <c>AuditoriaController</c>.</summary>
    public bool MuestraAuditoria { get; init; }
}

/// <summary>
/// Modelo del formulario de baja.
/// </summary>
public sealed class BajaTrabajadorVm
{
    public BajaTrabajadorRequest Datos { get; set; } = new();

    public string ApellidosYNombres { get; init; } = string.Empty;

    public string NumeroDocumento { get; init; } = string.Empty;

    public DateOnly FechaIngreso { get; init; }
}

/// <summary>
/// Modelo de la pantalla de carga masiva.
/// </summary>
public sealed class CargaMasivaVm
{
    /// <summary>Nulo mientras no se haya procesado ningún archivo en esta pantalla.</summary>
    public ResultadoCargaMasiva? Resultado { get; init; }
}

/// <summary>
/// Modelo de la página de error.
/// </summary>
public sealed class ErrorVm
{
    public string? IdSolicitud { get; init; }

    public bool MostrarIdSolicitud => !string.IsNullOrEmpty(IdSolicitud);
}
