using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Application.Seguridad.Dto;

/// <summary>Fila del listado de usuarios.</summary>
public sealed record UsuarioListaDto(
    string Id,
    string Correo,
    string NombreCompleto,
    string Rol,
    bool Activo);

/// <summary>Alta de un usuario.</summary>
/// <remarks>
/// No hay autorregistro: el flujo operativo es que un ADMINISTRADOR da de alta la cuenta,
/// igual que da de alta al trabajador en Personal. La contraseña la define aquí porque no
/// hay servidor de correo configurado para un enlace de activación.
/// </remarks>
public sealed record CrearUsuarioRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    public string Correo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required(ErrorMessage = "Seleccione el rol.")]
    public string Rol { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Clave { get; init; } = string.Empty;
}
