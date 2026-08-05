using Microsoft.AspNetCore.Identity;

namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Usuario del sistema.
/// </summary>
/// <remarks>
/// Vive en Infrastructure y no en Domain: es un concepto de ASP.NET Core Identity, y
/// Domain no referencia ningún paquete externo — ni siquiera este.
///
/// El nombre de usuario que queda en <c>CreadoPor</c> / <c>ModificadoPor</c> en toda la
/// auditoría del sistema es el correo de esta cuenta, guardado como texto en cada fila.
/// No es una referencia por Id a propósito: el histórico tiene que seguir siendo legible
/// aunque la cuenta se desactive después.
/// </remarks>
public sealed class ApplicationUser : IdentityUser
{
    public required string NombreCompleto { get; set; }

    /// <summary>
    /// Baja lógica de la cuenta.
    /// </summary>
    /// <remarks>
    /// Identity ya trae <c>LockoutEnd</c> para bloqueos temporales por intentos fallidos;
    /// esto es la baja definitiva de una cuenta que ya no debe volver a entrar, sin
    /// borrar el registro — nada se borra físicamente en este sistema.
    /// </remarks>
    public bool Activo { get; set; } = true;
}
