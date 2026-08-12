using ControlMarcacion.Application.Seguridad.Dto;

namespace ControlMarcacion.Application.Seguridad;

/// <summary>
/// Alta y baja lógica de cuentas de usuario.
/// </summary>
/// <remarks>
/// La interfaz vive en Application, sin conocer ASP.NET Core Identity: la implementación
/// en Infrastructure es la que habla con <c>UserManager</c> y <c>RoleManager</c>. Nada
/// aquí borra una cuenta físicamente — se desactiva, igual que el resto del sistema.
/// </remarks>
public interface IServicioUsuarios
{
    Task<IReadOnlyList<UsuarioListaDto>> ListarAsync(CancellationToken cancelacion = default);

    Task CrearAsync(CrearUsuarioRequest solicitud, CancellationToken cancelacion = default);

    /// <summary>
    /// Activa o desactiva una cuenta. Desactivarla bloquea el inicio de sesión de
    /// inmediato, no solo en el próximo intento.
    /// </summary>
    Task CambiarEstadoAsync(string id, bool activo, CancellationToken cancelacion = default);

    IReadOnlyList<string> RolesDisponibles();
}
