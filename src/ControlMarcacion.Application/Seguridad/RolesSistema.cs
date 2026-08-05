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
/// El detalle de qué puede hacer cada rol en cada pantalla no está definido por GCA
/// todavía; hoy solo se aplican donde la exposición es clara: cerrar/reabrir un periodo
/// y administrar cuentas de usuario, ambas restringidas a ADMINISTRADOR.
/// </remarks>
public static class RolesSistema
{
    public const string Administrador = "ADMINISTRADOR";
    public const string Rrhh = "RRHH";
    public const string Supervisor = "SUPERVISOR";
    public const string Consulta = "CONSULTA";

    public static readonly IReadOnlyList<string> Todos =
    [
        Administrador,
        Rrhh,
        Supervisor,
        Consulta
    ];
}
