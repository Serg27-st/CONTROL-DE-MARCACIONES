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
    /// <summary>Código del centro SAP. Nulo si el trabajador no tiene Centro de costo asignado.</summary>
    string? Centro,
    /// <summary>Código y descripción del centro de costo. Nulo si no se asignó.</summary>
    string? CentroCosto,
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

    /// <summary>Centro SAP (catálogo real, reemplaza el filtro anterior por Área).</summary>
    public long? CentroId { get; init; }

    /// <summary>Centro de costo (catálogo real, reemplaza el filtro anterior por Sección).</summary>
    public long? CentroCostoId { get; init; }

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

    /// <summary>
    /// Aproximación anterior a <see cref="CentroCostoId"/>. Ya no se captura desde
    /// el formulario de Personal — Centro y Centro de costo cumplen ese rol — pero
    /// se conserva como respaldo para el tareo final mientras el trabajador no
    /// tenga Centro de costo asignado. Si no se envía, el servicio conserva el
    /// valor existente en vez de borrarlo.
    /// </summary>
    public long? AreaId { get; init; }

    /// <summary>Ver <see cref="AreaId"/>: mismo respaldo, mismo motivo.</summary>
    public long? SeccionId { get; init; }

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    public DateOnly FechaIngreso { get; init; }

    /// <summary>
    /// Cese. <b>No se captura desde este formulario</b>: la baja tiene su propia acción
    /// (<see cref="BajaTrabajadorRequest"/>) porque exige motivo y cierra las asignaciones
    /// vigentes. Aquí solo viaja para que el formulario pueda mostrarla y para que el
    /// servicio detecte si alguien intentó cambiarla por la puerta de atrás.
    /// </summary>
    public DateOnly? FechaCese { get; init; }

    /// <summary>Ver <see cref="FechaCese"/>: viaja para mostrarse, no para editarse aquí.</summary>
    public MotivoCese? MotivoCese { get; init; }

    [Required(ErrorMessage = "Seleccione el puesto.")]
    public long PuestoId { get; init; }

    /// <summary>
    /// Centro de costo real de SAP. Opcional y sin vigencia por fecha: a diferencia
    /// del ID de marcador, el turno y el sueldo, nadie pidió historizarlo.
    /// </summary>
    public long? CentroCostoId { get; init; }

    // ---- Asignaciones iniciales ----

    /// <summary>
    /// Identificador que reporta el marcador. Texto para conservar los ceros a la
    /// izquierda: un "00123" convertido a número deja de cruzar con el archivo.
    /// </summary>
    [StringLength(30)]
    public string? IdMarcador { get; init; }

    public long? TurnoId { get; init; }

    /// <summary>
    /// Sueldo básico mensual. Se guarda con vigencia por fecha: si cambia respecto al
    /// vigente, se cierra la asignación anterior y se abre una nueva, igual que el ID
    /// de marcador y el turno.
    /// </summary>
    [Range(0, 999999.99, ErrorMessage = "El sueldo básico debe estar entre 0 y 999,999.99.")]
    public decimal? MontoSueldoBasico { get; init; }
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
    long? AreaId,
    long? SeccionId,
    DateOnly FechaIngreso,
    DateOnly? FechaCese,
    long PuestoId,
    long? CentroCostoId,
    /// <summary>
    /// Centro SAP del que depende <see cref="CentroCostoId"/>, resuelto por
    /// consulta. No se guarda en <see cref="Trabajador"/>: existe solo para que la
    /// pantalla de edición sepa con qué centro precargar la lista de centros de
    /// costo y qué opción marcar como seleccionada.
    /// </summary>
    long? CentroId,
    string? IdMarcador,
    long? TurnoId,
    MotivoCese? MotivoCese,
    string? ObservacionCese);

/// <summary>
/// Resultado de la búsqueda rápida del panel flotante. Proyección liviana para una
/// lista de coincidencias, no la ficha completa.
/// </summary>
public sealed record TrabajadorBusquedaRapidaDto(
    long Id,
    string TipoDocumento,
    string NumeroDocumento,
    string ApellidosYNombres,
    string Empresa,
    string Planta,
    string? IdMarcador,
    bool EstaActivo);

/// <summary>
/// Ficha para el panel flotante lateral: los mismos datos que <see cref="TrabajadorDetalleDto"/>
/// pero con los nombres ya resueltos (para el modo "ver", que se muestra como texto plano)
/// y el sueldo básico vigente a hoy.
/// </summary>
public sealed record TrabajadorPanelDto(
    long Id,
    string TipoDocumento,
    string NumeroDocumento,
    string ApellidoPaterno,
    string ApellidoMaterno,
    string Nombres,
    string ApellidosYNombres,
    Sexo Sexo,
    string? Telefono,
    string? Direccion,
    string? CorreoElectronico,
    bool TieneHijos,
    string Empresa,
    string? Unidad,
    TipoPlanilla TipoPlanilla,
    string Planta,
    string Puesto,
    /// <summary>Código y descripción del centro de costo elegido, ya resueltos. Nulo si no se asignó.</summary>
    string? CentroCosto,
    DateOnly FechaIngreso,
    DateOnly? FechaCese,
    string? IdMarcador,
    string? Turno,
    decimal? SueldoBasicoMensual,
    MotivoCese? MotivoCese,
    string? ObservacionCese)
{
    public bool EstaActivo => FechaCese is null;

    public string? MotivoCeseLegible => MotivoCese is { } motivo ? EtiquetasPersonal.Describir(motivo) : null;
}
