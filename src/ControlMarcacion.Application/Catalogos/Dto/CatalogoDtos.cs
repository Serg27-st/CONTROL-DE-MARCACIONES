using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del listado de un catálogo.
/// </summary>
public sealed record CatalogoItemDto(
    long Id,
    string Codigo,
    string Nombre,
    long? PadreId,
    string? PadreNombre,
    bool Activo,
    int Dependencias);

/// <summary>Cuántos elementos tiene un catálogo, separando los dados de baja.</summary>
public sealed record ConteoCatalogo(int Activos, int Inactivos)
{
    public static ConteoCatalogo Vacio { get; } = new(0, 0);

    public int Total => Activos + Inactivos;

    /// <summary>
    /// El catálogo no tiene ni un elemento utilizable. Es un aviso, no un error: los
    /// formularios que dependan de él saldrán con el desplegable vacío.
    /// </summary>
    public bool SinElementosUtilizables => Activos == 0;
}

/// <summary>
/// Tipo de documento para el desplegable, con sus reglas de formato.
/// </summary>
/// <remarks>
/// Lleva <c>Id</c> y <c>Nombre</c> con esos nombres a propósito: los formularios lo
/// alimentan con <c>SelectList(..., "Id", "Nombre")</c>, igual que a un
/// <see cref="Comun.OpcionDto"/> cualquiera.
///
/// Las longitudes viajan hasta la vista para que el navegador pueda acotar el campo
/// mientras se escribe. Eso es comodidad, no control: la validación que manda es la de
/// <c>Application</c>, porque un <c>maxlength</c> se quita desde las herramientas del
/// navegador en dos clics.
/// </remarks>
public sealed record TipoDocumentoOpcionDto(
    long Id,
    string Nombre,
    string Codigo,
    int? LongitudExacta,
    int? LongitudMaxima,
    bool SoloDigitos)
{
    /// <summary>
    /// Cuántos caracteres admite como máximo, ya resueltos exacta y máxima.
    /// </summary>
    /// <remarks>
    /// Sin regla propia cae en 20, que es el ancho de la columna
    /// <c>trabajador.numero_documento</c>. Devolver algo mayor dejaría escribir un valor
    /// que la base va a truncar.
    /// </remarks>
    public int MaximoCaracteres => LongitudExacta ?? LongitudMaxima ?? 20;
}

/// <summary>
/// Alta y edición de un elemento de catálogo.
/// </summary>
public sealed record GuardarCatalogoRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Obligatorio solo en los catálogos jerárquicos.</summary>
    public long? PadreId { get; init; }

    public bool Activo { get; init; } = true;
}

/// <summary>
/// Describe un catálogo mantenible: cómo se llama, de qué cuelga y cómo se rotula.
/// </summary>
/// <remarks>
/// Todo esto es metadato de presentación. Vive aquí, y no repetido en cada vista, para que
/// agregar un catálogo nuevo sea una línea en <see cref="CatalogosDisponibles"/> y no un
/// controlador con sus dos pantallas.
/// </remarks>
/// <param name="RotuloNuevo">Texto del botón de alta, por ejemplo "Nueva área".</param>
/// <param name="ConArticulo">
/// El singular con su artículo, por ejemplo "el área" o "la sección".
/// Va escrito en la definición y no se arma concatenando un artículo fijo: en español el
/// género no se deduce del sustantivo, y "Nuevo área" o "la área" saltan a la vista en una
/// interfaz que usa RRHH todos los días.
/// </param>
public sealed record DefinicionCatalogo(
    string Clave,
    string Titulo,
    string RotuloNuevo,
    string ConArticulo,
    string Descripcion,
    string? ClavePadre = null,
    string? EtiquetaPadre = null)
{
    public bool EsJerarquico => ClavePadre is not null;
}

/// <summary>
/// Catálogos que se mantienen con las pantallas genéricas.
/// </summary>
/// <remarks>
/// Tres catálogos <b>no</b> están aquí porque no son código + nombre y una pantalla
/// genérica los dejaría mal cargados:
/// <list type="bullet">
///   <item><b>Turnos</b>: horarios, refrigerio, tolerancia, días y plantas habilitadas.</item>
///   <item><b>Periodos</b>: rango de fechas y ciclo de apertura y cierre.</item>
///   <item><b>Tipos de ausencia</b>: el <i>comportamiento</i> decide cómo los trata el
///   cálculo; crear uno sin elegirlo produciría tareos incorrectos en silencio.</item>
/// </list>
/// </remarks>
public static class CatalogosDisponibles
{
    public static IReadOnlyList<DefinicionCatalogo> Todos { get; } =
    [
        new DefinicionCatalogo(
            "puestos",
            "Puestos",
            "Nuevo puesto",
            "el puesto",
            "Cargos que ocupan los trabajadores. Es una de las columnas del tareo final."),

        new DefinicionCatalogo(
            "plantas",
            "Plantas",
            "Nueva planta",
            "la planta",
            "Plantas de operación de cada razón social.",
            "empresas",
            "Empresa"),

        new DefinicionCatalogo(
            "areas",
            "Áreas",
            "Nueva área",
            "el área",
            "Áreas dentro de cada planta.",
            "plantas",
            "Planta"),

        new DefinicionCatalogo(
            "secciones",
            "Secciones",
            "Nueva sección",
            "la sección",
            "Secciones dentro de cada área. Es el nivel más fino de la jerarquía.",
            "areas",
            "Área"),

        new DefinicionCatalogo(
            "tipos-documento",
            "Tipos de documento",
            "Nuevo tipo de documento",
            "el tipo de documento",
            "Documentos de identidad admitidos en la ficha del trabajador."),
    ];

    public static DefinicionCatalogo? Buscar(string? clave) =>
        Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
