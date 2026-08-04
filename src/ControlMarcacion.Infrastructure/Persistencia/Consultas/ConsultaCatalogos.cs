using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

internal sealed class ConsultaCatalogos(MarcacionDbContext contexto) : IConsultaCatalogos
{
    public async Task<IReadOnlyList<OpcionDto>> TiposDocumentoAsync(CancellationToken cancelacion = default) =>
        await contexto.TiposDocumento
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Codigo)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> EmpresasAsync(CancellationToken cancelacion = default) =>
        await contexto.Empresas
            .AsNoTracking()
            .Where(e => e.Activo)
            .OrderBy(e => e.RazonSocial)
            .Select(e => new OpcionDto(e.Id, e.RazonSocial))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> PlantasAsync(long? empresaId, CancellationToken cancelacion = default) =>
        await contexto.Plantas
            .AsNoTracking()
            .Where(p => p.Activo && (empresaId == null || p.EmpresaId == empresaId))
            .OrderBy(p => p.Nombre)
            .Select(p => new OpcionDto(p.Id, p.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> AreasAsync(long? plantaId, CancellationToken cancelacion = default) =>
        await contexto.Areas
            .AsNoTracking()
            .Where(a => a.Activo && (plantaId == null || a.PlantaId == plantaId))
            .OrderBy(a => a.Nombre)
            .Select(a => new OpcionDto(a.Id, a.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> SeccionesAsync(long? areaId, CancellationToken cancelacion = default) =>
        await contexto.Secciones
            .AsNoTracking()
            .Where(s => s.Activo && (areaId == null || s.AreaId == areaId))
            .OrderBy(s => s.Nombre)
            .Select(s => new OpcionDto(s.Id, s.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> PuestosAsync(CancellationToken cancelacion = default) =>
        await contexto.Puestos
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new OpcionDto(p.Id, p.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> TurnosAsync(long? plantaId, CancellationToken cancelacion = default) =>
        await contexto.Turnos
            .AsNoTracking()
            .Where(t => t.Activo
                // Un turno sin plantas declaradas aplica a todas. Si declara alguna,
                // solo aparece en esas: así el turno especial de sábado no se ofrece
                // fuera de Planta 01, y la regla vive en los datos, no en el código.
                && (plantaId == null
                    || !t.PlantasHabilitadas.Any()
                    || t.PlantasHabilitadas.Any(tp => tp.PlantaId == plantaId)))
            .OrderBy(t => t.Codigo)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
