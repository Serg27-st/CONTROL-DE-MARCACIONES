using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

internal sealed class ConsultaCatalogos(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IConsultaCatalogos
{
    public async Task<IReadOnlyList<TipoDocumentoOpcionDto>> TiposDocumentoAsync(
        CancellationToken cancelacion = default) =>
        await contexto.TiposDocumento
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Codigo)
            .Select(t => new TipoDocumentoOpcionDto(
                t.Id,
                t.Codigo + " - " + t.Nombre,
                t.Codigo,
                t.LongitudExacta,
                t.LongitudMaxima,
                t.SoloDigitos))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<TipoDocumentoOpcionDto?> TipoDocumentoPorIdAsync(
        long id,
        CancellationToken cancelacion = default) =>
        await contexto.TiposDocumento
            .AsNoTracking()
            .Where(t => t.Id == id && t.Activo)
            .Select(t => new TipoDocumentoOpcionDto(
                t.Id,
                t.Codigo + " - " + t.Nombre,
                t.Codigo,
                t.LongitudExacta,
                t.LongitudMaxima,
                t.SoloDigitos))
            .FirstOrDefaultAsync(cancelacion)
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

    public async Task<long?> EmpresaDePlantaAsync(long plantaId, CancellationToken cancelacion = default) =>
        await contexto.Plantas
            .AsNoTracking()
            .Where(p => p.Id == plantaId && p.Activo)
            .Select(p => (long?)p.EmpresaId)
            .FirstOrDefaultAsync(cancelacion)
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

    public async Task<IReadOnlyList<OpcionDto>> CentrosAsync(long? empresaId, CancellationToken cancelacion = default) =>
        await contexto.Centros
            .AsNoTracking()
            .Where(c => c.Activo && (empresaId == null || c.EmpresaId == empresaId))
            .OrderBy(c => c.Codigo)
            .Select(c => new OpcionDto(c.Id, c.Codigo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> CentrosCostoAsync(long? centroId, CancellationToken cancelacion = default) =>
        await contexto.CentrosCosto
            .AsNoTracking()
            .Where(cc => cc.Activo && (centroId == null || cc.CentroId == centroId))
            .OrderBy(cc => cc.Codigo)
            .Select(cc => new OpcionDto(cc.Id, cc.Codigo + " - " + cc.Descripcion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> PuestosAsync(long? empresaId = null, CancellationToken cancelacion = default) =>
        await contexto.Puestos
            .AsNoTracking()
            // Un puesto sin empresa declarada aplica a todas (los 4 sembrados). Si
            // declara una, solo aparece para esa: la regla vive en los datos.
            .Where(p => p.Activo && (empresaId == null || p.EmpresaId == null || p.EmpresaId == empresaId))
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

    public async Task<IReadOnlyList<CuadrillaOpcionDto>> CuadrillasActivasAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Cuadrillas
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Planta!.Nombre)
            .ThenBy(c => c.Nombre)
            .Select(c => new CuadrillaOpcionDto(c.Id, c.Codigo, c.Nombre, c.PlantaId, c.Planta!.Nombre, c.Activo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<CuadrillaOpcionDto?> CuadrillaPorIdAsync(long id, CancellationToken cancelacion = default) =>
        await contexto.Cuadrillas
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CuadrillaOpcionDto(c.Id, c.Codigo, c.Nombre, c.PlantaId, c.Planta!.Nombre, c.Activo))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<EquipoOpcionDto>> EquiposActivosAsync(
        CancellationToken cancelacion = default)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        var consulta = contexto.EquiposMarcador
            .AsNoTracking()
            .Where(e => e.Activo);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(e => plantasVisibles.Contains(e.PlantaId));
        }

        return await consulta
            .OrderBy(e => e.Planta!.Nombre)
            .ThenBy(e => e.Nombre)
            .Select(e => new EquipoOpcionDto(
                e.Id,
                e.Codigo,
                e.Nombre,
                e.PlantaId,
                e.Planta!.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MarcadorCatalogoDto>> MarcadoresPorCodigoAsync(
        string codigo,
        long plantaId,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return [];
        }

        var buscado = codigo.Trim();

        return await contexto.Marcadores
            .AsNoTracking()
            .Where(m => m.Codigo == buscado && m.PlantaId == plantaId)
            .OrderBy(m => m.Equipo!.Nombre)
            .Select(m => new MarcadorCatalogoDto(
                m.Id,
                m.Codigo,
                m.EquipoId,
                m.Equipo!.Nombre,
                m.PlantaId,
                m.Planta!.Nombre,
                m.Activo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }
}
