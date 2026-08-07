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
///
/// Los datos personales de abajo son <b>nullable a nivel de columna</b>: las cuentas
/// creadas antes de que existieran no los tienen y no hay de dónde inventarlos. Toda cuenta
/// nueva los exige (ver <c>CrearUsuarioRequest</c>).
/// </remarks>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Nombre para mostrar, en el formato <c>APELLIDOS, Nombres</c>.
    /// </summary>
    /// <remarks>
    /// Se conserva como columna propia porque las cuentas anteriores solo tienen esto —
    /// no hay forma segura de partir un nombre libre en apellidos y nombres. En las cuentas
    /// nuevas se deriva de <see cref="ApellidoPaterno"/> / <see cref="ApellidoMaterno"/> /
    /// <see cref="Nombres"/> al crear la cuenta, igual que <c>Trabajador.ApellidosYNombres</c>.
    /// </remarks>
    public required string NombreCompleto { get; set; }

    public string? ApellidoPaterno { get; set; }

    public string? ApellidoMaterno { get; set; }

    public string? Nombres { get; set; }

    public long? TipoDocumentoId { get; set; }

    /// <summary>
    /// Número de documento de identidad de la persona dueña de la cuenta.
    /// <b>Siempre texto, jamás numérico:</b> un DNI que empieza en cero pierde el dígito
    /// al convertirse a número.
    /// </summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>
    /// Empresa de referencia de la cuenta. ⚠️ Hoy <b>no se escribe</b>: en el alta, el
    /// desplegable de empresa solo sirve para filtrar la cascada de plantas. La columna
    /// queda reservada para cuando GCA confirme si una cuenta debe quedar acotada a una
    /// razón social; mientras tanto, un NULL honesto es mejor que un dato que nadie aplica.
    /// </summary>
    public long? EmpresaId { get; set; }

    /// <summary>
    /// Baja lógica de la cuenta.
    /// </summary>
    /// <remarks>
    /// Identity ya trae <c>LockoutEnd</c> para bloqueos temporales por intentos fallidos;
    /// esto es la baja definitiva de una cuenta que ya no debe volver a entrar, sin
    /// borrar el registro — nada se borra físicamente en este sistema.
    /// </remarks>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Plantas que gestiona la cuenta. Solo se puebla para el rol <c>ADMINISTRADOR</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es un dato registrado, <b>no un filtro aplicado</b>: ninguna consulta del sistema
    /// acota resultados por esta lista todavía. Si alguna vez se usa para limitar el
    /// alcance de un usuario, hay que aplicarlo en los servicios de <c>Application</c> —
    /// nunca ocultando botones en la vista.
    /// </remarks>
    public ICollection<UsuarioPlanta> PlantasGestionadas { get; set; } = [];
}
