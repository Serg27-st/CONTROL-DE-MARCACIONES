using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de solicitudes.</summary>
public sealed class ListaSolicitudesVm
{
    public ResultadoPaginado<SolicitudListaDto> Resultado { get; init; } =
        ResultadoPaginado.Vacio<SolicitudListaDto>(50);

    public long? TrabajadorId { get; init; }

    public EstadoSolicitud? Estado { get; init; }

    public int Pagina { get; init; } = 1;

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; init; } = [];

    /// <summary>Filtros activos, para conservarlos al cambiar de página.</summary>
    /// <remarks>
    /// Vive en el modelo y no en la vista: es el propio listado quien sabe por qué filtra.
    /// La página la agrega <see cref="PaginacionVm"/>.
    /// </remarks>
    public Dictionary<string, string?> RutaFiltros() => new()
    {
        ["trabajadorId"] = TrabajadorId?.ToString(CultureInfo.InvariantCulture),
        ["estado"] = Estado is null ? null : ((int)Estado).ToString(CultureInfo.InvariantCulture)
    };
}

/// <summary>Modelo del formulario de alta de una solicitud.</summary>
public sealed class EditarSolicitudVm
{
    public GuardarSolicitudRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; set; } = [];

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<TipoAusenciaOpcionDto> TiposAusencia { get; set; } = [];

    /// <summary>
    /// Periodos vacacionales que ofrece el desplegable cuando se eligen vacaciones gozadas.
    /// </summary>
    /// <remarks>
    /// Se generan en el servidor: la lista es una regla de captura ("desde 2020"), no un
    /// detalle de presentación, y armarla en JavaScript la dejaría fuera del alcance de
    /// cualquier prueba.
    /// </remarks>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<string> PeriodosVacacionales { get; set; } = [];
}

/// <summary>Modelo del formulario de rechazo o anulación, que exigen motivo.</summary>
public sealed class DecisionSolicitudVm
{
    public long Id { get; set; }

    [BindNever]
    [ValidateNever]
    public string Trabajador { get; set; } = string.Empty;

    [BindNever]
    [ValidateNever]
    public string TipoAusencia { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe indicar el motivo.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener entre 3 y 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Modelo de la pantalla que agrega el sustento que faltaba.</summary>
/// <remarks>
/// Es una acción estrecha a propósito, no una edición: solo lleva el sustento y su ruta de
/// documento. Trabajador, tipo y fechas no viajan porque no se tocan.
/// </remarks>
public sealed class RegistrarSustentoVm
{
    public long Id { get; set; }

    [BindNever]
    [ValidateNever]
    public string Trabajador { get; set; } = string.Empty;

    [BindNever]
    [ValidateNever]
    public string TipoAusencia { get; set; } = string.Empty;

    [BindNever]
    [ValidateNever]
    public DateOnly FechaInicio { get; set; }

    [BindNever]
    [ValidateNever]
    public DateOnly FechaFin { get; set; }

    [Required(ErrorMessage = "Describa el sustento.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El sustento debe tener entre 5 y 500 caracteres.")]
    public string Sustento { get; set; } = string.Empty;

    [StringLength(400)]
    public string? RutaDocumentoSustento { get; set; }
}
