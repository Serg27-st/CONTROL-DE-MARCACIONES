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
    public async Task<ResultadoPaginado<SolicitudListaDto>> ListarAsync(
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

        // El COUNT va sobre la consulta filtrada y antes de proyectar: contar sobre la
        // proyección obligaría a SQL a resolver las subconsultas de nombre y tipo para
        // filas que después se descartan.
        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanio = Math.Clamp(filtro.TamanioPagina, 10, 200);

        var elementos = await Proyectar(
                consulta
                    // Se desempata por Id: dos solicitudes con la misma fecha de inicio
                    // tendrían orden indefinido y podrían repetirse o perderse entre páginas.
                    .OrderByDescending(s => s.FechaInicio)
                    .ThenByDescending(s => s.Id)
                    .Skip((pagina - 1) * tamanio)
                    .Take(tamanio))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<SolicitudListaDto>(elementos, total, pagina, tamanio);
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

        await ValidarLimitesAsync(solicitud, tipo, cancelacion).ConfigureAwait(false);

        var faltaDetalleClinico = ValidadorDetalleClinico.Validar(
            tipo, solicitud.Contingencia, solicitud.Diagnostico, solicitud.CentroAtencion);

        if (faltaDetalleClinico is not null)
        {
            throw new ReglaNegocioException(faltaDetalleClinico);
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

        // El subtipo y el periodo solo tienen sentido en vacaciones. Guardarlos en otro tipo
        // dejaría un dato que nadie lee y que confundiría a quien audite la solicitud.
        var esVacaciones = EsVacaciones(tipo);
        entidad.SubtipoVacaciones = esVacaciones ? Normalizar(solicitud.SubtipoVacaciones) : null;
        entidad.PeriodoVacacional = esVacaciones ? solicitud.PeriodoVacacional?.Trim() : null;

        // El detalle clínico solo vive en los tipos subsidiados. Depurar es obligatorio y no
        // cosmético: cambiar una solicitud de descanso médico a permiso dejaría el
        // diagnóstico anterior guardado en una solicitud que ya no tiene ningún motivo para
        // llevarlo, y eso es un dato sensible de salud sobreviviendo sin causa (Ley 29733).
        var (contingencia, diagnostico, centroAtencion) = ValidadorDetalleClinico.Depurar(
            tipo, solicitud.Contingencia, solicitud.Diagnostico, solicitud.CentroAtencion);

        entidad.Contingencia = contingencia;
        entidad.Diagnostico = diagnostico;
        entidad.CentroAtencion = centroAtencion;

        entidad.Sustento = solicitud.Sustento.Trim();
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

    public async Task<IReadOnlyList<TipoAusenciaOpcionDto>> TiposAusenciaAsync(
        CancellationToken cancelacion = default) =>
        await contexto.TiposAusencia
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoAusenciaOpcionDto(
                t.Id,
                t.Codigo,
                t.Nombre,
                t.MaximoDiasPorEvento,
                t.MaximoDiasPorAnioCalendario,
                // La misma condición que ValidadorDetalleClinico.Exige, traducida a SQL: no
                // se puede llamar al validador dentro de la proyección. Si un día deja de
                // ser el comportamiento, hay que cambiarla en los dos sitios.
                t.Comportamiento == ComportamientoAusencia.Subsidiada))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    // ------------------------------------------------------------------

    /// <summary>
    /// Comprueba los topes de días del tipo de ausencia.
    /// </summary>
    /// <remarks>
    /// La regla vive en <see cref="ValidadorLimitesSolicitud"/>, que es una función pura del
    /// dominio. Lo único que aporta este método es el dato que esa función no puede
    /// averiguar sola: cuántos días ya lleva el trabajador en el cupo que corresponde.
    ///
    /// Se cuentan las solicitudes <b>pendientes y aprobadas</b>: una pendiente todavía puede
    /// aprobarse, así que reservar su cupo evita que dos registros que por separado caben
    /// juntos se pasen del límite. Las rechazadas y anuladas no ocupan nada.
    /// </remarks>
    private async Task ValidarLimitesAsync(
        GuardarSolicitudRequest solicitud,
        TipoAusencia tipo,
        CancellationToken cancelacion)
    {
        var esVacaciones = EsVacaciones(tipo);

        // Si el tipo no tiene ningún tope y no es vacaciones, no hay nada que consultar:
        // ni siquiera se toca la base de datos.
        if (tipo.MaximoDiasPorEvento is null
            && tipo.MaximoDiasPorAnioCalendario is null
            && !esVacaciones)
        {
            return;
        }

        var acumulados = 0;

        if (tipo.MaximoDiasPorAnioCalendario is not null || esVacaciones)
        {
            var consulta = contexto.SolicitudesAusencia
                .AsNoTracking()
                .Where(s => s.TrabajadorId == solicitud.TrabajadorId
                            && s.TipoAusenciaId == solicitud.TipoAusenciaId
                            && s.Id != solicitud.Id
                            && (s.Estado == EstadoSolicitud.Pendiente
                                || s.Estado == EstadoSolicitud.Aprobada));

            if (esVacaciones)
            {
                // El cupo de vacaciones es por periodo y por subtipo: los 15 días de venta
                // y los 30 de goce del periodo 2025-2026 son cupos distintos, y ninguno
                // se mezcla con los del periodo siguiente.
                var subtipo = Normalizar(solicitud.SubtipoVacaciones);
                var periodo = solicitud.PeriodoVacacional?.Trim();

                consulta = consulta.Where(s => s.SubtipoVacaciones == subtipo
                                               && s.PeriodoVacacional == periodo);
            }
            else
            {
                // El tope anual se imputa al año en que arranca la ausencia, igual que el
                // mensaje de error.
                var anio = solicitud.FechaInicio.Year;

                consulta = consulta.Where(s => s.FechaInicio.Year == anio);
            }

            // La suma se hace en memoria sobre una proyección mínima: son unas pocas filas
            // por trabajador y evita depender de que el proveedor sepa traducir la
            // aritmética de DateOnly a SQL.
            var rangos = await consulta
                .Select(s => new { s.FechaInicio, s.FechaFin })
                .ToListAsync(cancelacion)
                .ConfigureAwait(false);

            acumulados = rangos.Sum(r => r.FechaFin.DayNumber - r.FechaInicio.DayNumber + 1);
        }

        var error = ValidadorLimitesSolicitud.Validar(
            tipo,
            solicitud.FechaInicio,
            solicitud.FechaFin,
            acumulados,
            solicitud.SubtipoVacaciones,
            solicitud.PeriodoVacacional);

        if (error is not null)
        {
            throw new ReglaNegocioException(error);
        }
    }

    private static bool EsVacaciones(TipoAusencia tipo) =>
        string.Equals(
            tipo.Codigo?.Trim(),
            ValidadorLimitesSolicitud.CodigoVacaciones,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Deja el subtipo en mayúsculas para que el cupo no dependa de cómo se tecleó.</summary>
    private static string? Normalizar(string? subtipo)
    {
        var texto = subtipo?.Trim();

        return string.IsNullOrEmpty(texto) ? null : texto.ToUpperInvariant();
    }

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
            s.SubtipoVacaciones,
            s.PeriodoVacacional,
            contexto.Justificaciones.Count(j => j.SolicitudAusenciaId == s.Id),
            s.Contingencia,
            s.Diagnostico,
            s.CentroAtencion));
}
