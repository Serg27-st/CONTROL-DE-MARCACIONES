using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Normativa;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Ejecuta el motor de cálculo sobre un periodo y persiste el tareo.
/// </summary>
/// <remarks>
/// Este servicio solo <b>orquesta</b>: resuelve qué turno regía cada día, qué marcaciones
/// caen en la jornada y si era feriado, y le entrega todo eso a
/// <see cref="CalculadoraTareo"/>. La aritmética de horas extras y nocturnidad vive en
/// Domain, sin base de datos, y por eso se puede probar con fechas literales.
/// </remarks>
internal sealed class ServicioCalculoTareo(MarcacionDbContext contexto) : IServicioCalculoTareo
{
    /// <summary>
    /// Cuánto después del fin del turno se siguen aceptando marcaciones como salida.
    /// </summary>
    /// <remarks>
    /// En el archivo real hay salidas a las 22:06 para un turno que cierra a las 19:00.
    /// Sin este margen esas marcas quedarían fuera de la jornada y el día saldría
    /// incompleto. Es amplio pero acotado: sin tope, la salida de un día podría capturarse
    /// como entrada del siguiente.
    /// </remarks>
    private static readonly TimeSpan MargenSalida = TimeSpan.FromHours(6);

    public async Task<ResultadoRecalculo> RecalcularPeriodoAsync(
        long periodoId,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var periodo = await contexto.Periodos
            .FirstOrDefaultAsync(p => p.Id == periodoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", periodoId);

        // Invariante del sistema: un periodo cerrado no admite ninguna escritura.
        // Sin "modo administrador" y sin excepciones silenciosas.
        if (!periodo.EstaAbierto)
        {
            // El constructor arma el mensaje a partir del código: no acepta texto libre.
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        var trabajadores = await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaIngreso <= periodo.FechaFin
                        && (t.FechaCese == null || t.FechaCese >= periodo.FechaInicio))
            .Select(t => new { t.Id, t.FechaIngreso, t.FechaCese, t.PlantaId })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return new ResultadoRecalculo(
                periodo.Id, periodo.Codigo, 0, 0, 0, 0,
                new Dictionary<EstadoTareo, int>());
        }

        var ids = trabajadores.ConvertAll(t => t.Id);

        // Se trae todo de una vez y se cruza en memoria: un periodo mensual son decenas de
        // miles de combinaciones trabajador-día, y consultar por cada una arrastraría el
        // cálculo durante minutos.
        var desdeVentana = periodo.FechaInicio.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var hastaVentana = periodo.FechaFin.ToDateTime(TimeOnly.MaxValue).AddDays(1);

        var marcaciones = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => ids.Contains(m.TrabajadorId)
                        && m.FechaHora >= desdeVentana && m.FechaHora <= hastaVentana)
            .Select(m => new { m.TrabajadorId, m.FechaHora })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porTrabajador = marcaciones
            .GroupBy(m => m.TrabajadorId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.FechaHora).OrderBy(f => f).ToList());

        var asignaciones = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => ids.Contains(a.TrabajadorId))
            .Select(a => new AsignacionVigente(a.TrabajadorId, a.TurnoId, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .Include(t => t.PlantasHabilitadas)
            .ToDictionaryAsync(t => t.Id, cancelacion)
            .ConfigureAwait(false);

        var feriados = await contexto.Feriados
            .AsNoTracking()
            .Where(f => f.Activo && f.Fecha >= periodo.FechaInicio && f.Fecha <= periodo.FechaFin)
            .Select(f => new { f.Fecha, f.PlantaId })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var existentes = await contexto.TareosDiarios
            .Where(t => t.PeriodoId == periodo.Id)
            .ToDictionaryAsync(t => (t.TrabajadorId, t.Fecha), cancelacion)
            .ConfigureAwait(false);

        // La otra mitad de la ecuación: marcador + justificaciones = tareo final.
        // Se traen con el comportamiento del tipo de ausencia ya resuelto, para que el
        // cálculo puro no tenga que consultar nada.
        var justificaciones = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.PeriodoId == periodo.Id && ids.Contains(j.TrabajadorId))
            .Select(j => new JustificacionResuelta(
                j.Id,
                j.TrabajadorId,
                j.Fecha,
                j.Tipo,
                j.TipoAusenciaId == null
                    || contexto.TiposAusencia
                        .Where(t => t.Id == j.TipoAusenciaId)
                        .Select(t => t.ComputaComoAsistencia)
                        .FirstOrDefault(),
                j.MinutosJustificados,
                j.Motivo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var justificacionesPorTrabajador = justificaciones
            .GroupBy(j => j.TrabajadorId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(j => j.Fecha));

        var creados = 0;
        var actualizados = 0;
        var porEstado = new Dictionary<EstadoTareo, int>();

        foreach (var trabajador in trabajadores)
        {
            var marcasDelTrabajador = porTrabajador.GetValueOrDefault(trabajador.Id, []);

            for (var fecha = periodo.FechaInicio; fecha <= periodo.FechaFin; fecha = fecha.AddDays(1))
            {
                var turno = ResolverTurno(asignaciones, turnos, trabajador.Id, fecha);

                var entrada = new EntradaCalculo
                {
                    Fecha = fecha,
                    Turno = turno,
                    TieneVinculo = trabajador.FechaIngreso <= fecha
                                   && (trabajador.FechaCese is null || trabajador.FechaCese >= fecha),
                    EsFeriado = feriados.Any(f => f.Fecha == fecha
                                                  && (f.PlantaId is null || f.PlantaId == trabajador.PlantaId)),
                    Marcaciones = turno is null
                        ? []
                        : MarcacionesDeLaJornada(marcasDelTrabajador, turno, fecha),
                    Justificacion = ResolverJustificacion(
                        justificacionesPorTrabajador, trabajador.Id, fecha)
                };

                var resultado = CalculadoraTareo.Calcular(entrada);

                porEstado[resultado.Estado] = porEstado.GetValueOrDefault(resultado.Estado) + 1;

                if (existentes.TryGetValue((trabajador.Id, fecha), out var tareo))
                {
                    // Se actualiza en sitio, nunca se borra y se vuelve a insertar: eso es
                    // lo que hace el recálculo idempotente y conserva el id que puedan
                    // estar referenciando las justificaciones.
                    Volcar(resultado, tareo, turno, usuario);
                    actualizados++;
                }
                else
                {
                    tareo = new TareoDiario
                    {
                        PeriodoId = periodo.Id,
                        TrabajadorId = trabajador.Id,
                        Fecha = fecha,
                        CreadoPor = usuario
                    };

                    Volcar(resultado, tareo, turno, usuario);
                    await contexto.TareosDiarios.AddAsync(tareo, cancelacion).ConfigureAwait(false);
                    creados++;
                }
            }
        }

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return new ResultadoRecalculo(
            periodo.Id,
            periodo.Codigo,
            trabajadores.Count,
            creados + actualizados,
            creados,
            actualizados,
            porEstado);
    }

    public async Task<IReadOnlyList<TareoDiarioDto>> ListarAsync(
        FiltroTareo filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.TareosDiarios.AsNoTracking();

        if (filtro.PeriodoId is { } periodoId)
        {
            consulta = consulta.Where(t => t.PeriodoId == periodoId);
        }

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(t => t.TrabajadorId == trabajadorId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(t => t.Estado == estado);
        }

        return await consulta
            .OrderBy(t => t.TrabajadorId).ThenBy(t => t.Fecha)
            .Select(t => new TareoDiarioDto(
                t.Id,
                t.TrabajadorId,
                contexto.Trabajadores
                    .Where(w => w.Id == t.TrabajadorId)
                    .Select(w => w.ApellidoPaterno + " " + w.ApellidoMaterno + ", " + w.Nombres)
                    .FirstOrDefault() ?? "-",
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == t.TrabajadorId && a.FechaFin == null)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                t.Fecha,
                t.TurnoId == null
                    ? null
                    : contexto.Turnos.Where(x => x.Id == t.TurnoId).Select(x => x.Codigo).FirstOrDefault(),
                t.EntradaReal,
                t.SalidaReal,
                t.MinutosTrabajados,
                t.MinutosOrdinarios,
                t.MinutosExtra25,
                t.MinutosExtra35,
                t.MinutosNocturnos,
                t.MinutosTardanza,
                t.MinutosSalidaAnticipada,
                t.TipoDiaNoLaborable,
                t.Estado,
                t.Observacion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<LiquidacionPeriodo?> LiquidarPeriodoAsync(
        long periodoId,
        CancellationToken cancelacion = default)
    {
        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodoId, cancelacion)
            .ConfigureAwait(false);

        if (periodo is null)
        {
            return null;
        }

        var totales = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.PeriodoId == periodoId)
            .GroupBy(t => 1)
            .Select(g => new
            {
                Ordinarios = g.Sum(t => t.MinutosOrdinarios),
                Extra25 = g.Sum(t => t.MinutosExtra25),
                Extra35 = g.Sum(t => t.MinutosExtra35),
                Nocturnos = g.Sum(t => t.MinutosNocturnos),
                Descanso = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.DescansoSemanal
                    ? t.MinutosTrabajados
                    : 0),
                Feriado = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.Feriado
                    ? t.MinutosTrabajados
                    : 0)
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (totales is null)
        {
            return new LiquidacionPeriodo(
                periodo.Id, periodo.Codigo, periodo.FechaInicio, periodo.FechaFin, 0, []);
        }

        // Las tasas se resuelven por la fecha de INICIO del periodo: es la que fija con qué
        // reglas se liquida el mes completo.
        var tasas = await contexto.TasasSobretasa
            .AsNoTracking()
            .Where(t => t.Activo
                        && t.VigenteDesde <= periodo.FechaInicio
                        && (t.VigenteHasta == null || t.VigenteHasta >= periodo.FechaInicio))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        ConceptoLiquidado Armar(ConceptoSobretasa concepto, string etiqueta, int minutos)
        {
            var tasa = tasas.FirstOrDefault(t => t.Concepto == concepto);

            return new ConceptoLiquidado(
                concepto,
                etiqueta,
                minutos,
                tasa?.Porcentaje ?? 0m,
                tasa?.BaseLegal,
                tasa is not null);
        }

        return new LiquidacionPeriodo(
            periodo.Id,
            periodo.Codigo,
            periodo.FechaInicio,
            periodo.FechaFin,
            totales.Ordinarios,
            [
                Armar(ConceptoSobretasa.HoraExtraPrimerasDos, "Horas extras, primeras 2 h", totales.Extra25),
                Armar(ConceptoSobretasa.HoraExtraAdicionales, "Horas extras adicionales", totales.Extra35),
                Armar(ConceptoSobretasa.TrabajoNocturno, "Trabajo nocturno (22:00–06:00)", totales.Nocturnos),
                Armar(ConceptoSobretasa.DescansoSemanalTrabajado, "Descanso semanal trabajado", totales.Descanso),
                Armar(ConceptoSobretasa.FeriadoTrabajado, "Feriado trabajado", totales.Feriado)
            ]);
    }

    // ------------------------------------------------------------------

    private static void Volcar(ResultadoCalculo resultado, TareoDiario tareo, Turno? turno, string usuario)
    {
        tareo.TurnoId = turno?.Id;
        tareo.EntradaReal = resultado.EntradaReal;
        tareo.SalidaReal = resultado.SalidaReal;
        tareo.MinutosTrabajados = resultado.MinutosTrabajados;
        tareo.MinutosOrdinarios = resultado.MinutosOrdinarios;
        tareo.MinutosExtra25 = resultado.MinutosExtra25;
        tareo.MinutosExtra35 = resultado.MinutosExtra35;
        tareo.MinutosNocturnos = resultado.MinutosNocturnos;
        tareo.MinutosExtra25Nocturnos = resultado.MinutosExtra25Nocturnos;
        tareo.MinutosExtra35Nocturnos = resultado.MinutosExtra35Nocturnos;
        tareo.MinutosTardanza = resultado.MinutosTardanza;
        tareo.MinutosSalidaAnticipada = resultado.MinutosSalidaAnticipada;
        tareo.TipoDiaNoLaborable = resultado.TipoDiaNoLaborable;
        tareo.Estado = resultado.Estado;
        tareo.JustificacionId = resultado.JustificacionId;
        tareo.Observacion = resultado.Observacion;
        tareo.CalculadoEn = DateTime.Now;
        tareo.ModificadoPor = usuario;
    }

    /// <summary>
    /// Turno vigente para ese trabajador en esa fecha, según la asignación con vigencia.
    /// </summary>
    /// <remarks>
    /// Nunca se resuelve por "el último turno del trabajador": los obreros rotan, y usar
    /// el turno actual recalcularía mal todos los meses anteriores.
    /// </remarks>
    private static Turno? ResolverTurno(
        List<AsignacionVigente> asignaciones,
        Dictionary<long, Turno> turnos,
        long trabajadorId,
        DateOnly fecha)
    {
        var vigente = asignaciones.FirstOrDefault(
            a => a.TrabajadorId == trabajadorId
                 && a.FechaInicio <= fecha
                 && (a.FechaFin is null || a.FechaFin >= fecha));

        return vigente is null ? null : turnos.GetValueOrDefault(vigente.TurnoId);
    }

    /// <summary>Asignación de turno con su vigencia, para resolver en memoria.</summary>
    private sealed record AsignacionVigente(
        long TrabajadorId,
        long TurnoId,
        DateOnly FechaInicio,
        DateOnly? FechaFin);

    /// <summary>
    /// Justificación con lo mínimo para resolver en memoria qué día toca.
    /// </summary>
    /// <remarks>
    /// Es el par trabajador-día el que indexa la justificación, así que estos dos campos
    /// viajan aquí aunque <see cref="JustificacionAplicable"/> no los lleve: se descartan
    /// al convertir.
    /// </remarks>
    private sealed record JustificacionResuelta(
        long Id,
        long TrabajadorId,
        DateOnly Fecha,
        TipoJustificacion Tipo,
        bool ComputaComoAsistencia,
        int? MinutosJustificados,
        string Motivo);

    /// <summary>
    /// Resuelve la justificación vigente para un trabajador en una fecha.
    /// </summary>
    /// <remarks>
    /// Actualmente solo soporta una justificación por día (la primera que encuentra).
    /// Si GCA requiere múltiples justificaciones en un mismo día (tardanza + marca
    /// incompleta), habrá que cambiarlo por una lista y que el cálculo las itere.
    /// </remarks>
    private static JustificacionAplicable? ResolverJustificacion(
        Dictionary<long, Dictionary<DateOnly, JustificacionResuelta>> porTrabajador,
        long trabajadorId,
        DateOnly fecha)
    {
        if (!porTrabajador.TryGetValue(trabajadorId, out var porFecha))
        {
            return null;
        }

        if (!porFecha.TryGetValue(fecha, out var justificacion))
        {
            return null;
        }

        return new JustificacionAplicable
        {
            Id = justificacion.Id,
            Tipo = justificacion.Tipo,
            ComputaComoAsistencia = justificacion.ComputaComoAsistencia,
            MinutosJustificados = justificacion.MinutosJustificados,
            Motivo = justificacion.Motivo
        };
    }

    /// <summary>
    /// Marcaciones que caen dentro de la ventana de la jornada de ese día.
    /// </summary>
    /// <remarks>
    /// La ventana arranca en la anticipación de entrada configurada en el turno y termina
    /// un margen después del cierre, para no perder las salidas con sobretiempo. En un
    /// turno que cruza medianoche, el fin cae al día siguiente.
    /// </remarks>
    private static List<DateTime> MarcacionesDeLaJornada(
        List<DateTime> marcaciones,
        Turno turno,
        DateOnly fecha)
    {
        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);

        var finTurno = turno.CruzaMedianoche
            ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
            : fecha.ToDateTime(turno.HoraFin);

        var desde = inicioTurno.AddMinutes(-turno.MinutosAnticipacionEntrada);
        var hasta = finTurno.Add(MargenSalida);

        return marcaciones.Where(m => m >= desde && m <= hasta).ToList();
    }
}
