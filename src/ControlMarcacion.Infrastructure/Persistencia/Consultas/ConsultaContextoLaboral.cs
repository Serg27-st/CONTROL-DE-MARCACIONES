using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Asistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Consultas;

/// <summary>Resultado observable de resolver la historia laboral para una fecha.</summary>
internal enum EstadoResolucionContextoLaboral
{
    PeriodoLaboral,
    LegacyFallback,
    SinPeriodo,
    ErrorConfiguracion
}

/// <summary>Asignación histórica mínima de planta; no expone la entidad seguida por EF.</summary>
internal sealed record TramoPlantaLaboral(
    long AsignacionPlantaId,
    long PlantaId,
    DateOnly FechaInicio,
    DateOnly? FechaFin)
{
    public bool Contiene(DateOnly fecha) =>
        FechaInicio <= fecha && (FechaFin is null || FechaFin.Value >= fecha);
}

/// <summary>Proyección inmutable de un vínculo y su historia de planta.</summary>
internal sealed record PeriodoLaboralResuelto(
    long PeriodoLaboralId,
    long EmpresaId,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    IReadOnlyList<TramoPlantaLaboral> Plantas)
{
    public bool Contiene(DateOnly fecha) =>
        FechaInicio <= fecha && (FechaFin is null || FechaFin.Value >= fecha);

    public bool SeSolapa(DateOnly desde, DateOnly hasta) =>
        FechaInicio <= hasta && (FechaFin is null || FechaFin.Value >= desde);
}

/// <summary>Contexto canónico listo para un consumidor histórico.</summary>
internal sealed record ContextoLaboralResuelto(
    long PeriodoLaboralId,
    long EmpresaId,
    long? PlantaId,
    DateOnly FechaInicio,
    DateOnly? FechaFin);

/// <summary>
/// Resolución con diagnóstico explícito. La ruta canónica nunca convierte silenciosamente
/// <see cref="EstadoResolucionContextoLaboral.SinPeriodo"/> en datos de la ficha actual.
/// </summary>
internal readonly record struct ResultadoResolucionContextoLaboral(
    EstadoResolucionContextoLaboral Estado,
    ContextoLaboralResuelto? Contexto,
    string? Diagnostico)
{
    public static ResultadoResolucionContextoLaboral Encontrado(ContextoLaboralResuelto contexto) =>
        new(EstadoResolucionContextoLaboral.PeriodoLaboral, contexto, null);

    public static ResultadoResolucionContextoLaboral SinPeriodo() =>
        new(EstadoResolucionContextoLaboral.SinPeriodo, null, null);

    public static ResultadoResolucionContextoLaboral Error(string diagnostico) =>
        new(EstadoResolucionContextoLaboral.ErrorConfiguracion, null, diagnostico);

    /// <summary>
    /// Única puerta para un respaldo legacy. Ningún consumidor migrado en 3C la usa; su
    /// existencia hace detectable un uso transitorio en tests o diagnóstico.
    /// </summary>
    public static ResultadoResolucionContextoLaboral DesdeLegacy(ContextoLaboralResuelto contexto) =>
        new(EstadoResolucionContextoLaboral.LegacyFallback, contexto, "PENDIENTE_3C");

    public ContextoLaboralResuelto? ObtenerOError()
    {
        if (Estado == EstadoResolucionContextoLaboral.ErrorConfiguracion)
        {
            throw new ReglaNegocioException(Diagnostico ?? "La historia laboral es ambigua.");
        }

        return Contexto;
    }
}

/// <summary>Línea de tiempo precargada de una persona.</summary>
internal sealed class HistoriaLaboralTrabajador(
    long trabajadorId,
    IReadOnlyList<PeriodoLaboralResuelto> periodos)
{
    public long TrabajadorId { get; } = trabajadorId;

    public IReadOnlyList<PeriodoLaboralResuelto> Periodos { get; } = periodos;

    public ResultadoResolucionContextoLaboral Resolver(DateOnly fecha)
    {
        var vigentes = Periodos.Where(p => p.Contiene(fecha)).ToList();

        if (vigentes.Count == 0)
        {
            return ResultadoResolucionContextoLaboral.SinPeriodo();
        }

        if (vigentes.Count > 1)
        {
            return ResultadoResolucionContextoLaboral.Error(
                $"El trabajador {TrabajadorId} tiene más de un periodo laboral vigente " +
                $"el {fecha:dd/MM/yyyy}.");
        }

        var periodo = vigentes[0];
        var plantas = periodo.Plantas.Where(p => p.Contiene(fecha)).ToList();

        if (plantas.Count > 1)
        {
            return ResultadoResolucionContextoLaboral.Error(
                $"El periodo laboral {periodo.PeriodoLaboralId} tiene más de una planta " +
                $"vigente el {fecha:dd/MM/yyyy}.");
        }

        return ResultadoResolucionContextoLaboral.Encontrado(new ContextoLaboralResuelto(
            periodo.PeriodoLaboralId,
            periodo.EmpresaId,
            plantas.SingleOrDefault()?.PlantaId,
            periodo.FechaInicio,
            periodo.FechaFin));
    }

    public IReadOnlyList<PeriodoLaboralResuelto> EnRango(DateOnly desde, DateOnly hasta) =>
        [.. Periodos.Where(p => p.SeSolapa(desde, hasta)).OrderBy(p => p.FechaInicio)];
}

/// <summary>
/// Consulta única de historia laboral para reportes y tareo. Precarga por rango y resuelve
/// después en memoria: nunca emite una consulta por trabajador/día.
/// </summary>
internal sealed class ConsultaContextoLaboral(MarcacionDbContext contexto)
{
    /// <summary>
    /// Aplica seguridad por la planta histórica de la fecha del tareo. Una planta actual
    /// nunca concede ni retira acceso retroactivo a una jornada ya calculada.
    /// </summary>
    public IQueryable<TareoDiario> AplicarAlcancePlantas(
        IQueryable<TareoDiario> consulta,
        IReadOnlyCollection<long>? plantasVisibles)
    {
        if (plantasVisibles is null)
        {
            return consulta;
        }

        var idsPlanta = plantasVisibles.ToList();
        return consulta.Where(t => contexto.AsignacionesPlanta.Any(a =>
            a.PeriodoLaboral!.TrabajadorId == t.TrabajadorId
            && a.PeriodoLaboral.FechaIngreso <= t.Fecha
            && (a.PeriodoLaboral.FechaCese == null || a.PeriodoLaboral.FechaCese >= t.Fecha)
            && a.FechaInicio <= t.Fecha
            && (a.FechaFin == null || a.FechaFin >= t.Fecha)
            && idsPlanta.Contains(a.PlantaId)));
    }

    public async Task<IReadOnlyDictionary<long, HistoriaLaboralTrabajador>> CargarRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyCollection<long>? trabajadorIds = null,
        CancellationToken cancelacion = default)
    {
        if (hasta < desde)
        {
            throw new ArgumentException("El fin del rango no puede ser anterior al inicio.", nameof(hasta));
        }

        var consulta = contexto.PeriodosLaborales
            .AsNoTracking()
            .Include(p => p.AsignacionesPlanta)
            .Where(p => p.FechaIngreso <= hasta
                        && (p.FechaCese == null || p.FechaCese >= desde));

        if (trabajadorIds is not null)
        {
            consulta = consulta.Where(p => trabajadorIds.Contains(p.TrabajadorId));
        }

        var filas = await consulta
            .OrderBy(p => p.TrabajadorId)
            .ThenBy(p => p.FechaIngreso)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var resultado = filas
            .GroupBy(p => p.TrabajadorId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => new HistoriaLaboralTrabajador(
                    grupo.Key,
                    [.. grupo.Select(p => new PeriodoLaboralResuelto(
                        p.Id,
                        p.EmpresaId,
                        p.FechaIngreso,
                        p.FechaCese,
                        [.. p.AsignacionesPlanta
                            .OrderBy(a => a.FechaInicio)
                            .Select(a => new TramoPlantaLaboral(
                                a.Id, a.PlantaId, a.FechaInicio, a.FechaFin))]))]));

        if (trabajadorIds is not null)
        {
            foreach (var trabajadorId in trabajadorIds)
            {
                resultado.TryAdd(trabajadorId, new HistoriaLaboralTrabajador(trabajadorId, []));
            }
        }

        return resultado;
    }
}

