using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del listado de un catálogo.</summary>
public sealed class ListaCatalogoVm
{
    public DefinicionCatalogo Definicion { get; init; } = null!;

    public IReadOnlyList<CatalogoItemDto> Elementos { get; init; } = [];

    public IReadOnlyList<OpcionDto> OpcionesPadre { get; init; } = [];

    public long? PadreId { get; init; }

    public bool IncluirInactivos { get; init; }
}

/// <summary>Modelo del formulario de alta y edición de un catálogo.</summary>
public sealed class EditarCatalogoVm
{
    /// <summary>
    /// Clave del catálogo. Viaja como campo oculto porque el POST de guardado no
    /// pasa por la ruta que la traía.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>
    /// Metadato del catálogo, resuelto por el controlador a partir de <see cref="Clave"/>.
    /// </summary>
    /// <remarks>
    /// Se excluye del enlace y de la validación: al ser una referencia no anulable, MVC la
    /// daba por campo obligatorio del formulario y rechazaba todos los POST con
    /// "The Definicion field is required". No es dato que envíe el usuario.
    /// </remarks>
    [BindNever]
    [ValidateNever]
    public DefinicionCatalogo Definicion { get; set; } = null!;

    public GuardarCatalogoRequest Datos { get; set; } = new();

    [BindNever]
    [ValidateNever]
    public IReadOnlyList<OpcionDto> OpcionesPadre { get; set; } = [];

    public bool EsNuevo => Datos.Id is null;
}
