using ControlMarcacion.Application.Asistencia.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Justificaciones;
using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Justificaciones: consulta con trazabilidad y alta manual.
/// </summary>
internal sealed class ServicioJustificaciones(
    MarcacionDbContext contexto,
    IProveedorAlcancePlantas alcance) : IServicioJustificaciones
{
    public async Task<ResultadoPaginado<JustificacionListaDto>> ListarAsync(
        FiltroJustificaciones filtro,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Justificaciones.AsNoTracking();

        // Aislamiento por planta: un ADMINISTRADOR solo ve justificaciones de trabajadores de
        // sus plantas. La justificación es donde se puede «maquillar» una falta, así que este
        // filtro es tan importante como los demás.
        var plantasVisibles = await alcance.PlantasVisiblesAsync(cancelacion).ConfigureAwait(false);

        if (plantasVisibles is not null)
        {
            consulta = consulta.Where(j => contexto.Trabajadores
                .Where(t => plantasVisibles.Contains(t.PlantaId))
                .Select(t => t.Id)
                .Contains(j.TrabajadorId));
        }

        if (filtro.PeriodoId is { } periodoId)
        {
            consulta = consulta.Where(j => j.PeriodoId == periodoId);
        }

        if (filtro.TrabajadorId is { } trabajadorId)
        {
            consulta = consulta.Where(j => j.TrabajadorId == trabajadorId);
        }

        if (filtro.Origen is { } origen)
        {
            consulta = consulta.Where(j => j.Origen == origen);
        }

        var total = await consulta.CountAsync(cancelacion).ConfigureAwait(false);

        var pagina = Math.Max(filtro.Pagina, 1);
        var tamanio = Math.Clamp(filtro.TamanioPagina, 10, 200);

        var elementos = await consulta
            // Se desempata por Id: al aprobar una solicitud se crean varias justificaciones
            // de fechas distintas, pero un mismo día puede tener filas de trabajadores
            // distintos y sin desempate el orden entre páginas queda indefinido.
            .OrderByDescending(j => j.Fecha)
            .ThenByDescending(j => j.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(j => new JustificacionListaDto(
                j.Id,
                j.TrabajadorId,
                contexto.Trabajadores
                    .Where(t => t.Id == j.TrabajadorId)
                    .Select(t => t.ApellidoPaterno + " " + t.ApellidoMaterno + ", " + t.Nombres)
                    .FirstOrDefault() ?? "-",
                j.PeriodoId,
                contexto.Periodos.Where(p => p.Id == j.PeriodoId).Select(p => p.Codigo).FirstOrDefault() ?? "-",
                j.Fecha,
                j.Origen,
                j.SolicitudAusenciaId,
                j.TipoAusenciaId == null
                    ? null
                    : contexto.TiposAusencia.Where(t => t.Id == j.TipoAusenciaId).Select(t => t.Nombre).FirstOrDefault(),
                j.Tipo,
                j.MinutosJustificados,
                j.EstadoAnterior,
                j.Motivo,
                j.RegistradoPorUsuario,
                j.RegistradoEn,
                j.Estado,
                j.EntradaRegularizada,
                j.SalidaRegularizada,
                j.ResueltaPorUsuario,
                j.ResueltaEn,
                j.MotivoResolucion))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return new ResultadoPaginado<JustificacionListaDto>(elementos, total, pagina, tamanio);
    }

    public async Task<long> CrearManualAsync(
        CrearJustificacionManualRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var trabajador = await contexto.Trabajadores
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);

        // Un administrador solo justifica a trabajadores de sus plantas. Se responde como si el
        // trabajador no existiera (404), sin confirmar que existe en otra planta.
        if (!await alcance.PuedeVerPlantaAsync(trabajador.PlantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);
        }

        // Seguido por el contexto (sin AsNoTracking) a propósito: hace falta para poder
        // afirmar su versión al guardar, que es lo que ata la escritura a que siga abierto.
        var periodo = await contexto.Periodos
            .FirstOrDefaultAsync(
                p => p.Activo && p.FechaInicio <= solicitud.Fecha && p.FechaFin >= solicitud.Fecha,
                cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException(
                $"La fecha {solicitud.Fecha:dd/MM/yyyy} no pertenece a ningún periodo registrado. " +
                "Cree el periodo correspondiente antes de justificar ese día.");

        // Invariante del sistema: periodo cerrado, nada se escribe. Sin excepciones
        // silenciosas ni "modo administrador".
        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        TipoAusencia? tipoAusencia = null;

        if (solicitud.TipoAusenciaId is { } tipoAusenciaId)
        {
            tipoAusencia = await contexto.TiposAusencia
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tipoAusenciaId, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el tipo de ausencia", tipoAusenciaId);

            if (!tipoAusencia.Activo)
            {
                throw new ReglaNegocioException(
                    $"El tipo de ausencia '{tipoAusencia.Nombre}' está inactivo.");
            }
        }
        else if (solicitud.Tipo == TipoJustificacion.Ausencia)
        {
            throw new ReglaNegocioException(
                "Para justificar una ausencia debe indicar el tipo de ausencia: es lo que " +
                "decide si el día se paga.");
        }

        if (solicitud.Tipo is TipoJustificacion.Tardanza or TipoJustificacion.SalidaAnticipada
            && solicitud.TipoAusenciaId is not null)
        {
            throw new ReglaNegocioException(
                "Una justificación de tardanza o de salida anticipada no lleva tipo de ausencia: " +
                "no reemplaza el día, solo perdona minutos.");
        }

        // El permiso por horas necesita su ventana —de qué hora a qué hora— para que el
        // motor sepa si descuenta tardanza o salida anticipada, y esa ventana solo la
        // captura y valida la solicitud de ausencia. Admitirlo aquí crearía justificaciones
        // sin ventana, que el motor solo puede tratar suponiendo dónde cae el permiso.
        if (solicitud.Tipo == TipoJustificacion.PermisoParcial)
        {
            throw new ReglaNegocioException(
                "Un permiso por horas se registra como solicitud de ausencia de un tipo por " +
                "horas, no como justificación manual: es la solicitud la que guarda de qué " +
                "hora a qué hora va el permiso.");
        }

        // El bloque entero va dentro de la estrategia de ejecución y de una transacción: la
        // comprobación de duplicado y la inserción tienen que ser una sola operación. Sueltas
        // eran un consultar-e-insertar que dos usuarios simultáneos atravesaban los dos, y el
        // índice único de (trabajador, fecha) —la garantía real— rechazaba al segundo con un
        // error de base que nadie podía leer.
        //
        // Todo lo que se acumula se construye DENTRO: el bloque puede reejecutarse completo.
        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database
                .BeginTransactionAsync(cancelacion)
                .ConfigureAwait(false);

            await LanzarSiYaExisteAsync(solicitud.TrabajadorId, solicitud.Fecha, cancelacion)
                .ConfigureAwait(false);

            var justificacion = new Justificacion
            {
                TrabajadorId = solicitud.TrabajadorId,
                PeriodoId = periodo.Id,
                Fecha = solicitud.Fecha,
                Origen = OrigenJustificacion.Manual,
                TipoAusenciaId = solicitud.TipoAusenciaId,
                Tipo = solicitud.Tipo,
                MinutosJustificados = solicitud.MinutosJustificados,
                EstadoAnterior = await ObtenerEstadoActualAsync(
                    solicitud.TrabajadorId, periodo.Id, solicitud.Fecha, cancelacion).ConfigureAwait(false),
                Motivo = solicitud.Motivo.Trim(),
                RutaDocumentoSustento = solicitud.RutaDocumentoSustento?.Trim(),
                RegistradoPorUsuario = usuario,
                RegistradoEn = DateTime.Now,
                CreadoPor = usuario
            };

            await contexto.Justificaciones.AddAsync(justificacion, cancelacion).ConfigureAwait(false);

            // Ata la inserción a que el periodo siga abierto: si se cerró desde la
            // comprobación de arriba, el guardado entero se pierde en vez de colarse.
            contexto.AfirmarAbierto(periodo);

            try
            {
                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw await contexto
                    .ExplicarChoqueAsync(periodo.Id, "registraba la justificación", cancelacion)
                    .ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // Quien ganó la carrera ya insertó la suya. Se suelta la entidad rechazada y
                // se vuelve a mirar la base, para responder lo mismo que habría respondido la
                // comprobación de arriba en vez de un error del motor.
                contexto.Entry(justificacion).State = EntityState.Detached;

                await LanzarSiYaExisteAsync(solicitud.TrabajadorId, solicitud.Fecha, cancelacion)
                    .ConfigureAwait(false);

                throw;
            }

            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

            return justificacion.Id;
        }).ConfigureAwait(false);
    }

    public async Task<long> RegularizarAsync(
        RegistrarRegularizacionRequest solicitud,
        string usuario,
        bool apruebaDirectamente,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.EntradaRegularizada is null && solicitud.SalidaRegularizada is null)
        {
            throw new ReglaNegocioException(
                "Indique al menos una hora: la de entrada, la de salida o las dos. " +
                "Una regularización sin hora no corrige nada.");
        }

        if (solicitud.EntradaRegularizada is { } entrada
            && solicitud.SalidaRegularizada is { } salida
            && salida <= entrada)
        {
            throw new ReglaNegocioException(
                "La hora de salida debe ser posterior a la de entrada.");
        }

        // Una hora verificada tiene que caer dentro del día que corrige o del siguiente: un
        // turno nocturno termina al día siguiente, pero nada más allá. Sin este tope, un
        // error de tipeo en la fecha produce una jornada de días enteros.
        foreach (var hora in new[] { solicitud.EntradaRegularizada, solicitud.SalidaRegularizada })
        {
            if (hora is { } valor && !EsDelDiaODelSiguiente(valor, solicitud.Fecha))
            {
                throw new ReglaNegocioException(
                    $"La hora {valor:dd/MM/yyyy HH:mm} no corresponde al " +
                    $"{solicitud.Fecha:dd/MM/yyyy} ni al día siguiente. Revise la fecha.");
            }
        }

        var trabajador = await contexto.Trabajadores
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);

        if (!await alcance.PuedeVerPlantaAsync(trabajador.PlantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);
        }

        // Seguido por el contexto (sin AsNoTracking) a propósito: hace falta para poder
        // afirmar su versión al guardar, que es lo que ata la escritura a que siga abierto.
        var periodo = await contexto.Periodos
            .FirstOrDefaultAsync(
                p => p.Activo && p.FechaInicio <= solicitud.Fecha && p.FechaFin >= solicitud.Fecha,
                cancelacion)
            .ConfigureAwait(false)
            ?? throw new ReglaNegocioException(
                $"La fecha {solicitud.Fecha:dd/MM/yyyy} no pertenece a ningún periodo registrado.");

        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await contexto.Database
                .BeginTransactionAsync(cancelacion)
                .ConfigureAwait(false);

            await LanzarSiYaExisteAsync(solicitud.TrabajadorId, solicitud.Fecha, cancelacion)
                .ConfigureAwait(false);

            var justificacion = new Justificacion
            {
                TrabajadorId = solicitud.TrabajadorId,
                PeriodoId = periodo.Id,
                Fecha = solicitud.Fecha,
                Origen = OrigenJustificacion.Manual,
                Tipo = TipoJustificacion.MarcacionRegularizada,
                TipoIncidencia = solicitud.TipoIncidencia,
                EntradaRegularizada = solicitud.EntradaRegularizada,
                SalidaRegularizada = solicitud.SalidaRegularizada,
                Estado = apruebaDirectamente
                    ? EstadoJustificacion.Aprobada
                    : EstadoJustificacion.Solicitada,
                ResueltaPorUsuario = apruebaDirectamente ? usuario : null,
                ResueltaEn = apruebaDirectamente ? DateTime.Now : null,
                EstadoAnterior = await ObtenerEstadoActualAsync(
                    solicitud.TrabajadorId, periodo.Id, solicitud.Fecha, cancelacion).ConfigureAwait(false),
                Motivo = solicitud.Motivo.Trim(),
                RutaDocumentoSustento = solicitud.RutaDocumentoSustento?.Trim(),
                RegistradoPorUsuario = usuario,
                RegistradoEn = DateTime.Now,
                CreadoPor = usuario
            };

            await contexto.Justificaciones.AddAsync(justificacion, cancelacion).ConfigureAwait(false);

            // Ata la inserción a que el periodo siga abierto: si se cerró desde la
            // comprobación de arriba, el guardado entero se pierde en vez de colarse.
            contexto.AfirmarAbierto(periodo);

            try
            {
                await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw await contexto
                    .ExplicarChoqueAsync(periodo.Id, "registraba la regularización", cancelacion)
                    .ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                contexto.Entry(justificacion).State = EntityState.Detached;

                await LanzarSiYaExisteAsync(solicitud.TrabajadorId, solicitud.Fecha, cancelacion)
                    .ConfigureAwait(false);

                throw;
            }

            await transaccion.CommitAsync(cancelacion).ConfigureAwait(false);

            return justificacion.Id;
        }).ConfigureAwait(false);
    }

    public async Task ResolverAsync(
        long id,
        bool aprueba,
        string? motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        if (!aprueba && string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException(
                "Indique por qué se rechaza: sin motivo, el trabajador no sabe qué corregir " +
                "y quien audita no sabe por qué el día quedó así.");
        }

        var justificacion = await contexto.Justificaciones
            .FirstOrDefaultAsync(j => j.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        var trabajador = await contexto.Trabajadores
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == justificacion.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        if (!await alcance.PuedeVerPlantaAsync(trabajador.PlantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("la justificación", id);
        }

        if (justificacion.Estado is not EstadoJustificacion.Solicitada)
        {
            throw new ReglaNegocioException(
                $"Esta justificación ya está {justificacion.Estado.ToString().ToLowerInvariant()} " +
                $"y no se puede volver a resolver.");
        }

        var periodo = await contexto.Periodos
            .FirstAsync(p => p.Id == justificacion.PeriodoId, cancelacion)
            .ConfigureAwait(false);

        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        justificacion.Estado = aprueba ? EstadoJustificacion.Aprobada : EstadoJustificacion.Rechazada;
        justificacion.ResueltaPorUsuario = usuario;
        justificacion.ResueltaEn = DateTime.Now;
        justificacion.MotivoResolucion = motivo?.Trim();
        justificacion.ModificadoPor = usuario;

        // El token de concurrencia: EF lo mete en el WHERE del UPDATE, así que la segunda
        // resolución simultánea no encuentra a quién actualizar.
        justificacion.Version++;

        // Y el del periodo, que aquí importa especialmente: aprobar es lo que hace que la
        // regularización empiece a cambiar el tareo, así que no puede colarse en un mes que
        // acaba de cerrarse.
        contexto.AfirmarAbierto(periodo);

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException excepcion)
        {
            // Dos tokens en el mismo guardado, y las dos salidas piden cosas distintas: si
            // chocó el periodo hay que reabrirlo, si chocó la justificación basta con mirar
            // cómo quedó. Un «error de concurrencia» a secas no distingue ninguna de las dos.
            if (excepcion.Entries.Any(e => e.Entity is Periodo))
            {
                throw await contexto
                    .ExplicarChoqueAsync(periodo.Id, "resolvía la regularización", cancelacion)
                    .ConfigureAwait(false);
            }

            throw new ReglaNegocioException(
                "Otra persona resolvió esta regularización mientras la revisaba. " +
                "Vuelva a abrir la bandeja para ver cómo quedó.");
        }
    }

    public async Task CancelarAsync(long id, string usuario, CancellationToken cancelacion = default)
    {
        var justificacion = await contexto.Justificaciones
            .FirstOrDefaultAsync(j => j.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        var trabajador = await contexto.Trabajadores
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == justificacion.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        if (!await alcance.PuedeVerPlantaAsync(trabajador.PlantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("la justificación", id);
        }

        if (justificacion.Estado is not EstadoJustificacion.Solicitada)
        {
            throw new ReglaNegocioException(
                $"Esta regularización ya está {justificacion.Estado.ToString().ToLowerInvariant()}: " +
                "solo se retira mientras nadie la ha resuelto.");
        }

        // Cancelar la de otro sería resolverla sin dejar constancia de la decisión, que es
        // justo lo que la aprobación existe para evitar. Quien no esté de acuerdo la rechaza,
        // con motivo, y eso queda escrito.
        if (!string.Equals(justificacion.RegistradoPorUsuario, usuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReglaNegocioException(
                $"Esta regularización la registró {justificacion.RegistradoPorUsuario} y solo " +
                "esa persona puede retirarla. Si no corresponde, recházela indicando el motivo.");
        }

        justificacion.Estado = EstadoJustificacion.Cancelada;
        justificacion.ResueltaPorUsuario = usuario;
        justificacion.ResueltaEn = DateTime.Now;
        justificacion.MotivoResolucion = "Retirada por quien la registró.";
        justificacion.ModificadoPor = usuario;
        justificacion.Version++;

        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReglaNegocioException(
                "Otra persona resolvió esta regularización mientras la retiraba. " +
                "Vuelva a abrir la bandeja para ver cómo quedó.");
        }
    }

    /// <summary>
    /// Una hora regularizada pertenece al día que corrige o al siguiente.
    /// </summary>
    /// <remarks>
    /// El día siguiente entra porque un turno nocturno termina ahí: la jornada del martes
    /// cierra el miércoles a las 07:00. Más allá no: sin tope, un error de tipeo en el año
    /// produce una jornada de meses y un sobretiempo absurdo que nadie revisaría a tiempo.
    /// </remarks>
    private static bool EsDelDiaODelSiguiente(DateTime hora, DateOnly fecha)
    {
        var dia = DateOnly.FromDateTime(hora);

        return dia == fecha || dia == fecha.AddDays(1);
    }

    /// <summary>
    /// Rechaza el alta si el trabajador ya tiene una justificación ese día, diciendo de
    /// dónde viene la que ya existe.
    /// </summary>
    /// <remarks>
    /// El motor resuelve una sola justificación por trabajador y día: una segunda no se
    /// ignoraría, colisionaría al recalcular el periodo entero.
    /// </remarks>
    private async Task LanzarSiYaExisteAsync(
        long trabajadorId,
        DateOnly fecha,
        CancellationToken cancelacion)
    {
        // ⚠️ El mismo criterio que el índice único filtrado de la base: una regularización
        // rechazada o cancelada se conserva como evidencia, pero NO ocupa el día. Si esta
        // consulta las contara, un rechazo bloquearía esa fecha para siempre —la persona no
        // podría volver a pedirla con la hora corregida— y la validación sería más estricta
        // que la garantía, que es la peor combinación posible: el error saldría por C# con
        // un mensaje que afirma algo que la base permite.
        var existente = await contexto.Justificaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(
                j => j.TrabajadorId == trabajadorId
                     && j.Fecha == fecha
                     && j.Estado != EstadoJustificacion.Rechazada
                     && j.Estado != EstadoJustificacion.Cancelada,
                cancelacion)
            .ConfigureAwait(false);

        if (existente is null)
        {
            return;
        }

        var origen = existente.Origen == OrigenJustificacion.Solicitud
            ? $"de la solicitud #{existente.SolicitudAusenciaId}"
            : "manual";

        throw new ReglaNegocioException(
            $"Ya existe una justificación {origen} para este trabajador en el " +
            $"{fecha:dd/MM/yyyy}. Retírela primero si necesita reemplazarla.");
    }

    public async Task EliminarAsync(long id, string usuario, CancellationToken cancelacion = default)
    {
        var justificacion = await contexto.Justificaciones
            .FirstOrDefaultAsync(j => j.Id == id, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la justificación", id);

        // Acceso por id: una justificación de un trabajador de otra planta se responde como
        // inexistente (404), no se retira.
        if (!await alcance.PuedeVerTrabajadorAsync(contexto, justificacion.TrabajadorId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("la justificación", id);
        }

        if (justificacion.Origen != OrigenJustificacion.Manual)
        {
            throw new ReglaNegocioException(
                "Esta justificación proviene de una solicitud de ausencia: para retirarla, " +
                "anule la solicitud que la generó.");
        }

        var periodo = await contexto.Periodos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == justificacion.PeriodoId, cancelacion)
            .ConfigureAwait(false);

        if (periodo is not null && !periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        // No se registra "usuario" en la fila porque va a desaparecer: quién la retiró
        // queda en el log de aplicación, no en una tabla que está a punto de perder la fila.
        _ = usuario;

        contexto.Justificaciones.Remove(justificacion);
        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(CancellationToken cancelacion = default)
    {
        var consulta = contexto.Trabajadores
            .AsNoTracking()
            .Where(t => t.Activo && t.FechaCese == null);

        // El desplegable solo ofrece trabajadores de las plantas que el usuario gestiona.
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

    public async Task<IReadOnlyList<OpcionDto>> TiposAusenciaAsync(CancellationToken cancelacion = default) =>
        await contexto.TiposAusencia
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Nombre)
            .Select(t => new OpcionDto(t.Id, t.Codigo + " - " + t.Nombre))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <summary>
    /// Estado que tenía el tareo antes de esta justificación, si ya se había calculado.
    /// </summary>
    /// <remarks>
    /// Es evidencia de auditoría congelada al momento de crear la justificación: si el
    /// tareo se recalcula después, este valor no cambia, porque describe lo que era
    /// <b>antes</b> de justificar, no lo que es ahora.
    /// </remarks>
    private async Task<string?> ObtenerEstadoActualAsync(
        long trabajadorId, long periodoId, DateOnly fecha, CancellationToken cancelacion) =>
        await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.TrabajadorId == trabajadorId && t.PeriodoId == periodoId && t.Fecha == fecha)
            .Select(t => t.Estado.ToString())
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);
}
