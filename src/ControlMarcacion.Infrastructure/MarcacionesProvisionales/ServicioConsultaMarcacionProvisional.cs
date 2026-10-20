using ControlMarcacion.Application.MarcacionesProvisionales;
using ControlMarcacion.Application.MarcacionesProvisionales.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.MarcacionesProvisionales;

internal sealed class ServicioConsultaMarcacionProvisional(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioConsultaMarcacionProvisional
{
    private const int DiasAdvertencia = 30;
    private const int DiasMaximoExportacion = 40;

    public async Task<ResultadoConsultaProvisional> ConsultarAsync(
        long equipoId,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken cancellationToken = default)
    {
        if (equipoId <= 0)
        {
            throw new ReglaNegocioException("Debe indicar el equipo marcador.");
        }

        if (fechaInicio > fechaFin)
        {
            throw new ReglaNegocioException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancellationToken).ConfigureAwait(false);
        var consultaEquipo = contexto.EquiposMarcador
            .AsNoTracking()
            .Where(e => e.Id == equipoId);

        if (plantasVisibles is not null)
        {
            consultaEquipo = consultaEquipo.Where(e => plantasVisibles.Contains(e.PlantaId));
        }

        var equipo = await consultaEquipo
            .Select(e => new EquipoPlano(e.Codigo, e.Nombre))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (equipo is null)
        {
            throw new ReglaNegocioException("El equipo marcador indicado no existe.");
        }

        var totalDias = fechaFin.DayNumber - fechaInicio.DayNumber + 1;

        var dtInicio = fechaInicio.ToDateTime(TimeOnly.MinValue);
        var dtFin = fechaFin.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var marcaciones = await contexto.MarcacionesProvisionales
            .AsNoTracking()
            .Where(m => m.EquipoId == equipoId
                && m.FechaHora >= dtInicio
                && m.FechaHora < dtFin)
            .OrderBy(m => m.FechaHora)
            .Select(m => new MarcacionPlana(m.IdMarcador, m.FechaHora))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (marcaciones.Count == 0)
        {
            return new ResultadoConsultaProvisional(
                [],
                totalDias,
                totalDias > DiasAdvertencia,
                totalDias > DiasMaximoExportacion);
        }

        var idsDistintos = marcaciones.Select(m => m.IdMarcador).Distinct().ToList();

        var asignacionesMarcador = await contexto.Set<Domain.Personal.AsignacionMarcador>()
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId
                && idsDistintos.Contains(a.IdMarcador)
                && a.FechaInicio <= fechaFin
                && (a.FechaFin == null || a.FechaFin >= fechaInicio))
            .Select(a => new VigenciaMarcador(a.TrabajadorId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var trabajadorIds = asignacionesMarcador.Select(a => a.TrabajadorId).Distinct().ToList();

        var trabajadores = trabajadorIds.Count > 0
            ? await contexto.Set<Domain.Personal.Trabajador>()
                .AsNoTracking()
                .Where(t => trabajadorIds.Contains(t.Id))
                .Select(t => new TrabajadorPlano(
                    t.Id,
                    t.NumeroDocumento,
                    t.ApellidoPaterno,
                    t.ApellidoMaterno,
                    t.Nombres,
                    t.AreaId,
                    t.CentroCostoId))
                .ToDictionaryAsync(t => t.Id, cancellationToken)
                .ConfigureAwait(false)
            : [];

        var historiasLaborales = await new ConsultaContextoLaboral(contexto)
            .CargarRangoAsync(fechaInicio, fechaFin, trabajadorIds, cancellationToken)
            .ConfigureAwait(false);
        var plantaIds = historiasLaborales.Values
            .SelectMany(h => h.Periodos)
            .SelectMany(p => p.Plantas)
            .Select(p => p.PlantaId)
            .Distinct()
            .ToList();

        var plantas = plantaIds.Count > 0
            ? await contexto.Set<Domain.Organizacion.Planta>()
                .AsNoTracking()
                .Where(p => plantaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Nombre, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<long, string>();

        var areaIds = trabajadores.Values
            .Where(t => t.AreaId.HasValue)
            .Select(t => t.AreaId!.Value)
            .Distinct()
            .ToList();

        var areas = areaIds.Count > 0
            ? await contexto.Set<Domain.Organizacion.Area>()
                .AsNoTracking()
                .Where(a => areaIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Nombre, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<long, string>();

        var ccIds = trabajadores.Values
            .Where(t => t.CentroCostoId.HasValue)
            .Select(t => t.CentroCostoId!.Value)
            .Distinct()
            .ToList();

        var centrosCosto = ccIds.Count > 0
            ? await contexto.Set<Domain.Organizacion.CentroCosto>()
                .AsNoTracking()
                .Where(cc => ccIds.Contains(cc.Id))
                .ToDictionaryAsync(cc => cc.Id, cc => cc.Descripcion, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<long, string>();

        var asignacionesTurno = trabajadorIds.Count > 0
            ? await contexto.Set<Domain.Personal.AsignacionTurno>()
                .AsNoTracking()
                .Where(at => trabajadorIds.Contains(at.TrabajadorId)
                    && at.FechaInicio <= fechaFin
                    && (at.FechaFin == null || at.FechaFin >= fechaInicio))
                .Select(at => new VigenciaTurno(at.TrabajadorId, at.TurnoId, at.CuadrillaId, at.FechaInicio, at.FechaFin))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var turnoIds = asignacionesTurno
            .Where(at => at.TurnoId.HasValue)
            .Select(at => at.TurnoId!.Value)
            .Distinct()
            .ToList();

        var turnos = turnoIds.Count > 0
            ? await contexto.Set<Domain.Catalogos.Turno>()
                .AsNoTracking()
                .Where(t => turnoIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => new TurnoPlano(t.Codigo, t.Nombre), cancellationToken)
                .ConfigureAwait(false)
            : [];

        var cuadrillaIds = asignacionesTurno
            .Where(at => at.CuadrillaId.HasValue)
            .Select(at => at.CuadrillaId!.Value)
            .Distinct()
            .ToList();

        var cuadrillas = cuadrillaIds.Count > 0
            ? await contexto.Set<Domain.Catalogos.Cuadrilla>()
                .AsNoTracking()
                .Where(c => cuadrillaIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nombre, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<long, string>();

        var grupos = marcaciones
            .GroupBy(m => (Fecha: DateOnly.FromDateTime(m.FechaHora), m.IdMarcador))
            .OrderBy(g => g.Key.Fecha)
            .ThenBy(g => g.Key.IdMarcador);

        var jornadas = new List<JornadaProvisionalDto>();

        foreach (var grupo in grupos)
        {
            var fecha = grupo.Key.Fecha;
            var idMarcador = grupo.Key.IdMarcador;
            var horas = grupo.Select(m => TimeOnly.FromDateTime(m.FechaHora)).OrderBy(h => h).ToList();

            var trabajadorId = ResolverTrabajador(asignacionesMarcador, idMarcador, fecha);

            string? doc = null, ap = null, am = null, nombres = null, planta = null, area = null, cc = null;
            string? turnoCodigo = null, turnoNombre = null;
            var observaciones = new List<string>();

            if (trabajadorId.HasValue && trabajadores.TryGetValue(trabajadorId.Value, out var trab))
            {
                doc = trab.NumeroDocumento;
                ap = trab.ApellidoPaterno;
                am = trab.ApellidoMaterno;
                nombres = trab.Nombres;

                var laboral = historiasLaborales[trabajadorId.Value]
                    .Resolver(fecha)
                    .ObtenerOError();
                if (laboral?.PlantaId is { } plantaId)
                {
                    plantas.TryGetValue(plantaId, out planta);
                }
                else
                {
                    observaciones.Add("Sin contexto laboral histórico para esta fecha");
                }

                if (trab.AreaId is long areaId && areas.TryGetValue(areaId, out var areaNombre))
                {
                    area = areaNombre;
                }

                if (trab.CentroCostoId is long ccId && centrosCosto.TryGetValue(ccId, out var ccDesc))
                {
                    cc = ccDesc;
                }

                ResolverTurno(asignacionesTurno, turnos, cuadrillas,
                    trabajadorId.Value, fecha,
                    out turnoCodigo, out turnoNombre);
            }
            else if (!trabajadorId.HasValue)
            {
                observaciones.Add("ID de marcador no asignado a ningún trabajador en esta fecha");
            }

            TimeOnly? horaIngreso;
            TimeOnly? horaSalida;

            if (horas.Count == 1)
            {
                horaIngreso = horas[0];
                horaSalida = null;
                observaciones.Add("Solo una marcación registrada");
            }
            else
            {
                horaIngreso = horas[0];
                horaSalida = horas[^1];

                if (horas.Count > 2)
                {
                    observaciones.Add($"{horas.Count} marcaciones registradas");
                }
            }

            if (horas.Count != horas.Distinct().Count())
            {
                observaciones.Add("Marcaciones duplicadas detectadas");
            }

            jornadas.Add(new JornadaProvisionalDto(
                fecha,
                equipoId,
                equipo.Codigo,
                idMarcador,
                trabajadorId,
                doc,
                ap,
                am,
                nombres,
                horaIngreso,
                horaSalida,
                turnoCodigo,
                turnoNombre,
                planta,
                area,
                cc,
                observaciones.Count > 0 ? string.Join(". ", observaciones) : null,
                horas.Count,
                horas));
        }

        return new ResultadoConsultaProvisional(
            jornadas,
            totalDias,
            totalDias > DiasAdvertencia,
            totalDias > DiasMaximoExportacion);
    }

    private static long? ResolverTrabajador(
        List<VigenciaMarcador> asignaciones,
        string idMarcador,
        DateOnly fecha)
    {
        foreach (var a in asignaciones)
        {
            if (a.IdMarcador == idMarcador
                && a.FechaInicio <= fecha
                && (a.FechaFin is null || a.FechaFin >= fecha))
            {
                return a.TrabajadorId;
            }
        }

        return null;
    }

    private static void ResolverTurno(
        List<VigenciaTurno> asignacionesTurno,
        Dictionary<long, TurnoPlano> turnos,
        Dictionary<long, string> cuadrillas,
        long trabajadorId,
        DateOnly fecha,
        out string? codigo,
        out string? nombre)
    {
        codigo = null;
        nombre = null;

        foreach (var at in asignacionesTurno)
        {
            if (at.TrabajadorId != trabajadorId)
            {
                continue;
            }

            if (at.FechaInicio <= fecha && (at.FechaFin is null || at.FechaFin >= fecha))
            {
                if (at.TurnoId is { } turnoId && turnos.TryGetValue(turnoId, out var turno))
                {
                    codigo = turno.Codigo;
                    nombre = turno.Nombre;
                }
                else if (at.CuadrillaId is { } cuadrillaId && cuadrillas.TryGetValue(cuadrillaId, out var cuadrillaNombre))
                {
                    codigo = "CUA";
                    nombre = cuadrillaNombre;
                }

                return;
            }
        }
    }

    private sealed record MarcacionPlana(string IdMarcador, DateTime FechaHora);
    private sealed record EquipoPlano(string Codigo, string Nombre);
    private sealed record VigenciaMarcador(long TrabajadorId, string IdMarcador, DateOnly FechaInicio, DateOnly? FechaFin);
    private sealed record TrabajadorPlano(long Id, string NumeroDocumento, string ApellidoPaterno, string ApellidoMaterno, string Nombres, long? AreaId, long? CentroCostoId);
    private sealed record VigenciaTurno(long TrabajadorId, long? TurnoId, long? CuadrillaId, DateOnly FechaInicio, DateOnly? FechaFin);
    private sealed record TurnoPlano(string Codigo, string Nombre);
}
