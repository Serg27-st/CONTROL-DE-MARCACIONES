using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Personal;

namespace ControlMarcacion.Application.Personal.Dto;

/// <summary>
/// Fila del listado de personal. Proyección, no entidad.
/// </summary>
public sealed record TrabajadorListaDto(
    long Id,
    string TipoDocumento,
    string NumeroDocumento,
    string ApellidosYNombres,
    string Empresa,
    string Planta,
    string Area,
    string Seccion,
    string Puesto,
    string? TurnoVigente,
    string? IdMarcador,
    DateOnly FechaIngreso,
    DateOnly? FechaCese)
{
    public bool EstaActivo => FechaCese is null;
}

/// <summary>
/// Filtros del listado de personal.
/// </summary>
public sealed record FiltroTrabajadores
{
    public string? Busqueda { get; init; }

    public long? EmpresaId { get; init; }

    public long? PlantaId { get; init; }

    public long? AreaId { get; init; }

    public long? SeccionId { get; init; }

    public TipoPlanilla? TipoPlanilla { get; init; }

    /// <summary>Nulo trae todos; true solo vigentes; false solo cesados.</summary>
    public bool? SoloActivos { get; init; } = true;

    public int Pagina { get; init; } = 1;

    public int TamanioPagina { get; init; } = 25;
}

/// <summary>
/// Datos de alta y edición de un trabajador.
/// </summary>
public sealed record GuardarTrabajadorRequest
{
    public long? Id { get; init; }

    // ---- Datos personales ----

    [Required(ErrorMessage = "Seleccione el tipo de documento.")]
    public long TipoDocumentoId { get; init; }

    [Required(ErrorMessage = "El número de documento es obligatorio.")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "El número de documento debe tener entre 6 y 20 caracteres.")]
    public string NumeroDocumento { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(80)]
    public string ApellidoPaterno { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido materno es obligatorio.")]
    [StringLength(80)]
    public string ApellidoMaterno { get; init; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(120)]
    public string Nombres { get; init; } = string.Empty;

    [Required(ErrorMessage = "Seleccione el sexo.")]
    public Sexo Sexo { get; init; }

    [StringLength(30)]
    public string? Telefono { get; init; }

    [StringLength(250)]
    public string? Direccion { get; init; }

    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [StringLength(150)]
    public string? CorreoElectronico { get; init; }

    public bool TieneHijos { get; init; }

    // ---- Datos laborales ----

    [Required(ErrorMessage = "Seleccione la empresa.")]
    public long EmpresaId { get; init; }

    [StringLength(120)]
    public string? Unidad { get; init; }

    [Required(ErrorMessage = "Seleccione el tipo de planilla.")]
    public TipoPlanilla TipoPlanilla { get; init; }

    [Required(ErrorMessage = "Seleccione la planta.")]
    public long PlantaId { get; init; }

    [Required(ErrorMessage = "Seleccione el área.")]
    public long AreaId { get; init; }

    [Required(ErrorMessage = "Seleccione la sección.")]
    public long SeccionId { get; init; }

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    public DateOnly FechaIngreso { get; init; }

    public DateOnly? FechaCese { get; init; }

    [Required(ErrorMessage = "Seleccione el puesto.")]
    public long PuestoId { get; init; }

    // ---- Asignaciones iniciales ----

    /// <summary>
    /// Identificador que reporta el marcador. Texto para conservar los ceros a la
    /// izquierda: un "00123" convertido a número deja de cruzar con el archivo.
    /// </summary>
    [StringLength(30)]
    public string? IdMarcador { get; init; }

    public long? TurnoId { get; init; }
}

/// <summary>
/// Ficha completa para la pantalla de edición.
/// </summary>
public sealed record TrabajadorDetalleDto(
    long Id,
    long TipoDocumentoId,
    string NumeroDocumento,
    string ApellidoPaterno,
    string ApellidoMaterno,
    string Nombres,
    Sexo Sexo,
    string? Telefono,
    string? Direccion,
    string? CorreoElectronico,
    bool TieneHijos,
    long EmpresaId,
    string? Unidad,
    TipoPlanilla TipoPlanilla,
    long PlantaId,
    long AreaId,
    long SeccionId,
    DateOnly FechaIngreso,
    DateOnly? FechaCese,
    long PuestoId,
    string? IdMarcador,
    long? TurnoId);
