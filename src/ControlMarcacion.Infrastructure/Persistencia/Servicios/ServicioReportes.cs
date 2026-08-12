using System.Globalization;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Normativa;
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

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == filtro.PeriodoId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el periodo", filtro.PeriodoId);

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
        // tarifa vigente al cierre del periodo.
        var valorMovilidadDiaria = ResolverVigente(
            await ObtenerParametroLegalAsync(TipoParametroLegal.ValorMovilidadDiaria, cancelacion)
                .ConfigureAwait(false),
            periodo.FechaFin,
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

        var filasTareo = await ArmarFilasTareoAsync(filtro, periodo, trabajadores, ids, cancelacion)
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
        FiltroReporte filtro,
        Periodo periodo,
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
            .Where(r => r.PeriodoId == filtro.PeriodoId && ids.Contains(r.TrabajadorId))
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
