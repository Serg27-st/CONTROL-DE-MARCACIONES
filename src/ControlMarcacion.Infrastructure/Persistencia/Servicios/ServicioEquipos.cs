using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

internal sealed class ServicioEquipos(MarcacionDbContext contexto) : IServicioEquipos
{
    public async Task<IReadOnlyList<EquipoListaDto>> ListarAsync(
        FiltroEquipos filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.EquiposMarcador.AsNoTracking();

        if (!filtro.IncluirInactivos)
        {
            consulta = consulta.Where(e => e.Activo);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(e => e.PlantaId == plantaId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            // Sin distinguir mayúsculas: ver BusquedaTexto.
            var termino = BusquedaTexto.Normalizar(filtro.Busqueda);

#pragma warning disable CA1304, CA1311, CA1862
            consulta = consulta.Where(e =>
                e.Codigo.ToUpper().Contains(termino)
                || e.Nombre.ToUpper().Contains(termino)
                || (e.Serie != null && e.Serie.ToUpper().Contains(termino))
                || (e.Descripcion != null && e.Descripcion.ToUpper().Contains(termino)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return await Proyectar(consulta.OrderBy(e => e.Planta!.Nombre).ThenBy(e => e.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<EquipoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.EquiposMarcador.AsNoTracking().Where(e => e.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarEquipoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim();
        var nombre = solicitud.Nombre.Trim();
        var serie = string.IsNullOrWhiteSpace(solicitud.Serie) ? null : solicitud.Serie.Trim();

        var plantaExiste = await contexto.Plantas
            .AnyAsync(p => p.Id == solicitud.PlantaId && p.Activo, cancelacion)
            .ConfigureAwait(false);

        if (!plantaExiste)
        {
            throw new ReglaNegocioException("La unidad indicada no existe o está inactiva.");
        }

        var codigoRepetido = await contexto.EquiposMarcador
            .AnyAsync(e => e.Codigo == codigo && e.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (codigoRepetido)
        {
            throw new ReglaNegocioException(
                $"Ya existe un equipo con el código '{codigo}'. El código identifica al aparato " +
                "y es único en todo el sistema.");
        }

        // La serie es la huella del aparato: si dos equipos declararan la misma, la
        // comprobación que hace la carga al leer un archivo no podría decidir de cuál
        // salió, que es justamente para lo que existe.
        if (serie is not null)
        {
            var serieRepetida = await contexto.EquiposMarcador
                .AnyAsync(e => e.Serie == serie && e.Id != solicitud.Id, cancelacion)
                .ConfigureAwait(false);

            if (serieRepetida)
            {
                throw new ReglaNegocioException(
                    $"La serie '{serie}' ya está declarada en otro equipo. Cada aparato tiene " +
                    "la suya, y es lo que permite comprobar que un archivo salió del equipo " +
                    "que se declaró al cargarlo.");
            }
        }

        EquipoMarcador equipo;

        if (solicitud.Id is { } id)
        {
            equipo = await contexto.EquiposMarcador
                .FirstOrDefaultAsync(e => e.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el equipo", id);

            await ValidarCambioDePlantaAsync(equipo, solicitud.PlantaId, cancelacion)
                .ConfigureAwait(false);

            equipo.ModificadoPor = usuario;
        }
        else
        {
            equipo = new EquipoMarcador { CreadoPor = usuario };
            await contexto.EquiposMarcador.AddAsync(equipo, cancelacion).ConfigureAwait(false);
        }

        equipo.Codigo = codigo;
        equipo.Nombre = nombre;
        equipo.Serie = serie;
        equipo.PlantaId = solicitud.PlantaId;
        equipo.Descripcion = string.IsNullOrWhiteSpace(solicitud.Descripcion)
            ? null
            : solicitud.Descripcion.Trim();
        equipo.Activo = solicitud.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return equipo.Id;
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var equipo = await contexto.EquiposMarcador
            .FirstOrDefaultAsync(e => e.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el equipo", id);

        if (!activo)
        {
            // Dar de baja el aparato deja fuera de los desplegables a todos sus códigos, y
            // con ellos la posibilidad de cargar sus archivos. Las marcaciones ya cargadas
            // se conservan —son inmutables— pero el mes en curso quedaría sin poder subirse.
            var marcadores = await contexto.Marcadores
                .CountAsync(m => m.EquipoId == equipo.Id && m.Activo, cancelacion)
                .ConfigureAwait(false);

            if (marcadores > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede dar de baja el equipo '{equipo.Nombre}': tiene {marcadores} " +
                    "marcador(es) activo(s). Dé de baja esos códigos primero, o el personal que " +
                    "los tiene asignados quedaría sin poder cargar sus marcaciones.");
            }
        }

        equipo.Activo = activo;
        equipo.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task RegistrarSerieAsync(
        long id,
        string serie,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serie);

        serie = serie.Trim();

        var equipo = await contexto.EquiposMarcador
            .FirstOrDefaultAsync(e => e.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el equipo", id);

        if (equipo.Serie is not null)
        {
            if (string.Equals(equipo.Serie, serie, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new ReglaNegocioException(
                $"El equipo '{equipo.Nombre}' ya tiene registrada la serie '{equipo.Serie}'. " +
                "Si está mal, corríjala en Equipos.");
        }

        // Se compara en memoria para no depender de si SQLite distingue mayúsculas: son
        // unos pocos equipos.
        var otro = (await contexto.EquiposMarcador
                .AsNoTracking()
                .Where(e => e.Serie != null && e.Id != id)
                .Select(e => new { e.Nombre, e.Serie })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false))
            .FirstOrDefault(e => string.Equals(e.Serie, serie, StringComparison.OrdinalIgnoreCase));

        if (otro is not null)
        {
            throw new ReglaNegocioException(
                $"La serie '{serie}' ya está registrada en el equipo '{otro.Nombre}'.");
        }

        equipo.Serie = serie;
        equipo.ModificadoPor = usuario;

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
    /// Impide mudar de planta un equipo que ya emitió códigos.
    /// </summary>
    /// <remarks>
    /// <c>Marcador.PlantaId</c> es una copia derivada de la del equipo, así que mudar el
    /// aparato arrastraría a otra planta a todos sus códigos y, con ellos, al personal que
    /// los tiene asignado — Personal valida que el marcador sea de la planta del trabajador,
    /// de modo que esas fichas quedarían imposibles de guardar sin tocarlas una por una.
    ///
    /// Un aparato se muda de verdad muy pocas veces; cuando pasa, lo correcto es darlo de
    /// baja y registrar el nuevo, que además deja la historia en su sitio: las marcaciones
    /// viejas siguen apuntando al equipo donde de verdad ocurrieron.
    /// </remarks>
    private async Task ValidarCambioDePlantaAsync(
        EquipoMarcador equipo,
        long plantaNueva,
        CancellationToken cancelacion)
    {
        if (equipo.PlantaId == plantaNueva)
        {
            return;
        }

        var marcadores = await contexto.Marcadores
            .CountAsync(m => m.EquipoId == equipo.Id, cancelacion)
            .ConfigureAwait(false);

        if (marcadores > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede cambiar de unidad el equipo '{equipo.Nombre}': tiene {marcadores} " +
                "marcador(es) registrados que heredan su unidad. Dé de baja este equipo y " +
                "registre el aparato en su nueva unidad.");
        }

        var importaciones = await contexto.Importaciones
            .CountAsync(i => i.EquipoId == equipo.Id, cancelacion)
            .ConfigureAwait(false);

        if (importaciones > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede cambiar de unidad el equipo '{equipo.Nombre}': tiene " +
                $"{importaciones} archivo(s) cargados que quedarían atribuidos a una unidad " +
                "en la que esas marcaciones nunca ocurrieron.");
        }
    }

    private IQueryable<EquipoListaDto> Proyectar(IQueryable<EquipoMarcador> consulta) =>
        consulta.Select(e => new EquipoListaDto(
            e.Id,
            e.Codigo,
            e.Nombre,
            e.Serie,
            e.PlantaId,
            e.Planta!.Nombre,
            e.Planta!.Empresa!.RazonSocial,
            e.Descripcion,
            e.Activo,
            contexto.Marcadores.Count(m => m.EquipoId == e.Id && m.Activo),
            contexto.Marcadores.Count(m => m.EquipoId == e.Id),
            contexto.Importaciones.Count(i => i.EquipoId == e.Id)));
}
