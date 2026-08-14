using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>Catálogo de feriados: sincronización con la fuente oficial y carga manual.</summary>
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

    public async Task<FeriadoDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await contexto.Feriados
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new FeriadoDto(
                f.Id,
                f.Fecha,
                f.Descripcion,
                f.PlantaId,
                f.PlantaId == null
                    ? null
                    : contexto.Plantas.Where(p => p.Id == f.PlantaId).Select(p => p.Nombre).FirstOrDefault(),
                f.Activo))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarFeriadoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // La validación de forma también vive en las anotaciones del request, que es lo que
        // pinta el error junto al campo. Aquí se repite porque el servicio se puede llamar
        // desde fuera de un formulario y no puede confiar en que alguien haya validado.
        if (solicitud.Fecha is not { } fecha)
        {
            throw new ReglaNegocioException("Indique la fecha del feriado.");
        }

        if (fecha.Year is < 2000 or > 2100)
        {
            throw new ReglaNegocioException("El año de la fecha no es válido.");
        }

        var descripcion = solicitud.Descripcion?.Trim() ?? string.Empty;

        if (descripcion.Length < 3)
        {
            throw new ReglaNegocioException(
                "Escriba de qué feriado se trata: es lo que explicará, meses después, por " +
                "qué ese día se pagó con sobretasa.");
        }

        if (solicitud.PlantaId is { } plantaId)
        {
            var existePlanta = await contexto.Plantas
                .AnyAsync(p => p.Id == plantaId, cancelacion)
                .ConfigureAwait(false);

            if (!existePlanta)
            {
                throw new RecursoNoEncontradoException("la planta", plantaId);
            }
        }

        var mismoDia = await contexto.Feriados
            .Where(f => f.Fecha == fecha && f.Id != solicitud.Id)
            .Select(f => new { f.PlantaId })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (mismoDia.Exists(f => f.PlantaId == solicitud.PlantaId))
        {
            throw new ReglaNegocioException(
                solicitud.PlantaId is null
                    ? $"Ya hay un feriado nacional cargado el {fecha:dd/MM/yyyy}."
                    : $"Esa planta ya tiene un feriado cargado el {fecha:dd/MM/yyyy}.");
        }

        // Un feriado de planta sobre un día que ya es nacional no aporta nada —ese día no
        // se trabaja en ninguna planta— y sí confunde: aparecerían dos filas para la misma
        // fecha y habría que adivinar cuál manda.
        if (solicitud.PlantaId is not null && mismoDia.Exists(f => f.PlantaId is null))
        {
            throw new ReglaNegocioException(
                $"El {fecha:dd/MM/yyyy} ya es feriado nacional, así que aplica a todas las " +
                "plantas. No hace falta cargarlo otra vez para una en particular.");
        }

        if (solicitud.Id is not { } id)
        {
            var nuevo = new Feriado
            {
                Fecha = fecha,
                Descripcion = descripcion,
                PlantaId = solicitud.PlantaId,
                Activo = solicitud.Activo,
                CreadoPor = usuario
            };

            await contexto.Feriados.AddAsync(nuevo, cancelacion).ConfigureAwait(false);
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

            return nuevo.Id;
        }

        var actual = await contexto.Feriados
            .FirstOrDefaultAsync(f => f.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el feriado", id);

        actual.Fecha = fecha;
        actual.Descripcion = descripcion;
        actual.PlantaId = solicitud.PlantaId;
        actual.Activo = solicitud.Activo;
        actual.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return actual.Id;
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var feriado = await contexto.Feriados
            .FirstOrDefaultAsync(f => f.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el feriado", id);

        feriado.Activo = activo;
        feriado.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<ResultadoSincronizacionFeriados> SincronizarAsync(
        int anio, string usuario, CancellationToken cancelacion = default)
    {
        if (anio < 2000 || anio > 2100)
        {
            throw new ReglaNegocioException("El año indicado no es válido.");
        }

        var externos = await proveedor.ObtenerAsync(anio, cancelacion).ConfigureAwait(false);

        // Solo cuentan como "ya cargados" los NACIONALES, que es lo único que esta
        // sincronización inserta. Antes se comparaba contra todas las filas de la fecha:
        // con la carga manual de feriados de planta, un aniversario de Planta 01 el 28 de
        // julio habría hecho que la sincronización se saltara Fiestas Patrias, y las otras
        // tres plantas se habrían quedado sin el feriado nacional.
        var existentes = await contexto.Feriados
            .Where(f => f.Fecha.Year == anio && f.PlantaId == null)
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
