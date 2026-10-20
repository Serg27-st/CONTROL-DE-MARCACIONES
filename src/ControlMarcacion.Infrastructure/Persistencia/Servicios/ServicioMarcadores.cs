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
            // Sin distinguir mayúsculas: ver BusquedaTexto.
            var termino = BusquedaTexto.Normalizar(filtro.Busqueda);

#pragma warning disable CA1304, CA1311, CA1862
            consulta = consulta.Where(m =>
                m.Codigo.ToUpper().Contains(termino)
                || (m.Descripcion != null && m.Descripcion.ToUpper().Contains(termino)));
#pragma warning restore CA1304, CA1311, CA1862
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

        // La planta NO llega del formulario: es una copia derivada de la del equipo. Se lee
        // de aquí para escribirla, de modo que no puedan quedar en desacuerdo.
        var equipo = await contexto.EquiposMarcador
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == solicitud.EquipoId && e.Activo, cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException(
                "El equipo indicado no existe o está de baja. Registre el aparato en " +
                "Equipos antes de darle de alta sus códigos.");

        // Único POR EQUIPO, no en todo el sistema: así lo numera el ZKTeco, y es lo que
        // permite que el A–L y el M–Z de la misma planta tengan ambos del 1 al 100. De qué
        // aparato salió cada marcación lo declara el operador al cargar el archivo, y la
        // serie del propio archivo lo confirma. Ver EquipoMarcador.
        var repetido = await contexto.Marcadores
            .AnyAsync(
                m => m.Codigo == codigo && m.EquipoId == solicitud.EquipoId && m.Id != solicitud.Id,
                cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException(
                $"El equipo '{equipo.Nombre}' ya tiene un marcador con el código '{codigo}'. " +
                "Dentro de un mismo aparato el código identifica a una sola persona.");
        }

        Marcador marcador;

        if (solicitud.Id is { } id)
        {
            marcador = await contexto.Marcadores
                .FirstOrDefaultAsync(m => m.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el marcador", id);

            await ValidarCambiosSobreUsadoAsync(marcador, codigo, solicitud.EquipoId, cancelacion)
                .ConfigureAwait(false);

            marcador.ModificadoPor = usuario;
        }
        else
        {
            marcador = new Marcador { CreadoPor = usuario };
            await contexto.Marcadores.AddAsync(marcador, cancelacion).ConfigureAwait(false);
        }

        marcador.Codigo = codigo;
        marcador.EquipoId = equipo.Id;
        marcador.PlantaId = equipo.PlantaId;
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
            // Acotado al equipo: sin eso, dar de baja el código 14 del aparato A–L contaría
            // también a quien tiene el 14 del M–Z, que es otra persona.
            var vigentes = await contexto.AsignacionesMarcador
                .CountAsync(
                    a => a.EquipoId == marcador.EquipoId
                         && a.IdMarcador == marcador.Codigo
                         && a.FechaFin == null,
                    cancelacion)
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

    public async Task<IReadOnlyList<EquipoOpcionDto>> EquiposAsync(
        CancellationToken cancelacion = default) =>
        await contexto.EquiposMarcador.AsNoTracking()
            .Where(e => e.Activo)
            .OrderBy(e => e.Planta!.Nombre).ThenBy(e => e.Nombre)
            .Select(e => new EquipoOpcionDto(e.Id, e.Codigo, e.Nombre, e.PlantaId, e.Planta!.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

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
        long equipoNuevo,
        CancellationToken cancelacion)
    {
        var cambiaCodigo = !string.Equals(marcador.Codigo, codigoNuevo, StringComparison.Ordinal);
        var cambiaEquipo = marcador.EquipoId != equipoNuevo;

        if (!cambiaCodigo && !cambiaEquipo)
        {
            return;
        }

        var asignaciones = await contexto.AsignacionesMarcador
            .CountAsync(
                a => a.EquipoId == marcador.EquipoId && a.IdMarcador == marcador.Codigo,
                cancelacion)
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

        // Mudar el código a otro aparato con asignaciones vigentes es más grave que el
        // antiguo cambio de planta: la asignación se resuelve por (equipo, código, fecha),
        // así que las marcaciones de esas personas dejarían de encontrar a su dueño y
        // pasarían al reporte de "IDs sin trabajador" — y, si en el equipo de destino ese
        // código ya es de alguien, se atribuirían a la persona equivocada.
        var vigentes = await contexto.AsignacionesMarcador
            .CountAsync(
                a => a.EquipoId == marcador.EquipoId
                     && a.IdMarcador == marcador.Codigo
                     && a.FechaFin == null,
                cancelacion)
            .ConfigureAwait(false);

        if (vigentes > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede mover de equipo el marcador '{marcador.Codigo}': lo tienen " +
                $"asignado {vigentes} trabajador(es) en el aparato actual.");
        }
    }

    private IQueryable<MarcadorListaDto> Proyectar(IQueryable<Marcador> consulta) =>
        consulta.Select(m => new MarcadorListaDto(
            m.Id,
            m.Codigo,
            m.EquipoId,
            m.Equipo!.Nombre,
            m.PlantaId,
            m.Planta!.Nombre,
            m.Planta!.Empresa!.RazonSocial,
            m.Descripcion,
            m.Activo,
            // Acotado al equipo, igual que la resolución de la ingesta: contar por código a
            // secas sumaría las asignaciones del mismo número en el otro aparato y la
            // columna "En uso" mostraría personal que no tiene nada que ver con esta fila.
            contexto.AsignacionesMarcador.Count(
                a => a.EquipoId == m.EquipoId && a.IdMarcador == m.Codigo && a.FechaFin == null),
            contexto.AsignacionesMarcador.Count(
                a => a.EquipoId == m.EquipoId && a.IdMarcador == m.Codigo),
            // Quién lo tiene hoy: es lo que se busca en esta pantalla antes de asignar un
            // número, para no chocar con el de otra persona.
            contexto.AsignacionesMarcador
                .Where(a => a.EquipoId == m.EquipoId && a.IdMarcador == m.Codigo && a.FechaFin == null)
                .Select(a => a.Trabajador!.ApellidoPaterno + " " + a.Trabajador.ApellidoMaterno + ", " + a.Trabajador.Nombres)
                .FirstOrDefault()));
}
