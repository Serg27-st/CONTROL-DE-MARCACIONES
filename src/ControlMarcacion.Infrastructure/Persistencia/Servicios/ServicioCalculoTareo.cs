using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Personal;
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
internal sealed class ServicioCalculoTareo(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioCalculoTareo
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

        var consultaTrabajadores = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaIngreso <= periodo.FechaFin
                        && (t.FechaCese == null || t.FechaCese >= periodo.FechaInicio));

        // Aislamiento por planta: un ADMINISTRADOR recalcula SOLO sus plantas. El recálculo es
        // idempotente por (trabajador, día), así que procesar un subconjunto no corrompe nada
        // —refresca sus plantas y deja las demás como estaban—; además evita que dispare el
        // recálculo de la planilla de la otra razón social y que el conteo del resultado mezcle
        // plantas. COORDINADOR (sin restricción) sigue recalculando el periodo entero.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consultaTrabajadores = consultaTrabajadores.Where(t => plantasVisibles.Contains(t.PlantaId));
        }

        var trabajadores = await consultaTrabajadores
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

        // Las marcaciones de una carga anulada existen y se conservan, pero no cuentan.
        // Es la única forma de deshacer una importación equivocada sin borrar el dato
        // duro del sistema: el archivo sigue ahí, sus filas siguen consultables, y lo
        // único que desaparece es su efecto sobre lo que se paga.
        var anuladas = contexto.Importaciones
            .Where(i => i.Anulada)
            .Select(i => i.Id);

        var marcaciones = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => ids.Contains(m.TrabajadorId)
                        && m.FechaHora >= desdeVentana && m.FechaHora <= hastaVentana
                        && !anuladas.Contains(m.ImportacionId))
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

            // Hasta dónde llegó la jornada del día anterior. Se siembra con el día previo
            // al periodo para que el día 1 del mes quede tan protegido como los demás:
            // si el trabajador venía de turno nocturno, su salida cae dentro del periodo.
            var anterior = periodo.FechaInicio.AddDays(-1);

            var finJornadaAnterior = FinJornadaTeorica(
                ResolverTurno(asignaciones, turnos, trabajador.Id, anterior), anterior);

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
                        : MarcacionesDeLaJornada(marcasDelTrabajador, turno, fecha, finJornadaAnterior),
                    Justificacion = ResolverJustificacion(
                        justificacionesPorTrabajador, trabajador.Id, fecha)
                };

                var resultado = CalculadoraTareo.Calcular(entrada);

                // La jornada de este día ya reclamó sus marcas: el día siguiente arranca
                // después de ellas. Se toma la salida real cuando existe y el fin teórico
                // del turno cuando no, para que un día sin marcar no reabra la ventana.
                finJornadaAnterior = MasTardia(resultado.SalidaReal, FinJornadaTeorica(turno, fecha));

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

    public async Task<ResultadoPaginado<TareoDeTrabajador>> ListarPorTrabajadorAsync(
        FiltroTareo filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var tamanio = Math.Clamp(filtro.TamanioPagina, 1, 100);
        var pagina = Math.Max(filtro.Pagina, 1);

        var consulta = await Filtrar(filtro, aplicarEstado: true, cancelacion).ConfigureAwait(false);

        // Primero QUIÉNES entran en la página. Se ordena por apellido y no por Id: el Id es
        // el orden en que se dieron de alta, que para quien lee el tareo es arbitrario.
        var personas = consulta
            .Select(x => new
            {
                x.Tareo.TrabajadorId,
                x.Trabajador.ApellidoPaterno,
                x.Trabajador.ApellidoMaterno,
                x.Trabajador.Nombres
            })
            .Distinct();

        var total = await personas.CountAsync(cancelacion).ConfigureAwait(false);

        var idsDeLaPagina = await personas
            .OrderBy(p => p.ApellidoPaterno)
            .ThenBy(p => p.ApellidoMaterno)
            .ThenBy(p => p.Nombres)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(p => p.TrabajadorId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (idsDeLaPagina.Count == 0)
        {
            return new ResultadoPaginado<TareoDeTrabajador>([], total, pagina, tamanio);
        }

        // Y ahora TODOS sus días, sin recortar: es lo que hace que los totales por persona
        // sean completos y que su mes no aparezca partido.
        var filas = await consulta
            .Where(x => idsDeLaPagina.Contains(x.Tareo.TrabajadorId))
            .OrderBy(x => x.Trabajador.ApellidoPaterno)
            .ThenBy(x => x.Trabajador.ApellidoMaterno)
            .ThenBy(x => x.Trabajador.Nombres)
            .ThenBy(x => x.Tareo.Fecha)
            .Select(x => new TareoDiarioDto(
                x.Tareo.Id,
                x.Tareo.TrabajadorId,
                x.Trabajador.ApellidoPaterno + " " + x.Trabajador.ApellidoMaterno + ", " + x.Trabajador.Nombres,
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == x.Tareo.TrabajadorId && a.FechaFin == null)
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                x.Tareo.Fecha,
                x.Tareo.TurnoId == null
                    ? null
                    : contexto.Turnos.Where(u => u.Id == x.Tareo.TurnoId).Select(u => u.Codigo).FirstOrDefault(),
                x.Tareo.EntradaReal,
                x.Tareo.SalidaReal,
                x.Tareo.MinutosTrabajados,
                x.Tareo.MinutosOrdinarios,
                x.Tareo.MinutosExtra25,
                x.Tareo.MinutosExtra35,
                x.Tareo.MinutosNocturnos,
                x.Tareo.MinutosTardanza,
                x.Tareo.MinutosSalidaAnticipada,
                x.Tareo.TipoDiaNoLaborable,
                x.Tareo.Estado,
                x.Tareo.Observacion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // El agrupado conserva el orden de las filas, que ya viene por apellido y fecha
        // desde SQL; GroupBy de LINQ to Objects respeta el orden de aparición.
        var grupos = filas
            .GroupBy(f => f.TrabajadorId)
            .Select(g => new TareoDeTrabajador(
                g.Key,
                g.First().Trabajador,
                g.First().IdMarcador,
                [.. g]))
            .ToList();

        return new ResultadoPaginado<TareoDeTrabajador>(grupos, total, pagina, tamanio);
    }

    public async Task<ResumenTareo> ResumirAsync(
        FiltroTareo filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Sin el estado: el reparto por estado ES el resumen, y filtrarlo por el estado
        // elegido dejaría una sola barra con el número que el usuario acaba de pedir.
        var consulta = await Filtrar(filtro, aplicarEstado: false, cancelacion).ConfigureAwait(false);

        var porEstado = await consulta
            .GroupBy(x => x.Tareo.Estado)
            .Select(g => new { Estado = g.Key, Conteo = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Conteo, cancelacion)
            .ConfigureAwait(false);

        if (porEstado.Count == 0)
        {
            return ResumenTareo.Vacio;
        }

        var totales = await consulta
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Dias = g.Count(),
                Trabajadores = g.Select(x => x.Tareo.TrabajadorId).Distinct().Count(),
                Ordinarios = g.Sum(x => x.Tareo.MinutosOrdinarios),
                Extra25 = g.Sum(x => x.Tareo.MinutosExtra25),
                Extra35 = g.Sum(x => x.Tareo.MinutosExtra35),
                Nocturnos = g.Sum(x => x.Tareo.MinutosNocturnos),
                Tardanza = g.Sum(x => x.Tareo.MinutosTardanza),
                SalidaAnticipada = g.Sum(x => x.Tareo.MinutosSalidaAnticipada)
            })
            .FirstAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResumenTareo(
            porEstado,
            totales.Dias,
            totales.Trabajadores,
            totales.Ordinarios,
            totales.Extra25,
            totales.Extra35,
            totales.Nocturnos,
            totales.Tardanza,
            totales.SalidaAnticipada);
    }

    /// <summary>
    /// Tareos cruzados con su trabajador, ya filtrados. Base común del listado y el resumen.
    /// </summary>
    /// <remarks>
    /// El cruce es explícito y no por subconsulta correlacionada porque hace falta para
    /// dos cosas a la vez: buscar por apellidos o documento, y <b>ordenar</b> por ellos.
    /// Una subconsulta en el ORDER BY se evalúa fila a fila.
    ///
    /// Que sea un <c>join</c> y no un <c>left join</c> es intencionado: un tareo sin
    /// trabajador es un dato roto y debe notarse, no colarse con el nombre en blanco.
    /// </remarks>
    private async Task<IQueryable<TareoConTrabajador>> Filtrar(
        FiltroTareo filtro, bool aplicarEstado, CancellationToken cancelacion)
    {
        var consulta =
            from t in contexto.TareosDiarios.AsNoTracking()
            join w in contexto.Trabajadores.AsNoTracking() on t.TrabajadorId equals w.Id
            select new TareoConTrabajador { Tareo = t, Trabajador = w };

        // Aislamiento por planta: el tareo visible —listado y resumen, que salen los dos de
        // aquí— es solo el de las plantas del usuario. Se aprovecha el join al trabajador.
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantas is not null)
        {
            consulta = consulta.Where(x => plantas.Contains(x.Trabajador.PlantaId));
        }

        if (filtro.PeriodoId is { } periodoId)
        {
            consulta = consulta.Where(x => x.Tareo.PeriodoId == periodoId);
        }

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(x => x.Tareo.TrabajadorId == trabajadorId);
        }

        if (aplicarEstado && filtro.Estado is { } estado)
        {
            consulta = consulta.Where(x => x.Tareo.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var texto = filtro.Busqueda.Trim();

            consulta = consulta.Where(x =>
                x.Trabajador.ApellidoPaterno.Contains(texto)
                || x.Trabajador.ApellidoMaterno.Contains(texto)
                || x.Trabajador.Nombres.Contains(texto)
                || x.Trabajador.NumeroDocumento.Contains(texto));
        }

        return consulta;
    }

    /// <summary>Par tareo-trabajador del cruce interno. No sale de esta clase.</summary>
    private sealed class TareoConTrabajador
    {
        public required TareoDiario Tareo { get; init; }

        public required Trabajador Trabajador { get; init; }
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

        var consultaTareos = contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.PeriodoId == periodoId);

        // Aislamiento por planta: la liquidación que ve un ADMINISTRADOR suma solo sus plantas,
        // no las de la otra razón social. Se muestra en la misma pantalla que el listado ya
        // acotado (/Asistencia), así que sus totales tienen que coincidir con lo que ve.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consultaTareos = consultaTareos.Where(t => contexto.Trabajadores
                .Where(w => plantasVisibles.Contains(w.PlantaId))
                .Select(w => w.Id)
                .Contains(t.TrabajadorId));
        }

        var totales = await consultaTareos
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
    ///
    /// <para>El <c>OrderByDescending</c> es una <b>red de seguridad</b>, no el mecanismo:
    /// las asignaciones no deben solaparse y <see cref="ValidadorVigencias"/> lo impide al
    /// guardar. Pero si alguna vez llegara un solapamiento a la base —una carga a medias,
    /// un arreglo manual—, gana la asignación que empezó después, que es la última decisión
    /// tomada. Sin orden explícito el ganador lo decidía el orden de lectura de la tabla, y
    /// el mismo periodo podía recalcularse distinto sin que nada hubiera cambiado.</para>
    /// </remarks>
    private static Turno? ResolverTurno(
        List<AsignacionVigente> asignaciones,
        Dictionary<long, Turno> turnos,
        long trabajadorId,
        DateOnly fecha)
    {
        var vigente = asignaciones
            .Where(a => a.TrabajadorId == trabajadorId
                        && a.FechaInicio <= fecha
                        && (a.FechaFin is null || a.FechaFin >= fecha))
            .OrderByDescending(a => a.FechaInicio)
            .FirstOrDefault();

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
    ///
    /// <para><b>Una marca pertenece a un solo día.</b> Por eso se descartan las anteriores
    /// a <paramref name="finJornadaAnterior"/>: en el cambio de rotación de nocturno a
    /// diurno, la salida de la última noche (07:00) cae también dentro de la ventana de
    /// entrada del turno diurno de ese mismo día, que abre a las 06:45. Sin este corte la
    /// misma marca se contaba dos veces —como salida del miércoles y como entrada del
    /// jueves— y el día de la rotación salía «Incompleto» para todo el personal que
    /// rotaba, con una entrada que nunca ocurrió.</para>
    /// </remarks>
    private static List<DateTime> MarcacionesDeLaJornada(
        List<DateTime> marcaciones,
        Turno turno,
        DateOnly fecha,
        DateTime? finJornadaAnterior)
    {
        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);
        var finTurno = FinJornadaTeorica(turno, fecha)!.Value;

        var desde = inicioTurno.AddMinutes(-turno.MinutosAnticipacionEntrada);
        var hasta = finTurno.Add(MargenSalida);

        return marcaciones
            .Where(m => m >= desde
                        && m <= hasta
                        && (finJornadaAnterior is null || m > finJornadaAnterior.Value))
            .ToList();
    }

    /// <summary>
    /// Momento en que termina la jornada de ese turno en esa fecha, sin mirar marcaciones.
    /// </summary>
    /// <remarks>
    /// En un turno que cruza medianoche cae al día siguiente: la jornada se imputa al día
    /// en que <b>inicia</b> el turno. Nulo cuando no había turno asignado, que es lo que
    /// deja la ventana del día siguiente sin recortar.
    /// </remarks>
    private static DateTime? FinJornadaTeorica(Turno? turno, DateOnly fecha) =>
        turno is null
            ? null
            : turno.CruzaMedianoche
                ? fecha.AddDays(1).ToDateTime(turno.HoraFin)
                : fecha.ToDateTime(turno.HoraFin);

    /// <summary>La más tardía de las dos, tolerando nulos en cualquiera de los lados.</summary>
    private static DateTime? MasTardia(DateTime? izquierda, DateTime? derecha) =>
        izquierda is null ? derecha
        : derecha is null ? izquierda
        : izquierda.Value >= derecha.Value ? izquierda
        : derecha;
}
