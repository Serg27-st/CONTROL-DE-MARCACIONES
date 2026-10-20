using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

internal sealed class ServicioTurnos(MarcacionDbContext contexto) : IServicioTurnos
{
    public async Task<IReadOnlyList<TurnoListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default) =>
        await Proyectar(
                contexto.Turnos.AsNoTracking()
                    .Where(t => incluirInactivos || t.Activo)
                    .OrderBy(t => t.Codigo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<TurnoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.Turnos.AsNoTracking().Where(t => t.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarTurnoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();
        var nombre = solicitud.Nombre.Trim();

        Validar(solicitud);

        var repetido = await contexto.Turnos
            .AnyAsync(t => t.Codigo == codigo && t.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException($"Ya existe un turno con el código '{codigo}'.");
        }

        Turno turno;

        if (solicitud.Id is { } id)
        {
            turno = await contexto.Turnos
                .Include(t => t.PlantasHabilitadas)
                .FirstOrDefaultAsync(t => t.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el turno", id);

            turno.ModificadoPor = usuario;
        }
        else
        {
            turno = new Turno { CreadoPor = usuario };
            await contexto.Turnos.AddAsync(turno, cancelacion).ConfigureAwait(false);
        }

        turno.Codigo = codigo;
        turno.Nombre = nombre;
        turno.HoraInicio = solicitud.HoraInicio;
        turno.HoraFin = solicitud.HoraFin;
        turno.MinutosRefrigerio = solicitud.MinutosRefrigerio;
        turno.MinutosTolerancia = solicitud.MinutosTolerancia;
        turno.MinutosAnticipacionEntrada = solicitud.MinutosAnticipacionEntrada;
        turno.MinutosAnticipacionSalida = solicitud.MinutosAnticipacionSalida;
        turno.MinutosGraciaSalida = solicitud.MinutosGraciaSalida;
        turno.MinutosJornadaOrdinaria = solicitud.MinutosJornadaOrdinaria;
        turno.DiasAplicables = solicitud.DiasAplicables;
        turno.Activo = solicitud.Activo;

        SincronizarPlantas(turno, solicitud.PlantasHabilitadas, usuario);

        await AjustarVigenciaDelHorarioAsync(turno, solicitud.RigeDesde, usuario, cancelacion)
            .ConfigureAwait(false);

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return turno.Id;
    }

    /// <summary>
    /// Deja el historial de horarios del turno consistente con lo que se acaba de guardar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es lo que impide que editar un turno reescriba el pasado. Antes, cambiar el horario de
    /// DIU en octubre y recalcular julio —por una justificación tardía, por un archivo que
    /// faltaba— recalculaba julio con el horario de octubre, y las tardanzas y el sobretiempo
    /// de un mes ya pagado cambiaban solos.
    /// </para>
    /// <para>
    /// Misma mecánica que las demás vigencias del sistema: la versión que cubría el día
    /// previo se cierra y se abre otra. Solo cuenta lo que el cálculo mira: renombrar el
    /// turno, moverlo de planta o darlo de baja no parte su historia.
    /// </para>
    /// </remarks>
    private async Task AjustarVigenciaDelHorarioAsync(
        Turno turno,
        DateOnly? rigeDesde,
        string usuario,
        CancellationToken cancelacion)
    {
        // Un turno recién creado no tiene pasado que proteger: su horario es el único que
        // tuvo, y se abre desde el origen para que valga también si se le asigna a alguien
        // con fecha retroactiva.
        if (turno.Id == 0)
        {
            turno.Versiones.Add(VersionTurno.De(turno, VersionTurno.VigenciaOriginal, usuario));
            return;
        }

        var vigente = await contexto.VersionesTurno
            .Where(v => v.TurnoId == turno.Id && v.Activo && v.FechaFin == null)
            .OrderByDescending(v => v.FechaInicio)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (vigente is null)
        {
            turno.Versiones.Add(VersionTurno.De(turno, VersionTurno.VigenciaOriginal, usuario));
            return;
        }

        if (vigente.MismoHorarioQue(turno))
        {
            return;
        }

        var desde = rigeDesde ?? DateOnly.FromDateTime(DateTime.Now);

        await GarantizarPeriodosAbiertosAsync(desde, cancelacion).ConfigureAwait(false);

        // El horario estaba mal desde el principio de esta versión: se corrige en sitio en
        // vez de abrir otra con vigencia de cero días.
        if (desde <= vigente.FechaInicio)
        {
            vigente.TomarHorarioDe(turno);
            vigente.ModificadoPor = usuario;
            return;
        }

        vigente.FechaFin = desde.AddDays(-1);
        vigente.ModificadoPor = usuario;

        turno.Versiones.Add(VersionTurno.De(turno, desde, usuario));
    }

    /// <summary>
    /// Ningún periodo cerrado alcanza esa fecha. Cambiar el horario que rige en días ya
    /// cerrados los cambiaría al siguiente recálculo.
    /// </summary>
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
            $"El cambio de horario alcanzaría días {cuales}. Indique que rige desde una fecha " +
            $"posterior al {cerrados[^1].FechaFin:dd/MM/yyyy}, o reabra el periodo indicando el motivo.");
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos
            .FirstOrDefaultAsync(t => t.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el turno", id);

        if (!activo)
        {
            // Desactivar un turno con asignaciones vigentes dejaría a esos trabajadores
            // sin horario contra el cual calcular, y el motor los reportaría como
            // "sin horario" el resto del periodo.
            var asignados = await contexto.AsignacionesTurno
                .CountAsync(a => a.TurnoId == id && a.FechaFin == null, cancelacion)
                .ConfigureAwait(false);

            if (asignados > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede desactivar el turno: hay {asignados} trabajador(es) con " +
                    "asignación vigente. Reasígnelos a otro turno primero.");
            }

            // Lo mismo para las cuadrillas: su ciclo seguiría asignando este turno.
            var cuadrillas = await contexto.TramosRotacion
                .Where(t => t.TurnoId == id && t.Programacion!.Activo && t.Programacion.FechaFin == null)
                .Select(t => t.Programacion!.Cuadrilla!.Nombre)
                .Distinct()
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            if (cuadrillas.Count > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede desactivar el turno: forma parte del ciclo vigente de " +
                    $"{string.Join(", ", cuadrillas)}. Programe esas cuadrillas sin él primero.");
            }
        }

        turno.Activo = activo;
        turno.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
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
    /// Proyecta a DTO.
    /// </summary>
    /// <remarks>
    /// El filtrado y el ordenamiento van <b>antes</b> de llamar aquí, sobre la entidad.
    /// Ordenar sobre el DTO ya proyectado revienta la traducción a SQL, porque el DTO
    /// lleva colecciones materializadas con <c>ToList()</c> y EF no sabe ordenar eso.
    /// </remarks>
    private IQueryable<TurnoListaDto> Proyectar(IQueryable<Turno> consulta) =>
        consulta
            .Select(t => new TurnoListaDto(
                t.Id,
                t.Codigo,
                t.Nombre,
                t.HoraInicio,
                t.HoraFin,
                t.HoraFin <= t.HoraInicio,
                t.MinutosRefrigerio,
                t.MinutosTolerancia,
                t.MinutosAnticipacionEntrada,
                t.MinutosAnticipacionSalida,
                t.MinutosGraciaSalida,
                t.MinutosJornadaOrdinaria,
                t.DiasAplicables,
                t.PlantasHabilitadas.Select(tp => tp.Planta!.Nombre).ToList(),
                t.PlantasHabilitadas.Select(tp => tp.PlantaId).ToList(),
                t.Activo,
                contexto.AsignacionesTurno.Count(a => a.TurnoId == t.Id && a.FechaFin == null)));

    private static void Validar(GuardarTurnoRequest solicitud)
    {
        if (solicitud.HoraInicio == solicitud.HoraFin)
        {
            throw new ReglaNegocioException(
                "La hora de inicio y la de fin no pueden ser iguales: el turno duraría cero minutos.");
        }

        var cruzaMedianoche = solicitud.HoraFin <= solicitud.HoraInicio;

        var duracionBruta = cruzaMedianoche
            ? (int)(TimeSpan.FromDays(1) - solicitud.HoraInicio.ToTimeSpan() + solicitud.HoraFin.ToTimeSpan()).TotalMinutes
            : (int)(solicitud.HoraFin.ToTimeSpan() - solicitud.HoraInicio.ToTimeSpan()).TotalMinutes;

        if (solicitud.MinutosRefrigerio >= duracionBruta)
        {
            throw new ReglaNegocioException(
                $"El refrigerio ({solicitud.MinutosRefrigerio} min) no puede ser mayor o igual " +
                $"que la duración del turno ({duracionBruta} min).");
        }

        if (solicitud.MinutosJornadaOrdinaria > duracionBruta - solicitud.MinutosRefrigerio)
        {
            throw new ReglaNegocioException(
                $"La jornada ordinaria ({solicitud.MinutosJornadaOrdinaria} min) no puede superar " +
                $"la duración efectiva del turno ({duracionBruta - solicitud.MinutosRefrigerio} min).");
        }

        // Si la ventana de entrada se estira más allá del punto medio, empieza a solaparse
        // con la ventana de salida y una misma marca podría clasificarse de dos formas.
        // Con turnos de 12 h esto solo salta con ventanas absurdas, pero en un turno corto
        // de 4 h una ventana de 2 h ya rompe el emparejamiento.
        var mitad = duracionBruta / 2;

        if (solicitud.MinutosAnticipacionEntrada >= mitad)
        {
            throw new ReglaNegocioException(
                $"La anticipación de entrada ({solicitud.MinutosAnticipacionEntrada} min) es demasiado " +
                $"amplia para un turno de {duracionBruta} min: se solaparía con la ventana de salida " +
                $"y una misma marcación podría tomarse como entrada o como salida. " +
                $"El máximo para este turno es {mitad - 1} min.");
        }

        if (solicitud.MinutosAnticipacionSalida >= mitad)
        {
            throw new ReglaNegocioException(
                $"La anticipación de salida ({solicitud.MinutosAnticipacionSalida} min) es demasiado " +
                $"amplia para un turno de {duracionBruta} min. El máximo para este turno es {mitad - 1} min.");
        }

        if (solicitud.DiasAplicables == DiasSemana.Ninguno)
        {
            throw new ReglaNegocioException("Debe indicar al menos un día en que aplica el turno.");
        }
    }

    /// <summary>
    /// Deja las plantas habilitadas igual a lo enviado: agrega las nuevas y quita las que
    /// ya no vienen. Una lista vacía significa que el turno aplica a todas las plantas.
    /// </summary>
    private void SincronizarPlantas(Turno turno, IReadOnlyList<long> plantas, string usuario)
    {
        var deseadas = plantas.Distinct().ToHashSet();

        var sobrantes = turno.PlantasHabilitadas
            .Where(tp => !deseadas.Contains(tp.PlantaId))
            .ToList();

        foreach (var sobrante in sobrantes)
        {
            turno.PlantasHabilitadas.Remove(sobrante);
            contexto.Remove(sobrante);
        }

        var existentes = turno.PlantasHabilitadas.Select(tp => tp.PlantaId).ToHashSet();

        foreach (var plantaId in deseadas.Where(p => !existentes.Contains(p)))
        {
            turno.PlantasHabilitadas.Add(new TurnoPlanta
            {
                PlantaId = plantaId,
                Activo = true,
                CreadoPor = usuario
            });
        }
    }
}
