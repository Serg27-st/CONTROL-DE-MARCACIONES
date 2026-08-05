using System.Globalization;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Reportes sobre el tareo ya calculado.
/// </summary>
/// <remarks>
/// Solo lee. La agregación por trabajador se hace en memoria después de traer las filas
/// del periodo: un mes trae como mucho unos pocos miles de filas de tareo, y agregarlas
/// aquí es más simple y más legible que un <c>GroupBy</c> traducido a SQL con sumas
/// condicionales por cada estado.
/// </remarks>
internal sealed class ServicioReportes(MarcacionDbContext contexto) : IServicioReportes
{
    /// <summary>Estados que representan una anomalía sobre la que alguien debe actuar.</summary>
    private static readonly EstadoTareo[] EstadosDeExcepcion =
    [
        EstadoTareo.Falta,
        EstadoTareo.Tardanza,
        EstadoTareo.AusenciaJustificada,
        EstadoTareo.Incompleto
    ];

    public async Task<IReadOnlyList<ResumenTareoDto>> ResumenTareoAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, cancelacion).ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return [];
        }

        var ids = trabajadores.Select(t => t.Id).ToList();

        var tareos = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.PeriodoId == filtro.PeriodoId && ids.Contains(t.TrabajadorId))
            .Select(t => new
            {
                t.TrabajadorId,
                t.Estado,
                t.TipoDiaNoLaborable,
                t.MinutosTrabajados,
                t.MinutosOrdinarios,
                t.MinutosExtra25,
                t.MinutosExtra35,
                t.MinutosNocturnos,
                t.MinutosTardanza
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porTrabajador = tareos.ToLookup(t => t.TrabajadorId);

        return
        [
            .. trabajadores.Select(trabajador =>
            {
                var dias = porTrabajador[trabajador.Id];

                return new ResumenTareoDto(
                    trabajador.Id,
                    trabajador.Nombre,
                    trabajador.IdMarcador,
                    trabajador.Empresa,
                    trabajador.Planta,
                    trabajador.Area,
                    trabajador.Seccion,
                    trabajador.Puesto,
                    dias.Count(d => d.Estado == EstadoTareo.Puntual),
                    dias.Count(d => d.Estado == EstadoTareo.Tardanza),
                    dias.Count(d => d.Estado == EstadoTareo.Falta),
                    dias.Count(d => d.Estado == EstadoTareo.AusenciaJustificada),
                    dias.Count(d => d.Estado == EstadoTareo.Incompleto),
                    dias.Count(d => d.TipoDiaNoLaborable == TipoDiaNoLaborable.DescansoSemanal
                                     && d.MinutosTrabajados > 0),
                    dias.Count(d => d.TipoDiaNoLaborable == TipoDiaNoLaborable.Feriado
                                     && d.MinutosTrabajados > 0),
                    dias.Sum(d => d.MinutosOrdinarios),
                    dias.Sum(d => d.MinutosExtra25),
                    dias.Sum(d => d.MinutosExtra35),
                    dias.Sum(d => d.MinutosNocturnos),
                    dias.Sum(d => d.MinutosTardanza));
            })
            .OrderBy(r => r.Trabajador)
        ];
    }

    public async Task<IReadOnlyList<ExcepcionDto>> ExcepcionesAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, cancelacion).ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return [];
        }

        var ids = trabajadores.Select(t => t.Id).ToList();
        var porId = trabajadores.ToDictionary(t => t.Id);

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Codigo, cancelacion)
            .ConfigureAwait(false);

        var excepciones = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.PeriodoId == filtro.PeriodoId
                        && ids.Contains(t.TrabajadorId)
                        && EstadosDeExcepcion.Contains(t.Estado))
            .OrderBy(t => t.Fecha).ThenBy(t => t.TrabajadorId)
            .Select(t => new
            {
                t.TrabajadorId,
                t.Fecha,
                t.TurnoId,
                t.EntradaReal,
                t.SalidaReal,
                t.Estado,
                t.MinutosTardanza,
                t.MinutosSalidaAnticipada,
                t.Observacion
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return
        [
            .. excepciones.Select(e => new ExcepcionDto(
                e.TrabajadorId,
                porId[e.TrabajadorId].Nombre,
                porId[e.TrabajadorId].IdMarcador,
                e.Fecha,
                e.TurnoId is { } turnoId ? turnos.GetValueOrDefault(turnoId) : null,
                e.EntradaReal,
                e.SalidaReal,
                e.Estado,
                e.MinutosTardanza,
                e.MinutosSalidaAnticipada,
                e.Observacion))
        ];
    }

    public async Task<TareoFinalDto> TareoFinalAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == filtro.PeriodoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", filtro.PeriodoId);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, cancelacion).ConfigureAwait(false);

        var razonSocial = filtro.EmpresaId is { } empresaId
            ? await contexto.Empresas.AsNoTracking()
                .Where(e => e.Id == empresaId).Select(e => e.RazonSocial)
                .FirstOrDefaultAsync(cancelacion).ConfigureAwait(false) ?? "—"
            : trabajadores.Select(t => t.Empresa).Distinct().Count() == 1
                ? trabajadores[0].Empresa
                : "TODAS LAS EMPRESAS";

        var nombreMes = CultureInfo.GetCultureInfo("es-PE")
            .DateTimeFormat.GetMonthName(periodo.FechaInicio.Month).ToUpperInvariant();

        if (trabajadores.Count == 0)
        {
            return new TareoFinalDto(
                razonSocial, nombreMes, periodo.FechaInicio, periodo.FechaFin, [], [], [], []);
        }

        var ids = trabajadores.Select(t => t.Id).ToList();

        var filasTareo = await ArmarFilasTareoAsync(filtro, trabajadores, ids, cancelacion)
            .ConfigureAwait(false);

        var (descansos, vacaciones, licencias) =
            await ArmarAnexosAsync(periodo, trabajadores, ids, razonSocial, nombreMes, cancelacion)
                .ConfigureAwait(false);

        return new TareoFinalDto(
            razonSocial, nombreMes, periodo.FechaInicio, periodo.FechaFin,
            filasTareo, descansos, vacaciones, licencias);
    }

    public async Task<MetadatosReporte> ObtenerMetadatosAsync(
        FiltroReporte filtro,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == filtro.PeriodoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", filtro.PeriodoId);

        var partesFiltro = new List<string>();

        if (filtro.EmpresaId is { } empresaId)
        {
            var nombre = await contexto.Empresas.AsNoTracking()
                .Where(e => e.Id == empresaId).Select(e => e.RazonSocial).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Empresa: {nombre}");
        }

        if (filtro.PlantaId is { } plantaId)
        {
            var nombre = await contexto.Plantas.AsNoTracking()
                .Where(p => p.Id == plantaId).Select(p => p.Nombre).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Planta: {nombre}");
        }

        if (filtro.AreaId is { } areaId)
        {
            var nombre = await contexto.Areas.AsNoTracking()
                .Where(a => a.Id == areaId).Select(a => a.Nombre).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Área: {nombre}");
        }

        if (filtro.SeccionId is { } seccionId)
        {
            var nombre = await contexto.Secciones.AsNoTracking()
                .Where(s => s.Id == seccionId).Select(s => s.Nombre).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Sección: {nombre}");
        }

        return new MetadatosReporte(
            periodo.Codigo,
            periodo.Nombre,
            periodo.FechaInicio,
            periodo.FechaFin,
            periodo.EstaAbierto,
            partesFiltro.Count == 0 ? "Todos los trabajadores" : string.Join(" · ", partesFiltro),
            DateTime.Now,
            usuario);
    }

    // ------------------------------------------------------------------

    /// <summary>Trabajador con los nombres de catálogo ya resueltos, para no repetir joins.</summary>
    private sealed record TrabajadorResuelto(
        long Id,
        string Nombre,
        string? IdMarcador,
        string Empresa,
        string Planta,
        string Area,
        string Seccion,
        string Puesto,
        string Dni,
        DateOnly FechaIngreso,
        DateOnly? FechaCese);

    private async Task<List<TrabajadorResuelto>> ResolverTrabajadoresAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion)
    {
        var consulta = contexto.Trabajadores.AsNoTracking().Where(t => t.Activo);

        if (filtro.EmpresaId is { } empresaId)
        {
            consulta = consulta.Where(t => t.EmpresaId == empresaId);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(t => t.PlantaId == plantaId);
        }

        if (filtro.AreaId is { } areaId)
        {
            consulta = consulta.Where(t => t.AreaId == areaId);
        }

        if (filtro.SeccionId is { } seccionId)
        {
            consulta = consulta.Where(t => t.SeccionId == seccionId);
        }

        return await consulta
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new TrabajadorResuelto(
                t.Id,
                t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.Id && a.FechaFin == null)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.Empresas.Where(e => e.Id == t.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault() ?? "-",
                contexto.Plantas.Where(p => p.Id == t.PlantaId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                contexto.Areas.Where(a => a.Id == t.AreaId).Select(a => a.Nombre).FirstOrDefault() ?? "-",
                contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Nombre).FirstOrDefault() ?? "-",
                contexto.Puestos.Where(p => p.Id == t.PuestoId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.FechaIngreso,
                t.FechaCese))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Tareo final
    // ------------------------------------------------------------------

    /// <summary>
    /// Códigos del catálogo de tipos de ausencia que alimentan cada columna de días del
    /// tareo final. Coinciden 1:1 con lo sembrado en <c>DatosSemilla</c>.
    /// </summary>
    private const string CodigoDescansoMedico = "DM";
    private const string CodigoVacaciones = "VAC";
    private const string CodigoLicenciaSinGoce = "LSG";
    private const string CodigoLicenciaConGoce = "LCG";
    private const string CodigoLicenciaPaternidad = "LPAT";
    private const string CodigoLicenciaMaternidad = "LMAT";
    private const string CodigoSubsidioEnfermedad = "SUBE";
    private const string CodigoPermiso = "PER";

    private async Task<List<FilaTareoFinalDto>> ArmarFilasTareoAsync(
        FiltroReporte filtro,
        List<TrabajadorResuelto> trabajadores,
        List<long> ids,
        CancellationToken cancelacion)
    {
        // El código del tipo de ausencia se trae junto al día para poder repartir los días
        // en las columnas DM / VACACI. / LIC. ... sin una consulta por columna.
        var dias = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.PeriodoId == filtro.PeriodoId && ids.Contains(t.TrabajadorId))
            .Select(t => new
            {
                t.TrabajadorId,
                t.Estado,
                t.TipoDiaNoLaborable,
                t.MinutosTrabajados,
                t.MinutosOrdinarios,
                t.MinutosExtra25,
                t.MinutosExtra35,
                t.MinutosNocturnos,
                t.MinutosExtra25Nocturnos,
                t.MinutosExtra35Nocturnos,
                t.MinutosTardanza,
                CodigoAusencia = contexto.Justificaciones
                    .Where(j => j.Id == t.JustificacionId)
                    .SelectMany(j => contexto.TiposAusencia
                        .Where(ta => ta.Id == j.TipoAusenciaId)
                        .Select(ta => ta.Codigo))
                    .FirstOrDefault()
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porTrabajador = dias.ToLookup(d => d.TrabajadorId);
        var numero = 0;

        return
        [
            .. trabajadores.Select(t =>
            {
                var suyos = porTrabajador[t.Id].ToList();

                // Días laborables y no laborables se separan para que las horas no se
                // cuenten dos veces: lo trabajado en domingo o feriado va íntegro al 100 %.
                var laborables = suyos
                    .Where(d => d.TipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno)
                    .ToList();

                var noLaborables = suyos
                    .Where(d => d.TipoDiaNoLaborable != TipoDiaNoLaborable.Ninguno
                                && d.MinutosTrabajados > 0)
                    .ToList();

                int DiasCon(string codigo) => suyos.Count(d => d.CodigoAusencia == codigo);

                return new FilaTareoFinalDto
                {
                    Numero = ++numero,
                    Dni = t.Dni,
                    ApellidosYNombres = t.Nombre,
                    Planta = t.Planta,
                    DescripcionCeco = t.Area,
                    CentroCosto = t.Seccion,
                    FechaIngreso = t.FechaIngreso,
                    FechaCese = t.FechaCese,
                    Observacion = t.FechaCese is null ? null : "CESE",

                    DiasFalta = suyos.Count(d => d.Estado == EstadoTareo.Falta),
                    DiasFaltaJustificada = suyos.Count(d => d.Estado == EstadoTareo.AusenciaJustificada),
                    DiasDescansoMedico = DiasCon(CodigoDescansoMedico),
                    DiasVacaciones = DiasCon(CodigoVacaciones),
                    DiasLicenciaSinGoce = DiasCon(CodigoLicenciaSinGoce),
                    DiasLicenciaConGoce = DiasCon(CodigoLicenciaConGoce),
                    DiasLicenciaPaternidad = DiasCon(CodigoLicenciaPaternidad),
                    DiasSubsidioMaternidad = DiasCon(CodigoLicenciaMaternidad),
                    DiasSubsidioEnfermedad = DiasCon(CodigoSubsidioEnfermedad),
                    DiasPermiso = DiasCon(CodigoPermiso),
                    MinutosTardanza = suyos.Sum(d => d.MinutosTardanza),

                    MinutosOrdinarios = laborables.Sum(d => d.MinutosOrdinarios),
                    MinutosExtra25 = laborables.Sum(d => d.MinutosExtra25),
                    MinutosExtra35 = laborables.Sum(d => d.MinutosExtra35),
                    MinutosNoLaborable = noLaborables.Sum(d => d.MinutosTrabajados),

                    MinutosNocturnos = suyos.Sum(d => d.MinutosNocturnos),
                    MinutosExtra25Nocturnos = laborables.Sum(d => d.MinutosExtra25Nocturnos),
                    MinutosExtra35Nocturnos = laborables.Sum(d => d.MinutosExtra35Nocturnos),
                    MinutosNoLaborableNocturnos = noLaborables.Sum(d => d.MinutosNocturnos)
                };
            })
        ];
    }

    /// <summary>
    /// Las hojas de descanso médico y de vacaciones-licencias.
    /// </summary>
    /// <remarks>
    /// Salen de las <b>solicitudes aprobadas</b> que se cruzan con el periodo, no del tareo
    /// diario: son el sustento documental de la ausencia, y necesitan el rango completo
    /// —que puede empezar antes o terminar después del mes— para que planilla sepa cuántos
    /// días caen en cada periodo.
    /// </remarks>
    private async Task<(List<FilaDescansoMedicoDto>, List<FilaVacacionesDto>, List<FilaLicenciaDto>)>
        ArmarAnexosAsync(
            Periodo periodo,
            List<TrabajadorResuelto> trabajadores,
            List<long> ids,
            string razonSocial,
            string nombreMes,
            CancellationToken cancelacion)
    {
        var porId = trabajadores.ToDictionary(t => t.Id);

        var solicitudes = await contexto.SolicitudesAusencia
            .AsNoTracking()
            .Where(s => ids.Contains(s.TrabajadorId)
                        && s.Estado == EstadoSolicitud.Aprobada
                        && s.FechaInicio <= periodo.FechaFin
                        && s.FechaFin >= periodo.FechaInicio)
            .Select(s => new
            {
                s.TrabajadorId,
                s.FechaInicio,
                s.FechaFin,
                s.Sustento,
                Codigo = contexto.TiposAusencia
                    .Where(ta => ta.Id == s.TipoAusenciaId).Select(ta => ta.Codigo).FirstOrDefault(),
                Nombre = contexto.TiposAusencia
                    .Where(ta => ta.Id == s.TipoAusenciaId).Select(ta => ta.Nombre).FirstOrDefault()
            })
            .OrderBy(s => s.FechaInicio)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        /// Días de la solicitud que caen dentro del periodo, no la duración total.
        int DiasEnPeriodo(DateOnly desde, DateOnly hasta)
        {
            var inicio = desde > periodo.FechaInicio ? desde : periodo.FechaInicio;
            var fin = hasta < periodo.FechaFin ? hasta : periodo.FechaFin;
            return fin < inicio ? 0 : fin.DayNumber - inicio.DayNumber + 1;
        }

        var descansos = solicitudes
            .Where(s => s.Codigo is CodigoDescansoMedico or CodigoSubsidioEnfermedad)
            .Select(s => new FilaDescansoMedicoDto(
                nombreMes,
                razonSocial,
                porId[s.TrabajadorId].Planta,
                porId[s.TrabajadorId].Dni,
                porId[s.TrabajadorId].Nombre,
                s.FechaInicio,
                s.FechaFin,
                DiasEnPeriodo(s.FechaInicio, s.FechaFin),
                Contingencia: null,
                Diagnostico: null,
                CentroAtencion: null,
                // El modelo distingue el descanso corto del que ya pasó a subsidio.
                Observacion: s.Codigo == CodigoSubsidioEnfermedad ? "SUBSIDIO" : "DESCANSO MEDICO"))
            .ToList();

        var numero = 0;

        var vacaciones = solicitudes
            .Where(s => s.Codigo == CodigoVacaciones)
            .Select(s =>
            {
                var dias = s.FechaFin.DayNumber - s.FechaInicio.DayNumber + 1;
                var enPeriodo = DiasEnPeriodo(s.FechaInicio, s.FechaFin);

                return new FilaVacacionesDto(
                    ++numero,
                    porId[s.TrabajadorId].Dni,
                    porId[s.TrabajadorId].Nombre,
                    dias,
                    s.FechaInicio,
                    s.FechaFin,
                    enPeriodo,
                    dias - enPeriodo,
                    s.FechaFin.AddDays(1));
            })
            .ToList();

        var licencias = solicitudes
            .Where(s => s.Codigo is CodigoLicenciaConGoce or CodigoLicenciaSinGoce
                        or CodigoLicenciaPaternidad or CodigoLicenciaMaternidad or CodigoPermiso)
            .Select(s => new FilaLicenciaDto(
                nombreMes,
                porId[s.TrabajadorId].Dni,
                porId[s.TrabajadorId].Nombre,
                s.FechaInicio,
                s.FechaFin,
                DiasEnPeriodo(s.FechaInicio, s.FechaFin),
                s.Nombre?.ToUpperInvariant() ?? "—",
                Diagnostico: null,
                CentroAtencion: null,
                Observacion: s.Sustento))
            .ToList();

        return (descansos, vacaciones, licencias);
    }
}
