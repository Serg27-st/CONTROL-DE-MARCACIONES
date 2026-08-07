using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;

namespace ControlMarcacion.Application.Personal;

/// <summary>
/// Casos de uso del mantenimiento de personal.
/// </summary>
/// <remarks>
/// Es la única puerta de entrada al módulo Personal: ningún otro módulo debe tocar
/// <see cref="ITrabajadorRepository"/> directamente.
///
/// Depende de <see cref="IConsultaCatalogos"/> — un contrato público de Application, no
/// la tabla — para comprobar contra el catálogo de marcadores que el ID que se asigna
/// existe y es de la planta del trabajador.
/// </remarks>
public sealed class ServicioTrabajadores(
    ITrabajadorRepository repositorio,
    IConsultaTrabajadores consulta,
    IConsultaCatalogos catalogos)
{
    public Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(
        FiltroTrabajadores filtro,
        CancellationToken cancelacion = default) =>
        consulta.ListarAsync(filtro, cancelacion);

    /// <summary>Búsqueda en vivo para el panel flotante. Ver <see cref="IConsultaTrabajadores.BuscarRapidoAsync"/>.</summary>
    public Task<IReadOnlyList<TrabajadorBusquedaRapidaDto>> BuscarRapidoAsync(
        string? texto,
        CancellationToken cancelacion = default) =>
        consulta.BuscarRapidoAsync(texto, cancelacion);

    public async Task<TrabajadorDetalleDto> ObtenerAsync(long id, CancellationToken cancelacion = default)
    {
        var detalle = await consulta.ObtenerDetalleAsync(id, cancelacion).ConfigureAwait(false);

        return detalle ?? throw new RecursoNoEncontradoException("el trabajador", id);
    }

    /// <summary>Ficha para el panel flotante, con nombres resueltos y sueldo básico vigente.</summary>
    public async Task<TrabajadorPanelDto> ObtenerPanelAsync(long id, CancellationToken cancelacion = default)
    {
        var detalle = await consulta.ObtenerPanelAsync(id, cancelacion).ConfigureAwait(false);

        return detalle ?? throw new RecursoNoEncontradoException("el trabajador", id);
    }

    /// <summary>
    /// Historial por rangos de fecha: vínculos laborales, sueldo, ID de marcador y turno.
    /// </summary>
    public async Task<HistorialTrabajadorDto> ObtenerHistorialAsync(
        long id,
        CancellationToken cancelacion = default)
    {
        var historial = await consulta.ObtenerHistorialAsync(id, cancelacion).ConfigureAwait(false);

        return historial ?? throw new RecursoNoEncontradoException("el trabajador", id);
    }

    public async Task<long> CrearAsync(
        GuardarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        await ValidarAsync(solicitud, null, solicitud.FechaCese, cancelacion).ConfigureAwait(false);

        var trabajador = new Trabajador
        {
            TipoDocumentoId = solicitud.TipoDocumentoId,
            NumeroDocumento = solicitud.NumeroDocumento.Trim(),
            ApellidoPaterno = solicitud.ApellidoPaterno.Trim(),
            ApellidoMaterno = solicitud.ApellidoMaterno.Trim(),
            Nombres = solicitud.Nombres.Trim(),
            Sexo = solicitud.Sexo,
            Telefono = solicitud.Telefono?.Trim(),
            Direccion = solicitud.Direccion?.Trim(),
            CorreoElectronico = solicitud.CorreoElectronico?.Trim(),
            TieneHijos = solicitud.TieneHijos,
            EmpresaId = solicitud.EmpresaId,
            Unidad = solicitud.Unidad?.Trim(),
            TipoPlanilla = solicitud.TipoPlanilla,
            PlantaId = solicitud.PlantaId,
            AreaId = solicitud.AreaId,
            SeccionId = solicitud.SeccionId,
            FechaIngreso = solicitud.FechaIngreso,
            FechaCese = solicitud.FechaCese,
            MotivoCese = solicitud.MotivoCese,
            PuestoId = solicitud.PuestoId,
            CentroCostoId = solicitud.CentroCostoId,
            CreadoPor = usuario
        };

        AplicarAsignaciones(trabajador, solicitud, usuario);

        // El vínculo laboral se abre junto con la ficha: si se creara recién al primer
        // cese, los trabajadores que nunca cesan no tendrían historial y la pantalla
        // mostraría un padrón a medias.
        trabajador.AbrirVinculo(usuario);

        await repositorio.AgregarAsync(trabajador, cancelacion).ConfigureAwait(false);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        return trabajador.Id;
    }

    public async Task ActualizarAsync(
        GuardarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Id is not { } id)
        {
            throw new ReglaNegocioException("No se indicó qué trabajador se va a actualizar.");
        }

        var trabajador = await repositorio.ObtenerPorIdAsync(id, cancelacion).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", id);

        // El cese no se edita desde este formulario: tiene su propia acción, que exige
        // motivo. Se valida contra la fecha que el trabajador ya tiene registrada.
        await ValidarAsync(solicitud, id, trabajador.FechaCese, cancelacion).ConfigureAwait(false);

        // Va antes que el resto de asignaciones: si el cambio de empresa es inválido, la
        // actualización completa se rechaza y no queda una ficha a medio modificar.
        trabajador.CambiarEmpresa(solicitud.EmpresaId, usuario);

        trabajador.TipoDocumentoId = solicitud.TipoDocumentoId;
        trabajador.NumeroDocumento = solicitud.NumeroDocumento.Trim();
        trabajador.ApellidoPaterno = solicitud.ApellidoPaterno.Trim();
        trabajador.ApellidoMaterno = solicitud.ApellidoMaterno.Trim();
        trabajador.Nombres = solicitud.Nombres.Trim();
        trabajador.Sexo = solicitud.Sexo;
        trabajador.Telefono = solicitud.Telefono?.Trim();
        trabajador.Direccion = solicitud.Direccion?.Trim();
        trabajador.CorreoElectronico = solicitud.CorreoElectronico?.Trim();
        trabajador.TieneHijos = solicitud.TieneHijos;
        trabajador.Unidad = solicitud.Unidad?.Trim();
        trabajador.TipoPlanilla = solicitud.TipoPlanilla;
        trabajador.PlantaId = solicitud.PlantaId;

        // Área y Sección ya no se capturan en el formulario de Personal (Centro y
        // Centro de costo cumplen ese rol). Si la solicitud no las trae, se
        // conserva el valor existente en vez de borrarlo: ServicioReportes las usa
        // todavía como respaldo del tareo final para los trabajadores que aún no
        // tienen Centro de costo asignado.
        if (solicitud.AreaId is { } areaId)
        {
            trabajador.AreaId = areaId;
        }

        if (solicitud.SeccionId is { } seccionId)
        {
            trabajador.SeccionId = seccionId;
        }

        trabajador.FechaIngreso = solicitud.FechaIngreso;
        trabajador.PuestoId = solicitud.PuestoId;
        trabajador.CentroCostoId = solicitud.CentroCostoId;
        trabajador.ModificadoPor = usuario;

        SincronizarVinculoAbierto(trabajador, usuario);
        AplicarAsignaciones(trabajador, solicitud, usuario);

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Registra la baja: fecha de cese y motivo, los dos obligatorios.
    /// </summary>
    /// <remarks>
    /// Es una acción aparte y no un campo del formulario porque cierra el vínculo
    /// laboral y las asignaciones vigentes de marcador, turno y sueldo. Además es el
    /// único hecho que habilita cambiar al trabajador de razón social: ver
    /// <see cref="Trabajador.CambiarEmpresa"/>.
    /// </remarks>
    public async Task DarDeBajaAsync(
        BajaTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Motivo is not { } motivo)
        {
            throw new ReglaNegocioException(
                "Debe indicar el motivo de la baja: renuncia voluntaria, término de contrato, " +
                "no superó el periodo de prueba, abandono laboral, despido o destitución.");
        }

        var trabajador = await repositorio
            .ObtenerPorIdAsync(solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);

        trabajador.DarDeBaja(solicitud.FechaCese, motivo, solicitud.Observacion, usuario);

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
    }

    private async Task ValidarAsync(
        GuardarTrabajadorRequest solicitud,
        long? idExistente,
        DateOnly? fechaCeseVigente,
        CancellationToken cancelacion)
    {
        if (fechaCeseVigente is { } cese && cese < solicitud.FechaIngreso)
        {
            throw new ReglaNegocioException(
                "La fecha de cese no puede ser anterior a la fecha de ingreso.");
        }

        var documento = solicitud.NumeroDocumento.Trim();

        var documentoRepetido = await repositorio
            .ExisteDocumentoAsync(solicitud.TipoDocumentoId, documento, idExistente, cancelacion)
            .ConfigureAwait(false);

        if (documentoRepetido)
        {
            throw new ReglaNegocioException(
                $"Ya existe un trabajador registrado con el documento {documento}.");
        }

        if (string.IsNullOrWhiteSpace(solicitud.IdMarcador))
        {
            return;
        }

        var idMarcador = solicitud.IdMarcador.Trim();

        await ValidarContraCatalogoAsync(idMarcador, solicitud.PlantaId, cancelacion).ConfigureAwait(false);

        // El ID del marcador se reasigna con el tiempo, pero dos personas no pueden
        // tenerlo a la vez: las marcaciones de ese periodo serían inatribuibles.
        var marcadorOcupado = await repositorio
            .IdMarcadorEnUsoAsync(
                idMarcador,
                solicitud.FechaIngreso,
                fechaCeseVigente,
                idExistente,
                cancelacion)
            .ConfigureAwait(false);

        if (marcadorOcupado)
        {
            throw new ReglaNegocioException(
                $"El identificador de marcador '{idMarcador}' ya está asignado " +
                "a otro trabajador en un periodo que se cruza con este. Cierre la asignación " +
                "anterior antes de reutilizarlo.");
        }
    }

    /// <summary>
    /// Comprueba que el ID exista en el catálogo de marcadores y sea de la planta del
    /// trabajador.
    /// </summary>
    /// <remarks>
    /// Antes el ID se escribía a mano y cualquier texto pasaba. Un dígito de más se
    /// aceptaba sin queja y sus marcaciones se acumulaban en el reporte de "IDs sin
    /// trabajador asignado" hasta que alguien lo notaba a fin de mes, con el periodo
    /// a punto de cerrarse.
    /// </remarks>
    private async Task ValidarContraCatalogoAsync(
        string idMarcador,
        long plantaId,
        CancellationToken cancelacion)
    {
        var marcador = await catalogos
            .MarcadorPorCodigoAsync(idMarcador, cancelacion)
            .ConfigureAwait(false);

        if (marcador is null)
        {
            throw new ReglaNegocioException(
                $"El identificador de marcador '{idMarcador}' no existe en el catálogo. " +
                "Regístrelo primero en Catálogos → Marcadores, indicando a qué planta pertenece.");
        }

        if (!marcador.Activo)
        {
            throw new ReglaNegocioException(
                $"El marcador '{idMarcador}' está dado de baja en el catálogo y no puede asignarse.");
        }

        if (marcador.PlantaId != plantaId)
        {
            throw new ReglaNegocioException(
                $"El marcador '{idMarcador}' pertenece a la planta {marcador.Planta} y el " +
                "trabajador está asignado a otra. Elija un marcador de su planta o corrija la " +
                "planta del trabajador.");
        }
    }

    /// <summary>
    /// Mantiene el vínculo laboral abierto alineado con la ficha.
    /// </summary>
    /// <remarks>
    /// Solo toca el periodo abierto. Los ya cerrados son historia y no se reescriben:
    /// corregir hacia atrás la fecha de ingreso de un vínculo terminado cambiaría el
    /// sustento de tareos ya pagados.
    /// </remarks>
    private static void SincronizarVinculoAbierto(Trabajador trabajador, string usuario)
    {
        var abierto = trabajador.PeriodosLaborales.FirstOrDefault(p => p.EstaAbierto);

        if (abierto is null)
        {
            // Ficha creada antes de que existiera el historial, o reingreso: se abre uno
            // nuevo en vez de dejar al trabajador sin vínculo vigente.
            trabajador.AbrirVinculo(usuario);
            return;
        }

        if (abierto.FechaIngreso == trabajador.FechaIngreso && abierto.EmpresaId == trabajador.EmpresaId)
        {
            return;
        }

        abierto.FechaIngreso = trabajador.FechaIngreso;
        abierto.EmpresaId = trabajador.EmpresaId;
        abierto.ModificadoPor = usuario;
    }

    /// <summary>
    /// Crea las asignaciones de marcador, turno y sueldo básico cuando cambian.
    /// </summary>
    /// <remarks>
    /// Las asignaciones no se editan: se cierra la vigente y se abre una nueva. Reescribir
    /// la asignación anterior haría que las marcaciones históricas se reinterpreten con
    /// datos que no estaban vigentes cuando ocurrieron.
    /// </remarks>
    private static void AplicarAsignaciones(
        Trabajador trabajador,
        GuardarTrabajadorRequest solicitud,
        string usuario)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        if (!string.IsNullOrWhiteSpace(solicitud.IdMarcador))
        {
            var idMarcador = solicitud.IdMarcador.Trim();
            var vigente = trabajador.AsignacionesMarcador.FirstOrDefault(a => a.FechaFin is null);

            if (vigente is null)
            {
                trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
                {
                    IdMarcador = idMarcador,
                    FechaInicio = trabajador.FechaIngreso,
                    CreadoPor = usuario
                });
            }
            else if (!string.Equals(vigente.IdMarcador, idMarcador, StringComparison.Ordinal))
            {
                vigente.FechaFin = hoy;
                vigente.ModificadoPor = usuario;

                trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
                {
                    IdMarcador = idMarcador,
                    FechaInicio = hoy.AddDays(1),
                    CreadoPor = usuario
                });
            }
        }

        if (solicitud.TurnoId is { } turnoId)
        {
            var turnoVigente = trabajador.AsignacionesTurno.FirstOrDefault(a => a.FechaFin is null);

            if (turnoVigente is null)
            {
                trabajador.AsignacionesTurno.Add(new AsignacionTurno
                {
                    TurnoId = turnoId,
                    FechaInicio = trabajador.FechaIngreso,
                    CreadoPor = usuario
                });
            }
            else if (turnoVigente.TurnoId != turnoId)
            {
                turnoVigente.FechaFin = hoy;
                turnoVigente.ModificadoPor = usuario;

                trabajador.AsignacionesTurno.Add(new AsignacionTurno
                {
                    TurnoId = turnoId,
                    FechaInicio = hoy.AddDays(1),
                    CreadoPor = usuario
                });
            }
        }

        if (solicitud.MontoSueldoBasico is { } sueldoBasico)
        {
            var sueldoVigente = trabajador.AsignacionesSueldo.FirstOrDefault(a => a.FechaFin is null);

            if (sueldoVigente is null)
            {
                trabajador.AsignacionesSueldo.Add(new AsignacionSueldo
                {
                    SueldoBasicoMensual = sueldoBasico,
                    FechaInicio = trabajador.FechaIngreso,
                    CreadoPor = usuario
                });
            }
            else if (sueldoVigente.SueldoBasicoMensual != sueldoBasico)
            {
                sueldoVigente.FechaFin = hoy;
                sueldoVigente.ModificadoPor = usuario;

                trabajador.AsignacionesSueldo.Add(new AsignacionSueldo
                {
                    SueldoBasicoMensual = sueldoBasico,
                    FechaInicio = hoy.AddDays(1),
                    CreadoPor = usuario
                });
            }
        }
    }
}
