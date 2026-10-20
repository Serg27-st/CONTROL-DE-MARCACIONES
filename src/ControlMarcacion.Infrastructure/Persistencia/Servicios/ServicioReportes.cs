using System.Globalization;
using System.Text.Json;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Reportes;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
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
internal sealed class ServicioReportes(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioReportes
{
    private readonly ConsultaContextoLaboral consultaContextoLaboral = new(contexto);

    /// <summary>Estados que representan una anomalía sobre la que alguien debe actuar.</summary>
    private static readonly EstadoTareo[] EstadosDeExcepcion =
    [
        EstadoTareo.Falta,
        EstadoTareo.Tardanza,
        EstadoTareo.AusenciaJustificada,
        EstadoTareo.Incompleto,

        // Un día que el sistema no pudo resolver es la excepción por excelencia: si no
        // saliera aquí, la única forma de enterarse sería entrar a la bandeja, y el Excel
        // de excepciones que se envía por correo lo ocultaría justo donde más importa.
        EstadoTareo.PendienteValidacion
    ];

    public async Task<IReadOnlyList<ResumenTareoDto>> ResumenTareoAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var rango = await ResolverRangoAsync(filtro, cancelacion).ConfigureAwait(false);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, rango, cancelacion).ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return [];
        }

        var ids = trabajadores.Select(t => t.Id).ToList();
        var trabajadorPorId = trabajadores.ToDictionary(t => t.Id);

        var tareos = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.Fecha >= rango.Desde && t.Fecha <= rango.Hasta
                        && ids.Contains(t.TrabajadorId))
            .Select(t => new
            {
                t.TrabajadorId,
                t.Fecha,
                t.Estado,
                t.TipoDiaNoLaborable,
                t.EntradaReal,
                t.SalidaReal,
                t.MinutosTrabajados,
                t.MinutosOrdinarios,
                t.MinutosExtra25,
                t.MinutosExtra35,
                t.MinutosNocturnos,
                t.MinutosTardanza
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        tareos = tareos
            .Where(t => trabajadorPorId[t.TrabajadorId].IncluyeFecha(t.Fecha))
            .ToList();

        var porTrabajador = tareos.ToLookup(t => t.TrabajadorId);

        // Bono nocturno: es dinero, así que se resuelve aparte de los minutos y día por día,
        // con el RMV/tasa/sueldo vigentes en la fecha de CADA jornada — no en una fecha fija
        // del periodo. Ver CalcularBonoNocturnoPorTrabajadorAsync.
        var diasNocturnos = tareos
            .Where(t => t.MinutosNocturnos > 0)
            .Select(t => (t.TrabajadorId, t.Fecha, t.MinutosNocturnos))
            .ToList();

        var bonoNocturnoPorTrabajador = await CalcularBonoNocturnoPorTrabajadorAsync(
            diasNocturnos, trabajadores, cancelacion).ConfigureAwait(false);

        // Movilidad: mismo criterio que el tareo final (ArmarFilasTareoAsync) — días
        // asistidos (marcación real de entrada Y salida, Puntual o Tardanza; NO
        // MinutosTrabajados > 0, que también se rellena en vacaciones justificadas) por la
        // tarifa vigente al último día del alcance.
        var valorMovilidadDiaria = ResolverVigente(
            await ObtenerParametroLegalAsync(TipoParametroLegal.ValorMovilidadDiaria, cancelacion)
                .ConfigureAwait(false),
            rango.Hasta,
            "el valor de movilidad diaria");

        return
        [
            .. trabajadores.Select(trabajador =>
            {
                var dias = porTrabajador[trabajador.Id];

                var diasAsistidos = dias.Count(d =>
                    d.EntradaReal != null && d.SalidaReal != null
                    && (d.Estado == EstadoTareo.Puntual || d.Estado == EstadoTareo.Tardanza));

                return new ResumenTareoDto(
                    trabajador.Id,
                    trabajador.Nombre,
                    trabajador.IdMarcador,
                    trabajador.Equipo,
                    trabajador.Empresa,
                    trabajador.Planta,
                    trabajador.CentroCodigo ?? trabajador.AreaCodigo,
                    trabajador.CentroCostoDescripcion is null
                        ? trabajador.Seccion
                        : $"{trabajador.CentroCostoCodigo} - {trabajador.CentroCostoDescripcion}",
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
                    dias.Sum(d => d.MinutosTardanza),
                    bonoNocturnoPorTrabajador.GetValueOrDefault(trabajador.Id, 0m),
                    diasAsistidos * valorMovilidadDiaria);
            })
            .OrderBy(r => r.Trabajador)
        ];
    }

    public async Task<ResumenTareoPagina> ResumenTareoPaginadoAsync(
        FiltroReporte filtro,
        int pagina,
        int tamanioPagina,
        CancellationToken cancelacion = default)
    {
        var tamanio = Math.Clamp(tamanioPagina, 10, 500);
        var numero = Math.Max(pagina, 1);

        // Se calcula el alcance completo y se recorta después. Los totales tienen que
        // salir de TODAS las filas —es la fila que planilla suma—, y calcularlas ya
        // obliga a recorrer el alcance entero: el bono nocturno se resuelve día por día
        // con la RMV vigente en cada fecha. Ver IServicioReportes.
        var todas = await ResumenTareoAsync(filtro, cancelacion).ConfigureAwait(false);

        var recorte = todas
            .Skip((numero - 1) * tamanio)
            .Take(tamanio)
            .ToList();

        return new ResumenTareoPagina(
            new ResultadoPaginado<ResumenTareoDto>(recorte, todas.Count, numero, tamanio),
            TotalesResumenTareo.De(todas));
    }

    public async Task<IReadOnlyList<ExcepcionDto>> ExcepcionesAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        var pagina = await ExcepcionesInternoAsync(filtro, pagina: null, tamanioPagina: 0, cancelacion)
            .ConfigureAwait(false);

        return pagina.Elementos;
    }

    public async Task<ResultadoPaginado<ExcepcionDto>> ExcepcionesPaginadasAsync(
        FiltroReporte filtro,
        int pagina,
        int tamanioPagina,
        CancellationToken cancelacion = default) =>
        await ExcepcionesInternoAsync(
                filtro,
                Math.Max(pagina, 1),
                Math.Clamp(tamanioPagina, 10, 500),
                cancelacion)
            .ConfigureAwait(false);

    /// <summary>
    /// Excepciones del alcance, con o sin recorte de página.
    /// </summary>
    /// <remarks>
    /// Una sola implementación para la pantalla y para el Excel: si fueran dos consultas
    /// separadas, un filtro corregido en una y no en la otra haría que el archivo que se
    /// envía a planilla no coincidiera con lo que se vio antes de exportarlo.
    ///
    /// Con <paramref name="pagina"/> nulo devuelve todas las filas, que es lo que necesita
    /// la exportación.
    /// </remarks>
    private async Task<ResultadoPaginado<ExcepcionDto>> ExcepcionesInternoAsync(
        FiltroReporte filtro,
        int? pagina,
        int tamanioPagina,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Resolver el rango primero también valida que el periodo exista: antes, un
        // PeriodoId inventado devolvía lista vacía y la pantalla decía "no hay excepciones"
        // cuando lo que no había era el periodo.
        var rango = await ResolverRangoAsync(filtro, cancelacion).ConfigureAwait(false);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, rango, cancelacion).ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return new ResultadoPaginado<ExcepcionDto>([], 0, pagina ?? 1, tamanioPagina);
        }

        var ids = trabajadores.Select(t => t.Id).ToList();
        var porId = trabajadores.ToDictionary(t => t.Id);

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Codigo, cancelacion)
            .ConfigureAwait(false);

        var excepciones = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.Fecha >= rango.Desde && t.Fecha <= rango.Hasta
                        && ids.Contains(t.TrabajadorId)
                        && EstadosDeExcepcion.Contains(t.Estado))
            .Select(t => new
            {
                t.TrabajadorId,
                t.Fecha,
                t.TurnoId,
                t.EsDescansoProgramado,
                t.EntradaReal,
                t.SalidaReal,
                t.Estado,
                t.MinutosTardanza,
                t.MinutosSalidaAnticipada,
                t.Observacion
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // Empresa/planta y alcance de seguridad se resuelven por la fecha de cada fila.
        // El recorte se hace después de este filtro histórico para que Total y página no
        // cuenten filas que pertenecían a otra organización en esa fecha.
        excepciones = excepciones
            .Where(e => porId[e.TrabajadorId].IncluyeFecha(e.Fecha))
            .OrderBy(e => e.Fecha)
            .ThenBy(e => e.TrabajadorId)
            .ToList();

        var total = excepciones.Count;
        if (pagina is { } numero)
        {
            excepciones = excepciones
                .Skip((numero - 1) * tamanioPagina)
                .Take(tamanioPagina)
                .ToList();
        }

        var filas = excepciones.ConvertAll(e => new ExcepcionDto(
            e.TrabajadorId,
            porId[e.TrabajadorId].Nombre,
            porId[e.TrabajadorId].IdMarcador,
            porId[e.TrabajadorId].Equipo,
            e.Fecha,
            e.EsDescansoProgramado
                ? "DESCANSO PROGRAMADO"
                : e.TurnoId is { } turnoId ? turnos.GetValueOrDefault(turnoId) : null,
            e.EntradaReal,
            e.SalidaReal,
            e.Estado,
            e.MinutosTardanza,
            e.MinutosSalidaAnticipada,
            e.Observacion));

        return new ResultadoPaginado<ExcepcionDto>(
            filas, total, pagina ?? 1, pagina is null ? Math.Max(total, 1) : tamanioPagina);
    }

    public async Task<TareoFinalDto> TareoFinalAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // El libro de planilla es por periodo y solo por periodo: sus reintegros se cargan
        // por trabajador y periodo, y el encabezado declara si está abierto o cerrado.
        // Sobre medio mes saldría sin reintegros y sin estado — un entregable incompleto que
        // parece completo.
        if (filtro.Alcance != AlcanceReporte.Periodo)
        {
            throw new ReglaNegocioException(
                "El tareo final se emite por periodo de planilla, no por mes, día ni rango " +
                "libre: sus reintegros y su estado de cierre son del periodo. Elija el " +
                "alcance \"Periodo\" para descargarlo.");
        }

        var rango = await ResolverRangoAsync(filtro, cancelacion).ConfigureAwait(false);

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstAsync(p => p.Id == rango.PeriodoId, cancelacion)
            .ConfigureAwait(false);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, rango, cancelacion).ConfigureAwait(false);

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

        var filasTareo = await ArmarFilasTareoAsync(periodo, trabajadores, ids, cancelacion)
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

        var rango = await ResolverRangoAsync(filtro, cancelacion).ConfigureAwait(false);

        var periodo = rango.PeriodoId is { } periodoId
            ? await contexto.Periodos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == periodoId, cancelacion).ConfigureAwait(false)
            : null;

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

        if (filtro.CentroId is { } centroId)
        {
            var codigo = await contexto.Centros.AsNoTracking()
                .Where(c => c.Id == centroId).Select(c => c.Codigo).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Centro: {codigo}");
        }

        if (filtro.CentroCostoId is { } centroCostoId)
        {
            var codigo = await contexto.CentrosCosto.AsNoTracking()
                .Where(cc => cc.Id == centroCostoId).Select(cc => cc.Codigo).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Centro de costo: {codigo}");
        }

        if (filtro.EquipoId is { } equipoDelFiltro)
        {
            var nombre = await contexto.EquiposMarcador.AsNoTracking()
                .Where(e => e.Id == equipoDelFiltro).Select(e => e.Nombre).FirstOrDefaultAsync(cancelacion)
                .ConfigureAwait(false);
            partesFiltro.Add($"Aparato: {nombre}");
        }

        var calculo = await EvaluarVigenciaDelCalculoAsync(rango, filtro, cancelacion).ConfigureAwait(false);

        return new MetadatosReporte(
            rango.Etiqueta,
            rango.Desde,
            rango.Hasta,
            periodo?.Codigo,
            periodo?.Nombre,
            periodo?.EstaAbierto,
            partesFiltro.Count == 0 ? "Todos los trabajadores" : string.Join(" · ", partesFiltro),
            DateTime.Now,
            usuario,
            calculo);
    }

    // ------------------------------------------------------------------
    // Alcance temporal
    // ------------------------------------------------------------------

    /// <summary>
    /// Convierte la elección del filtro en un rango de fechas válido.
    /// </summary>
    /// <remarks>
    /// La validación de cada alcance vive en <see cref="ResolutorRangoReporte"/>, que es
    /// puro y está probado con fechas literales. Lo único que aporta este método es la
    /// consulta del periodo, que sí necesita base de datos.
    /// </remarks>
    private async Task<RangoReporte> ResolverRangoAsync(
        FiltroReporte filtro,
        CancellationToken cancelacion)
    {
        if (filtro.Alcance != AlcanceReporte.Periodo)
        {
            return filtro.Alcance switch
            {
                AlcanceReporte.Mes => ResolutorRangoReporte.DeMes(filtro.Anio, filtro.Mes),
                AlcanceReporte.Dia => ResolutorRangoReporte.DeDia(filtro.Desde),
                AlcanceReporte.Rango => ResolutorRangoReporte.DeRango(filtro.Desde, filtro.Hasta),
                _ => throw new ReglaNegocioException(
                    $"El alcance de reporte «{filtro.Alcance}» no está contemplado.")
            };
        }

        if (filtro.PeriodoId is not { } periodoId)
        {
            throw new ReglaNegocioException("Elija el periodo que quiere reportar.");
        }

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Id == periodoId)
            .Select(p => new { p.Id, p.Codigo, p.FechaInicio, p.FechaFin })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", periodoId);

        return ResolutorRangoReporte.DePeriodo(
            periodo.Id, periodo.Codigo, periodo.FechaInicio, periodo.FechaFin);
    }

    // ------------------------------------------------------------------
    // Vigencia del cálculo
    // ------------------------------------------------------------------

    /// <summary>
    /// Compara el cálculo más antiguo del alcance contra lo último que pudo cambiarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este módulo consulta el tareo que dejó el motor y no lo recalcula: por eso un reporte
    /// puede salir con cifras viejas sin verse distinto. Las seis cosas que mueven el
    /// resultado de un día ya calculado se consultan con la misma planta del recálculo:
    /// marcaciones y sus cargas, justificaciones, asignaciones, versiones de turno,
    /// programación y tramos de cuadrilla, feriados y los campos laborales o de tipo de
    /// ausencia que el motor consume. Un solo tareo antiguo basta para que el alcance no
    /// esté al día; por eso se usa <c>MIN(CalculadoEn)</c>, nunca el máximo global.
    /// </para>
    /// <para>
    /// Los borrados de justificación y feriado se resuelven con la bitácora automática ya
    /// existente. Se leen solo eventos posteriores al cálculo y se filtran en memoria; no
    /// se escanea la bitácora completa ni se consulta por cada fila.
    /// </para>
    /// <para>
    /// La unidad de vigencia es periodo + plantas visibles (y la planta explícita del
    /// filtro, si existe), igual que la unidad que puede recalcular el usuario. Una planta
    /// recién recalculada no puede ocultar otra que conserva filas antiguas.
    /// </para>
    /// </remarks>
    private async Task<EstadoCalculoTareo> EvaluarVigenciaDelCalculoAsync(
        RangoReporte rango,
        FiltroReporte filtro,
        CancellationToken cancelacion)
    {
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        IReadOnlyCollection<long>? plantasDelAlcance = plantasVisibles;

        if (filtro.PlantaId is { } plantaElegida)
        {
            plantasDelAlcance = plantasVisibles is null || plantasVisibles.Contains(plantaElegida)
                ? new[] { plantaElegida }
                : Array.Empty<long>();
        }

        var historias = await consultaContextoLaboral
            .CargarRangoAsync(rango.Desde, rango.Hasta, cancelacion: cancelacion)
            .ConfigureAwait(false);
        var plantasPermitidas = plantasDelAlcance?.ToHashSet();
        var idsDelAlcance = historias.Values
            .Where(h => h.EnRango(rango.Desde, rango.Hasta).Any(p =>
                (filtro.EmpresaId is null || p.EmpresaId == filtro.EmpresaId.Value)
                && (plantasPermitidas is null || p.Plantas.Any(ap =>
                    plantasPermitidas.Contains(ap.PlantaId)
                    && ap.FechaInicio <= rango.Hasta
                    && (ap.FechaFin is null || ap.FechaFin.Value >= rango.Desde)))))
            .Select(h => h.TrabajadorId)
            .ToList();

        var trabajadoresDelAlcance = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => idsDelAlcance.Contains(t.Id));

        // MIN y no MAX: si una planta o un trabajador conserva una fila vieja, el alcance
        // entero sigue pendiente aunque otra planta se haya recalculado hace un minuto.
        var calculadoMasAntiguo = await (
                from tareo in contexto.TareosDiarios.AsNoTracking()
                join trabajador in trabajadoresDelAlcance on tareo.TrabajadorId equals trabajador.Id
                where tareo.Fecha >= rango.Desde && tareo.Fecha <= rango.Hasta
                select (DateTime?)tareo.CalculadoEn)
            .MinAsync(cancelacion)
            .ConfigureAwait(false);

        if (calculadoMasAntiguo is null)
        {
            return EstadoCalculoTareo.NuncaCalculado;
        }

        var desde = rango.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = rango.Hasta.ToDateTime(TimeOnly.MaxValue);

        // Marcaciones del rango: se mira CreadoEn (cuándo entró la fila al sistema), no
        // FechaHora (cuándo marcó la persona). Un archivo de julio cargado en agosto cambia
        // el tareo de julio en agosto.
        var ultimaMarcacion = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .Where(m => m.FechaHora >= desde && m.FechaHora <= hasta
                        && contexto.AsignacionesMarcador.Any(a =>
                            idsDelAlcance.Contains(a.TrabajadorId)
                            && a.EquipoId == m.EquipoId
                            && a.IdMarcador == m.IdMarcador
                            && a.FechaInicio <= rango.Hasta
                            && (a.FechaFin == null || a.FechaFin >= rango.Desde)))
            .MaxAsync(m => (DateTime?)m.CreadoEn, cancelacion)
            .ConfigureAwait(false);

        // Anular o reactivar una carga no borra ni inserta ninguna marcación, pero cambia
        // cuáles cuenta el motor: sin esto, "subí el archivo equivocado y lo anulé" dejaría
        // el reporte viejo pasando por vigente.
        var ultimaAnulacion = await contexto.Importaciones
            .AsNoTracking()
            .Where(i => contexto.MarcacionesCrudas
                .Any(m => m.ImportacionId == i.Id
                          && m.FechaHora >= desde && m.FechaHora <= hasta
                          && contexto.AsignacionesMarcador.Any(a =>
                              idsDelAlcance.Contains(a.TrabajadorId)
                              && a.EquipoId == m.EquipoId
                              && a.IdMarcador == m.IdMarcador
                              && a.FechaInicio <= rango.Hasta
                              && (a.FechaFin == null || a.FechaFin >= rango.Desde))))
            .MaxAsync(i => i.ModificadoEn, cancelacion)
            .ConfigureAwait(false);

        var ultimaJustificacion = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.Fecha >= rango.Desde && j.Fecha <= rango.Hasta
                        && j.Estado == Domain.Justificaciones.EstadoJustificacion.Aprobada
                        && idsDelAlcance.Contains(j.TrabajadorId))
            .MaxAsync(j => (DateTime?)(j.ModificadoEn ?? j.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        // Las marcaciones se atribuyen al calcular, con la asignación de ID vigente en su
        // fecha: asignar o corregir un ID que rige en el rango cambia de quién son sus
        // marcaciones, igual que cargar un archivo nuevo.
        var ultimaAsignacionId = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => idsDelAlcance.Contains(a.TrabajadorId)
                        && a.FechaInicio <= rango.Hasta
                        && (a.FechaFin == null || a.FechaFin >= rango.Desde))
            .MaxAsync(a => (DateTime?)(a.ModificadoEn ?? a.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        // Una rotación registrada con fecha pasada cambia el turno de días ya calculados.
        // Cada forma de registrarla —abrir un tramo, cerrar el anterior, correr la frontera
        // entre los dos— deja creada o modificada la asignación que hoy cubre esos días, así
        // que basta con mirar las que se solapan con el rango.
        var ultimaAsignacionTurno = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => idsDelAlcance.Contains(a.TrabajadorId)
                        && a.FechaInicio <= rango.Hasta
                        && (a.FechaFin == null || a.FechaFin >= rango.Desde))
            .MaxAsync(a => (DateTime?)(a.ModificadoEn ?? a.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        // Editar el turno mismo —horario, tolerancia, ventanas, gracia de salida— cambia el
        // cálculo de todos los días de quienes lo tienen asignado. Solo cuentan los turnos
        // que alguien tiene en el rango: tocar uno sin uso no desactualiza nada.
        var ultimaEdicionTurno = await contexto.Turnos
            .AsNoTracking()
            .Where(t => contexto.AsignacionesTurno.Any(a =>
                            a.TurnoId == t.Id
                            && idsDelAlcance.Contains(a.TrabajadorId)
                            && a.FechaInicio <= rango.Hasta
                            && (a.FechaFin == null || a.FechaFin >= rango.Desde))
                        || contexto.TramosRotacion.Any(tr =>
                            tr.TurnoId == t.Id
                            && tr.Programacion!.Activo
                            && tr.Programacion.FechaInicio <= rango.Hasta
                            && (tr.Programacion.FechaFin == null || tr.Programacion.FechaFin >= rango.Desde)
                            && contexto.AsignacionesTurno.Any(a =>
                                a.CuadrillaId == tr.Programacion.CuadrillaId
                                && idsDelAlcance.Contains(a.TrabajadorId)
                                && a.FechaInicio <= rango.Hasta
                                && (a.FechaFin == null || a.FechaFin >= rango.Desde))))
            .MaxAsync(t => t.ModificadoEn, cancelacion)
            .ConfigureAwait(false);

        var ultimaVersionTurno = await contexto.VersionesTurno
            .AsNoTracking()
            .Where(v => v.Activo
                        && v.FechaInicio <= rango.Hasta
                        && (v.FechaFin == null || v.FechaFin >= rango.Desde.AddDays(-1))
                        && (contexto.AsignacionesTurno.Any(a =>
                                a.TurnoId == v.TurnoId
                                && idsDelAlcance.Contains(a.TrabajadorId)
                                && a.FechaInicio <= rango.Hasta
                                && (a.FechaFin == null || a.FechaFin >= rango.Desde))
                            || contexto.TramosRotacion.Any(tr =>
                                tr.TurnoId == v.TurnoId
                                && tr.Programacion!.Activo
                                && tr.Programacion.FechaInicio <= rango.Hasta
                                && (tr.Programacion.FechaFin == null || tr.Programacion.FechaFin >= rango.Desde)
                                && contexto.AsignacionesTurno.Any(a =>
                                    a.CuadrillaId == tr.Programacion.CuadrillaId
                                    && idsDelAlcance.Contains(a.TrabajadorId)
                                    && a.FechaInicio <= rango.Hasta
                                    && (a.FechaFin == null || a.FechaFin >= rango.Desde)))))
            .MaxAsync(v => (DateTime?)(v.ModificadoEn ?? v.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        // El ciclo de una cuadrilla decide el turno de sus integrantes: programarla, correr su
        // rotación o reemplazar una programación cambia días ya calculados. Se miran también
        // las dadas de baja, porque darlas de baja es justamente el cambio.
        var ultimaProgramacion = await contexto.ProgramacionesCuadrilla
            .AsNoTracking()
            .Where(p => p.FechaInicio <= rango.Hasta
                        && (p.FechaFin == null || p.FechaFin >= rango.Desde)
                        && contexto.AsignacionesTurno.Any(a =>
                            a.CuadrillaId == p.CuadrillaId
                            && idsDelAlcance.Contains(a.TrabajadorId)
                            && a.FechaInicio <= rango.Hasta
                            && (a.FechaFin == null || a.FechaFin >= rango.Desde)))
            .MaxAsync(p => (DateTime?)(p.ModificadoEn ?? p.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var ultimoTramoRotacion = await contexto.TramosRotacion
            .AsNoTracking()
            .Where(t => t.Programacion!.Activo
                        && t.Programacion.FechaInicio <= rango.Hasta
                        && (t.Programacion.FechaFin == null || t.Programacion.FechaFin >= rango.Desde)
                        && contexto.AsignacionesTurno.Any(a =>
                            a.CuadrillaId == t.Programacion.CuadrillaId
                            && idsDelAlcance.Contains(a.TrabajadorId)
                            && a.FechaInicio <= rango.Hasta
                            && (a.FechaFin == null || a.FechaFin >= rango.Desde)))
            .MaxAsync(t => (DateTime?)(t.ModificadoEn ?? t.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var ultimoFeriado = await contexto.Feriados
            .AsNoTracking()
            .Where(f => f.Activo
                        && f.Fecha >= rango.Desde && f.Fecha <= rango.Hasta
                        && (plantasDelAlcance == null
                            || f.PlantaId == null
                            || plantasDelAlcance.Contains(f.PlantaId.Value)))
            .MaxAsync(f => (DateTime?)(f.ModificadoEn ?? f.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        // Un alta laboral dentro del alcance exige generar sus días; no tener aún filas de
        // tareo para esa persona no puede volver invisible el alta.
        var ultimoTrabajadorNuevo = await contexto.PeriodosLaborales
            .AsNoTracking()
            .Where(p => idsDelAlcance.Contains(p.TrabajadorId)
                        && p.FechaIngreso <= rango.Hasta
                        && (p.FechaCese == null || p.FechaCese >= rango.Desde)
                        && !contexto.TareosDiarios.Any(td =>
                            td.TrabajadorId == p.TrabajadorId
                            && td.Fecha >= rango.Desde && td.Fecha <= rango.Hasta))
            .MaxAsync(p => (DateTime?)(p.ModificadoEn ?? p.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var ultimoCambioVinculo = await contexto.PeriodosLaborales
            .AsNoTracking()
            .Where(p => idsDelAlcance.Contains(p.TrabajadorId)
                        && p.FechaIngreso <= rango.Hasta
                        && (p.FechaCese == null || p.FechaCese >= rango.Desde))
            .MaxAsync(p => (DateTime?)(p.ModificadoEn ?? p.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var ultimoCambioPlantaLaboral = await contexto.AsignacionesPlanta
            .AsNoTracking()
            .Where(a => idsDelAlcance.Contains(a.PeriodoLaboral!.TrabajadorId)
                        && a.FechaInicio <= rango.Hasta
                        && (a.FechaFin == null || a.FechaFin >= rango.Desde))
            .MaxAsync(a => (DateTime?)(a.ModificadoEn ?? a.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var eventosAuditados = await contexto.RegistrosAuditoria
            .AsNoTracking()
            // Filtrar primero por tiempo permite que SQLite use el índice dedicado
            // fecha_hora. El índice (tabla, clave, fecha) no puede acotar bien por fecha si
            // no hay una clave concreta; las cuatro tablas se recortan después sobre el
            // conjunto, ya pequeño, de eventos posteriores al cálculo.
            .Where(r => r.FechaHora > calculadoMasAntiguo)
            .Select(r => new EventoAuditoria(
                r.Tabla, r.ClaveRegistro, r.Operacion, r.FechaHora, r.Detalle))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
        eventosAuditados = eventosAuditados
            .Where(e => e.Tabla is "justificacion" or "feriado" or "trabajador" or "tipo_ausencia")
            .ToList();

        (DateTime? Momento, string Motivo) ultimaAuditoriaRelevante = (null, string.Empty);

        if (eventosAuditados.Count > 0)
        {
            var idsTrabajadoresDelAlcance = idsDelAlcance.ToHashSet();
            var idsTiposAusenciaUsados = eventosAuditados.Any(e => e.Tabla == "tipo_ausencia")
                ? await contexto.Justificaciones
                    .AsNoTracking()
                    .Where(j => j.TipoAusenciaId != null
                                && j.Estado == Domain.Justificaciones.EstadoJustificacion.Aprobada
                                && j.Fecha >= rango.Desde && j.Fecha <= rango.Hasta
                                && idsDelAlcance.Contains(j.TrabajadorId))
                    .Select(j => j.TipoAusenciaId!.Value)
                    .Distinct()
                    .ToListAsync(cancelacion)
                    .ConfigureAwait(false)
                : [];

            ultimaAuditoriaRelevante = await ResolverAuditoriaRelevanteAsync(
                    eventosAuditados,
                    idsTrabajadoresDelAlcance,
                    idsTiposAusenciaUsados,
                    plantasDelAlcance,
                    rango,
                    cancelacion)
                .ConfigureAwait(false);
        }

        var cambios = new (DateTime? Momento, string Motivo)[]
        {
            (ultimaMarcacion, "una carga de marcaciones"),
            (ultimaAnulacion, "un cambio en el estado de una carga de marcaciones"),
            (ultimaJustificacion, "una justificación nueva o modificada"),
            (ultimaAsignacionId, "una asignación de ID de marcador nueva o corregida"),
            (ultimaAsignacionTurno, "un cambio de turno que rige en esas fechas"),
            (ultimaEdicionTurno, "una edición de un turno que rige en esas fechas"),
            (ultimaVersionTurno, "un cambio en la versión de un turno que rige en esas fechas"),
            (ultimaProgramacion, "un cambio en la programación de una cuadrilla en esas fechas"),
            (ultimoTramoRotacion, "un cambio en el ciclo de una cuadrilla en esas fechas"),
            (ultimoFeriado, "un cambio de feriado que rige en esas fechas"),
            (ultimoTrabajadorNuevo, "un alta laboral que alcanza esas fechas"),
            (ultimoCambioVinculo, "un cambio de periodo laboral que alcanza esas fechas"),
            (ultimoCambioPlantaLaboral, "un cambio de planta histórica que alcanza esas fechas"),
            ultimaAuditoriaRelevante
        };

        var ultimo = cambios
            .Where(c => c.Momento is not null)
            .OrderByDescending(c => c.Momento)
            .FirstOrDefault();

        return new EstadoCalculoTareo(calculadoMasAntiguo, ultimo.Momento, ultimo.Motivo);
    }

    private async Task<(DateTime? Momento, string Motivo)> ResolverAuditoriaRelevanteAsync(
        IReadOnlyCollection<EventoAuditoria> eventos,
        HashSet<long> trabajadoresDelAlcance,
        IReadOnlyCollection<long> tiposAusenciaUsados,
        IReadOnlyCollection<long>? plantasDelAlcance,
        RangoReporte rango,
        CancellationToken cancelacion)
    {
        var candidatos = new List<(DateTime Momento, string Motivo)>();
        var modificacionesJustificacion = eventos
            .Where(e => e.Tabla == "justificacion"
                        && e.Operacion is OperacionAuditada.Modificacion or OperacionAuditada.BajaLogica
                        && CambioTocaEstadoAprobado(LeerCambios(e.Detalle)))
            .Select(e => LeerId(e.ClaveRegistro))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var justificacionesModificadas = modificacionesJustificacion.Count == 0
            ? new Dictionary<long, (long TrabajadorId, DateOnly Fecha)>()
            : await contexto.Justificaciones.AsNoTracking()
                .Where(j => modificacionesJustificacion.Contains(j.Id))
                .Select(j => new { j.Id, j.TrabajadorId, j.Fecha })
                .ToDictionaryAsync(j => j.Id, j => (j.TrabajadorId, j.Fecha), cancelacion)
                .ConfigureAwait(false);

        foreach (var evento in eventos)
        {
            var cambios = LeerCambios(evento.Detalle);

            if (evento.Tabla == "justificacion")
            {
                long? trabajadorId = LeerLong(cambios, "TrabajadorId", anterior: true);
                DateOnly? fecha = LeerFecha(cambios, "Fecha", anterior: true);

                if ((trabajadorId is null || fecha is null)
                    && LeerId(evento.ClaveRegistro) is { } justificacionId
                    && justificacionesModificadas.TryGetValue(justificacionId, out var actual))
                {
                    trabajadorId = actual.TrabajadorId;
                    fecha = actual.Fecha;
                }

                var eliminaAprobada = evento.Operacion == OperacionAuditada.Eliminacion
                                      && string.Equals(
                                          LeerTexto(cambios, "Estado", anterior: true),
                                          Domain.Justificaciones.EstadoJustificacion.Aprobada.ToString(),
                                          StringComparison.Ordinal);
                var cambiaAprobacion = evento.Operacion
                                           is OperacionAuditada.Modificacion or OperacionAuditada.BajaLogica
                                       && CambioTocaEstadoAprobado(cambios);

                if ((eliminaAprobada || cambiaAprobacion)
                    && trabajadorId is { } id
                    && fecha is { } dia
                    && trabajadoresDelAlcance.Contains(id)
                    && dia >= rango.Desde && dia <= rango.Hasta)
                {
                    candidatos.Add((evento.FechaHora, "la eliminación o anulación de una justificación aprobada"));
                }
            }
            else if (evento.Tabla == "feriado" && evento.Operacion == OperacionAuditada.Eliminacion)
            {
                var fecha = LeerFecha(cambios, "Fecha", anterior: true);
                var plantaId = LeerLong(cambios, "PlantaId", anterior: true);

                if (fecha is { } dia
                    && dia >= rango.Desde && dia <= rango.Hasta
                    && (plantasDelAlcance is null
                        || plantaId is null
                        || plantasDelAlcance.Contains(plantaId.Value)))
                {
                    candidatos.Add((evento.FechaHora, "la eliminación de un feriado que regía en esas fechas"));
                }
            }
            else if (evento.Tabla == "trabajador"
                     && evento.Operacion is OperacionAuditada.Modificacion or OperacionAuditada.BajaLogica
                     && LeerId(evento.ClaveRegistro) is { } trabajadorId
                     && trabajadoresDelAlcance.Contains(trabajadorId)
                     && cambios.Any(c => CamposLaboralesQueCambianTareo.Contains(c.Campo)))
            {
                candidatos.Add((evento.FechaHora, "un cambio de vigencia laboral o planta"));
            }
            else if (evento.Tabla == "tipo_ausencia"
                     && evento.Operacion is OperacionAuditada.Modificacion or OperacionAuditada.BajaLogica
                     && LeerId(evento.ClaveRegistro) is { } tipoId
                     && tiposAusenciaUsados.Contains(tipoId)
                     && cambios.Any(c => CamposTipoAusenciaQueCambianTareo.Contains(c.Campo)))
            {
                candidatos.Add((evento.FechaHora, "un cambio en el tratamiento de una ausencia usada"));
            }
        }

        return candidatos.Count == 0
            ? (null, string.Empty)
            : candidatos.OrderByDescending(c => c.Momento).First();
    }

    private static readonly HashSet<string> CamposLaboralesQueCambianTareo =
        new(StringComparer.Ordinal) { "Activo", "FechaIngreso", "FechaCese", "PlantaId" };

    private static readonly HashSet<string> CamposTipoAusenciaQueCambianTareo =
        new(StringComparer.Ordinal) { "Activo", "Comportamiento", "ComputaComoAsistencia" };

    private static bool CambioTocaEstadoAprobado(IReadOnlyCollection<CambioAuditoria> cambios)
    {
        var cambio = cambios.FirstOrDefault(c => c.Campo == "Estado");
        return cambio is not null
               && (string.Equals(cambio.Anterior, "Aprobada", StringComparison.Ordinal)
                   || string.Equals(cambio.Nuevo, "Aprobada", StringComparison.Ordinal));
    }

    private static List<CambioAuditoria> LeerCambios(string detalle)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CambioAuditoria>>(detalle) ?? [];
        }
        catch (JsonException)
        {
            // Una fila histórica ilegible no debe derribar todos los reportes. Se conserva
            // en auditoría para revisión, pero no se inventa que afectaba este alcance.
            return [];
        }
    }

    private static long? LeerId(string clave) =>
        long.TryParse(clave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static string? LeerTexto(
        IReadOnlyCollection<CambioAuditoria> cambios,
        string campo,
        bool anterior)
    {
        var cambio = cambios.FirstOrDefault(c => c.Campo == campo);
        return anterior ? cambio?.Anterior : cambio?.Nuevo;
    }

    private static long? LeerLong(
        IReadOnlyCollection<CambioAuditoria> cambios,
        string campo,
        bool anterior)
    {
        var texto = LeerTexto(cambios, campo, anterior);
        return long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    private static DateOnly? LeerFecha(
        IReadOnlyCollection<CambioAuditoria> cambios,
        string campo,
        bool anterior)
    {
        var texto = LeerTexto(cambios, campo, anterior);
        return DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;
    }

    private sealed record EventoAuditoria(
        string Tabla,
        string ClaveRegistro,
        OperacionAuditada Operacion,
        DateTime FechaHora,
        string Detalle);

    private sealed record CambioAuditoria(string Campo, string? Anterior, string? Nuevo);

    // ------------------------------------------------------------------

    /// <summary>Trabajador con los nombres de catálogo ya resueltos, para no repetir joins.</summary>
    private sealed record TrabajadorResuelto(
        long Id,
        string Nombre,
        string? IdMarcador,
        string? Equipo,
        string Empresa,
        string Planta,
        string Area,
        string AreaCodigo,
        string Seccion,
        string SeccionCodigo,
        string Puesto,
        string Dni,
        DateOnly FechaIngreso,
        DateOnly? FechaCese,
        bool TieneHijos,
        string? CentroCodigo,
        string? CentroCostoCodigo,
        string? CentroCostoDescripcion,
        HistoriaLaboralTrabajador Historia,
        long? EmpresaFiltrada,
        long? PlantaFiltrada,
        IReadOnlySet<long>? PlantasVisibles)
    {
        public bool IncluyeFecha(DateOnly fecha)
        {
            var contexto = Historia.Resolver(fecha).ObtenerOError();

            if (contexto is null
                || EmpresaFiltrada is { } empresaId && contexto.EmpresaId != empresaId
                || PlantaFiltrada is { } plantaId && contexto.PlantaId != plantaId
                || PlantasVisibles is not null
                    && (contexto.PlantaId is null || !PlantasVisibles.Contains(contexto.PlantaId.Value)))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Los trabajadores que el reporte debe listar: los que tuvieron vínculo laboral
    /// <b>dentro del rango</b>, no todo el padrón activo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El solapamiento (<c>ingresó antes de que terminara</c> y <c>no había cesado cuando
    /// empezó</c>) es <b>el mismo filtro que aplica el motor</b> al calcular el periodo
    /// —<c>ServicioCalculoTareo</c>, al armar la lista de trabajadores—. Que sea el mismo no
    /// es coincidencia: si el reporte listara a alguien que el motor no calcula, esa fila
    /// saldría en cero, y en el tareo final saldría además con su fecha de cese y la
    /// observación «CESE», como si hubiera cesado este mes.
    /// </para>
    /// <para>
    /// Sin esto, alguien cesado en marzo aparecía en el tareo de julio y alguien contratado
    /// en agosto también: <c>Activo</c> es la baja lógica de la ficha, que a propósito
    /// <b>no</b> se apaga al registrar un cese (<c>Trabajador.DarDeBaja</c> solo escribe
    /// <c>FechaCese</c>), porque la ficha tiene que seguir consultable. Filtrar por
    /// <c>Activo</c> a secas confundía "existe" con "trabajaba en estas fechas".
    /// </para>
    /// <para>
    /// Se usan los campos planos <c>FechaIngreso</c>/<c>FechaCese</c> y no
    /// <c>PeriodoLaboral</c>, otra vez para coincidir con el motor: la historia de vínculos
    /// existe para no perder los reingresos, pero mientras el motor calcule contra los
    /// campos planos, el reporte tiene que leer lo mismo o los dos dirán cosas distintas.
    /// </para>
    /// </remarks>
    private async Task<List<TrabajadorResuelto>> ResolverTrabajadoresAsync(
        FiltroReporte filtro,
        RangoReporte rango,
        CancellationToken cancelacion)
    {
        // La población sale primero de la autoridad histórica. No se filtra por Activo ni
        // por las fechas planas: una ficha cesada o reingresada sigue siendo visible en el
        // rango donde sí tuvo vínculo.
        var historias = await consultaContextoLaboral
            .CargarRangoAsync(rango.Desde, rango.Hasta, cancelacion: cancelacion)
            .ConfigureAwait(false);

        if (historias.Count == 0)
        {
            return [];
        }

        var idsHistoricos = historias.Keys.ToList();
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => idsHistoricos.Contains(t.Id));

        if (filtro.CentroId is { } centroId)
        {
            consulta = consulta.Where(t =>
                contexto.CentrosCosto.Any(cc => cc.Id == t.CentroCostoId && cc.CentroId == centroId));
        }

        if (filtro.CentroCostoId is { } centroCostoId)
        {
            consulta = consulta.Where(t => t.CentroCostoId == centroCostoId);
        }

        // Por solapamiento con el rango y no por la asignación abierta hoy: quien cambió de
        // aparato en agosto tiene que seguir saliendo en el reporte de julio, porque las
        // marcaciones de julio salieron del aparato que tenía entonces. Es el invariante 5
        // aplicado al filtro.
        if (filtro.EquipoId is { } equipoId)
        {
            consulta = consulta.Where(t => contexto.AsignacionesMarcador.Any(a =>
                a.TrabajadorId == t.Id
                && a.EquipoId == equipoId
                && a.FechaInicio <= rango.Hasta
                && (a.FechaFin == null || a.FechaFin >= rango.Desde)));
        }

        // Aislamiento por planta: un ADMINISTRADOR solo reporta sobre sus plantas. Como TODAS
        // las salidas de reporte (resumen, excepciones, tareo final) resuelven su padrón por
        // aquí, filtrar este único método las cubre a las tres. Hoy Reportes es de COORDINADOR
        // y GERENCIA —ambos sin restricción—, así que es defensa en profundidad; mantiene el
        // invariante uniforme y queda listo si algún día la matriz de roles cambia. El
        // detector de vigencia usa este mismo proveedor para evaluar exactamente el alcance
        // que el usuario puede recalcular.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        var conjuntoPlantasVisibles = plantasVisibles?.ToHashSet();

        var planos = await consulta
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new
            {
                t.Id,
                Nombre = t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres,
                Area = contexto.Areas.Where(a => a.Id == t.AreaId).Select(a => a.Nombre).FirstOrDefault() ?? "-",
                AreaCodigo = contexto.Areas.Where(a => a.Id == t.AreaId).Select(a => a.Codigo).FirstOrDefault() ?? "-",
                Seccion = contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Nombre).FirstOrDefault() ?? "-",
                SeccionCodigo = contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Codigo).FirstOrDefault() ?? "-",
                Puesto = contexto.Puestos.Where(p => p.Id == t.PuestoId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.TieneHijos,
                CentroCodigo = contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Centro!.Codigo)
                    .FirstOrDefault(),
                CentroCostoCodigo = contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Codigo)
                    .FirstOrDefault(),
                CentroCostoDescripcion = contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Descripcion)
                    .FirstOrDefault()
            })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var empresaIds = historias.Values
            .SelectMany(h => h.Periodos)
            .Select(p => p.EmpresaId)
            .Distinct()
            .ToList();
        var plantaIds = historias.Values
            .SelectMany(h => h.Periodos)
            .SelectMany(p => p.Plantas)
            .Select(p => p.PlantaId)
            .Distinct()
            .ToList();

        var empresas = await contexto.Empresas.AsNoTracking()
            .Where(e => empresaIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.RazonSocial, cancelacion)
            .ConfigureAwait(false);
        var plantas = await contexto.Plantas.AsNoTracking()
            .Where(p => plantaIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Nombre, cancelacion)
            .ConfigureAwait(false);

        var trabajadores = new List<TrabajadorResuelto>(planos.Count);

        foreach (var plano in planos)
        {
            var historia = historias[plano.Id];
            var periodos = historia.EnRango(rango.Desde, rango.Hasta);

            if (filtro.EmpresaId is { } empresaId
                && !periodos.Any(p => p.EmpresaId == empresaId))
            {
                continue;
            }

            var tramosPlanta = periodos
                .SelectMany(p => p.Plantas)
                .Where(p => p.FechaInicio <= rango.Hasta
                            && (p.FechaFin is null || p.FechaFin.Value >= rango.Desde))
                .OrderBy(p => p.FechaInicio)
                .ToList();

            if (filtro.PlantaId is { } plantaId
                && !tramosPlanta.Any(p => p.PlantaId == plantaId))
            {
                continue;
            }

            if (conjuntoPlantasVisibles is not null
                && !tramosPlanta.Any(p => conjuntoPlantasVisibles.Contains(p.PlantaId)))
            {
                continue;
            }

            var empresasDelRango = periodos
                .Where(p => filtro.EmpresaId is null || p.EmpresaId == filtro.EmpresaId.Value)
                .Select(p => empresas.GetValueOrDefault(p.EmpresaId, "-"))
                .Distinct()
                .ToList();
            var plantasDelRango = tramosPlanta
                .Where(p => filtro.PlantaId is null || p.PlantaId == filtro.PlantaId.Value)
                .Where(p => conjuntoPlantasVisibles is null || conjuntoPlantasVisibles.Contains(p.PlantaId))
                .Select(p => plantas.GetValueOrDefault(p.PlantaId, "-"))
                .Distinct()
                .ToList();

            var primero = periodos[0];
            var ultimo = periodos[^1];

            trabajadores.Add(new TrabajadorResuelto(
                plano.Id,
                plano.Nombre,
                null,
                null,
                string.Join(" → ", empresasDelRango),
                plantasDelRango.Count == 0 ? "-" : string.Join(" → ", plantasDelRango),
                plano.Area,
                plano.AreaCodigo,
                plano.Seccion,
                plano.SeccionCodigo,
                plano.Puesto,
                plano.NumeroDocumento,
                primero.FechaInicio,
                ultimo.FechaFin,
                plano.TieneHijos,
                plano.CentroCodigo,
                plano.CentroCostoCodigo,
                plano.CentroCostoDescripcion,
                historia,
                filtro.EmpresaId,
                filtro.PlantaId,
                conjuntoPlantasVisibles));
        }

        trabajadores = await ResolverMarcadoresDelRangoAsync(trabajadores, rango, cancelacion)
            .ConfigureAwait(false);

        return await ResolverAreasDelRangoAsync(trabajadores, rango, cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Reemplaza el área de cada trabajador por la que tuvo <b>dentro del rango</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Salía de <c>Trabajador.AreaId</c>, que es la de <b>hoy</b>: un resumen de julio
    /// emitido en setiembre mostraba a la persona en el área a la que la movieron después, y
    /// la columna <c>AREA</c> del tareo final —que va a planilla— cambiaba sola en un mes ya
    /// entregado. Es el mismo error que <see cref="ResolverMarcadoresDelRangoAsync"/> corrige
    /// para el número y el aparato.
    /// </para>
    /// <para>
    /// Quien cambió de área dentro del rango sale con las dos, en orden y en la misma celda
    /// («AEROSOLES → ENVASADO»), por lo mismo que el marcador: la fila TOTAL del Excel va en
    /// columnas fijas y una columna más correría todos los datos.
    /// </para>
    /// <para>
    /// Sin ninguna asignación en el rango se conserva lo ya proyectado desde la ficha. Es el
    /// caso de las fichas anteriores a esta vigencia cuyo relleno no alcanzó, y el valor de
    /// hoy es mejor respaldo que un «-» que rompería el cruce con SAP.
    /// </para>
    /// </remarks>
    private async Task<List<TrabajadorResuelto>> ResolverAreasDelRangoAsync(
        List<TrabajadorResuelto> trabajadores,
        RangoReporte rango,
        CancellationToken cancelacion)
    {
        if (trabajadores.Count == 0)
        {
            return trabajadores;
        }

        var ids = trabajadores.ConvertAll(t => t.Id);

        var asignaciones = await contexto.AsignacionesArea
            .AsNoTracking()
            .Where(a => ids.Contains(a.TrabajadorId)
                        && a.FechaInicio <= rango.Hasta
                        && (a.FechaFin == null || a.FechaFin >= rango.Desde))
            .OrderBy(a => a.FechaInicio)
            .Select(a => new { a.TrabajadorId, Nombre = a.Area!.Nombre, Codigo = a.Area!.Codigo })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porTrabajador = asignaciones
            .GroupBy(a => a.TrabajadorId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Nombres: string.Join(" → ", g.Select(a => a.Nombre).Distinct()),
                    Codigos: string.Join(" → ", g.Select(a => a.Codigo).Distinct())));

        return trabajadores.ConvertAll(t => porTrabajador.TryGetValue(t.Id, out var suyas)
            ? t with { Area = suyas.Nombres, AreaCodigo = suyas.Codigos }
            : t);
    }

    /// <summary>
    /// Completa el número de marcador y el aparato de cada trabajador con los que tuvo
    /// <b>dentro del rango</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Antes salían de la asignación <b>abierta hoy</b> (<c>FechaFin == null</c>). Un
    /// reporte de julio emitido en setiembre mostraba entonces el número que la persona tiene
    /// ahora, no aquel del que salieron las marcaciones que produjeron ese tareo: la columna
    /// contradecía a la pantalla de Tareo y a la carga de marcaciones. Es el invariante 5,
    /// que el motor ya respeta al atribuir.
    /// </para>
    /// <para>
    /// Quien cambió de número o de aparato dentro del rango sale con los dos, en orden
    /// («14 → 27»), en la misma celda: el Excel de resumen y el de excepciones escriben la
    /// fila TOTAL en columnas fijas, así que una columna más correría los datos y dejaría
    /// cada total bajo el título equivocado.
    /// </para>
    /// <para>
    /// Sin ninguna asignación en el rango queda nulo, y la pantalla muestra «—». Es lo
    /// correcto: esa persona no tenía número entonces y sus días salen sin marcaciones.
    /// </para>
    /// </remarks>
    private async Task<List<TrabajadorResuelto>> ResolverMarcadoresDelRangoAsync(
        List<TrabajadorResuelto> trabajadores,
        RangoReporte rango,
        CancellationToken cancelacion)
    {
        if (trabajadores.Count == 0)
        {
            return trabajadores;
        }

        var ids = trabajadores.ConvertAll(t => t.Id);

        var asignaciones = await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Where(a => ids.Contains(a.TrabajadorId)
                        && a.FechaInicio <= rango.Hasta
                        && (a.FechaFin == null || a.FechaFin >= rango.Desde))
            .OrderBy(a => a.FechaInicio)
            .Select(a => new { a.TrabajadorId, a.IdMarcador, Equipo = a.Equipo!.Nombre })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var porTrabajador = asignaciones
            .GroupBy(a => a.TrabajadorId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Numeros: string.Join(" → ", g.Select(a => a.IdMarcador).Distinct()),
                    Equipos: string.Join(" → ", g.Select(a => a.Equipo).Distinct())));

        return trabajadores.ConvertAll(t => porTrabajador.TryGetValue(t.Id, out var suyas)
            ? t with { IdMarcador = suyas.Numeros, Equipo = suyas.Equipos }
            : t);
    }

    // ------------------------------------------------------------------
    // Normativa: RMV, sobretasas y parámetros legales con vigencia por fecha
    // ------------------------------------------------------------------

    /// <summary>
    /// Un valor con vigencia por fecha, proyectado sin cargar la entidad completa: es lo
    /// mínimo para resolver en memoria "qué valor regía tal fecha".
    /// </summary>
    private sealed record ValorVigente(decimal Valor, DateOnly VigenteDesde, DateOnly? VigenteHasta)
    {
        public bool EstaVigenteEn(DateOnly fecha) =>
            fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta.Value);
    }

    private async Task<List<ValorVigente>> ObtenerParametroLegalAsync(
        TipoParametroLegal tipo, CancellationToken cancelacion) =>
        await contexto.ParametrosLegales
            .AsNoTracking()
            .Where(p => p.Activo && p.Tipo == tipo)
            .Select(p => new ValorVigente(p.Valor, p.VigenteDesde, p.VigenteHasta))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    private async Task<List<ValorVigente>> ObtenerTasaSobretasaAsync(
        ConceptoSobretasa concepto, CancellationToken cancelacion) =>
        await contexto.TasasSobretasa
            .AsNoTracking()
            .Where(t => t.Activo && t.Concepto == concepto)
            .Select(t => new ValorVigente(t.Porcentaje, t.VigenteDesde, t.VigenteHasta))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <summary>
    /// Resuelve el valor vigente a una fecha, o falla con un mensaje accionable.
    /// </summary>
    /// <remarks>
    /// REGLA DURA, no negociable: nunca un 0 silencioso por falta de parámetro. Si
    /// <paramref name="valores"/> no tiene ninguna fila vigente para <paramref name="fecha"/>
    /// (por ejemplo, se está liquidando un periodo anterior a que existiera el dato), el
    /// trabajador cobraría 0 de bono nocturno, movilidad o asignación familiar sin que nadie
    /// se entere — exactamente la clase de falla que este sistema existe para evitar (ver
    /// CLAUDE.md: "cada resultado se explica"). Se prefiere que la generación del reporte
    /// falle a la vista, con un mensaje que dice qué cargar.
    /// </remarks>
    private static decimal ResolverVigente(IReadOnlyList<ValorVigente> valores, DateOnly fecha, string etiqueta)
    {
        var vigente = valores.FirstOrDefault(v => v.EstaVigenteEn(fecha));

        if (vigente is null)
        {
            throw new ReglaNegocioException(
                $"No hay {etiqueta} vigente para el {fecha:dd/MM/yyyy}: cargue el parámetro legal " +
                "antes de generar el tareo final.");
        }

        return vigente.Valor;
    }

    /// <summary>
    /// Bono nocturno del D.S. 007-2002-TR art. 8, sumado día por día.
    /// </summary>
    /// <remarks>
    /// <see cref="CalculadoraBonoNocturno"/> es la función pura que hace la cuenta; este
    /// método solo resuelve, para cada día con minutos en franja nocturna, los tres números
    /// que ella necesita —RMV, sobretasa y sueldo básico— <b>vigentes ese día</b>, no en una
    /// fecha fija del periodo. Si el sueldo cambió a mitad de mes, el día 10 usa el sueldo
    /// de antes del cambio y el día 20 el de después.
    /// </remarks>
    private async Task<Dictionary<long, decimal>> CalcularBonoNocturnoPorTrabajadorAsync(
        List<(long TrabajadorId, DateOnly Fecha, int MinutosNocturnos)> diasNocturnos,
        List<TrabajadorResuelto> trabajadores,
        CancellationToken cancelacion)
    {
        var resultado = new Dictionary<long, decimal>();

        if (diasNocturnos.Count == 0)
        {
            return resultado;
        }

        var rmv = await ObtenerParametroLegalAsync(TipoParametroLegal.RemuneracionMinimaVital, cancelacion)
            .ConfigureAwait(false);

        var tasaNocturna = await ObtenerTasaSobretasaAsync(ConceptoSobretasa.TrabajoNocturno, cancelacion)
            .ConfigureAwait(false);

        var idsConNocturnidad = diasNocturnos.Select(d => d.TrabajadorId).Distinct().ToList();

        var sueldos = await contexto.AsignacionesSueldo
            .AsNoTracking()
            .Where(a => a.Activo && idsConNocturnidad.Contains(a.TrabajadorId))
            .Select(a => new { a.TrabajadorId, a.SueldoBasicoMensual, a.FechaInicio, a.FechaFin })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var sueldosPorTrabajador = sueldos.ToLookup(s => s.TrabajadorId);
        var nombresPorId = trabajadores.ToDictionary(t => t.Id, t => t.Nombre);

        foreach (var dia in diasNocturnos)
        {
            var rmvVigente = ResolverVigente(rmv, dia.Fecha, "la Remuneración Mínima Vital (RMV)");
            var tasaVigente = ResolverVigente(tasaNocturna, dia.Fecha, "la sobretasa de trabajo nocturno");

            var sueldoVigente = sueldosPorTrabajador[dia.TrabajadorId]
                .FirstOrDefault(s => s.FechaInicio <= dia.Fecha && (s.FechaFin is null || s.FechaFin >= dia.Fecha));

            if (sueldoVigente is null)
            {
                var nombre = nombresPorId.GetValueOrDefault(dia.TrabajadorId, $"El trabajador {dia.TrabajadorId}");

                throw new ReglaNegocioException(
                    $"{nombre} no tiene sueldo básico vigente al {dia.Fecha:dd/MM/yyyy}, día con minutos " +
                    "en franja nocturna: no se puede calcular su bono nocturno sin ese dato. Cargue su " +
                    "asignación de sueldo antes de generar el reporte.");
            }

            var diasDelMes = DateTime.DaysInMonth(dia.Fecha.Year, dia.Fecha.Month);

            var bono = CalculadoraBonoNocturno.BonoDiario(
                rmvVigente, tasaVigente, sueldoVigente.SueldoBasicoMensual, diasDelMes);

            resultado[dia.TrabajadorId] = resultado.GetValueOrDefault(dia.TrabajadorId) + bono;
        }

        return resultado;
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
    private const string CodigoSubsidioAccidente = "SUBA";
    private const string CodigoPermiso = "PER";

    /// <summary>
    /// Texto de la columna <c>CONTINGENCIA</c>, tal cual aparece en el archivo de GCA.
    /// </summary>
    /// <remarks>
    /// No se usa <c>ToString()</c> del enum: daría "EnfermedadComun". El archivo modelo
    /// trae "ENFERMEDAD COMUN", en mayúsculas y sin tilde, y la hoja se pega tal cual en la
    /// plantilla que planilla ya tiene armada.
    /// </remarks>
    private static string? TextoContingencia(Contingencia? contingencia) => contingencia switch
    {
        Contingencia.EnfermedadComun => "ENFERMEDAD COMUN",
        Contingencia.AccidenteLaboral => "ACCIDENTE LABORAL",
        _ => null
    };

    /// <summary>Texto de la columna <c>CENTRO DE ATENCIÓN</c>, como en el archivo de GCA.</summary>
    private static string? TextoCentroAtencion(CentroAtencion? centro) => centro switch
    {
        CentroAtencion.Essalud => "ESSALUD",
        CentroAtencion.Particular => "PARTICULAR",
        _ => null
    };

    private async Task<List<FilaTareoFinalDto>> ArmarFilasTareoAsync(
        Periodo periodo,
        List<TrabajadorResuelto> trabajadores,
        List<long> ids,
        CancellationToken cancelacion)
    {
        // El código del tipo de ausencia se trae junto al día para poder repartir los días
        // en las columnas DM / VACACI. / LIC. ... sin una consulta por columna.
        var dias = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.Fecha >= periodo.FechaInicio && t.Fecha <= periodo.FechaFin
                        && ids.Contains(t.TrabajadorId))
            .Select(t => new
            {
                t.TrabajadorId,
                t.Fecha,
                t.EntradaReal,
                t.SalidaReal,
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

        var trabajadorPorId = trabajadores.ToDictionary(t => t.Id);
        dias = dias
            .Where(d => trabajadorPorId[d.TrabajadorId].IncluyeFecha(d.Fecha))
            .ToList();

        var porTrabajador = dias.ToLookup(d => d.TrabajadorId);

        // Reintegros y descuentos manuales, cargados a mano por planilla en su propia
        // pantalla (bloque AO-BF). Proyectados sin la entidad completa: son 18 columnas,
        // no hace falta traer Id/auditoría/lo que no se muestra.
        var reintegros = await contexto.ReintegrosTareo
            .AsNoTracking()
            .Where(r => r.PeriodoId == periodo.Id && ids.Contains(r.TrabajadorId))
            .Select(r => new
            {
                r.TrabajadorId,
                r.HorasNormales,
                r.HorasExtra25,
                r.HorasExtra35,
                r.HorasNoLaborable,
                r.MontoMovilidad,
                r.HorasNocturnas,
                r.HorasExtra25Nocturnas,
                r.HorasExtra35Nocturnas,
                r.HorasNoLaborableNocturnas,
                r.Viatico,
                r.DiasDescansoMedico,
                r.DiasSubsidioMaternidad,
                r.DiasSubsidioAccidente,
                r.DiasLicenciaPaternidad,
                r.LicenciaConGoce,
                r.ReintegroAsignacionFamiliar,
                r.ReintegroAfecto,
                r.DescuentoPagoIndebido
            })
            .ToDictionaryAsync(r => r.TrabajadorId, cancelacion)
            .ConfigureAwait(false);

        // Movilidad y asignación familiar comparten vigencia (ver DatosSemilla), así que se
        // resuelven una sola vez con una fecha dentro del periodo — a diferencia del bono
        // nocturno, que sí varía día a día y se resuelve aparte, en ResumenTareoAsync.
        var fechaResolucion = periodo.FechaFin;

        var valorMovilidadDiaria = ResolverVigente(
            await ObtenerParametroLegalAsync(TipoParametroLegal.ValorMovilidadDiaria, cancelacion)
                .ConfigureAwait(false),
            fechaResolucion,
            "el valor de movilidad diaria");

        var rmv = ResolverVigente(
            await ObtenerParametroLegalAsync(TipoParametroLegal.RemuneracionMinimaVital, cancelacion)
                .ConfigureAwait(false),
            fechaResolucion,
            "la Remuneración Mínima Vital (RMV)");

        var porcentajeAsignacionFamiliar = ResolverVigente(
            await ObtenerParametroLegalAsync(TipoParametroLegal.PorcentajeAsignacionFamiliar, cancelacion)
                .ConfigureAwait(false),
            fechaResolucion,
            "el porcentaje de asignación familiar");

        // Ley 25129: 10 % de la RMV, monto fijo mensual — no se prorratea por días.
        var asignacionFamiliarSiCorresponde =
            Math.Round(porcentajeAsignacionFamiliar / 100m * rmv, 2, MidpointRounding.AwayFromZero);

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

                // "Día asistido" para movilidad: hubo marcación real de entrada Y salida, y
                // el día quedó Puntual o Tardanza. NO es MinutosTrabajados > 0: ese campo
                // también se rellena en un día de vacaciones justificado (para pagarlo en
                // horas), y un trabajador de vacaciones no viajó a la planta ese día.
                var diasAsistidos = suyos.Count(d =>
                    d.EntradaReal != null && d.SalidaReal != null
                    && (d.Estado == EstadoTareo.Puntual || d.Estado == EstadoTareo.Tardanza));

                reintegros.TryGetValue(t.Id, out var reintegro);

                return new FilaTareoFinalDto
                {
                    Numero = ++numero,
                    Dni = t.Dni,
                    ApellidosYNombres = t.Nombre,
                    Planta = t.Planta,

                    // Las tres columnas de CECO salen siempre de la misma fila del catálogo
                    // real que entregó GCA (Organizacion/Centro y Organizacion/CentroCosto):
                    // CENTRO = Centro.Codigo (CO_AREA), CENTRO DE COSTO = CentroCosto.Codigo
                    // (CO_SECC) y DESCRIP. CECO = CentroCosto.Descripcion (DE_SECC).
                    //
                    // Sin respaldo con Área/Sección: AREA es ahora una columna propia del
                    // formato, y rellenar el CECO con la jerarquía interna daría un código de
                    // centro de costo que no existe en SAP. Si el trabajador todavía no tiene
                    // uno asignado, las tres salen vacías — el dato falta y así se ve.
                    Centro = t.CentroCodigo,
                    CentroCosto = t.CentroCostoCodigo,
                    DescripcionCeco = t.CentroCostoDescripcion,

                    // Área interna de trabajo (ENVASADO, LAVAVAJILLAS, PROCESOS…). Es un dato
                    // por trabajador y no se deriva del centro de costo.
                    Area = t.Area,

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
                    MinutosNoLaborableNocturnos = noLaborables.Sum(d => d.MinutosNocturnos),

                    MontoMovilidad = diasAsistidos * valorMovilidadDiaria,
                    MontoAsignacionFamiliar = t.TieneHijos ? asignacionFamiliarSiCorresponde : 0m,

                    ReintegroHorasNormales = reintegro?.HorasNormales ?? 0m,
                    ReintegroHorasExtra25 = reintegro?.HorasExtra25 ?? 0m,
                    ReintegroHorasExtra35 = reintegro?.HorasExtra35 ?? 0m,
                    ReintegroHorasNoLaborable = reintegro?.HorasNoLaborable ?? 0m,
                    ReintegroMontoMovilidad = reintegro?.MontoMovilidad ?? 0m,
                    ReintegroHorasNocturnas = reintegro?.HorasNocturnas ?? 0m,
                    ReintegroHorasExtra25Nocturnas = reintegro?.HorasExtra25Nocturnas ?? 0m,
                    ReintegroHorasExtra35Nocturnas = reintegro?.HorasExtra35Nocturnas ?? 0m,
                    ReintegroHorasNoLaborableNocturnas = reintegro?.HorasNoLaborableNocturnas ?? 0m,
                    ReintegroViatico = reintegro?.Viatico ?? 0m,
                    ReintegroDiasDescansoMedico = reintegro?.DiasDescansoMedico ?? 0,
                    ReintegroDiasSubsidioMaternidad = reintegro?.DiasSubsidioMaternidad ?? 0,
                    ReintegroDiasSubsidioAccidente = reintegro?.DiasSubsidioAccidente ?? 0,
                    ReintegroDiasLicenciaPaternidad = reintegro?.DiasLicenciaPaternidad ?? 0,
                    ReintegroLicenciaConGoce = reintegro?.LicenciaConGoce ?? 0m,
                    ReintegroAsignacionFamiliar = reintegro?.ReintegroAsignacionFamiliar ?? 0m,
                    ReintegroAfecto = reintegro?.ReintegroAfecto ?? 0m,
                    ReintegroDescuentoPagoIndebido = reintegro?.DescuentoPagoIndebido ?? 0m
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
                s.Contingencia,
                s.Diagnostico,
                s.CentroAtencion,
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

        // La hoja se llama "Descanso Medico" pero el formato de GCA la define como el
        // detalle de descansos médicos Y subsidios. Se incluye el subsidio por accidente
        // (SUBA) junto con DM y SUBE: excluirlo dejaba fuera de la entrega el único tipo
        // cuya contingencia es siempre ACCIDENTE LABORAL, y desde que la solicitud exige
        // detalle clínico ese dato se capturaba para no llegar a ninguna parte.
        var descansos = solicitudes
            .Where(s => s.Codigo is CodigoDescansoMedico
                                 or CodigoSubsidioEnfermedad
                                 or CodigoSubsidioAccidente)
            .Select(s => new FilaDescansoMedicoDto(
                nombreMes,
                razonSocial,
                porId[s.TrabajadorId].Planta,
                porId[s.TrabajadorId].Dni,
                porId[s.TrabajadorId].Nombre,
                s.FechaInicio,
                s.FechaFin,
                DiasEnPeriodo(s.FechaInicio, s.FechaFin),
                // Las tres columnas clínicas se escriben con el texto exacto del archivo
                // modelo de GCA (ENFERMEDAD COMUN / ACCIDENTE LABORAL, ESSALUD /
                // PARTICULAR), en mayúsculas y sin tildes: la hoja se pega en su plantilla
                // y un "EsSalud" con otra grafía rompe los filtros que ellos ya tienen.
                TextoContingencia(s.Contingencia),
                s.Diagnostico,
                TextoCentroAtencion(s.CentroAtencion),
                // El modelo distingue el descanso corto del que ya pasó a subsidio.
                Observacion: s.Codigo is CodigoSubsidioEnfermedad or CodigoSubsidioAccidente
                    ? "SUBSIDIO"
                    : "DESCANSO MEDICO"))
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
