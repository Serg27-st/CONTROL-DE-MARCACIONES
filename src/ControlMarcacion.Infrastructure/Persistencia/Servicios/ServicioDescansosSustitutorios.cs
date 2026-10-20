using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Registra el vínculo explícito entre un feriado trabajado y su descanso sustitutorio.
/// No infiere descansos ni modifica tareos, marcaciones o historia laboral.
/// </summary>
internal sealed class ServicioDescansosSustitutorios(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioDescansosSustitutorios
{
    private readonly ConsultaContextoLaboral consultaLaboral = new(contexto);

    public async Task<IReadOnlyList<DescansoSustitutorioDto>> ListarAsync(
        CancellationToken cancelacion = default)
    {
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        var consulta = contexto.DescansosSustitutorios.AsNoTracking();

        if (plantas is not null)
        {
            var ids = plantas.ToList();
            consulta = consulta.Where(d =>
                contexto.AsignacionesPlanta.Any(a =>
                    a.PeriodoLaboral!.TrabajadorId == d.TrabajadorId
                    && a.FechaInicio <= d.FechaFeriadoOrigen
                    && (a.FechaFin == null || a.FechaFin >= d.FechaFeriadoOrigen)
                    && ids.Contains(a.PlantaId))
                && contexto.AsignacionesPlanta.Any(a =>
                    a.PeriodoLaboral!.TrabajadorId == d.TrabajadorId
                    && a.FechaInicio <= d.FechaDescanso
                    && (a.FechaFin == null || a.FechaFin >= d.FechaDescanso)
                    && ids.Contains(a.PlantaId)));
        }

        return await (
                from descanso in consulta
                join trabajador in contexto.Trabajadores.AsNoTracking()
                    on descanso.TrabajadorId equals trabajador.Id
                join tareo in contexto.TareosDiarios.AsNoTracking()
                    on new { descanso.TrabajadorId, Fecha = descanso.FechaFeriadoOrigen }
                    equals new { tareo.TrabajadorId, tareo.Fecha }
                    into tareos
                from tareo in tareos.DefaultIfEmpty()
                orderby descanso.FechaDescanso descending, descanso.Id descending
                select new DescansoSustitutorioDto(
                    descanso.Id,
                    descanso.TrabajadorId,
                    trabajador.ApellidoPaterno + " " + trabajador.ApellidoMaterno + ", " + trabajador.Nombres,
                    descanso.FechaFeriadoOrigen,
                    tareo == null ? 0 : tareo.MinutosTrabajados,
                    descanso.FechaDescanso,
                    descanso.Estado,
                    descanso.CreadoPor ?? "sistema",
                    descanso.CreadoEn,
                    descanso.AprobadoPorUsuario,
                    descanso.AprobadoEn,
                    descanso.AnuladoPorUsuario,
                    descanso.AnuladoEn,
                    descanso.MotivoAnulacion,
                    descanso.Version))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Trabajadores.AsNoTracking().Where(t => t.Activo);
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantas is not null)
        {
            consulta = consulta.Where(t => plantas.Contains(t.PlantaId));
        }

        return await consulta
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno).ThenBy(t => t.Nombres)
            .Select(t => new OpcionDto(
                t.Id, t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<long> OtorgarAsync(
        RegistrarDescansoSustitutorioRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var entidad = DescansoSustitutorio.Otorgar(
            solicitud.TrabajadorId,
            solicitud.FechaFeriadoOrigen,
            solicitud.FechaDescanso,
            usuario,
            DateTime.Now);

        await ValidarRelacionAsync(entidad, cancelacion).ConfigureAwait(false);

        var duplicado = await contexto.DescansosSustitutorios.AsNoTracking().AnyAsync(
                d => d.Activo
                     && d.TrabajadorId == solicitud.TrabajadorId
                     && (d.FechaFeriadoOrigen == solicitud.FechaFeriadoOrigen
                         || d.FechaDescanso == solicitud.FechaDescanso),
                cancelacion)
            .ConfigureAwait(false);

        if (duplicado)
        {
            throw new ReglaNegocioException(
                "Ya existe un descanso sustitutorio vigente para ese feriado o para esa fecha de descanso.");
        }

        var periodos = await CargarPeriodosAbiertosAsync(
                solicitud.FechaFeriadoOrigen, solicitud.FechaDescanso, cancelacion)
            .ConfigureAwait(false);

        await contexto.DescansosSustitutorios.AddAsync(entidad, cancelacion).ConfigureAwait(false);
        foreach (var periodo in periodos)
        {
            contexto.AfirmarAbierto(periodo);
        }

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException(
                "Uno de los periodos cambió mientras se otorgaba el descanso; no se guardó nada.");
        }
        catch (DbUpdateException excepcion) when (EsRestriccionUnica(excepcion))
        {
            throw new ReglaNegocioException(
                "Otro usuario ya otorgó un descanso sustitutorio para ese feriado o esa fecha.");
        }

        return entidad.Id;
    }

    public async Task AnularAsync(
        AnularDescansoSustitutorioRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var entidad = await contexto.DescansosSustitutorios
            .FirstOrDefaultAsync(d => d.Id == solicitud.Id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el descanso sustitutorio", solicitud.Id);

        if (entidad.Version != solicitud.Version)
        {
            throw new ReglaNegocioException(
                "El descanso fue modificado por otro usuario. Actualice la pantalla antes de continuar.");
        }

        await ValidarAlcanceAsync(
                entidad.TrabajadorId, entidad.FechaFeriadoOrigen, entidad.FechaDescanso, cancelacion)
            .ConfigureAwait(false);

        var periodos = await CargarPeriodosAbiertosAsync(
                entidad.FechaFeriadoOrigen, entidad.FechaDescanso, cancelacion)
            .ConfigureAwait(false);

        entidad.Anular(solicitud.Motivo, usuario, DateTime.Now);
        foreach (var periodo in periodos)
        {
            contexto.AfirmarAbierto(periodo);
        }

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException(
                "El descanso o uno de sus periodos cambió mientras se anulaba; no se guardó nada.");
        }
    }

    private async Task ValidarRelacionAsync(
        DescansoSustitutorio entidad,
        CancellationToken cancelacion)
    {
        var trabajadorExiste = await contexto.Trabajadores.AsNoTracking()
            .AnyAsync(t => t.Id == entidad.TrabajadorId, cancelacion)
            .ConfigureAwait(false);

        if (!trabajadorExiste)
        {
            throw new RecursoNoEncontradoException("el trabajador", entidad.TrabajadorId);
        }

        var historia = await consultaLaboral.CargarRangoAsync(
                entidad.FechaFeriadoOrigen,
                entidad.FechaDescanso,
                [entidad.TrabajadorId],
                cancelacion)
            .ConfigureAwait(false);
        var linea = historia[entidad.TrabajadorId];
        var origen = linea.Resolver(entidad.FechaFeriadoOrigen).ObtenerOError();
        var descanso = linea.Resolver(entidad.FechaDescanso).ObtenerOError();

        if (origen is null)
        {
            throw new ReglaNegocioException(
                "El trabajador no tenía vínculo laboral vigente en la fecha del feriado.");
        }

        if (descanso is null)
        {
            throw new ReglaNegocioException(
                "El trabajador no tiene vínculo laboral vigente en la fecha del descanso.");
        }

        if (origen.PeriodoLaboralId != descanso.PeriodoLaboralId)
        {
            throw new ReglaNegocioException(
                "El feriado y su descanso deben pertenecer al mismo vínculo laboral continuo.");
        }

        await ValidarAlcanceAsync(entidad.TrabajadorId, entidad.FechaFeriadoOrigen, entidad.FechaDescanso, cancelacion)
            .ConfigureAwait(false);

        var tareoOrigen = await contexto.TareosDiarios.AsNoTracking()
            .Where(t => t.TrabajadorId == entidad.TrabajadorId
                        && t.Fecha == entidad.FechaFeriadoOrigen)
            .Select(t => new { t.TipoDiaNoLaborable, t.MinutosTrabajados })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (tareoOrigen is null
            || tareoOrigen.TipoDiaNoLaborable != TipoDiaNoLaborable.Feriado
            || tareoOrigen.MinutosTrabajados <= 0)
        {
            throw new ReglaNegocioException(
                "La fecha de origen debe tener un tareo de feriado efectivamente trabajado.");
        }

        var trabajoEnDescanso = await contexto.TareosDiarios.AsNoTracking()
            .AnyAsync(t => t.TrabajadorId == entidad.TrabajadorId
                           && t.Fecha == entidad.FechaDescanso
                           && t.MinutosTrabajados > 0,
                cancelacion)
            .ConfigureAwait(false);

        if (trabajoEnDescanso)
        {
            throw new ReglaNegocioException(
                "La fecha elegida ya tiene trabajo registrado y no puede otorgarse como descanso completo.");
        }
    }

    private async Task ValidarAlcanceAsync(
        long trabajadorId,
        DateOnly origen,
        DateOnly descanso,
        CancellationToken cancelacion)
    {
        var historia = await consultaLaboral.CargarRangoAsync(
                origen, descanso, [trabajadorId], cancelacion)
            .ConfigureAwait(false);
        var linea = historia[trabajadorId];
        var plantaOrigen = linea.Resolver(origen).ObtenerOError()?.PlantaId;
        var plantaDescanso = linea.Resolver(descanso).ObtenerOError()?.PlantaId;

        if (plantaOrigen is null || plantaDescanso is null
            || !await alcance.PuedeVerPlantaAsync(plantaOrigen.Value, cancelacion).ConfigureAwait(false)
            || !await alcance.PuedeVerPlantaAsync(plantaDescanso.Value, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", trabajadorId);
        }
    }

    private async Task<IReadOnlyList<ControlMarcacion.Domain.Catalogos.Periodo>> CargarPeriodosAbiertosAsync(
        DateOnly origen,
        DateOnly descanso,
        CancellationToken cancelacion)
    {
        var periodos = await contexto.Periodos
            .Where(p => (p.FechaInicio <= origen && p.FechaFin >= origen)
                        || (p.FechaInicio <= descanso && p.FechaFin >= descanso))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var periodoOrigen = periodos.SingleOrDefault(p => p.FechaInicio <= origen && p.FechaFin >= origen)
            ?? throw new ReglaNegocioException("No existe un periodo de tareo para la fecha del feriado.");
        var periodoDescanso = periodos.SingleOrDefault(p => p.FechaInicio <= descanso && p.FechaFin >= descanso)
            ?? throw new ReglaNegocioException("No existe un periodo de tareo para la fecha del descanso.");

        if (!periodoOrigen.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodoOrigen.Codigo);
        }

        if (!periodoDescanso.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodoDescanso.Codigo);
        }

        return periodoOrigen.Id == periodoDescanso.Id
            ? [periodoOrigen]
            : [periodoOrigen, periodoDescanso];
    }

    private static bool EsRestriccionUnica(DbUpdateException excepcion) =>
        excepcion.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
        == true;
}
