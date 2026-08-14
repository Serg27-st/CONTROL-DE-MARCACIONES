using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de feriados: listado de un año, sincronización y alta manual.</summary>
public sealed class ListaFeriadosVm
{
    public int Anio { get; init; }

    public IReadOnlyList<FeriadoDto> Feriados { get; init; } = [];

    public int Nacionales => Feriados.Count(f => f.EsNacional && f.Activo);

    public int DePlanta => Feriados.Count(f => !f.EsNacional && f.Activo);

    public int Inactivos => Feriados.Count(f => !f.Activo);
}

/// <summary>Modelo del formulario de alta y edición manual de un feriado.</summary>
public sealed class EditarFeriadoVm
{
    public GuardarFeriadoRequest Datos { get; set; } = new();

    /// <summary>
    /// Plantas para el desplegable de alcance.
    /// </summary>
    /// <remarks>
    /// Se excluye del enlace y de la validación: no es dato que envíe el usuario, y al ser
    /// una referencia no anulable MVC la daría por campo obligatorio y rechazaría el POST.
    /// Mismo motivo que en <see cref="EditarCatalogoVm.Definicion"/>.
    /// </remarks>
    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> Plantas { get; set; } = [];

    /// <summary>Año al que volver en el listado tras guardar o cancelar.</summary>
    public int Anio { get; set; }

    public bool EsNuevo => Datos.Id is null;
}
