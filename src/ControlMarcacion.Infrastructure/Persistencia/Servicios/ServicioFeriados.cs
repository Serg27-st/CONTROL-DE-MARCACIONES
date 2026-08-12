using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>Sincronización manual del catálogo de feriados contra un proveedor externo.</summary>
internal sealed class ServicioFeriados(
    MarcacionDbContext contexto,
    IProveedorFeriadosExternos proveedor) : IServicioFeriados
{
    public async Task<IReadOnlyList<FeriadoDto>> ListarAsync(int anio, CancellationToken cancelacion = default) =>
        await contexto.Feriados
            .AsNoTracking()
            .Where(f => f.Fecha.Year == anio)
            .OrderBy(f => f.Fecha)
            .Select(f => new FeriadoDto(
                f.Id,
                f.Fecha,
                f.Descripcion,
                f.PlantaId,
                f.PlantaId == null
                    ? null
                    : contexto.Plantas.Where(p => p.Id == f.PlantaId).Select(p => p.Nombre).FirstOrDefault(),
                f.Activo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<ResultadoSincronizacionFeriados> SincronizarAsync(
        int anio, string usuario, CancellationToken cancelacion = default)
    {
        if (anio < 2000 || anio > 2100)
        {
            throw new ReglaNegocioException("El año indicado no es válido.");
        }

        var externos = await proveedor.ObtenerAsync(anio, cancelacion).ConfigureAwait(false);

        var existentes = await contexto.Feriados
            .Where(f => f.Fecha.Year == anio)
            .Select(f => f.Fecha)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var yaCargadas = existentes.ToHashSet();

        // El proveedor puede repetir la misma fecha (dos conmemoraciones el mismo día); el
        // sistema guarda una fila por fecha, así que se agrupa antes de insertar. Nunca se
        // borra ni se pisa un feriado ya cargado, sea del proveedor o de un alta manual.
        var nuevos = externos
            .Where(e => !yaCargadas.Contains(e.Fecha))
            .GroupBy(e => e.Fecha)
            .Select(g => new Feriado
            {
                Fecha = g.Key,
                Descripcion = g.First().Descripcion,
                PlantaId = null, // Feriado nacional: aplica a todas las plantas.
                CreadoPor = usuario
            })
            .ToList();

        if (nuevos.Count > 0)
        {
            await contexto.Feriados.AddRangeAsync(nuevos, cancelacion).ConfigureAwait(false);
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }

        return new ResultadoSincronizacionFeriados(anio, nuevos.Count, existentes.Count);
    }
}
