using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Solicitudes de ausencia y su conversión en justificaciones.
/// </summary>
internal sealed class ServicioSolicitudes(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance,
    IServicioCalculoTareo? calculo = null) : IServicioSolicitudes
{
    public async Task<ResultadoPaginado<SolicitudListaDto>> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.SolicitudesAusencia.AsNoTracking();

        // Aislamiento por planta: un ADMINISTRADOR solo ve las solicitudes de trabajadores de
        // sus plantas. Va antes que los filtros de pantalla porque es un límite del alcance.
        consulta = await FiltrarPorAlcanceAsync(consulta, cancelacion).ConfigureAwait(false);

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

    public async Task<IReadOnlyList<SolicitudCambioTurnoDto>> ListarCambiosTurnoAsync(
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.SolicitudesCambioTurno.AsNoTracking();
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);
        if (plantas is not null)
        {
            consulta = consulta.Where(s => contexto.AsignacionesPlanta.Any(a =>
                a.PeriodoLaboral!.TrabajadorId == s.TrabajadorId && a.FechaInicio <= s.FechaEfectiva
                && (a.FechaFin == null || a.FechaFin >= s.FechaEfectiva)
                && plantas.Contains(a.PlantaId)));
        }

        return await consulta.OrderByDescending(s => s.FechaEfectiva).ThenByDescending(s => s.Id)
            .Select(s => new SolicitudCambioTurnoDto(
                s.Id, s.TrabajadorId,
                contexto.Trabajadores.Where(t => t.Id == s.TrabajadorId)
                    .Select(t => t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres)
                    .FirstOrDefault() ?? "-",
                s.FechaEfectiva, s.TurnoSolicitadoId,
                s.TurnoSolicitado!.Codigo + " - " + s.TurnoSolicitado.Nombre,
                s.TurnoAnteriorId == null ? "Cuadrilla programada" :
                    contexto.Turnos.Where(t => t.Id == s.TurnoAnteriorId)
                        .Select(t => t.Codigo + " - " + t.Nombre).FirstOrDefault() ?? "-",
                s.Motivo, s.Estado, s.SolicitadoPorUsuario, s.SolicitadoEn,
                s.ResueltoPorUsuario, s.ResueltoEn, s.MotivoResolucion))
            .ToListAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<long> GuardarCambioTurnoAsync(
        GuardarCambioTurnoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Motivo.Trim().Length < 5)
        {
            throw new ReglaNegocioException("Indique un motivo de al menos 5 caracteres.");
        }

        var plantaId = await PlantaHistoricaAsync(solicitud.TrabajadorId, solicitud.FechaEfectiva, cancelacion)
            .ConfigureAwait(false);
        await GarantizarPlantaEnAlcanceAsync(plantaId, solicitud.TrabajadorId, cancelacion).ConfigureAwait(false);

        if (!await contexto.PeriodosLaborales.AsNoTracking().AnyAsync(p =>
                p.TrabajadorId == solicitud.TrabajadorId && p.FechaIngreso <= solicitud.FechaEfectiva
                && (p.FechaCese == null || p.FechaCese >= solicitud.FechaEfectiva), cancelacion)
            .ConfigureAwait(false))
        {
            throw new ReglaNegocioException("La fecha efectiva no pertenece a un vínculo laboral vigente.");
        }

        var periodo = await contexto.Periodos.AsNoTracking().FirstOrDefaultAsync(p =>
            p.Activo && p.FechaInicio <= solicitud.FechaEfectiva && p.FechaFin >= solicitud.FechaEfectiva,
            cancelacion).ConfigureAwait(false)
            ?? throw new ReglaNegocioException("La fecha efectiva no pertenece a un periodo registrado.");
        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        var turno = await contexto.Turnos.AsNoTracking().Include(t => t.PlantasHabilitadas)
            .FirstOrDefaultAsync(t => t.Id == solicitud.TurnoId, cancelacion).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el turno", solicitud.TurnoId);
        if (!turno.Activo || (turno.PlantasHabilitadas.Count > 0
                              && turno.PlantasHabilitadas.All(p => p.PlantaId != plantaId)))
        {
            throw new ReglaNegocioException("El turno solicitado no está habilitado para la unidad de esa fecha.");
        }

        var aplicables = await contexto.AsignacionesTurno.AsNoTracking().Where(a =>
            a.TrabajadorId == solicitud.TrabajadorId && a.FechaInicio <= solicitud.FechaEfectiva
            && (a.FechaFin == null || a.FechaFin >= solicitud.FechaEfectiva))
            .ToListAsync(cancelacion).ConfigureAwait(false);
        var vigente = aplicables.Count == 1 ? aplicables[0] : throw new ReglaNegocioException(
            aplicables.Count == 0 ? "El trabajador no tiene turno vigente en la fecha efectiva."
                : "El trabajador tiene asignaciones de turno solapadas en la fecha efectiva.");
        if (vigente.TurnoId == solicitud.TurnoId)
        {
            throw new ReglaNegocioException("El turno solicitado ya es el vigente para esa fecha.");
        }

        if (await contexto.SolicitudesCambioTurno.AnyAsync(s =>
                s.TrabajadorId == solicitud.TrabajadorId && s.FechaEfectiva == solicitud.FechaEfectiva
                && (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aprobada), cancelacion)
            .ConfigureAwait(false))
        {
            throw new ReglaNegocioException("Ya existe una solicitud vigente para ese trabajador y fecha.");
        }

        var entidad = new SolicitudCambioTurno
        {
            TrabajadorId = solicitud.TrabajadorId,
            FechaEfectiva = solicitud.FechaEfectiva,
            TurnoSolicitadoId = solicitud.TurnoId,
            TurnoAnteriorId = vigente.TurnoId,
            CuadrillaAnteriorId = vigente.CuadrillaId,
            Motivo = solicitud.Motivo.Trim(),
            SolicitadoPorUsuario = usuario,
            SolicitadoEn = DateTime.Now,
            CreadoPor = usuario
        };
        await contexto.SolicitudesCambioTurno.AddAsync(entidad, cancelacion).ConfigureAwait(false);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        return entidad.Id;
    }

    public async Task ResolverCambioTurnoAsync(
        long id,
        bool aprueba,
        string? motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        if (!aprueba && string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException("Indique el motivo del rechazo.");
        }

        var solicitud = await contexto.SolicitudesCambioTurno
            .FirstOrDefaultAsync(s => s.Id == id, cancelacion).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la solicitud de cambio de turno", id);
        var plantaId = await PlantaHistoricaAsync(
            solicitud.TrabajadorId, solicitud.FechaEfectiva, cancelacion).ConfigureAwait(false);
        await GarantizarPlantaEnAlcanceAsync(plantaId, solicitud.TrabajadorId, cancelacion).ConfigureAwait(false);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new ReglaNegocioException("La solicitud ya fue resuelta y no admite una segunda decisión.");
        }
        if (string.Equals(solicitud.SolicitadoPorUsuario, usuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException("Quien solicitó el cambio no puede aprobarlo ni rechazarlo.");
        }

        var periodo = await contexto.Periodos.FirstOrDefaultAsync(p =>
            p.Activo && p.FechaInicio <= solicitud.FechaEfectiva && p.FechaFin >= solicitud.FechaEfectiva,
            cancelacion).ConfigureAwait(false)
            ?? throw new ReglaNegocioException("La fecha efectiva no pertenece a un periodo registrado.");
        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        var vinculoVigente = await contexto.PeriodosLaborales.AsNoTracking().AnyAsync(p =>
            p.TrabajadorId == solicitud.TrabajadorId
            && p.FechaIngreso <= solicitud.FechaEfectiva
            && (p.FechaCese == null || p.FechaCese >= solicitud.FechaEfectiva), cancelacion)
            .ConfigureAwait(false);
        if (!vinculoVigente)
        {
            throw new ReglaNegocioException("El vínculo laboral ya no cubre la fecha efectiva.");
        }

        var turnoSolicitadoValido = await contexto.Turnos.AsNoTracking()
            .Include(t => t.PlantasHabilitadas)
            .AnyAsync(t => t.Id == solicitud.TurnoSolicitadoId && t.Activo
                           && (t.PlantasHabilitadas.Count == 0
                               || t.PlantasHabilitadas.Any(p => p.PlantaId == plantaId)), cancelacion)
            .ConfigureAwait(false);
        if (!turnoSolicitadoValido)
        {
            throw new ReglaNegocioException(
                "El turno solicitado dejó de estar activo o habilitado para la unidad.");
        }

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
        if (aprueba)
        {
            var asignaciones = await contexto.AsignacionesTurno
                .Where(a => a.TrabajadorId == solicitud.TrabajadorId)
                .OrderBy(a => a.FechaInicio).ToListAsync(cancelacion).ConfigureAwait(false);
            var vigentes = asignaciones.Where(a => a.EstaVigenteEn(solicitud.FechaEfectiva)).ToList();
            if (vigentes.Count != 1)
            {
                throw new ReglaNegocioException("La vigencia de turno cambió o está solapada; revise antes de aprobar.");
            }

            var vigente = vigentes[0];
            if (!vigente.Asigna(solicitud.TurnoAnteriorId, solicitud.CuadrillaAnteriorId))
            {
                throw new ReglaNegocioException(
                    "El turno vigente cambió después de registrar la solicitud; anúlela y registre otra.");
            }

            AplicarExcepcionDeUnDia(
                asignaciones, vigente, solicitud.FechaEfectiva, solicitud.TurnoSolicitadoId, usuario);
            ValidadorVigencias.GarantizarLineaDeTiempoSana(asignaciones, "turno");
            contexto.AsignacionesTurno.AddRange(asignaciones.Where(a => a.Id == 0));
        }

        solicitud.Estado = aprueba ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada;
        solicitud.ResueltoPorUsuario = usuario;
        solicitud.ResueltoEn = DateTime.Now;
        solicitud.MotivoResolucion = motivo?.Trim();
        solicitud.ModificadoPor = usuario;
        solicitud.Version++;
        contexto.AfirmarAbierto(periodo);

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            if (aprueba && calculo is not null)
            {
                await calculo.RecalcularJornadaAsync(
                    periodo.Id, solicitud.TrabajadorId, solicitud.FechaEfectiva, usuario, cancelacion)
                    .ConfigureAwait(false);
            }
            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException(
                "Otra persona resolvió la solicitud o cerró el periodo mientras se aprobaba.");
        }
    }

    public async Task<IReadOnlyList<OpcionDto>> TurnosAsync(CancellationToken cancelacion = default) =>
        await contexto.Turnos.AsNoTracking().Where(t => t.Activo)
            .OrderBy(t => t.Codigo)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    public async Task<SolicitudListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default)
    {
        var consulta = contexto.SolicitudesAusencia.AsNoTracking().Where(s => s.Id == id);

        // Acceso por id directo: si la solicitud es de un trabajador de otra planta no aparece
        // (404 en el controlador), igual que en el listado.
        consulta = await FiltrarPorAlcanceAsync(consulta, cancelacion).ConfigureAwait(false);

        return await Proyectar(consulta)
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
    }

    public async Task<long> GuardarAsync(
        GuardarSolicitudRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // Un administrador solo registra ausencias de trabajadores de sus plantas. El
        // desplegable ya viene filtrado; esto es el control de servidor detrás de él.
        await GarantizarTrabajadorEnAlcanceAsync(solicitud.TrabajadorId, cancelacion).ConfigureAwait(false);

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

        // Cadena vacía y null son lo mismo aquí: "todavía no hay sustento". Guardar "" haría
        // que FaltaSustento y AprobarAsync tuvieran que comprobar dos formas del mismo
        // estado, y basta con que una se olvide para aprobar algo sin respaldo.
        entidad.Sustento = string.IsNullOrWhiteSpace(solicitud.Sustento)
            ? null
            : solicitud.Sustento.Trim();
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
        // permiso muchas veces todavía no tiene el certificado en la mano. La contraparte
        // de esta regla es RegistrarSustentoAsync, que permite agregarlo cuando llega —
        // antes el mensaje pedía algo que el sistema no dejaba hacer.
        if (tipo.RequiereSustento && string.IsNullOrWhiteSpace(solicitud.Sustento))
        {
            throw new ReglaNegocioException(
                $"El tipo '{tipo.Nombre}' exige sustento documentado y la solicitud no lo tiene. " +
                "Use «Registrar sustento» en el listado para agregarlo, y apruebe después.");
        }

        // Los periodos quedan seguidos por el contexto: además de validar su estado antes
        // de preparar las justificaciones, al guardar se afirma su versión. Así un cierre
        // concurrente hace fallar el SaveChanges completo en vez de permitir que la
        // solicitud quede aprobada dentro de un periodo que acaba de cerrarse.
        var periodos = await contexto.Periodos
            .Where(p => p.Activo && p.FechaInicio <= solicitud.FechaFin && p.FechaFin >= solicitud.FechaInicio)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        // Se traen TODAS las justificaciones del trabajador en el rango, no solo las de
        // esta solicitud: el motor de cálculo resuelve una sola justificación por día, así
        // que una manual ya cargada para esa fecha tiene que bloquear la nueva, no coexistir.
        var existentes = await contexto.Justificaciones
            .Where(j => j.TrabajadorId == solicitud.TrabajadorId
                        && j.Fecha >= solicitud.FechaInicio && j.Fecha <= solicitud.FechaFin
                        // Las rechazadas y canceladas quedan como evidencia, pero ya no
                        // ocupan el día. Es el mismo criterio del índice único filtrado.
                        && j.Estado != EstadoJustificacion.Rechazada
                        && j.Estado != EstadoJustificacion.Cancelada)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var enCerrado = new List<DateOnly>();
        var sinPeriodo = new List<DateOnly>();
        var conJustificacionExistente = new List<DateOnly>();
        var diasAcrear = new List<(DateOnly Fecha, Periodo Periodo)>();
        var periodosAfirmar = new Dictionary<long, Periodo>();

        // Primera pasada: validar TODO el rango sin cambiar estado ni agregar filas. Antes,
        // cada impedimento hacía continue y los demás días sí se agregaban; al final la
        // cabecera quedaba Aprobada aunque su efecto fuera parcial (AUS-007).
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

            periodosAfirmar.TryAdd(periodo.Id, periodo);

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

            diasAcrear.Add((fecha, periodo));
        }

        if (enCerrado.Count > 0 || sinPeriodo.Count > 0 || conJustificacionExistente.Count > 0)
        {
            var impedimentos = new List<string>();

            if (enCerrado.Count > 0)
            {
                impedimentos.Add(
                    $"{enCerrado.Count} día(s) pertenecen a un periodo cerrado " +
                    $"({FormatearFechas(enCerrado)})");
            }

            if (sinPeriodo.Count > 0)
            {
                impedimentos.Add(
                    $"{sinPeriodo.Count} día(s) no pertenecen a ningún periodo activo " +
                    $"({FormatearFechas(sinPeriodo)})");
            }

            if (conJustificacionExistente.Count > 0)
            {
                impedimentos.Add(
                    $"{conJustificacionExistente.Count} día(s) ya tienen otra justificación " +
                    $"({FormatearFechas(conJustificacionExistente)})");
            }

            throw new ReglaNegocioException(
                "La solicitud no se aprobó: todos sus días deben poder justificarse en una sola " +
                $"operación. {string.Join("; ", impedimentos)}. Corrija esos días y vuelva a intentarlo.");
        }

        // Segunda pasada: recién cuando todo el rango es válido se preparan las filas. El
        // cambio de cabecera y todas las justificaciones viajan en un único SaveChanges,
        // que EF confirma o revierte como una sola transacción.
        foreach (var (fecha, periodo) in diasAcrear)
        {
            await contexto.Justificaciones.AddAsync(new Justificacion
            {
                TrabajadorId = solicitud.TrabajadorId,
                PeriodoId = periodo.Id,
                Fecha = fecha,
                Origen = OrigenJustificacion.Solicitud,
                SolicitudAusenciaId = solicitud.Id,
                TipoAusenciaId = solicitud.TipoAusenciaId,
                // Un permiso por horas NO es una tardanza: puede caer al final de la jornada,
                // y registrarlo como tardanza descontaba de un contador que ese día valía
                // cero mientras la salida anticipada quedaba intacta pese al permiso
                // aprobado. El motor ubica la ventana contra el turno del día.
                Tipo = tipo.EsPorHoras ? TipoJustificacion.PermisoParcial : TipoJustificacion.Ausencia,
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
        }

        foreach (var periodo in periodosAfirmar.Values)
        {
            contexto.AfirmarAbierto(periodo);
        }

        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.ModificadoPor = usuario;

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException(
                "Uno de los periodos cambió mientras se aprobaba la solicitud; no se aprobó " +
                "la cabecera ni se guardó ninguna justificación. Actualice la pantalla y vuelva a intentarlo.");
        }
        catch (DbUpdateException excepcion) when (EsRestriccionUnica(excepcion))
        {
            throw new ReglaNegocioException(
                "Otra operación registró una justificación para uno de los días mientras se " +
                "aprobaba la solicitud; no se aprobó la cabecera ni se guardó ninguna " +
                "justificación nueva. Actualice la pantalla y vuelva a intentarlo.");
        }

        return new ResultadoAprobacion(solicitud.Id, diasAcrear.Count, [], [], []);
    }

    public async Task RegistrarSustentoAsync(
        long id,
        string sustento,
        string? rutaDocumentoSustento,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var texto = sustento?.Trim() ?? string.Empty;

        if (texto.Length < 5)
        {
            throw new ReglaNegocioException(
                "Describa el sustento: es lo que explicará, ante una inspección, por qué " +
                "ese día no quedó como falta.");
        }

        if (texto.Length > 500)
        {
            throw new ReglaNegocioException("El sustento no puede superar los 500 caracteres.");
        }

        var solicitud = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        // Solo pendientes. Una solicitud aprobada ya generó justificaciones que heredaron
        // el texto del sustento; cambiarlo después las dejaría explicando una cosa distinta
        // de la solicitud que las originó, que es justo lo que una auditoría busca.
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new ReglaNegocioException(
                "Solo se puede registrar el sustento de una solicitud pendiente. Esta está " +
                $"{solicitud.Estado.ToString().ToLowerInvariant()}.");
        }

        solicitud.Sustento = texto;

        if (!string.IsNullOrWhiteSpace(rutaDocumentoSustento))
        {
            solicitud.RutaDocumentoSustento = rutaDocumentoSustento.Trim();
        }

        solicitud.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
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

        // No se intenta "arreglar" una solicitud histórica ya incompleta. AUS-007 evita
        // nuevos casos, pero una cabecera aprobada anterior podría haber quedado con días
        // omitidos. Cancelar solo lo que sobrevivió ocultaría esa inconsistencia y la fila
        // perdida no puede reconstruirse con actor/fecha/ID auténticos.
        var fechasEsperadas = Enumerable
            .Range(0, solicitud.FechaFin.DayNumber - solicitud.FechaInicio.DayNumber + 1)
            .Select(desplazamiento => solicitud.FechaInicio.AddDays(desplazamiento))
            .ToHashSet();
        var evidenciaCompleta = justificaciones.Count == fechasEsperadas.Count
                                && justificaciones.Select(j => j.Fecha).Distinct().Count()
                                == fechasEsperadas.Count
                                && justificaciones.All(j =>
                                    fechasEsperadas.Contains(j.Fecha)
                                    && j.Origen == OrigenJustificacion.Solicitud
                                    && j.TrabajadorId == solicitud.TrabajadorId
                                    && j.TipoAusenciaId == solicitud.TipoAusenciaId
                                    && j.Estado == EstadoJustificacion.Aprobada);

        if (!evidenciaCompleta)
        {
            throw new ReglaNegocioException(
                "No se puede anular: la evidencia derivada de la solicitud está incompleta " +
                "o es inconsistente. No se modificó nada; audite esas filas y trátelas en " +
                "un saneamiento separado.");
        }

        var idsPeriodos = justificaciones
            .Select(j => j.PeriodoId)
            .Distinct()
            .ToList();

        // Se cargan seguidos por el contexto: al guardar, AfirmarAbierto convierte la
        // comprobación en parte de la misma transacción. Si un periodo se cierra entre
        // esta lectura y el SaveChanges, no se anula ni la cabecera ni una sola fila.
        var periodos = await contexto.Periodos
            .Where(p => idsPeriodos.Contains(p.Id))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var idsPeriodosEncontrados = periodos.Select(p => p.Id).ToHashSet();
        var sinPeriodo = idsPeriodos.FindAll(id => !idsPeriodosEncontrados.Contains(id));

        if (sinPeriodo.Count > 0)
        {
            throw new ReglaNegocioException(
                "No se puede anular: una o más justificaciones apuntan a periodos que ya no " +
                "existen. Preserve la evidencia y revise esos datos antes de continuar.");
        }

        // Si alguna justificación cayó en un periodo ya cerrado, no se puede dejar sin
        // efecto: el tareo de ese periodo está congelado. Se avisa en vez de romper la
        // invariante.
        var periodosCerrados = periodos
            .Where(p => p.Estado != EstadoPeriodo.Abierto)
            .Select(p => p.Id)
            .ToHashSet();
        var bloqueadas = justificaciones.FindAll(j => periodosCerrados.Contains(j.PeriodoId));

        if (bloqueadas.Count > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede anular: {bloqueadas.Count} de sus justificaciones caen en un periodo " +
                "cerrado y su tareo ya está congelado. Reabra el periodo si realmente corresponde.");
        }

        var ahora = DateTime.Now;
        var motivoNormalizado = motivo.Trim();

        foreach (var justificacion in justificaciones)
        {
            // La anulación ya no borra la evidencia derivada. Cancelada es un estado no
            // aplicable al tareo y no ocupa el índice único del día, pero conserva la fila,
            // su origen, su vínculo con la solicitud y quién tomó la decisión.
            justificacion.Estado = EstadoJustificacion.Cancelada;
            justificacion.ResueltaPorUsuario = usuario;
            justificacion.ResueltaEn = ahora;
            justificacion.MotivoResolucion = motivoNormalizado;
            justificacion.ModificadoPor = usuario;
            justificacion.Version++;
        }

        foreach (var periodo in periodos)
        {
            contexto.AfirmarAbierto(periodo);
        }

        solicitud.Estado = EstadoSolicitud.Anulada;
        solicitud.MotivoAnulacion = motivoNormalizado;
        solicitud.ModificadoPor = usuario;

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException excepcion)
        {
            var periodoEnConflicto = excepcion.Entries
                .Select(e => e.Entity)
                .OfType<Periodo>()
                .FirstOrDefault();

            if (periodoEnConflicto is not null)
            {
                throw await contexto
                    .ExplicarChoqueAsync(
                        periodoEnConflicto.Id,
                        "anulaba la solicitud",
                        cancelacion)
                    .ConfigureAwait(false);
            }

            throw new ReglaNegocioException(
                "Otra operación modificó una justificación mientras se anulaba la solicitud; " +
                "no se guardó ningún cambio. Actualice la pantalla y vuelva a intentarlo.");
        }
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default)
    {
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaCese == null);

        // El desplegable de trabajadores del formulario solo ofrece los de las plantas que el
        // usuario gestiona: no puede registrar una ausencia de alguien que no ve.
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantas is not null)
        {
            consulta = consulta.Where(t => plantas.Contains(t.PlantaId));
        }

        return await consulta
            .OrderBy(t => t.ApellidoPaterno).ThenBy(t => t.ApellidoMaterno)
            .Select(t => new OpcionDto(
                t.Id, t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

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

    private static string FormatearFechas(IEnumerable<DateOnly> fechas) =>
        string.Join(", ", fechas.Select(
            fecha => fecha.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)));

    private static bool EsRestriccionUnica(DbUpdateException excepcion) =>
        excepcion.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.OrdinalIgnoreCase) == true;

    private static void AplicarExcepcionDeUnDia(
        List<AsignacionTurno> asignaciones,
        AsignacionTurno vigente,
        DateOnly fecha,
        long turnoId,
        string usuario)
    {
        var inicioOriginal = vigente.FechaInicio;
        var finOriginal = vigente.FechaFin;

        if (inicioOriginal < fecha)
        {
            vigente.FechaFin = fecha.AddDays(-1);
            vigente.ModificadoPor = usuario;
            if (finOriginal is null || finOriginal > fecha)
            {
                asignaciones.Add(new AsignacionTurno
                {
                    TrabajadorId = vigente.TrabajadorId,
                    TurnoId = vigente.TurnoId,
                    CuadrillaId = vigente.CuadrillaId,
                    FechaInicio = fecha.AddDays(1),
                    FechaFin = finOriginal,
                    CreadoPor = usuario
                });
            }
        }
        else if (finOriginal is null || finOriginal > fecha)
        {
            vigente.FechaInicio = fecha.AddDays(1);
            vigente.ModificadoPor = usuario;
        }
        else
        {
            vigente.TurnoId = turnoId;
            vigente.CuadrillaId = null;
            vigente.ModificadoPor = usuario;
            return;
        }

        asignaciones.Add(new AsignacionTurno
        {
            TrabajadorId = vigente.TrabajadorId,
            TurnoId = turnoId,
            FechaInicio = fecha,
            FechaFin = fecha,
            CreadoPor = usuario
        });
    }

    private async Task<long> PlantaHistoricaAsync(
        long trabajadorId,
        DateOnly fecha,
        CancellationToken cancelacion)
    {
        var plantas = await contexto.AsignacionesPlanta.AsNoTracking()
            .Where(a => a.PeriodoLaboral!.TrabajadorId == trabajadorId && a.FechaInicio <= fecha
                        && (a.FechaFin == null || a.FechaFin >= fecha))
            .Select(a => a.PlantaId).ToListAsync(cancelacion).ConfigureAwait(false);
        return plantas.Count == 1 ? plantas[0] : throw new ReglaNegocioException(
            plantas.Count == 0 ? "El trabajador no tiene unidad vigente en la fecha efectiva."
                : "El trabajador tiene unidades solapadas en la fecha efectiva.");
    }

    private async Task GarantizarPlantaEnAlcanceAsync(
        long plantaId,
        long trabajadorId,
        CancellationToken cancelacion)
    {
        if (!await alcance.PuedeVerPlantaAsync(plantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", trabajadorId);
        }
    }

    private async Task<SolicitudAusencia> BuscarAsync(long id, CancellationToken cancelacion)
    {
        var solicitud = await contexto.SolicitudesAusencia
            .FirstOrDefaultAsync(s => s.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la solicitud", id);

        // Chokepoint del acceso por id: aprobar, rechazar, anular, registrar sustento y editar
        // pasan todas por aquí. Si la solicitud es de un trabajador de otra planta, se responde
        // como si no existiera, sin confirmar que existe en otra planta.
        if (!await alcance.PuedeVerTrabajadorAsync(contexto, solicitud.TrabajadorId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("la solicitud", id);
        }

        return solicitud;
    }

    /// <summary>Acota una consulta de solicitudes a las plantas visibles (null = sin límite).</summary>
    private async Task<IQueryable<SolicitudAusencia>> FiltrarPorAlcanceAsync(
        IQueryable<SolicitudAusencia> consulta, CancellationToken cancelacion)
    {
        var plantas = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        return plantas is null
            ? consulta
            : consulta.Where(s => contexto.Trabajadores
                .Where(t => plantas.Contains(t.PlantaId))
                .Select(t => t.Id)
                .Contains(s.TrabajadorId));
    }

    /// <summary>404 si el trabajador no es de una planta que el usuario gestiona.</summary>
    private async Task GarantizarTrabajadorEnAlcanceAsync(long trabajadorId, CancellationToken cancelacion)
    {
        if (!await alcance.PuedeVerTrabajadorAsync(contexto, trabajadorId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", trabajadorId);
        }
    }

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
