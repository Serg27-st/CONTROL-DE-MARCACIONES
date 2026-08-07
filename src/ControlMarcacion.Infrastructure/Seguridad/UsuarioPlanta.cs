namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Planta que gestiona una cuenta con rol <c>ADMINISTRADOR</c>.
/// </summary>
/// <remarks>
/// Vive en Infrastructure y no en Domain porque su lado izquierdo es la tabla de usuarios
/// de ASP.NET Core Identity, que también es de Infrastructure. Domain no sabe que Identity
/// existe y así debe seguir.
///
/// ⚠️ Hoy es un <b>registro de intención</b>, no un control de acceso: se guarda qué plantas
/// dijo el coordinador que gestiona esta persona, pero ninguna consulta de Personal,
/// Solicitudes o Reportes se filtra por esto todavía. Cuando GCA confirme que el alcance
/// debe aplicarse, el filtro va en los servicios de <c>Application</c>.
/// </remarks>
public sealed class UsuarioPlanta
{
    /// <summary>Id de la cuenta en <c>asp_net_users</c>.</summary>
    public string UsuarioId { get; set; } = null!;

    public long PlantaId { get; set; }

    public ApplicationUser? Usuario { get; set; }
}
