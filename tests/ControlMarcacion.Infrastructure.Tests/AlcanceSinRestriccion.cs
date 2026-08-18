using ControlMarcacion.Application.Comun;

namespace ControlMarcacion.Infrastructure.Tests;

/// <summary>
/// Alcance de plantas «ve todo», para los tests que no van sobre el aislamiento por planta.
/// </summary>
/// <remarks>
/// Devuelve <c>null</c> (sin restricción), igual que el respaldo de producción para
/// COORDINADOR y los procesos sin sesión. Los tests específicos del aislamiento por planta
/// usan un doble propio que devuelve una lista concreta.
/// </remarks>
internal sealed class AlcanceSinRestriccion : IProveedorAlcancePlantas
{
    public Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyCollection<long>?>(null);
}

/// <summary>
/// Alcance acotado a un conjunto concreto de plantas, para los tests del aislamiento. Una
/// lista vacía es el ADMINISTRADOR sin plantas: no ve ninguna (fail-closed).
/// </summary>
internal sealed class AlcanceDePlantas(params long[] plantas) : IProveedorAlcancePlantas
{
    public Task<IReadOnlyCollection<long>?> PlantasVisiblesAsync(CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyCollection<long>?>(plantas);
}
