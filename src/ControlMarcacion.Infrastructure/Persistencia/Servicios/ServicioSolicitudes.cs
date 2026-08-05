using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Solicitudes de ausencia y su conversión en justificaciones.
/// </summary>
internal sealed class ServicioSolicitudes(MarcacionDbContext contexto) : IServicioSolicitudes
{
    public async Task<IReadOnlyList<SolicitudListaDto>> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.SolicitudesAusencia.AsNoTracking();

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(s => s.TrabajadorId == trabajadorId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(s => s.Estado == estado);
        }

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(s => s.FechaFin >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(s => s.FechaInicio <= hasta);
        }

        return await Proyectar(consulta.OrderByDescending(s => s.FechaInicio))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<SolicitudListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.SolicitudesAusencia.AsNoTracking().Where(s => s.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarSolicitudRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.FechaFin < solicitud.FechaInicio)
        {
            throw new ReglaNegocioException(
                "La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        var tipo = await contexto.TiposAusencia
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TipoAusenciaId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el tipo de ausencia", solicitud.TipoAusenciaId);

        if (!tipo.Activo)
        {
            throw new ReglaNegocioException(
                $"El tipo de ausencia '{tipo.Nombre}' está inactivo y no puede usarse en solicitudes nuevas.");
        }

        ValidarHoras(solicitud, tipo);

        // Dos ausencias que se pisan son un error de captura: el motor no sabría cuál
        // aplicar al día en común.
        var solapada = await contexto.SolicitudesAusencia
            .AsNoTracking()
            .Where(s => s.TrabajadorId == solicitud.TrabajadorId
                        && s.Id != solicitud.Id
                        && s.Estado != EstadoSolicitud.Anulada
                        && s.Estado != EstadoSolicitud.Rechazada
                        && s.FechaInicio <= solicitud.FechaFin
                        && s.FechaFin >= solicitud.FechaInicio)
            .Select(s => new { s.FechaInicio, s.FechaFin })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (solapada is not null)
        {
            throw new ReglaNegocioException(
                $"El trabajador ya tiene una solicitud del {solapada.FechaInicio:dd/MM/yyyy} al " +
                $"{solapada.FechaFin:dd/MM/yyyy} que se cruza con estas fechas.");
        }

        SolicitudAusencia entidad;

        if (solicitud.Id is { } id)
        {
            entidad = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

            // Una solicitud ya decidida no se edita: sus justificaciones ya afectaron el
            // tareo. Para corregirla hay que anularla y crear una nueva, dejando rastro.
            if (entidad.Estado != EstadoSolicitud.Pendiente)
            {
                throw new ReglaNegocioException(
                    $"La solicitud está {entidad.Estado.ToString().ToLowerInvariant()} y ya no admite " +
                    "modificaciones. Anúlela y registre una nueva si necesita corregirla.");
            }

            entidad.ModificadoPor = usuario;
        }
        else
        {
            entidad = new SolicitudAusencia { CreadoPor = usuario, Estado = EstadoSolicitud.Pendiente };
            await contexto.SolicitudesAusencia.AddAsync(entidad, cancelacion).ConfigureAwait(false);
        }

        entidad.TrabajadorId = solicitud.TrabajadorId;
        entidad.TipoAusenciaId = solicitud.TipoAusenciaId;
        entidad.FechaInicio = solicitud.FechaInicio;
        entidad.FechaFin = solicitud.FechaFin;
        entidad.HoraInicio = tipo.EsPorHoras ? solicitud.HoraInicio : null;
        entidad.HoraFin = tipo.EsPorHoras ? solicitud.HoraFin : null;
        entidad.Sustento = solicitud.Sustento?.Trim();
        entidad.RutaDocumentoSustento = solicitud.RutaDocumentoSustento?.Trim();
        entidad.Observacion = solicitud.Observacion?.Trim();

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return entidad.Id;
    }

    public async Task<ResultadoAprobacion> AprobarAsync(
        long id,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var solicitud = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new ReglaNegocioException(
                $"Solo se puede aprobar una solicitud pendiente. Esta está " +
                $"{solicitud.Estado.ToString().ToLowerInvariant()}.");
        }

        var tipo = await contexto.TiposAusencia
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TipoAusenciaId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el tipo de ausencia", solicitud.TipoAusenciaId);

        // Si el tipo exige sustento, exigirlo al aprobar y no al registrar: quien pide el
        // permiso muchas veces todavía no tiene el certificado en la mano.
        if (tipo.RequiereSustento && string.IsNullOrWhiteSpace(solicitud.Sustento))
        {
            throw new ReglaNegocioException(
                $"El tipo '{tipo.Nombre}' exige sustento documentado y la solicitud no lo tiene. " +
                "Regístrelo antes de aprobar.");
        }

        var periodos = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Activo && p.FechaInicio <= solicitud.FechaFin && p.FechaFin >= solicitud.FechaInicio)
            .Select(p => new { p.Id, p.FechaInicio, p.FechaFin, p.Estado })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // Se traen TODAS las justificaciones del trabajador en el rango, no solo las de
        // esta solicitud: el motor de cálculo resuelve una sola justificación por día, así
        // que una manual ya cargada para esa fecha tiene que bloquear la nueva, no coexistir.
        var existentes = await contexto.Justificaciones
            .Where(j => j.TrabajadorId == solicitud.TrabajadorId
                        && j.Fecha >= solicitud.FechaInicio && j.Fecha <= solicitud.FechaFin)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var creadas = 0;
        var enCerrado = new List<DateOnly>();
        var sinPeriodo = new List<DateOnly>();
        var conJustificacionExistente = new List<DateOnly>();

        for (var fecha = solicitud.FechaInicio; fecha <= solicitud.FechaFin; fecha = fecha.AddDays(1))
        {
            var periodo = periodos.Find(p => p.FechaInicio <= fecha && p.FechaFin >= fecha);

            if (periodo is null)
            {
                sinPeriodo.Add(fecha);
                continue;
            }

            // Invariante del sistema: un periodo cerrado no admite ninguna escritura.
            if (periodo.Estado != EstadoPeriodo.Abierto)
            {
                enCerrado.Add(fecha);
                continue;
            }

            var existente = existentes.Find(j => j.Fecha == fecha);

            if (existente is not null)
            {
                // Si ya es de ESTA solicitud, es una reaprobación idempotente: no cuenta
                // como omisión. Si es de otro origen, sí es un choque real que hay que avisar.
                if (existente.SolicitudAusenciaId != solicitud.Id)
                {
                    conJustificacionExistente.Add(fecha);
                }

                continue;
            }

            await contexto.Justificaciones.AddAsync(new Justificacion
            {
                TrabajadorId = solicitud.TrabajadorId,
                PeriodoId = periodo.Id,
                Fecha = fecha,
                Origen = OrigenJustificacion.Solicitud,
                SolicitudAusenciaId = solicitud.Id,
                TipoAusenciaId = solicitud.TipoAusenciaId,
                Tipo = tipo.EsPorHoras ? TipoJustificacion.Tardanza : TipoJustificacion.Ausencia,
                MinutosJustificados = MinutosDelPermiso(solicitud, tipo),
                Motivo = $"{tipo.Nombre}. Solicitud #{solicitud.Id}" +
                         (string.IsNullOrWhiteSpace(solicitud.Sustento)
                             ? string.Empty
                             : $". {solicitud.Sustento.Trim()}"),
                RutaDocumentoSustento = solicitud.RutaDocumentoSustento,
                RegistradoPorUsuario = usuario,
                RegistradoEn = DateTime.Now,
                CreadoPor = usuario
            }, cancelacion).ConfigureAwait(false);

            creadas++;
        }

        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return new ResultadoAprobacion(solicitud.Id, creadas, enCerrado, sinPeriodo, conJustificacionExistente);
    }

    public async Task RechazarAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException("Debe indicar el motivo del rechazo.");
        }

        var solicitud = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new ReglaNegocioException(
                $"Solo se puede rechazar una solicitud pendiente. Esta está " +
                $"{solicitud.Estado.ToString().ToLowerInvariant()}.");
        }

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.Observacion = motivo.Trim();
        solicitud.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task AnularAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException(
                "Debe indicar el motivo de la anulación: es información de auditoría.");
        }

        var solicitud = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (solicitud.Estado != EstadoSolicitud.Aprobada)
        {
            throw new ReglaNegocioException(
                "Solo se puede anular una solicitud aprobada. Una pendiente se rechaza.");
        }

        var justificaciones = await contexto.Justificaciones
            .Where(j => j.SolicitudAusenciaId == id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var periodosCerrados = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Estado != EstadoPeriodo.Abierto)
            .Select(p => p.Id)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // Si alguna justificación cayó en un periodo ya cerrado, no se puede retirar: el
        // tareo de ese periodo está congelado. Se avisa en vez de romper la invariante.
        var bloqueadas = justificaciones.FindAll(j => periodosCerrados.Contains(j.PeriodoId));

        if (bloqueadas.Count > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede anular: {bloqueadas.Count} de sus justificaciones caen en un periodo " +
                "cerrado y su tareo ya está congelado. Reabra el periodo si realmente corresponde.");
        }

        contexto.Justificaciones.RemoveRange(justificaciones);

        solicitud.Estado = EstadoSolicitud.Anulada;
        solicitud.MotivoAnulacion = motivo.Trim();
        solicitud.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default) =>
        await contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaCese == null)
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new OpcionDto(
                t.Id, t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<OpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default) =>
        await contexto.TiposAusencia
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    // ------------------------------------------------------------------

    private static void ValidarHoras(GuardarSolicitudRequest solicitud, TipoAusencia tipo)
    {
        if (!tipo.EsPorHoras)
        {
            return;
        }

        if (solicitud.HoraInicio is not { } inicio || solicitud.HoraFin is not { } fin)
        {
            throw new ReglaNegocioException(
                $"El tipo '{tipo.Nombre}' se registra por horas: indique la hora de inicio y la de fin.");
        }

        if (fin <= inicio)
        {
            throw new ReglaNegocioException(
                "La hora de fin del permiso debe ser posterior a la de inicio.");
        }

        if (solicitud.FechaFin != solicitud.FechaInicio)
        {
            throw new ReglaNegocioException(
                "Un permiso por horas es de un solo día: la fecha de inicio y la de fin deben coincidir.");
        }
    }

    /// <summary>Minutos del permiso parcial. Nulo cuando cubre el día completo.</summary>
    private static int? MinutosDelPermiso(SolicitudAusencia solicitud, TipoAusencia tipo) =>
        tipo.EsPorHoras && solicitud.HoraInicio is { } inicio && solicitud.HoraFin is { } fin
            ? (int)(fin.ToTimeSpan() - inicio.ToTimeSpan()).TotalMinutes
            : null;

    private async Task<SolicitudAusencia> BuscarAsync(long id, CancellationToken cancelacion) =>
        await contexto.SolicitudesAusencia
            .FirstOrDefaultAsync(s => s.Id == id, cancelacion)
            .ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException("la solicitud", id);

    private IQueryable<SolicitudListaDto> Proyectar(IQueryable<SolicitudAusencia> consulta) =>
        consulta.Select(s => new SolicitudListaDto(
            s.Id,
            s.TrabajadorId,
            contexto.Trabajadores
                .Where(t => t.Id == s.TrabajadorId)
                .Select(t => t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres)
                .FirstOrDefault() ?? "-",
            s.TipoAusencia!.Nombre,
            s.TipoAusencia!.Comportamiento.ToString(),
            s.FechaInicio,
            s.FechaFin,
            s.HoraInicio,
            s.HoraFin,
            s.Estado,
            s.TipoAusencia!.RequiereSustento,
            s.Sustento,
            s.Observacion,
            s.MotivoAnulacion,
            contexto.Justificaciones.Count(j => j.SolicitudAusenciaId == s.Id)));
}
