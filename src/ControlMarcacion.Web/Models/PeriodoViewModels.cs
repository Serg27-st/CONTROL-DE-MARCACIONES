using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Application.Catalogos.Dto;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de periodos.</summary>
public sealed class ListaPeriodosVm
{
    public IReadOnlyList<PeriodoListaDto> Periodos { get; init; } = [];

    public bool IncluirInactivos { get; init; }

    /// <summary>
    /// Días con marcaciones que no caen en ningún periodo activo.
    /// </summary>
    /// <remarks>
    /// Se muestra en el listado porque es el único lugar donde el problema es visible y
    /// accionable: quien administra periodos es quien puede corregir el rango.
    /// </remarks>
    public IReadOnlyList<DiaSinPeriodo> DiasSinPeriodo { get; init; } = [];
}

/// <summary>Modelo del formulario de alta y edición de un periodo.</summary>
public sealed class EditarPeriodoVm
{
    public GuardarPeriodoRequest Datos { get; set; } = new();

    public bool EsNuevo => Datos.Id is null;
}

/// <summary>Modelo del formulario de reapertura de un periodo cerrado.</summary>
public sealed class ReabrirPeriodoVm
{
    public long Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Motivo de la reapertura. Obligatorio: es la justificación de auditoría de por qué
    /// se descongeló algo que ya estaba cerrado (y posiblemente pagado).
    /// </summary>
    [Required(ErrorMessage = "Debe indicar el motivo de la reapertura.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "El motivo debe tener al menos 3 caracteres y no superar los 500.")]
    public string Motivo { get; set; } = string.Empty;
}
