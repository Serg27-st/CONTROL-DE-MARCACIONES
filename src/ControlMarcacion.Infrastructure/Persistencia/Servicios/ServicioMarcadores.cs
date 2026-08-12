using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

internal sealed class ServicioMarcadores(MarcacionDbContext contexto) : IServicioMarcadores
{
    public async Task<IReadOnlyList<MarcadorListaDto>> ListarAsync(
        FiltroMarcadores filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Marcadores.AsNoTracking();

        if (!filtro.IncluirInactivos)
        {
            consulta = consulta.Where(m => m.Activo);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(m => m.PlantaId == plantaId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var termino = filtro.Busqueda.Trim();

            consulta = consulta.Where(m =>
                m.Codigo.Contains(termino)
                || (m.Descripcion != null && m.Descripcion.Contains(termino)));
        }

        return await Proyectar(consulta.OrderBy(m => m.Planta!.Nombre).ThenBy(m => m.Codigo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<MarcadorListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.Marcadores.AsNoTracking().Where(m => m.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarMarcadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim();

        var plantaExiste = await contexto.Plantas
            .AnyAsync(p => p.Id == solicitud.PlantaId && p.Activo, cancelacion)
            .ConfigureAwait(false);

        if (!plantaExiste)
        {
            throw new ReglaNegocioException("La planta indicada no existe o está inactiva.");
        }

        // Único en todo el sistema, no por planta: el TXT del G3 no dice de qué planta
        // salió cada marcación. Ver Marcador.
        var repetido = await contexto.Marcadores
            .AnyAsync(m => m.Codigo == codigo && m.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException(
                $"Ya existe un marcador con el código '{codigo}'. Los códigos son únicos en " +
                "todo el sistema porque el archivo del marcador no indica de qué planta " +
                "proviene cada marcación.");
        }

        Marcador marcador;

        if (solicitud.Id is { } id)
        {
            marcador = await contexto.Marcadores
                .FirstOrDefaultAsync(m => m.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el marcador", id);

            await ValidarCambiosSobreUsadoAsync(marcador, codigo, solicitud.PlantaId, cancelacion)
                .ConfigureAwait(false);

            marcador.ModificadoPor = usuario;
        }
        else
        {
            marcador = new Marcador { CreadoPor = usuario };
            await contexto.Marcadores.AddAsync(marcador, cancelacion).ConfigureAwait(false);
        }

        marcador.Codigo = codigo;
        marcador.PlantaId = solicitud.PlantaId;
        marcador.Descripcion = string.IsNullOrWhiteSpace(solicitud.Descripcion)
            ? null
            : solicitud.Descripcion.Trim();
        marcador.Activo = solicitud.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return marcador.Id;
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var marcador = await contexto.Marcadores
            .FirstOrDefaultAsync(m => m.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el marcador", id);

        if (!activo)
        {
            var vigentes = await contexto.AsignacionesMarcador
                .CountAsync(a => a.IdMarcador == marcador.Codigo && a.FechaFin == null, cancelacion)
                .ConfigureAwait(false);

            if (vigentes > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede dar de baja el marcador '{marcador.Codigo}': lo tienen asignado " +
                    $"{vigentes} trabajador(es) sin fecha de fin. Reasígnelos o regístreles la baja " +
                    "primero.");
            }
        }

        marcador.Activo = activo;
        marcador.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default) =>
        await contexto.Plantas.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Empresa!.RazonSocial).ThenBy(p => p.Nombre)
            .Select(p => new OpcionDto(p.Id, p.Empresa!.RazonSocial + " · " + p.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    // ------------------------------------------------------------------

    /// <summary>
    /// Bloquea los cambios que romperían asignaciones ya registradas.
    /// </summary>
    /// <remarks>
    /// <c>AsignacionMarcador</c> guarda el <b>texto</b> del código, no la clave de esta
    /// fila: así lo exige la ingesta, que cruza el TXT por ese texto. La consecuencia es
    /// que renombrar un marcador ya usado no arrastra sus asignaciones — las deja
    /// apuntando a un código que ya no existe en el catálogo, y sus marcaciones pasan a
    /// caer en el reporte de "IDs sin trabajador".
    /// </remarks>
    private async Task ValidarCambiosSobreUsadoAsync(
        Marcador marcador,
        string codigoNuevo,
        long plantaNueva,
        CancellationToken cancelacion)
    {
        var cambiaCodigo = !string.Equals(marcador.Codigo, codigoNuevo, StringComparison.Ordinal);
        var cambiaPlanta = marcador.PlantaId != plantaNueva;

        if (!cambiaCodigo && !cambiaPlanta)
        {
            return;
        }

        var asignaciones = await contexto.AsignacionesMarcador
            .CountAsync(a => a.IdMarcador == marcador.Codigo, cancelacion)
            .ConfigureAwait(false);

        if (asignaciones == 0)
        {
            return;
        }

        if (cambiaCodigo)
        {
            throw new ReglaNegocioException(
                $"No se puede cambiar el código del marcador '{marcador.Codigo}': tiene " +
                $"{asignaciones} asignación(es) registradas que lo referencian por su texto. " +
                "Dé de baja este marcador y cree uno nuevo con el código correcto.");
        }

        // Cambiar de planta con asignaciones vigentes dejaría a esos trabajadores con un
        // marcador que ya no es de su planta, que es justo lo que la validación de
        // Personal rechaza: quedarían imposibles de guardar sin tocarles el marcador.
        var vigentes = await contexto.AsignacionesMarcador
            .CountAsync(a => a.IdMarcador == marcador.Codigo && a.FechaFin == null, cancelacion)
            .ConfigureAwait(false);

        if (vigentes > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede cambiar de planta el marcador '{marcador.Codigo}': lo tienen " +
                $"asignado {vigentes} trabajador(es) de la planta actual.");
        }
    }

    private IQueryable<MarcadorListaDto> Proyectar(IQueryable<Marcador> consulta) =>
        consulta.Select(m => new MarcadorListaDto(
            m.Id,
            m.Codigo,
            m.PlantaId,
            m.Planta!.Nombre,
            m.Planta!.Empresa!.RazonSocial,
            m.Descripcion,
            m.Activo,
            contexto.AsignacionesMarcador.Count(a => a.IdMarcador == m.Codigo && a.FechaFin == null),
            contexto.AsignacionesMarcador.Count(a => a.IdMarcador == m.Codigo)));
}
