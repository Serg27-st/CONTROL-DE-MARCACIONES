namespace ControlMarcacion.Application.Seguridad;

/// <summary>
/// Los cuatro roles del sistema.
/// </summary>
/// <remarks>
/// Son constantes de texto y nada más: no dependen de ASP.NET Core Identity, así que
/// viven en Application y las puede usar tanto Web (en atributos <c>[Authorize(Roles=...)]</c>,
/// que exigen una constante de tiempo de compilación) como Infrastructure (al sembrar los
/// roles y crear usuarios), sin que ninguna de las dos tenga que conocer a la otra.
///
/// Matriz de acceso vigente (la autorización real la dan los <c>[Authorize]</c> de los
/// controladores; el menú solo oculta lo que igual estaría bloqueado):
///
/// <list type="bullet">
///   <item><b>COORDINADOR</b>: todo el sistema, incluida la gestión de cuentas.</item>
///   <item><b>ADMINISTRADOR</b>: gestiona el tareo — personal, catálogos, marcaciones,
///     justificaciones, turnos, tipos de ausencia, asistencia, solicitudes y el cierre y
///     reapertura de periodo. No administra cuentas ni ve reportes.</item>
///   <item><b>GERENCIA</b>: solo reportes.</item>
///   <item><b>TRABAJADOR_SOCIAL</b>: solo solicitudes de ausencia.</item>
/// </list>
///
/// ⚠️ <c>ADMINISTRADOR</c> conserva el nombre que tenía antes pero <b>ya no es el rol
/// superior</b>: ese lugar lo ocupa <c>COORDINADOR</c>. Si aparece un
/// <c>[Authorize(Roles = RolesSistema.Administrador)]</c> suelto donde se esperaba "el que
/// manda", está mal migrado.
/// </remarks>
public static class RolesSistema
{
    /// <summary>Rol superior: única puerta a la gestión de cuentas de usuario.</summary>
    public const string Coordinador = "COORDINADOR";

    /// <summary>Gestiona el tareo del día a día. No administra cuentas.</summary>
    public const string Administrador = "ADMINISTRADOR";

    /// <summary>Consulta y exporta reportes.</summary>
    public const string Gerencia = "GERENCIA";

    /// <summary>Atiende solicitudes de ausencia.</summary>
    public const string TrabajadorSocial = "TRABAJADOR_SOCIAL";

    public static readonly IReadOnlyList<string> Todos =
    [
        Coordinador,
        Administrador,
        Gerencia,
        TrabajadorSocial
    ];
}
