using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Justificaciones: consulta con trazabilidad y alta manual.
/// </summary>
internal sealed class ServicioJustificaciones(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioJustificaciones
{
    public async Task<ResultadoPaginado<JustificacionListaDto>> ListarAsync(
        FiltroJustificaciones filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Justificaciones.AsNoTracking();

        // Aislamiento por planta: un ADMINISTRADOR solo ve justificaciones de trabajadores de
        // sus plantas. La justificación es donde se puede «maquillar» una falta, así que este
        // filtro es tan importante como los demás.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(j => contexto.Trabajadores
                .Where(t => plantasVisibles.Contains(t.PlantaId))
                .Select(t => t.Id)
                .Contains(j.TrabajadorId));
        }

        if (filtro.PeriodoId is { } periodoId)
        {
            consulta = consulta.Where(j => j.PeriodoId == periodoId);
        }

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(j => j.TrabajadorId == trabajadorId);
        }

        if (filtro.Origen is { } origen)
        {
            consulta = consulta.Where(j => j.Origen == origen);
        }

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanio = Math.Clamp(filtro.TamanioPagina, 10, 200);

        var elementos = await consulta
            // Se desempata por Id: al aprobar una solicitud se crean varias justificaciones
            // de fechas distintas, pero un mismo día puede tener filas de trabajadores
            // distintos y sin desempate el orden entre páginas queda indefinido.
            .OrderByDescending(j => j.Fecha)
            .ThenByDescending(j => j.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(j => new JustificacionListaDto(
                j.Id,
                j.TrabajadorId,
                contexto.Trabajadores
                    .Where(t => t.Id == j.TrabajadorId)
                    .Select(t => t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres)
                    .FirstOrDefault() ?? "-",
                j.PeriodoId,
                contexto.Periodos.Where(p => p.Id == j.PeriodoId).Select(p => p.Codigo).FirstOrDefault() ?? "-",
                j.Fecha,
                j.Origen,
                j.SolicitudAusenciaId,
                j.TipoAusenciaId == null
                    ? null
                    : contexto.TiposAusencia.Where(t => t.Id == j.TipoAusenciaId).Select(t => t.Nombre).FirstOrDefault(),
                j.Tipo,
                j.MinutosJustificados,
                j.EstadoAnterior,
                j.Motivo,
                j.RegistradoPorUsuario,
                j.RegistradoEn))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<JustificacionListaDto>(elementos, total, pagina, tamanio);
    }

    public async Task<long> CrearManualAsync(
        CrearJustificacionManualRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var trabajador = await contexto.Trabajadores
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);

        // Un administrador solo justifica a trabajadores de sus plantas. Se responde como si el
        // trabajador no existiera (404), sin confirmar que existe en otra planta.
        if (!await alcance.PuedeVerPlantaAsync(trabajador.PlantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);
        }

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Activo && p.FechaInicio <= solicitud.Fecha && p.FechaFin >= solicitud.Fecha,
                cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException(
                $"La fecha {solicitud.Fecha:dd/MM/yyyy} no pertenece a ningún periodo registrado. " +
                "Cree el periodo correspondiente antes de justificar ese día.");

        // Invariante del sistema: periodo cerrado, nada se escribe. Sin excepciones
        // silenciosas ni "modo administrador".
        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        TipoAusencia? tipoAusencia = null;

        if (solicitud.TipoAusenciaId is { } tipoAusenciaId)
        {
            tipoAusencia = await contexto.TiposAusencia
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tipoAusenciaId, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el tipo de ausencia", tipoAusenciaId);

            if (!tipoAusencia.Activo)
            {
                throw new ReglaNegocioException(
                    $"El tipo de ausencia '{tipoAusencia.Nombre}' está inactivo.");
            }
        }
        else if (solicitud.Tipo == TipoJustificacion.Ausencia)
        {
            throw new ReglaNegocioException(
                "Para justificar una ausencia debe indicar el tipo de ausencia: es lo que " +
                "decide si el día se paga.");
        }

        if (solicitud.Tipo is TipoJustificacion.Tardanza or TipoJustificacion.SalidaAnticipada
            && solicitud.TipoAusenciaId is not null)
        {
            throw new ReglaNegocioException(
                "Una justificación de tardanza o de salida anticipada no lleva tipo de ausencia: " +
                "no reemplaza el día, solo perdona minutos.");
        }

        // El motor de cálculo resuelve una sola justificación por trabajador y día: una
        // segunda no se ignoraría, colisionaría al recalcular. Se bloquea aquí, con un
        // mensaje que dice de dónde viene la que ya existe.
        var existente = await contexto.Justificaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(
                j => j.TrabajadorId == solicitud.TrabajadorId && j.Fecha == solicitud.Fecha,
                cancelacion)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            var origen = existente.Origen == OrigenJustificacion.Solicitud
                ? $"de la solicitud #{existente.SolicitudAusenciaId}"
                : "manual";

            throw new ReglaNegocioException(
                $"Ya existe una justificación {origen} para este trabajador en el " +
                $"{solicitud.Fecha:dd/MM/yyyy}. Retírela primero si necesita reemplazarla.");
        }

        var justificacion = new Justificacion
        {
            TrabajadorId = solicitud.TrabajadorId,
            PeriodoId = periodo.Id,
            Fecha = solicitud.Fecha,
            Origen = OrigenJustificacion.Manual,
            TipoAusenciaId = solicitud.TipoAusenciaId,
            Tipo = solicitud.Tipo,
            MinutosJustificados = solicitud.MinutosJustificados,
            EstadoAnterior = await ObtenerEstadoActualAsync(
                solicitud.TrabajadorId, periodo.Id, solicitud.Fecha, cancelacion).ConfigureAwait(false),
            Motivo = solicitud.Motivo.Trim(),
            RutaDocumentoSustento = solicitud.RutaDocumentoSustento?.Trim(),
            RegistradoPorUsuario = usuario,
            RegistradoEn = DateTime.Now,
            CreadoPor = usuario
        };

        await contexto.Justificaciones.AddAsync(justificacion, cancelacion).ConfigureAwait(false);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return justificacion.Id;
    }

    public async Task EliminarAsync(long id, string usuario, CancellationToken cancelacion = default)
    {
        var justificacion = await contexto.Justificaciones
            .FirstOrDefaultAsync(j => j.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        // Acceso por id: una justificación de un trabajador de otra planta se responde como
        // inexistente (404), no se retira.
        if (!await alcance.PuedeVerTrabajadorAsync(contexto, justificacion.TrabajadorId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("la justificación", id);
        }

        if (justificacion.Origen != OrigenJustificacion.Manual)
        {
            throw new ReglaNegocioException(
                "Esta justificación proviene de una solicitud de ausencia: para retirarla, " +
                "anule la solicitud que la generó.");
        }

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == justificacion.PeriodoId, cancelacion)
            .ConfigureAwait(false);

        if (periodo is not null && !periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        // No se registra "usuario" en la fila porque va a desaparecer: quién la retiró
        // queda en el log de aplicación, no en una tabla que está a punto de perder la fila.
        _ = usuario;

        contexto.Justificaciones.Remove(justificacion);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default)
    {
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaCese == null);

        // El desplegable solo ofrece trabajadores de las plantas que el usuario gestiona.
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantas is not null)
        {
            consulta = consulta.Where(t => plantas.Contains(t.PlantaId));
        }

        return await consulta
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new OpcionDto(
                t.Id, t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default) =>
        await contexto.TiposAusencia
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <summary>
    /// Estado que tenía el tareo antes de esta justificación, si ya se había calculado.
    /// </summary>
    /// <remarks>
    /// Es evidencia de auditoría congelada al momento de crear la justificación: si el
    /// tareo se recalcula después, este valor no cambia, porque describe lo que era
    /// <b>antes</b> de justificar, no lo que es ahora.
    /// </remarks>
    private async Task<string?> ObtenerEstadoActualAsync(
        long trabajadorId, long periodoId, DateOnly fecha, CancellationToken cancelacion) =>
        await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.TrabajadorId == trabajadorId && t.PeriodoId == periodoId && t.Fecha == fecha)
            .Select(t => t.Estado.ToString())
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
}
