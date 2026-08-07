using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de solicitudes.</summary>
public sealed class ListaSolicitudesVm
{
    public IReadOnlyList<SolicitudListaDto> Solicitudes { get; init; } = [];

    public long? TrabajadorId { get; init; }

    public EstadoSolicitud? Estado { get; init; }

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Trabajadores { get; init; } = [];
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
