using System.Globalization;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Normativa;
using ControlMarcacion.Domain.Reportes;
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

        var rango = await ResolverRangoAsync(filtro, cancelacion).ConfigureAwait(false);

        var trabajadores = await ResolverTrabajadoresAsync(filtro, rango, cancelacion).ConfigureAwait(false);

        if (trabajadores.Count == 0)
        {
            return [];
        }

        var ids = trabajadores.Select(t => t.Id).ToList();

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

        var consulta = contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.Fecha >= rango.Desde && t.Fecha <= rango.Hasta
                        && ids.Contains(t.TrabajadorId)
                        && EstadosDeExcepcion.Contains(t.Estado));

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var ordenada = consulta.OrderBy(t => t.Fecha).ThenBy(t => t.TrabajadorId);

        // El recorte baja a SQL. Sin paginación el Skip/Take no se aplica y la consulta
        // queda exactamente como estaba, que es lo que exporta el Excel.
        var recortada = pagina is { } numero
            ? ordenada.Skip((numero - 1) * tamanioPagina).Take(tamanioPagina)
            : (IQueryable<TareoDiario>)ordenada;

        var excepciones = await recortada
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

        var filas = excepciones.ConvertAll(e => new ExcepcionDto(
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

        var calculo = await EvaluarVigenciaDelCalculoAsync(rango, cancelacion).ConfigureAwait(false);

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
    /// Compara cuándo se calculó el tareo del alcance contra lo último que pudo cambiarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este módulo consulta el tareo que dejó el motor y no lo recalcula: por eso un reporte
    /// puede salir con cifras viejas sin verse distinto. Las tres cosas que mueven el
    /// resultado de un día ya calculado son una <b>marcación</b> nueva sobre ese día, la
    /// <b>anulación o reactivación</b> de la carga que la trajo, y una <b>justificación</b>
    /// creada o modificada. Si alguna es posterior al último <c>CalculadoEn</c> del rango,
    /// el reporte está desactualizado y hay que decirlo.
    /// </para>
    /// <para>
    /// Son cuatro agregados escalares sobre índices ya existentes, y solo se pagan al pedir
    /// los metadatos —es decir, una vez por pantalla o por exportación—, no por fila.
    /// </para>
    /// <para>
    /// ⚠️ Deliberadamente <b>no</b> se filtra por los trabajadores del filtro de organización:
    /// la pregunta que responde es "¿el tareo de estas fechas está al día?", que es del
    /// periodo y no de a quién se esté mirando. Filtrarlo haría que el mismo periodo
    /// apareciera al día en una planta y desactualizado en otra.
    /// </para>
    /// </remarks>
    private async Task<EstadoCalculoTareo> EvaluarVigenciaDelCalculoAsync(
        RangoReporte rango,
        CancellationToken cancelacion)
    {
        var calculadoMasReciente = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.Fecha >= rango.Desde && t.Fecha <= rango.Hasta)
            .MaxAsync(t => (DateTime?)t.CalculadoEn, cancelacion)
            .ConfigureAwait(false);

        if (calculadoMasReciente is null)
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
            .Where(m => m.FechaHora >= desde && m.FechaHora <= hasta)
            .MaxAsync(m => (DateTime?)m.CreadoEn, cancelacion)
            .ConfigureAwait(false);

        // Anular o reactivar una carga no borra ni inserta ninguna marcación, pero cambia
        // cuáles cuenta el motor: sin esto, "subí el archivo equivocado y lo anulé" dejaría
        // el reporte viejo pasando por vigente.
        var ultimaAnulacion = await contexto.Importaciones
            .AsNoTracking()
            .Where(i => contexto.MarcacionesCrudas
                .Any(m => m.ImportacionId == i.Id && m.FechaHora >= desde && m.FechaHora <= hasta))
            .MaxAsync(i => i.ModificadoEn, cancelacion)
            .ConfigureAwait(false);

        var ultimaJustificacion = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.Fecha >= rango.Desde && j.Fecha <= rango.Hasta)
            .MaxAsync(j => (DateTime?)(j.ModificadoEn ?? j.CreadoEn), cancelacion)
            .ConfigureAwait(false);

        var cambios = new (DateTime? Momento, string Motivo)[]
        {
            (ultimaMarcacion, "una carga de marcaciones"),
            (ultimaAnulacion, "un cambio en el estado de una carga de marcaciones"),
            (ultimaJustificacion, "una justificación nueva o modificada")
        };

        var ultimo = cambios
            .Where(c => c.Momento is not null)
            .OrderByDescending(c => c.Momento)
            .FirstOrDefault();

        return new EstadoCalculoTareo(calculadoMasReciente, ultimo.Momento, ultimo.Motivo);
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
        string? CentroCostoDescripcion);

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
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo
                        && t.FechaIngreso <= rango.Hasta
                        && (t.FechaCese == null || t.FechaCese >= rango.Desde));

        if (filtro.EmpresaId is { } empresaId)
        {
            consulta = consulta.Where(t => t.EmpresaId == empresaId);
        }

        if (filtro.PlantaId is { } plantaId)
        {
            consulta = consulta.Where(t => t.PlantaId == plantaId);
        }

        if (filtro.CentroId is { } centroId)
        {
            consulta = consulta.Where(t =>
                contexto.CentrosCosto.Any(cc => cc.Id == t.CentroCostoId && cc.CentroId == centroId));
        }

        if (filtro.CentroCostoId is { } centroCostoId)
        {
            consulta = consulta.Where(t => t.CentroCostoId == centroCostoId);
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
                contexto.Areas.Where(a => a.Id == t.AreaId).Select(a => a.Codigo).FirstOrDefault() ?? "-",
                contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Nombre).FirstOrDefault() ?? "-",
                contexto.Secciones.Where(s => s.Id == t.SeccionId).Select(s => s.Codigo).FirstOrDefault() ?? "-",
                contexto.Puestos.Where(p => p.Id == t.PuestoId).Select(p => p.Nombre).FirstOrDefault() ?? "-",
                t.NumeroDocumento,
                t.FechaIngreso,
                t.FechaCese,
                t.TieneHijos,
                // Catálogo real de CECO (Centro/CentroCosto). Nulo mientras nadie le haya
                // asignado uno al trabajador: ArmarFilasTareoAsync recurre entonces a la
                // aproximación con Área/Sección, no a un "-" que rompería el cruce.
                contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Centro!.Codigo)
                    .FirstOrDefault(),
                contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Codigo)
                    .FirstOrDefault(),
                contexto.CentrosCosto
                    .Where(cc => cc.Id == t.CentroCostoId)
                    .Select(cc => cc.Descripcion)
                    .FirstOrDefault()))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
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
