using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Seguridad.Dto;

/// <summary>Fila del listado de usuarios.</summary>
/// <remarks>
/// <c>NumeroDocumento</c> es dato personal (Ley 29733): se muestra porque esta pantalla es
/// exclusiva del COORDINADOR y dos personas pueden llamarse igual, pero no debe replicarse
/// en pantallas de menor alcance ni salir en logs.
/// </remarks>
public sealed record UsuarioListaDto(
    string Id,
    string Correo,
    string NombreCompleto,
    string? NumeroDocumento,
    string Rol,
    string PlantasGestionadas,
    bool Activo);

/// <summary>Alta de un usuario.</summary>
/// <remarks>
/// No hay autorregistro: el flujo operativo es que un COORDINADOR da de alta la cuenta,
/// igual que da de alta al trabajador en Personal. La contraseña la define aquí porque no
/// hay servidor de correo configurado para un enlace de activación.
/// </remarks>
public sealed record CrearUsuarioRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    public string Correo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido paterno es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido paterno debe tener entre 2 y 100 caracteres.")]
    public string ApellidoPaterno { get; init; } = string.Empty;

    [Required(ErrorMessage = "El apellido materno es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido materno debe tener entre 2 y 100 caracteres.")]
    public string ApellidoMaterno { get; init; } = string.Empty;

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 120 caracteres.")]
    public string Nombres { get; init; } = string.Empty;

    /// <summary>
    /// Nullable a propósito: con <c>long</c> a secas, el "Seleccione…" del desplegable llega
    /// como 0 y <c>[Required]</c> lo daría por válido.
    /// </summary>
    [Required(ErrorMessage = "Seleccione el tipo de documento.")]
    public long? TipoDocumentoId { get; init; }

    /// <summary>
    /// Documento de identidad. <b>Siempre texto, jamás numérico:</b> un DNI que empieza
    /// en cero pierde el dígito al convertirse a número.
    /// </summary>
    [Required(ErrorMessage = "El número de documento es obligatorio.")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "El número de documento debe tener entre 6 y 20 caracteres.")]
    public string NumeroDocumento { get; init; } = string.Empty;

    [Required(ErrorMessage = "Seleccione el rol.")]
    public string Rol { get; init; } = string.Empty;

    /// <summary>
    /// Empresa elegida en el formulario. <b>Solo filtra la cascada de plantas</b>: no se
    /// guarda en la cuenta ni acota lo que el usuario puede ver.
    /// </summary>
    public long? EmpresaId { get; init; }

    /// <summary>
    /// Plantas que gestionará la cuenta. Solo se persiste cuando el rol es
    /// <c>ADMINISTRADOR</c>; para cualquier otro rol se ignora aunque venga poblada, para
    /// que nadie deduzca un alcance que el sistema no le dio.
    /// </summary>
    public List<long> PlantaIds { get; init; } = [];

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Clave { get; init; } = string.Empty;

    /// <summary>
    /// Nombre para mostrar, en el mismo formato que <c>Trabajador.ApellidosYNombres</c>.
    /// </summary>
    public string NombreCompleto =>
        $"{ApellidoPaterno.Trim()} {ApellidoMaterno.Trim()}, {Nombres.Trim()}";
}
