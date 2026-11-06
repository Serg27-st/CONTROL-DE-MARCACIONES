using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Cuadrillas y su ciclo de rotación. Ver <see cref="IServicioCuadrillas"/>.
/// </summary>
internal sealed class ServicioCuadrillas(MarcacionDbContext contexto) : IServicioCuadrillas
{
    /// <summary>Cuántos días hacia adelante se buscan las próximas rotaciones del listado.</summary>
    private const int HorizonteProximosCambios = 120;

    private const int CantidadProximosCambios = 3;

    public async Task<IReadOnlyList<CuadrillaListaDto>> ListarAsync(
        DateOnly fecha,
        bool incluirInactivas,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Cuadrillas.AsNoTracking();

        if (!incluirInactivas)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        var cuadrillas = await consulta
            .OrderBy(c => c.Planta!.Nombre)
            .ThenBy(c => c.Codigo)
            .Select(c => new { c.Id, c.Codigo, c.Nombre, c.PlantaId, Planta = c.Planta!.Nombre, c.Activo })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var ids = cuadrillas.ConvertAll(c => c.Id);

        var integrantes = await contexto.AsignacionesTurno
            .AsNoTracking()
            .Where(a => a.CuadrillaId != null
                        && ids.Contains(a.CuadrillaId.Value)
                        && a.FechaInicio <= fecha
                        && (a.FechaFin == null || a.FechaFin >= fecha))
            .GroupBy(a => a.CuadrillaId!.Value)
            .Select(g => new { CuadrillaId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.CuadrillaId, g => g.Cantidad, cancelacion)
            .ConfigureAwait(false);

        var programaciones = await ProgramacionesActivasAsync(ids, cancelacion).ConfigureAwait(false);

        return cuadrillas.ConvertAll(c =>
        {
            var propias = programaciones.GetValueOrDefault(c.Id, []);

            // La que rige hoy o, si todavía no rige ninguna, la próxima: una cuadrilla recién
            // programada para el lunes tiene que verse programada desde el viernes.
            var vigente = propias.FirstOrDefault(p => p.EstaVigenteEn(fecha))
                          ?? propias.Where(p => p.FechaInicio > fecha).MinBy(p => p.FechaInicio);

            var turnoHoy = propias.FirstOrDefault(p => p.EstaVigenteEn(fecha)) is { } hoy
                ? DescribirResultado(hoy, fecha)
                : null;

            return new CuadrillaListaDto(
                c.Id,
                c.Codigo,
                c.Nombre,
                c.PlantaId,
                c.Planta,
                c.Activo,
                integrantes.GetValueOrDefault(c.Id),
                vigente is null ? null : ADto(vigente),
                turnoHoy,
                ProximosCambios(propias, fecha));
        });
    }

    public async Task<CuadrillaListaDto?> ObtenerAsync(long id, DateOnly fecha, CancellationToken cancelacion = default) =>
        (await ListarAsync(fecha, incluirInactivas: true, cancelacion).ConfigureAwait(false))
            .FirstOrDefault(c => c.Id == id);

    public async Task<IReadOnlyList<ProgramacionCuadrillaDto>> ProgramacionesAsync(
        long id,
        CancellationToken cancelacion = default)
    {
        var programaciones = await contexto.ProgramacionesCuadrilla
            .AsNoTracking()
            .Include(p => p.Tramos)
            .ThenInclude(t => t.Turno)
            .Where(p => p.CuadrillaId == id)
            .OrderByDescending(p => p.FechaInicio)
            .ThenByDescending(p => p.Id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return programaciones.ConvertAll(ADto);
    }

    public async Task<long> GuardarAsync(
        GuardarCuadrillaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();
        var nombre = solicitud.Nombre.Trim();

        var plantaExiste = await contexto.Plantas
            .AnyAsync(p => p.Id == solicitud.PlantaId && p.Activo, cancelacion)
            .ConfigureAwait(false);

        if (!plantaExiste)
        {
            throw new ReglaNegocioException("La unidad indicada no existe o está inactiva.");
        }

        var repetido = await contexto.Cuadrillas
            .AnyAsync(c => c.PlantaId == solicitud.PlantaId && c.Codigo == codigo && c.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException($"Ya existe una cuadrilla con el código '{codigo}' en esa unidad.");
        }

        Cuadrilla cuadrilla;

        if (solicitud.Id is { } id)
        {
            cuadrilla = await contexto.Cuadrillas
                .FirstOrDefaultAsync(c => c.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("la cuadrilla", id);

            // Mudar de planta una cuadrilla con historia la llevaría con todos sus
            // integrantes, que son de la planta de origen.
            if (cuadrilla.PlantaId != solicitud.PlantaId)
            {
                var tuvoIntegrantes = await contexto.AsignacionesTurno
                    .AnyAsync(a => a.CuadrillaId == id, cancelacion)
                    .ConfigureAwait(false);

                if (tuvoIntegrantes)
                {
                    throw new ReglaNegocioException(
                        "No se puede cambiar de unidad una cuadrilla que ya tuvo integrantes. Cree " +
                        "una cuadrilla nueva en la otra unidad.");
                }
            }

            // Desmarcar «Activa» en el formulario es la misma baja que el botón del listado
            // y pasa por la misma regla. Sin esto, la cuadrilla quedaba invisible en el
            // listado y sin poder reprogramarse —CuadrillaActivaAsync la rechaza—, pero su
            // ciclo seguía decidiendo el turno de sus integrantes en cada recálculo, que es
            // justo lo que CambiarEstadoAsync existe para impedir.
            if (cuadrilla.Activo && !solicitud.Activo)
            {
                await GarantizarSinIntegrantesVigentesAsync(id, cancelacion).ConfigureAwait(false);
            }

            cuadrilla.ModificadoPor = usuario;
        }
        else
        {
            cuadrilla = new Cuadrilla { CreadoPor = usuario };
            await contexto.Cuadrillas.AddAsync(cuadrilla, cancelacion).ConfigureAwait(false);
        }

        cuadrilla.Codigo = codigo;
        cuadrilla.Nombre = nombre;
        cuadrilla.PlantaId = solicitud.PlantaId;
        cuadrilla.Activo = solicitud.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return cuadrilla.Id;
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var cuadrilla = await contexto.Cuadrillas
            .FirstOrDefaultAsync(c => c.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la cuadrilla", id);

        if (!activo)
        {
            await GarantizarSinIntegrantesVigentesAsync(id, cancelacion).ConfigureAwait(false);
        }

        cuadrilla.Activo = activo;
        cuadrilla.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task ProgramarAsync(
        ProgramarCuadrillaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Desde is not { } desde)
        {
            throw new ReglaNegocioException("Indique desde qué fecha rige el ciclo.");
        }

        var cuadrilla = await CuadrillaActivaAsync(solicitud.CuadrillaId, cancelacion).ConfigureAwait(false);

        var tramos = solicitud.Tramos
            .Where(t => t.Tipo == TipoTramoRotacion.Descanso
                        || t.TurnoId is not null
                        || t.Dias is not null)
            .ToList();

        if (tramos.Count == 0)
        {
            throw new ReglaNegocioException("La secuencia necesita al menos un tramo de trabajo o descanso.");
        }

        if (tramos.Any(t => t.Dias is null))
        {
            throw new ReglaNegocioException("Cada tramo de la secuencia necesita su duración.");
        }

        if (tramos.Any(t => t.Dias is < 1 or > 366))
        {
            throw new ReglaNegocioException("Cada tramo debe durar entre 1 y 366 días.");
        }

        var ordenValidacion = 1;
        foreach (var tramo in tramos)
        {
            new TramoRotacion
            {
                Orden = ordenValidacion++,
                Tipo = tramo.Tipo,
                TurnoId = tramo.TurnoId,
                Dias = tramo.Dias!.Value
            }.Validar();
        }

        var turnoIds = tramos
            .Where(t => t.Tipo == TipoTramoRotacion.Trabajo)
            .Select(t => t.TurnoId!.Value)
            .Distinct()
            .ToList();

        var turnos = await contexto.Turnos
            .AsNoTracking()
            .Include(t => t.PlantasHabilitadas)
            .Where(t => turnoIds.Contains(t.Id))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        foreach (var turnoId in turnoIds)
        {
            var turno = turnos.FirstOrDefault(t => t.Id == turnoId);

            if (turno is null || !turno.Activo)
            {
                throw new ReglaNegocioException("Uno de los turnos del ciclo no existe o está dado de baja.");
            }

            // Vacío significa que el turno aplica a todas las plantas.
            if (turno.PlantasHabilitadas.Count > 0
                && turno.PlantasHabilitadas.All(tp => tp.PlantaId != cuadrilla.PlantaId))
            {
                throw new ReglaNegocioException(
                    $"El turno {turno.Codigo} no está habilitado en la unidad de la cuadrilla.");
            }
        }

        var orden = 1;

        await ReemplazarDesdeAsync(
                cuadrilla,
                desde,
                ancla: desde,
                tramos.Select(t => new TramoRotacion
                {
                    Orden = orden++,
                    Tipo = t.Tipo,
                    TurnoId = t.TurnoId,
                    Dias = t.Dias!.Value,
                    CreadoPor = usuario
                }).ToList(),
                usuario,
                cancelacion)
            .ConfigureAwait(false);
    }

    public async Task CorrerRotacionAsync(
        CorrerRotacionRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Desde is not { } desde)
        {
            throw new ReglaNegocioException("Indique desde qué fecha se corre la rotación.");
        }

        if (solicitud.Dias == 0)
        {
            throw new ReglaNegocioException(
                "Indique cuántos días se corre la rotación: negativo si rotaron antes de lo " +
                "programado, positivo si rotaron después.");
        }

        var cuadrilla = await CuadrillaActivaAsync(solicitud.CuadrillaId, cancelacion).ConfigureAwait(false);

        var vigente = (await ProgramacionesActivasAsync([cuadrilla.Id], cancelacion).ConfigureAwait(false))
            .GetValueOrDefault(cuadrilla.Id, [])
            .FirstOrDefault(p => p.EstaVigenteEn(desde))
            ?? throw new ReglaNegocioException(
                $"La cuadrilla no tiene ciclo programado el {desde:dd/MM/yyyy}: no hay rotación que correr. " +
                "Prográmela primero.");

        if (Math.Abs(solicitud.Dias) >= vigente.DiasDelCiclo)
        {
            throw new ReglaNegocioException(
                $"No se puede desplazar {Math.Abs(solicitud.Dias)} día(s) una secuencia finita " +
                $"de {vigente.DiasDelCiclo}. Reprograme una secuencia nueva con sus tramos explícitos.");
        }

        // Mismo contenido finito, nueva vigencia y ancla desplazada. No se usa módulo: si
        // el desplazamiento deja días fuera de la secuencia, esos días quedan expresamente
        // sin programación hasta que se registre otra secuencia.
        await ReemplazarDesdeAsync(
                cuadrilla,
                desde,
                ancla: vigente.FechaAncla.AddDays(solicitud.Dias),
                vigente.TramosOrdenados()
                    .Select(t => new TramoRotacion
                    {
                        Orden = t.Orden,
                        Tipo = t.Tipo,
                        TurnoId = t.TurnoId,
                        Dias = t.Dias,
                        CreadoPor = usuario
                    })
                    .ToList(),
                usuario,
                cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> PlantasAsync(CancellationToken cancelacion = default) =>
        await contexto.Plantas.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Empresa!.RazonSocial).ThenBy(p => p.Nombre)
            .Select(p => new OpcionDto(p.Id, p.Empresa!.RazonSocial + " · " + p.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    // ------------------------------------------------------------------

    /// <summary>
    /// Desde <paramref name="desde"/>, este ciclo: la regla de toda programación nueva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La programación que cubría el día previo se cierra ese día. Las que empezaban en la
    /// fecha o después quedan reemplazadas enteras y se dan de <b>baja lógica</b>: no se
    /// borran, siguen en el historial con su autor, pero dejan de contar.
    /// </para>
    /// <para>
    /// Un periodo cerrado no admite cambios: si alguno termina en la fecha o después, se
    /// rechaza. Es la misma regla que el cambio de turno con fecha de la ficha.
    /// </para>
    /// </remarks>
    private async Task ReemplazarDesdeAsync(
        Cuadrilla cuadrilla,
        DateOnly desde,
        DateOnly ancla,
        List<TramoRotacion> tramos,
        string usuario,
        CancellationToken cancelacion)
    {
        await GarantizarPeriodosAbiertosAsync(desde, cancelacion).ConfigureAwait(false);

        var existentes = await contexto.ProgramacionesCuadrilla
            .Where(p => p.CuadrillaId == cuadrilla.Id && p.Activo)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        foreach (var programacion in existentes)
        {
            if (programacion.FechaInicio >= desde)
            {
                programacion.Activo = false;
                programacion.ModificadoPor = usuario;
            }
            else if (programacion.FechaFin is null || programacion.FechaFin.Value >= desde)
            {
                programacion.FechaFin = desde.AddDays(-1);
                programacion.ModificadoPor = usuario;
            }
        }

        var nueva = new ProgramacionCuadrilla
        {
            CuadrillaId = cuadrilla.Id,
            FechaInicio = desde,
            FechaFin = ancla.AddDays(tramos.Sum(t => t.Dias) - 1),
            FechaAncla = ancla,
            CreadoPor = usuario
        };

        foreach (var tramo in tramos)
        {
            nueva.Tramos.Add(tramo);
        }

        await contexto.ProgramacionesCuadrilla.AddAsync(nueva, cancelacion).ConfigureAwait(false);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    private async Task GarantizarPeriodosAbiertosAsync(DateOnly desde, CancellationToken cancelacion)
    {
        var cerrados = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Activo && p.Estado == EstadoPeriodo.Cerrado && p.FechaFin >= desde)
            .OrderBy(p => p.FechaInicio)
            .Select(p => new { p.Codigo, p.FechaFin })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (cerrados.Count == 0)
        {
            return;
        }

        var cuales = cerrados.Count == 1
            ? $"del periodo {cerrados[0].Codigo}, que está cerrado y no admite cambios"
            : $"de los periodos {string.Join(", ", cerrados.Select(p => p.Codigo))}, que están " +
              "cerrados y no admiten cambios";

        throw new ReglaNegocioException(
            $"El cambio de programación alcanzaría días {cuales}. Elija una fecha posterior al " +
            $"{cerrados[^1].FechaFin:dd/MM/yyyy}, o reabra el periodo indicando el motivo.");
    }

    /// <summary>
    /// Nadie sigue rotando con esta cuadrilla. Es la condición para darla de baja, venga la
    /// baja del botón del listado o de la casilla del formulario de edición.
    /// </summary>
    private async Task GarantizarSinIntegrantesVigentesAsync(long id, CancellationToken cancelacion)
    {
        var integrantes = await contexto.AsignacionesTurno
            .CountAsync(a => a.CuadrillaId == id && a.FechaFin == null, cancelacion)
            .ConfigureAwait(false);

        if (integrantes > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede dar de baja la cuadrilla: tiene {integrantes} integrante(s) vigente(s). " +
                "Páselos a otra cuadrilla o a un turno fijo primero.");
        }
    }

    private async Task<Cuadrilla> CuadrillaActivaAsync(long id, CancellationToken cancelacion)
    {
        var cuadrilla = await contexto.Cuadrillas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la cuadrilla", id);

        return cuadrilla.Activo
            ? cuadrilla
            : throw new ReglaNegocioException("La cuadrilla está dada de baja: reactívela antes de programarla.");
    }

    /// <summary>Programaciones activas de esas cuadrillas, con sus tramos y turnos.</summary>
    private async Task<Dictionary<long, List<ProgramacionCuadrilla>>> ProgramacionesActivasAsync(
        List<long> cuadrillaIds,
        CancellationToken cancelacion)
    {
        var programaciones = await contexto.ProgramacionesCuadrilla
            .AsNoTracking()
            .Include(p => p.Tramos)
            .ThenInclude(t => t.Turno)
            .Where(p => p.Activo && cuadrillaIds.Contains(p.CuadrillaId))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return programaciones
            .GroupBy(p => p.CuadrillaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.FechaInicio).ToList());
    }

    /// <summary>
    /// Las próximas rotaciones, recorriendo los días y resolviendo en cada uno la
    /// programación que rige: así aparece también la que empieza una programación nueva.
    /// </summary>
    private static List<CambioProgramadoDto> ProximosCambios(List<ProgramacionCuadrilla> programaciones, DateOnly fecha)
    {
        var cambios = new List<CambioProgramadoDto>(CantidadProximosCambios);
        TramoRotacion? anterior = Tramo(programaciones, fecha);

        for (var dia = fecha.AddDays(1);
             dia <= fecha.AddDays(HorizonteProximosCambios) && cambios.Count < CantidadProximosCambios;
             dia = dia.AddDays(1))
        {
            var tramo = Tramo(programaciones, dia);

            if (tramo is not null
                && (tramo.Tipo != anterior?.Tipo || tramo.TurnoId != anterior?.TurnoId))
            {
                cambios.Add(tramo.Tipo == TipoTramoRotacion.Descanso
                    ? new CambioProgramadoDto(dia, "DESCANSO", "DESCANSO PROGRAMADO")
                    : new CambioProgramadoDto(
                        dia,
                        tramo.Turno?.Codigo ?? "?",
                        tramo.Turno?.Nombre ?? "?"));
            }

            anterior = tramo;
        }

        return cambios;

        static TramoRotacion? Tramo(List<ProgramacionCuadrilla> programaciones, DateOnly dia)
        {
            var programacion = programaciones.FirstOrDefault(p => p.EstaVigenteEn(dia));
            if (programacion is null)
            {
                return null;
            }

            var resultado = programacion.ResolverEn(dia);
            return resultado.Estado is EstadoProgramacionDiaria.Trabajo
                or EstadoProgramacionDiaria.DescansoProgramado
                ? programacion.TramosOrdenados().FirstOrDefault(t => t.Id == resultado.TramoId)
                : null;
        }
    }

    private static string DescribirTurno(TramoRotacion tramo) =>
        tramo.Tipo == TipoTramoRotacion.Descanso
            ? "DESCANSO PROGRAMADO"
            : tramo.Turno is { } turno ? $"{turno.Codigo} - {turno.Nombre}" : "?";

    private static string DescribirResultado(ProgramacionCuadrilla programacion, DateOnly fecha)
    {
        var resultado = programacion.ResolverEn(fecha);
        if (resultado.Estado == EstadoProgramacionDiaria.DescansoProgramado)
        {
            return "DESCANSO PROGRAMADO";
        }

        if (resultado.Estado != EstadoProgramacionDiaria.Trabajo)
        {
            return resultado.Estado == EstadoProgramacionDiaria.ErrorConfiguracion
                ? "ERROR DE CONFIGURACIÓN"
                : "SIN PROGRAMACIÓN";
        }

        var tramo = programacion.TramosOrdenados().First(t => t.Id == resultado.TramoId);
        return DescribirTurno(tramo);
    }

    private static ProgramacionCuadrillaDto ADto(ProgramacionCuadrilla programacion) =>
        new(
            programacion.Id,
            programacion.FechaInicio,
            programacion.FechaFin,
            programacion.FechaAncla,
            programacion.Activo,
            [.. programacion.TramosOrdenados().Select(t => new TramoRotacionDto(
                t.Orden,
                t.Tipo,
                t.TurnoId,
                t.Turno?.Codigo,
                t.Turno?.Nombre,
                t.Dias))],
            programacion.ModificadoPor ?? programacion.CreadoPor,
            programacion.ModificadoEn ?? programacion.CreadoEn);
}
