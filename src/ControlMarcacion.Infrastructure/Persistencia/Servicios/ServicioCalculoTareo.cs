using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
internal sealed partial class ServicioCalculoTareo(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance,
    IServicioImportacion importacion,
    ILogger<ServicioCalculoTareo> registro) : IServicioCalculoTareo
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
    private readonly ConsultaContextoLaboral consultaContextoLaboral = new(contexto);

    public async Task<ResultadoRecalculo> RecalcularPeriodoAsync(
        long periodoId,
        string usuario,
        CancellationToken cancelacion = default) =>
        await RecalcularAsync(periodoId, null, null, usuario, cancelacion).ConfigureAwait(false);

    public async Task<ResultadoRecalculo> RecalcularJornadaAsync(
        long periodoId,
        long trabajadorId,
        DateOnly fecha,
        string usuario,
        CancellationToken cancelacion = default) =>
        await RecalcularAsync(periodoId, trabajadorId, fecha, usuario, cancelacion).ConfigureAwait(false);

    private async Task<ResultadoRecalculo> RecalcularAsync(
        long periodoId,
        long? trabajadorObjetivo,
        DateOnly? fechaObjetivo,
        string usuario,
        CancellationToken cancelacion)
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

        if (fechaObjetivo is { } fechaFuera
            && (fechaFuera < periodo.FechaInicio || fechaFuera > periodo.FechaFin))
        {
            throw new ReglaNegocioException(
                $"La fecha {fechaFuera:dd/MM/yyyy} no pertenece al periodo {periodo.Codigo}.");
        }

        // Aislamiento por planta: un ADMINISTRADOR recalcula SOLO sus plantas. El recálculo es
        // idempotente por (trabajador, día), así que procesar un subconjunto no corrompe nada
        // —refresca sus plantas y deja las demás como estaban—; además evita que dispare el
        // recálculo de la planilla de la otra razón social y que el conteo del resultado mezcle
        // plantas. COORDINADOR (sin restricción) sigue recalculando el periodo entero.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        // 3C-4: la población sale de los vínculos que se solapan con el periodo, no de la
        // fotografía plana/actual de Trabajador. La consulta se hace una sola vez y la
        // resolución diaria ocurre en memoria para evitar un N+1 trabajador-día.
        var historiasCargadas = await consultaContextoLaboral
            .CargarRangoAsync(periodo.FechaInicio, periodo.FechaFin, cancelacion: cancelacion)
            .ConfigureAwait(false);

        var historias = historiasCargadas
            .Where(par => trabajadorObjetivo is null || par.Key == trabajadorObjetivo.Value)
            .Where(par => plantasVisibles is null
                          || par.Value.EnRango(periodo.FechaInicio, periodo.FechaFin)
                              .SelectMany(p => p.Plantas)
                              .Any(a => a.FechaInicio <= periodo.FechaFin
                                        && (a.FechaFin is null || a.FechaFin >= periodo.FechaInicio)
                                        && plantasVisibles.Contains(a.PlantaId)))
            .ToDictionary(par => par.Key, par => par.Value);

        var idsHistoricos = historias.Keys.ToList();
        var trabajadores = await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => idsHistoricos.Contains(t.Id))
            .Select(t => new { t.Id })
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

        // La marcación no guarda de quién es: se atribuye aquí, con la asignación de ID
        // vigente en su fecha (AtribucionMarcaciones). Primero las vigencias de estos
        // trabajadores, luego las marcaciones de esos (aparato, número) en la ventana, y el
        // cruce en memoria. Así un ID asignado o corregido después de cargar el archivo
        // cuenta con solo recalcular.
        var vigenciasId = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => ids.Contains(a.TrabajadorId))
            .Select(a => new VigenciaIdMarcador(a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var equiposConId = vigenciasId.Select(v => v.EquipoId).Distinct().ToList();
        var numerosConId = vigenciasId.Select(v => v.IdMarcador).Distinct().ToList();

        var marcaciones = vigenciasId.Count == 0
            ? []
            : await contexto.MarcacionesCrudas
                .AsNoTracking()
                .Where(m => equiposConId.Contains(m.EquipoId)
                            && numerosConId.Contains(m.IdMarcador)
                            && m.FechaHora >= desdeVentana && m.FechaHora <= hastaVentana
                            && !anuladas.Contains(m.ImportacionId))
                .Select(m => new { m.Id, m.EquipoId, m.IdMarcador, m.FechaHora })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

        var atribucion = new AtribucionMarcaciones(vigenciasId);

        // La otra dirección de la misma vigencia: de quién es el número sirve para atribuir
        // marcaciones, y de qué aparato es la persona sirve para saber si su archivo llegó.
        var vigenciasPorTrabajador = vigenciasId
            .GroupBy(v => v.TrabajadorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var porTrabajador = marcaciones
            .Select(m => new
            {
                TrabajadorId = atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora),
                Marca = new MarcaAtribuida(m.Id, m.FechaHora)
            })
            .Where(m => m.TrabajadorId is not null)
            .GroupBy(m => m.TrabajadorId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Marca).OrderBy(m => m.FechaHora).ToList());

        var asignaciones = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => ids.Contains(a.TrabajadorId))
            .Select(a => new AsignacionVigente(a.TrabajadorId, a.TurnoId, a.CuadrillaId, a.FechaInicio, a.FechaFin))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // Quien integra una cuadrilla no tiene turno propio: el de cada día sale del ciclo que
        // la cuadrilla tenía programado en esa fecha. Solo las programaciones activas; una
        // reemplazada entera quedó de baja y ya no cuenta.
        var cuadrillaIds = asignaciones
            .Where(a => a.CuadrillaId is not null)
            .Select(a => a.CuadrillaId!.Value)
            .Distinct()
            .ToList();

        var programaciones = cuadrillaIds.Count == 0
            ? []
            : await contexto.ProgramacionesCuadrilla
                .AsNoTracking()
                .Include(p => p.Tramos)
                .Include(p => p.Cuadrilla)
                .Where(p => p.Activo
                            && cuadrillaIds.Contains(p.CuadrillaId)
                            && p.FechaInicio <= periodo.FechaFin.AddDays(1)
                            && (p.FechaFin == null || p.FechaFin >= periodo.FechaInicio.AddDays(-1)))
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

        var programacionesPorCuadrilla = programaciones
            .GroupBy(p => p.CuadrillaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.FechaInicio).ToList());

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .Include(t => t.PlantasHabilitadas)
            .ToDictionaryAsync(t => t.Id, cancelacion)
            .ConfigureAwait(false);

        // El horario del turno también tiene vigencia por fecha: recalcular julio usa el
        // horario que regía en julio, no el que alguien dejó en el catálogo en octubre. Se
        // traen las versiones que se solapan con el periodo, más las que cubren el día
        // previo, que es el que siembra la jornada nocturna de arrastre.
        var versiones = await contexto.VersionesTurno
            .AsNoTracking()
            .Where(v => v.Activo
                        && v.FechaInicio <= periodo.FechaFin.AddDays(1)
                        && (v.FechaFin == null || v.FechaFin >= periodo.FechaInicio.AddDays(-1)))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var horarios = new HorariosPorFecha(turnos, versiones);

        // Candidatos para detectar que el turno asignado no es el que se trabajó: los
        // habilitados en la planta de la persona. Un turno sin plantas habilitadas aplica a
        // todas. No cambia ni un minuto del cálculo —solo señala el día—, así que se arma
        // una vez por planta y se reparte.
        var candidatosPorPlanta = historias.Values
            .SelectMany(h => h.EnRango(periodo.FechaInicio, periodo.FechaFin))
            .SelectMany(p => p.Plantas)
            .Where(a => a.FechaInicio <= periodo.FechaFin
                        && (a.FechaFin is null || a.FechaFin >= periodo.FechaInicio))
            .Select(a => a.PlantaId)
            .Distinct()
            .ToDictionary(
                plantaId => plantaId,
                plantaId => (IReadOnlyList<Turno>)turnos.Values
                    .Where(t => t.Activo
                                && (t.PlantasHabilitadas.Count == 0
                                    || t.PlantasHabilitadas.Any(p => p.PlantaId == plantaId)))
                    .ToList());

        // Qué aparatos no entregaron archivo cada día. Sale del módulo de Marcaciones, que
        // es donde vive la única regla de cobertura del sistema: si el motor tuviera la
        // suya, el calendario podría decir «mes cubierto» mientras el tareo saca faltas de
        // los días que faltan.
        var aparatosSinCarga = await importacion
            .AparatosSinCargaAsync(periodo.FechaInicio, periodo.FechaFin, cancelacion)
            .ConfigureAwait(false);

        var feriados = await contexto.Feriados
            .AsNoTracking()
            .Where(f => f.Activo && f.Fecha >= periodo.FechaInicio && f.Fecha <= periodo.FechaFin)
            .Select(f => new { f.Fecha, f.PlantaId })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // FER-003: el descanso existe solo si fue otorgado explícitamente. Se carga como
        // otra entrada del motor; no se infiere de una ausencia ni modifica el tareo del
        // feriado origen.
        var descansosSustitutorios = await contexto.DescansosSustitutorios
            .AsNoTracking()
            .Where(d => d.Activo
                        && d.Estado == EstadoDescansoSustitutorio.Otorgado
                        && ids.Contains(d.TrabajadorId)
                        && d.FechaDescanso >= periodo.FechaInicio
                        && d.FechaDescanso <= periodo.FechaFin)
            .Select(d => new { d.TrabajadorId, d.FechaDescanso, d.FechaFeriadoOrigen })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
        var descansosPorDia = descansosSustitutorios.ToDictionary(
            d => (d.TrabajadorId, d.FechaDescanso),
            d => d.FechaFeriadoOrigen);

        var existentes = await contexto.TareosDiarios
            .Where(t => t.PeriodoId == periodo.Id)
            .ToDictionaryAsync(t => (t.TrabajadorId, t.Fecha), cancelacion)
            .ConfigureAwait(false);

        // El catálogo entero son nueve filas: se trae una vez y se cruza en memoria. Antes
        // cada justificación resolvía su tipo con una subconsulta correlacionada dentro de
        // la proyección, y esa subconsulta traía un solo campo, así que el comportamiento
        // —lo que de verdad decide si el día se paga— no llegaba al motor.
        var tiposAusencia = await contexto.TiposAusencia
            .AsNoTracking()
            .Select(t => new { t.Id, t.ComputaComoAsistencia, t.Comportamiento })
            .ToDictionaryAsync(
                t => t.Id,
                t => new TipoAusenciaResuelto(t.ComputaComoAsistencia, t.Comportamiento),
                cancelacion)
            .ConfigureAwait(false);

        // La otra mitad de la ecuación: marcador + justificaciones = tareo final.
        // El permiso por horas viaja con su ventana, tomada de la solicitud que lo generó:
        // es lo que permite al motor ubicarlo contra el turno en vez de suponer que todo
        // permiso empieza con la jornada.
        var justificaciones = await contexto.Justificaciones
            .AsNoTracking()
            // ⚠️ Solo las APROBADAS modifican el tareo. Una regularización pedida y todavía
            // no autorizada que ya cambiara el día convertiría la aprobación en un trámite
            // decorativo, y una rechazada seguiría surtiendo efecto pese al rechazo.
            .Where(j => j.PeriodoId == periodo.Id
                        && ids.Contains(j.TrabajadorId)
                        && j.Estado == EstadoJustificacion.Aprobada)
            .Select(j => new JustificacionResuelta(
                j.Id,
                j.TrabajadorId,
                j.Fecha,
                j.Tipo,
                j.TipoAusenciaId,
                j.MinutosJustificados,
                j.EntradaRegularizada,
                j.SalidaRegularizada,
                contexto.SolicitudesAusencia
                    .Where(s => s.Id == j.SolicitudAusenciaId)
                    .Select(s => s.HoraInicio)
                    .FirstOrDefault(),
                contexto.SolicitudesAusencia
                    .Where(s => s.Id == j.SolicitudAusenciaId)
                    .Select(s => s.HoraFin)
                    .FirstOrDefault(),
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
            var historia = historias[trabajador.Id];
            var marcasDelTrabajador = porTrabajador.GetValueOrDefault(trabajador.Id, []);

            // Marcas que alguna jornada ya reclamó. Sirve para lo contrario de lo que
            // parece: las que al final del día NO están aquí son las que el turno dejó
            // fuera, y esas son las que hay que declarar en vez de descartar en silencio.
            var reclamadas = new HashSet<long>();
            var desdeResolucion = periodo.FechaInicio.AddDays(-1);
            var hastaResolucion = periodo.FechaFin.AddDays(1);
            var resoluciones = new Dictionary<DateOnly, TurnoResuelto>();

            for (var dia = desdeResolucion; dia <= hastaResolucion; dia = dia.AddDays(1))
            {
                resoluciones[dia] = ResolverTurno(
                    asignaciones, horarios, programacionesPorCuadrilla, trabajador.Id, dia);
            }

            // BD-01, fase 1: todas las jornadas con turno reclaman antes que cualquier
            // descanso. Incluye D-1 y D+1 para proteger la salida nocturna anterior y la
            // ventana natural de la siguiente jornada.
            var marcasDeJornadas = new Dictionary<DateOnly, List<MarcaAtribuida>>();
            DateTime? finJornadaAnterior = null;

            for (var dia = desdeResolucion; dia <= hastaResolucion; dia = dia.AddDays(1))
            {
                var resuelto = resoluciones[dia];
                if (resuelto.Estado != EstadoProgramacionDiaria.Trabajo || resuelto.Turno is null)
                {
                    continue;
                }

                var seleccionadas = MarcacionesDeLaJornada(
                    marcasDelTrabajador, resuelto.Turno, dia, finJornadaAnterior, reclamadas);
                marcasDeJornadas[dia] = seleccionadas;
                reclamadas.UnionWith(seleccionadas.Select(m => m.Id));
                finJornadaAnterior = MasTardia(
                    seleccionadas.LastOrDefault()?.FechaHora,
                    FinJornadaTeorica(resuelto.Turno, dia));
            }

            for (var fecha = periodo.FechaInicio; fecha <= periodo.FechaFin; fecha = fecha.AddDays(1))
            {
                if (fechaObjetivo is { } objetivo && fecha != objetivo)
                {
                    continue;
                }

                var contextoLaboral = historia.Resolver(fecha).ObtenerOError();

                // Un administrador restringido solo puede reescribir días cuya planta
                // histórica está dentro de su alcance. Un día sin vínculo no hereda la
                // planta actual como atajo de autorización.
                if (plantasVisibles is not null
                    && (contextoLaboral?.PlantaId is not { } plantaVisible
                        || !plantasVisibles.Contains(plantaVisible)))
                {
                    continue;
                }

                var resuelto = resoluciones[fecha];
                var turno = resuelto.Turno;
                var esDescansoSustitutorio = descansosPorDia.TryGetValue(
                    (trabajador.Id, fecha), out var fechaFeriadoCompensado);
                List<MarcaAtribuida> deLaJornada;
                var delimitacionAmbigua = false;

                if (resuelto.Estado == EstadoProgramacionDiaria.DescansoProgramado)
                {
                    // BD-01, fase 2: solo el residuo no reclamado puede formar el intervalo
                    // del descanso. Su primera marca siempre pertenece al día natural D.
                    var delimitacion = DelimitarDescanso(
                        marcasDelTrabajador, reclamadas, fecha, resoluciones);
                    deLaJornada = delimitacion.Marcas;
                    delimitacionAmbigua = delimitacion.Ambigua;
                    reclamadas.UnionWith(deLaJornada.Select(m => m.Id));
                }
                else
                {
                    deLaJornada = marcasDeJornadas.GetValueOrDefault(fecha, []);
                }

                var entrada = new EntradaCalculo
                {
                    Fecha = fecha,
                    Turno = turno,
                    OrigenTurno = resuelto.Origen,
                    MotivoSinTurno = resuelto.MotivoSinTurno,
                    EsDescansoProgramado = resuelto.Estado == EstadoProgramacionDiaria.DescansoProgramado,
                    EsTrabajoProgramadoEnRotacion =
                        resuelto.Estado == EstadoProgramacionDiaria.Trabajo
                        && resuelto.ProgramacionId is not null,
                    EsDescansoSustitutorio = esDescansoSustitutorio,
                    FechaFeriadoCompensado = esDescansoSustitutorio
                        ? fechaFeriadoCompensado
                        : null,
                    DelimitacionDescansoAmbigua = delimitacionAmbigua,
                    TieneVinculo = contextoLaboral is not null,
                    EsFeriado = feriados.Any(f => f.Fecha == fecha
                                                  && (f.PlantaId is null || f.PlantaId == contextoLaboral?.PlantaId)),
                    Marcaciones = deLaJornada.Select(m => m.FechaHora).ToList(),
                    MarcacionesFueraVentana = SinReclamar(marcasDelTrabajador, reclamadas, fecha),
                    SinCoberturaDeCarga = SinCobertura(vigenciasPorTrabajador, aparatosSinCarga, trabajador.Id, fecha),
                    TurnosCandidatos = contextoLaboral?.PlantaId is { } plantaId
                        ? candidatosPorPlanta.GetValueOrDefault(plantaId, [])
                        : [],
                    // La justificación histórica se conserva en su tabla, pero fuera de un
                    // vínculo no se reinterpreta como asistencia ni crea minutos pagados.
                    Justificacion = contextoLaboral is null
                        ? null
                        : ResolverJustificacion(
                            justificacionesPorTrabajador, tiposAusencia, trabajador.Id, fecha)
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

        // La comprobación de «está abierto» se hizo al empezar y desde entonces han pasado
        // decenas de miles de filas: si alguien cerró el mes mientras tanto, nada de esto
        // puede escribirse. Afirmar la versión del periodo mete esa comprobación DENTRO del
        // guardado —va en el WHERE del UPDATE—, y como todo el lote viaja en una sola
        // transacción, o entra completo o no entra nada.
        contexto.AfirmarAbierto(periodo);

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw await contexto
                .ExplicarChoqueAsync(periodo.Id, "recalculaba el tareo", cancelacion)
                .ConfigureAwait(false);
        }

        // El recálculo es la operación que reescribe el tareo entero de un periodo y no deja
        // rastro en la auditoría, porque TareoDiario está excluido a propósito (es derivado y
        // reconstruible). Este registro es lo único que dice cuándo corrió, quién lo disparó
        // y cuántos días quedaron esperando decisión — que es el número que hay que mirar
        // antes de cerrar el mes.
        //
        // ⚠️ Ley 29733: solo conteos y el código del periodo. Ningún dato personal.
        var pendientes = porEstado.GetValueOrDefault(EstadoTareo.PendienteValidacion);

        Recalculado(registro, periodo.Codigo, usuario, trabajadores.Count, creados + actualizados, pendientes);

        return new ResultadoRecalculo(
            periodo.Id,
            periodo.Codigo,
            trabajadores.Count,
            creados + actualizados,
            creados,
            actualizados,
            porEstado);
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Tareo del periodo {Periodo} recalculado por {Usuario}: {Trabajadores} trabajadores, " +
                  "{Dias} días, {Pendientes} pendientes de validar.")]
    private static partial void Recalculado(
        ILogger registro, string periodo, string usuario, int trabajadores, int dias, int pendientes);

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
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == x.Tareo.TrabajadorId && a.FechaFin == null)
                    .Select(a => a.Equipo!.Nombre)
                    .FirstOrDefault(),
                x.Tareo.Fecha,
                x.Tareo.EsDescansoProgramado
                    ? "DESCANSO PROGRAMADO"
                    : x.Tareo.TurnoId == null
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
                x.Tareo.Observacion,
                x.Tareo.TipoIncidencia,
                x.Tareo.RequiereValidacion,
                contexto.DescansosSustitutorios.Any(d =>
                    d.Activo
                    && d.Estado == EstadoDescansoSustitutorio.Otorgado
                    && d.TrabajadorId == x.Tareo.TrabajadorId
                    && d.FechaFeriadoOrigen == x.Tareo.Fecha),
                contexto.DescansosSustitutorios
                    .Where(d => d.Activo
                                && d.Estado == EstadoDescansoSustitutorio.Otorgado
                                && d.TrabajadorId == x.Tareo.TrabajadorId
                                && d.FechaFeriadoOrigen == x.Tareo.Fecha)
                    .Select(d => (DateOnly?)d.FechaDescanso)
                    .FirstOrDefault()))
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
                g.First().Equipo,
                [.. g]))
            .ToList();

        return new ResultadoPaginado<TareoDeTrabajador>(grupos, total, pagina, tamanio);
    }

    public async Task<ResultadoPaginado<IncidenciaDto>> ListarIncidenciasAsync(
        FiltroIncidencias filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var tamanio = Math.Clamp(filtro.TamanioPagina, 1, 200);
        var pagina = Math.Max(filtro.Pagina, 1);

        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        var tareosVisibles = consultaContextoLaboral.AplicarAlcancePlantas(
            contexto.TareosDiarios.AsNoTracking(), plantasVisibles);

        var consulta = from tareo in tareosVisibles
                       join trabajador in contexto.Trabajadores.AsNoTracking()
                           on tareo.TrabajadorId equals trabajador.Id
                       join periodo in contexto.Periodos.AsNoTracking()
                           on tareo.PeriodoId equals periodo.Id
                       select new { Tareo = tareo, Trabajador = trabajador, Periodo = periodo };

        if (filtro.SoloPendientes)
        {
            consulta = consulta.Where(x => x.Tareo.RequiereValidacion);
        }
        else
        {
            // Sin el filtro de pendientes la bandeja sirve para auditar: todo día que
            // alguna vez tuvo algo que resolver, esté resuelto o no.
            consulta = consulta.Where(x => x.Tareo.TipoIncidencia != null);
        }

        if (filtro.PeriodoId is { } periodoId)
        {
            consulta = consulta.Where(x => x.Tareo.PeriodoId == periodoId);
        }

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(x => x.Tareo.TrabajadorId == trabajadorId);
        }

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(x => x.Tareo.TipoIncidencia == tipo);
        }

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var filas = await consulta
            // Lo más viejo primero: es lo que está más cerca del cierre del periodo y lo
            // que más urge resolver.
            .OrderBy(x => x.Tareo.Fecha)
            .ThenBy(x => x.Trabajador.ApellidoPaterno)
            .ThenBy(x => x.Trabajador.Nombres)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(x => new IncidenciaDto(
                x.Tareo.Id,
                x.Tareo.TrabajadorId,
                x.Trabajador.ApellidoPaterno + " " + x.Trabajador.ApellidoMaterno + ", " + x.Trabajador.Nombres,
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == x.Tareo.TrabajadorId
                                && a.FechaInicio <= x.Tareo.Fecha
                                && (a.FechaFin == null || a.FechaFin >= x.Tareo.Fecha))
                    .Select(a => a.IdMarcador)
                    .FirstOrDefault(),
                contexto.AsignacionesMarcador
                    .Where(a => a.TrabajadorId == x.Tareo.TrabajadorId
                                && a.FechaInicio <= x.Tareo.Fecha
                                && (a.FechaFin == null || a.FechaFin >= x.Tareo.Fecha))
                    .Select(a => a.Equipo!.Codigo)
                    .FirstOrDefault(),
                x.Tareo.Fecha,
                x.Tareo.PeriodoId,
                x.Periodo.Codigo,
                x.Periodo.Estado == EstadoPeriodo.Abierto,
                x.Tareo.TurnoId == null
                    ? null
                    : contexto.Turnos.Where(t => t.Id == x.Tareo.TurnoId).Select(t => t.Codigo).FirstOrDefault(),
                x.Tareo.TurnoId == null
                    ? null
                    : contexto.Turnos.Where(t => t.Id == x.Tareo.TurnoId).Select(t => (TimeOnly?)t.HoraInicio).FirstOrDefault(),
                x.Tareo.TurnoId == null
                    ? null
                    : contexto.Turnos.Where(t => t.Id == x.Tareo.TurnoId).Select(t => (TimeOnly?)t.HoraFin).FirstOrDefault(),
                x.Tareo.EntradaReal,
                x.Tareo.SalidaReal,
                x.Tareo.Estado,
                x.Tareo.TipoIncidencia,
                x.Tareo.MarcacionesFueraVentana,
                x.Tareo.Observacion,
                // La justificación vigente gana sobre la evidencia rechazada/cancelada; si
                // solo queda historia, se muestra la más reciente. AUS-008 permite que ambas
                // convivan el mismo día, así que un FirstOrDefault sin orden podría enseñar
                // una cancelada mientras ya existe otra aprobada.
                contexto.Justificaciones
                    .Where(j => j.TrabajadorId == x.Tareo.TrabajadorId && j.Fecha == x.Tareo.Fecha)
                    .OrderBy(j => j.Estado == EstadoJustificacion.Rechazada
                                  || j.Estado == EstadoJustificacion.Cancelada)
                    .ThenByDescending(j => j.Id)
                    .Select(j => (long?)j.Id)
                    .FirstOrDefault(),
                contexto.Justificaciones
                    .Where(j => j.TrabajadorId == x.Tareo.TrabajadorId && j.Fecha == x.Tareo.Fecha)
                    .OrderBy(j => j.Estado == EstadoJustificacion.Rechazada
                                  || j.Estado == EstadoJustificacion.Cancelada)
                    .ThenByDescending(j => j.Id)
                    .Select(j => (EstadoJustificacion?)j.Estado)
                    .FirstOrDefault()))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<IncidenciaDto>(filas, total, pagina, tamanio);
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
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        var tareosVisibles = consultaContextoLaboral.AplicarAlcancePlantas(
            contexto.TareosDiarios.AsNoTracking(), plantas);

        var consulta =
            from t in tareosVisibles
            join w in contexto.Trabajadores.AsNoTracking() on t.TrabajadorId equals w.Id
            select new TareoConTrabajador { Tareo = t, Trabajador = w };

        // El listado y el resumen ya vienen aislados por la planta histórica de cada día.

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
            // Sin distinguir mayúsculas: quien busca teclea "quispe", no "QUISPE". Ver
            // BusquedaTexto para por qué se compara con ToUpper() en los dos lados.
            var texto = BusquedaTexto.Normalizar(filtro.Busqueda);

#pragma warning disable CA1304, CA1311, CA1862
            consulta = consulta.Where(x =>
                x.Trabajador.ApellidoPaterno.ToUpper().Contains(texto)
                || x.Trabajador.ApellidoMaterno.ToUpper().Contains(texto)
                || x.Trabajador.Nombres.ToUpper().Contains(texto)
                || x.Trabajador.NumeroDocumento.ToUpper().Contains(texto));
#pragma warning restore CA1304, CA1311, CA1862
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

        consultaTareos = consultaContextoLaboral.AplicarAlcancePlantas(
            consultaTareos, plantasVisibles);

        var totales = await consultaTareos
            .GroupBy(t => 1)
            .Select(g => new
            {
                // En un día no laborable trabajado, MinutosTrabajados es la base física del
                // concepto al 100 %. Conservar además sus ordinarios históricos volvería a
                // sumar la misma jornada. La ausencia pagada queda fuera de esta corrección:
                // P1-08 debe decidir su crédito antes de cambiarlo.
                Ordinarios = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno
                                        || t.Estado == EstadoTareo.AusenciaJustificada
                    ? t.MinutosOrdinarios
                    : 0),
                Extra25 = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno
                    ? t.MinutosExtra25
                    : 0),
                Extra35 = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno
                    ? t.MinutosExtra35
                    : 0),
                Nocturnos = g.Sum(t => t.MinutosNocturnos),
                Descanso = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.DescansoSemanal
                    ? t.MinutosTrabajados
                    // Una jornada sabatina que entra al domingo conserva FechaJornada y
                    // TipoDiaNoLaborable=Ninguno. Su HE100 es el remanente físico que no
                    // pertenece a ordinario, HE25 ni HE35.
                    : t.TipoDiaNoLaborable == TipoDiaNoLaborable.Ninguno
                        ? (t.MinutosTrabajados
                           - t.MinutosOrdinarios
                           - t.MinutosExtra25
                           - t.MinutosExtra35 > 0
                            ? t.MinutosTrabajados
                              - t.MinutosOrdinarios
                              - t.MinutosExtra25
                              - t.MinutosExtra35
                            : 0)
                        : 0),
                Feriado = g.Sum(t => t.TipoDiaNoLaborable == TipoDiaNoLaborable.Feriado
                                      && !contexto.DescansosSustitutorios.Any(d =>
                                          d.Activo
                                          && d.Estado == EstadoDescansoSustitutorio.Otorgado
                                          && d.TrabajadorId == t.TrabajadorId
                                          && d.FechaFeriadoOrigen == t.Fecha)
                    ? t.MinutosTrabajados
                    : 0),

                // Los derechos ordinarios se cuentan en días-persona, no en minutos. El
                // estado Feriado cubre tareos históricos sin trabajo que se persistieron
                // antes de guardar TipoDiaNoLaborable=Feriado; los trabajados se reconocen
                // por el tipo. El 1 de mayo coincidente se separa porque, a diferencia de
                // un feriado ordinario, conserva dos componentes diarios independientes.
                FeriadosSinCoincidenciaPrimeroDeMayo = g.Count(t =>
                    (t.TipoDiaNoLaborable == TipoDiaNoLaborable.Feriado
                     || t.Estado == EstadoTareo.Feriado)
                    && t.CoincidenciaFeriadoDescanso
                         != TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo),
                CoincidenciasPrimeroDeMayo = g.Count(t =>
                    t.CoincidenciaFeriadoDescanso
                        == TipoCoincidenciaFeriadoDescanso.PrimeroDeMayo)
            })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (totales is null)
        {
            return new LiquidacionPeriodo(
                periodo.Id, periodo.Codigo, periodo.FechaInicio, periodo.FechaFin, 0, [], []);
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
            ],
            [
                new ComponenteRemuneracionDiaria(
                    TipoComponenteRemuneracionDiaria.RemuneracionOrdinariaFeriado,
                    "Remuneración ordinaria por feriado",
                    totales.FeriadosSinCoincidenciaPrimeroDeMayo,
                    IncluidoEnRemuneracionBase: true,
                    BaseLegal: "D. Leg. 713, art. 8"),
                new ComponenteRemuneracionDiaria(
                    TipoComponenteRemuneracionDiaria.RemuneracionDescansoSemanal,
                    "Remuneración por descanso semanal coincidente con 1 de mayo",
                    totales.CoincidenciasPrimeroDeMayo,
                    IncluidoEnRemuneracionBase: true,
                    BaseLegal: "D.S. 012-92-TR, art. 9"),
                new ComponenteRemuneracionDiaria(
                    TipoComponenteRemuneracionDiaria.RemuneracionAdicionalPrimeroDeMayo,
                    "Remuneración adicional por 1 de mayo coincidente con descanso",
                    totales.CoincidenciasPrimeroDeMayo,
                    IncluidoEnRemuneracionBase: false,
                    BaseLegal: "D.S. 012-92-TR, art. 9")
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
        tareo.EsDescansoProgramado = resultado.EsDescansoProgramado;
        tareo.CoincidenciaFeriadoDescanso = resultado.CoincidenciaFeriadoDescanso;
        tareo.Estado = resultado.Estado;
        tareo.TipoIncidencia = resultado.TipoIncidencia;
        tareo.RequiereValidacion = resultado.RequiereValidacion;
        tareo.MarcacionesFueraVentana = resultado.MarcacionesFueraVentana;
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
    ///
    /// <para>Con una <b>cuadrilla</b>, la asignación dice a qué grupo pertenece la persona y
    /// la programación del grupo vigente en esa fecha dice qué turno le toca. La misma regla
    /// de vigencia, un nivel más adentro: recalcular un mes antiguo usa el ciclo de entonces.
    /// Si la cuadrilla no tenía ciclo programado ese día, el día queda sin horario con un
    /// motivo que lo dice, en vez del genérico «no tenía turno asignado».</para>
    /// </remarks>
    private static TurnoResuelto ResolverTurno(
        List<AsignacionVigente> asignaciones,
        HorariosPorFecha horarios,
        Dictionary<long, List<ProgramacionCuadrilla>> programacionesPorCuadrilla,
        long trabajadorId,
        DateOnly fecha)
    {
        var vigente = asignaciones
            .Where(a => a.TrabajadorId == trabajadorId
                        && a.FechaInicio <= fecha
                        && (a.FechaFin is null || a.FechaFin >= fecha))
            .OrderByDescending(a => a.FechaInicio)
            .FirstOrDefault();

        if (vigente is null)
        {
            return new TurnoResuelto(
                null, EstadoProgramacionDiaria.SinProgramacion, null, null, null, null);
        }

        if (vigente.TurnoId is { } turnoFijo)
        {
            var turnoResuelto = horarios.En(turnoFijo, fecha);
            return new TurnoResuelto(
                turnoResuelto,
                EstadoProgramacionDiaria.Trabajo,
                null,
                null,
                null,
                turnoResuelto is null ? horarios.MotivoNoDisponible(turnoFijo, fecha) : null);
        }

        var programacion = vigente.CuadrillaId is { } cuadrillaId
            ? programacionesPorCuadrilla.GetValueOrDefault(cuadrillaId)?.FirstOrDefault(p => p.EstaVigenteEn(fecha))
            : null;

        if (programacion is null)
        {
            return new TurnoResuelto(
                null,
                EstadoProgramacionDiaria.SinProgramacion,
                null,
                null,
                null,
                "El trabajador integra una cuadrilla que no tenía ciclo programado ese día " +
                "(secuencia finita). " +
                "Programe la cuadrilla para poder calcular.");
        }

        var resultado = programacion.ResolverEn(fecha);
        var cuadrilla = programacion.Cuadrilla?.Codigo ?? "?";

        if (resultado.Estado == EstadoProgramacionDiaria.ErrorConfiguracion)
        {
            return new TurnoResuelto(
                null,
                resultado.Estado,
                resultado.ProgramacionId,
                resultado.TramoId,
                $"Cuadrilla {cuadrilla}.",
                $"Error de configuración de la rotación: {resultado.Error}");
        }

        if (resultado.Estado == EstadoProgramacionDiaria.DescansoProgramado)
        {
            return new TurnoResuelto(
                null,
                resultado.Estado,
                resultado.ProgramacionId,
                resultado.TramoId,
                $"Cuadrilla {cuadrilla}: descanso programado.",
                null);
        }

        if (resultado.Estado == EstadoProgramacionDiaria.SinProgramacion
            || resultado.TurnoId is not { } turnoId)
        {
            return new TurnoResuelto(
                null,
                EstadoProgramacionDiaria.SinProgramacion,
                resultado.ProgramacionId,
                resultado.TramoId,
                null,
                "La secuencia finita de la cuadrilla ya terminó o todavía no había empezado.");
        }

        var turno = horarios.En(turnoId, fecha);
        return new TurnoResuelto(
            turno,
            EstadoProgramacionDiaria.Trabajo,
            resultado.ProgramacionId,
            resultado.TramoId,
            turno is null ? null : $"Cuadrilla {cuadrilla}: turno {turno.Codigo}.",
            turno is null ? horarios.MotivoNoDisponible(turnoId, fecha) : null);
    }

    /// <summary>
    /// El turno con el horario que regía en una fecha. Ver <see cref="VersionTurno"/>.
    /// </summary>
    /// <remarks>
    /// Es el tercer nivel de vigencia que resuelve el recálculo: la <b>asignación</b> dice a
    /// qué turno o cuadrilla pertenece la persona ese día, la <b>programación</b> de la
    /// cuadrilla dice cuál de sus turnos le toca, y esto dice con qué <b>horario</b> se
    /// calcula ese turno en esa fecha.
    /// </remarks>
    private sealed class HorariosPorFecha(Dictionary<long, Turno> turnos, List<VersionTurno> versiones)
    {
        private readonly Dictionary<long, List<VersionTurno>> porTurno = versiones
            .GroupBy(v => v.TurnoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.FechaInicio).ToList());

        /// <summary>Una sola copia por versión, que el periodo entero comparte.</summary>
        /// <remarks>
        /// Hidratar por cada par (trabajador, día) crearía decenas de miles de turnos
        /// idénticos: las versiones son un puñado y la copia solo depende de cuál rige.
        /// </remarks>
        private readonly Dictionary<long, Turno> hidratados = [];

        public Turno? En(long turnoId, DateOnly fecha)
        {
            if (turnos.GetValueOrDefault(turnoId) is not { } turno)
            {
                return null;
            }

            var version = porTurno.GetValueOrDefault(turnoId)?.FirstOrDefault(v => v.EstaVigenteEn(fecha));

            // Sin versión aplicable no existe una política económica verificable para la
            // fecha. Usar los valores actuales del catálogo inventaría historia y podría
            // contaminar liquidaciones. Las marcaciones se conservan; el día queda sin
            // horario económico hasta completar la configuración.
            if (version is null)
            {
                return null;
            }

            if (!hidratados.TryGetValue(version.Id, out var conHorario))
            {
                conHorario = version.AplicarA(turno);
                hidratados[version.Id] = conHorario;
            }

            return conHorario;
        }

        public string MotivoNoDisponible(long turnoId, DateOnly fecha) =>
            !turnos.ContainsKey(turnoId)
                ? "El turno programado no existe."
                : $"El turno no tiene una versión económica vigente para el {fecha:dd/MM/yyyy}. " +
                  "Complete la configuración antes de calcular conceptos definitivos.";
    }

    /// <summary>Asignación de turno con su vigencia, para resolver en memoria.</summary>
    private sealed record AsignacionVigente(
        long TrabajadorId,
        long? TurnoId,
        long? CuadrillaId,
        DateOnly FechaInicio,
        DateOnly? FechaFin);

    /// <summary>El turno de un día, de dónde salió y, si no hay, por qué.</summary>
    private sealed record TurnoResuelto(
        Turno? Turno,
        EstadoProgramacionDiaria Estado,
        long? ProgramacionId,
        long? TramoId,
        string? Origen,
        string? MotivoSinTurno);

    private sealed record MarcaAtribuida(long Id, DateTime FechaHora);

    private sealed record DelimitacionDescanso(List<MarcaAtribuida> Marcas, bool Ambigua);

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
        long? TipoAusenciaId,
        int? MinutosJustificados,
        DateTime? EntradaRegularizada,
        DateTime? SalidaRegularizada,
        TimeOnly? InicioPermiso,
        TimeOnly? FinPermiso,
        string Motivo);

    /// <summary>Lo que el motor necesita saber del tipo de ausencia de una justificación.</summary>
    private sealed record TipoAusenciaResuelto(bool ComputaComoAsistencia, ComportamientoAusencia Comportamiento);

    /// <summary>
    /// Resuelve la justificación vigente para un trabajador en una fecha.
    /// </summary>
    /// <remarks>
    /// Actualmente solo soporta una justificación por día (la primera que encuentra).
    /// Si GCA requiere múltiples justificaciones en un mismo día (tardanza + marca
    /// incompleta), habrá que cambiarlo por una lista y que el cálculo las itere.
    ///
    /// <para>⚠️ Una justificación <b>sin tipo de ausencia</b> —completar una marca impar,
    /// perdonar una tardanza— no computa como asistencia ni paga nada. Aquí se resolvía al
    /// revés: la ausencia de tipo se leía como "sí computa", y una marca impar justificada
    /// terminaba con la jornada ordinaria plana. En un turno de doce horas eso borraba
    /// cuatro horas de jornada y todo el sobretiempo del día.</para>
    /// </remarks>
    private static JustificacionAplicable? ResolverJustificacion(
        Dictionary<long, Dictionary<DateOnly, JustificacionResuelta>> porTrabajador,
        IReadOnlyDictionary<long, TipoAusenciaResuelto> tiposAusencia,
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

        var tipo = justificacion.TipoAusenciaId is { } tipoAusenciaId
            ? tiposAusencia.GetValueOrDefault(tipoAusenciaId)
            : null;

        return new JustificacionAplicable
        {
            Id = justificacion.Id,
            Tipo = justificacion.Tipo,
            ComputaComoAsistencia = tipo?.ComputaComoAsistencia ?? false,
            Comportamiento = tipo?.Comportamiento,
            MinutosJustificados = justificacion.MinutosJustificados,
            InicioPermiso = justificacion.InicioPermiso,
            FinPermiso = justificacion.FinPermiso,
            EntradaRegularizada = justificacion.EntradaRegularizada,
            SalidaRegularizada = justificacion.SalidaRegularizada,
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
    private static List<MarcaAtribuida> MarcacionesDeLaJornada(
        List<MarcaAtribuida> marcaciones,
        Turno turno,
        DateOnly fecha,
        DateTime? finJornadaAnterior,
        HashSet<long> reclamadas)
    {
        var inicioTurno = fecha.ToDateTime(turno.HoraInicio);
        var finTurno = FinJornadaTeorica(turno, fecha)!.Value;

        var desde = inicioTurno.AddMinutes(-turno.MinutosAnticipacionEntrada);
        var hasta = finTurno.Add(MargenSalida);

        return marcaciones
            .Where(m => m.FechaHora >= desde
                        && m.FechaHora <= hasta
                        && !reclamadas.Contains(m.Id)
                        && (finJornadaAnterior is null || m.FechaHora > finJornadaAnterior.Value))
            .ToList();
    }

    /// <summary>
    /// Delimita trabajo en descanso sobre el residuo no reclamado. La primera marca debe
    /// caer en D; un cierre en D+1 solo entra antes de la ventana de la jornada laboral de
    /// D+1. Si D+1 no aporta una frontera laboral, sus marcas no se adjudican a ciegas.
    /// </summary>
    private static DelimitacionDescanso DelimitarDescanso(
        List<MarcaAtribuida> marcas,
        HashSet<long> reclamadas,
        DateOnly fecha,
        IReadOnlyDictionary<DateOnly, TurnoResuelto> resoluciones)
    {
        var residualesDelDia = marcas
            .Where(m => !reclamadas.Contains(m.Id)
                        && DateOnly.FromDateTime(m.FechaHora) == fecha)
            .OrderBy(m => m.FechaHora)
            .ToList();

        if (residualesDelDia.Count == 0)
        {
            return new DelimitacionDescanso([], false);
        }

        var seleccionadas = new List<MarcaAtribuida>(residualesDelDia);
        var siguienteFecha = fecha.AddDays(1);

        if (resoluciones.TryGetValue(siguienteFecha, out var siguiente)
            && siguiente.Estado == EstadoProgramacionDiaria.Trabajo
            && siguiente.Turno is { } turnoSiguiente)
        {
            var frontera = InicioVentana(turnoSiguiente, siguienteFecha);
            seleccionadas.AddRange(marcas.Where(m =>
                !reclamadas.Contains(m.Id)
                && DateOnly.FromDateTime(m.FechaHora) == siguienteFecha
                && m.FechaHora < frontera));
        }
        else if (resoluciones.TryGetValue(siguienteFecha, out siguiente)
                 && siguiente.Estado == EstadoProgramacionDiaria.ErrorConfiguracion)
        {
            return new DelimitacionDescanso(residualesDelDia, true);
        }

        return new DelimitacionDescanso(
            [.. seleccionadas.OrderBy(m => m.FechaHora)],
            false);
    }

    private static DateTime InicioVentana(Turno turno, DateOnly fecha) =>
        fecha.ToDateTime(turno.HoraInicio).AddMinutes(-turno.MinutosAnticipacionEntrada);

    /// <summary>
    /// Marcas del día que ninguna jornada reclamó: las que el turno dejó fuera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se miran por su <b>fecha natural</b>, no por la ventana: la ventana es justamente lo
    /// que no las quiso. Una marca del día D solo la puede reclamar la jornada de D o la de
    /// D-1 —la de D+1 abre, como mucho, la anticipación de entrada antes de su propio
    /// inicio—, y las dos ya pasaron cuando se llega aquí, así que el conjunto está completo.
    /// </para>
    /// <para>
    /// ⚠️ Queda un borde teórico: un turno que empezara pasada la medianoche con una
    /// anticipación de entrada lo bastante larga como para alcanzar el día anterior
    /// reclamaría una marca ya reportada como fuera de ventana. Los turnos de GCA empiezan
    /// a las 07:00 y a las 19:00, así que hoy no ocurre; si algún día se configura uno así,
    /// el efecto es una incidencia de más, nunca un minuto de menos.
    /// </para>
    /// </remarks>
    private static List<DateTime> SinReclamar(
        List<MarcaAtribuida> marcas,
        HashSet<long> reclamadas,
        DateOnly fecha) =>
        marcas
            .Where(m => DateOnly.FromDateTime(m.FechaHora) == fecha && !reclamadas.Contains(m.Id))
            .Select(m => m.FechaHora)
            .ToList();

    /// <summary>
    /// Si no llegó el archivo del aparato que le correspondía a esa persona ese día.
    /// </summary>
    /// <remarks>
    /// Sin número asignado la respuesta es <c>false</c>: no se sabe de qué aparato era, así
    /// que tampoco se puede decir que faltó el suyo. Ese caso ya se ve en otro lado —el
    /// padrón incompleto— y confundirlo con un archivo sin cargar taparía los dos.
    /// </remarks>
    private static bool SinCobertura(
        Dictionary<long, List<VigenciaIdMarcador>> vigenciasPorTrabajador,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<long>> aparatosSinCarga,
        long trabajadorId,
        DateOnly fecha)
    {
        if (!aparatosSinCarga.TryGetValue(fecha, out var faltantes)
            || !vigenciasPorTrabajador.TryGetValue(trabajadorId, out var vigencias))
        {
            return false;
        }

        var suyo = vigencias.FirstOrDefault(
            v => v.Desde <= fecha && (v.Hasta is null || v.Hasta >= fecha));

        return suyo is not null && faltantes.Contains(suyo.EquipoId);
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
