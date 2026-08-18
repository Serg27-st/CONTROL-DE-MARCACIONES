using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Infrastructure.Seguridad;

/// <summary>
/// Respaldo sin restricción: ve todas las plantas.
/// </summary>
/// <remarks>
/// Igual que <c>ProveedorUsuarioSistema</c> para la auditoría, cubre los caminos sin petición
/// HTTP —siembra, arranque, herramientas de EF—, donde no hay sesión de la que sacar un rol y
/// no se expone dato a nadie. La capa Web registra el suyo, que lee el rol y las plantas de la
/// sesión, y lo sustituye.
///
/// Devuelve <c>null</c> (sin restricción) y no una lista vacía: vacío significaría «no ve
/// ninguna planta» y dejaría a la siembra sin poder consultar nada.
/// </remarks>
internal sealed class AlcancePlantasSistema : IProveedorAlcancePlantas
{
    private static readonly Task<IReadOnlyCollection<long>?> SinRestriccion =
        Task.FromResult<IReadOnlyCollection<long>?>(null);

    public Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default) =>
        SinRestriccion;
}
