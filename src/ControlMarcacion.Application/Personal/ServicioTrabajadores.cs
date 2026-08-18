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
    IConsultaCatalogos catalogos,
    IProveedorAlcancePlantas alcance)
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

        // Un administrador solo da de alta trabajadores en las plantas que gestiona.
        await GarantizarPlantaAsignableAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);

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

        // Un alta puede nacer ya cesada —la carga masiva de personal histórico lo hace—, y
        // en ese caso las asignaciones que se acaban de crear tienen que cerrarse en la
        // misma fecha. Si no, el ID de marcador queda ocupado por alguien que ya no está.
        trabajador.CerrarAsignacionesSiNaceCesado(usuario);

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

        // El administrador no puede tocar la ficha de un trabajador de otra planta (404, no
        // se confirma que exista), ni moverlo a una planta que no gestiona (regla de negocio).
        // Se comprueba la planta ACTUAL antes de aplicar cambios y la DESTINO que trae la
        // solicitud.
        await GarantizarAccesoTrabajadorAsync(trabajador.PlantaId, id, cancelacion).ConfigureAwait(false);
        await GarantizarPlantaAsignableAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);

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

        // Dar de baja a alguien de otra planta se responde como 404: un administrador no
        // gestiona ese vínculo.
        await GarantizarAccesoTrabajadorAsync(trabajador.PlantaId, solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false);

        trabajador.DarDeBaja(solicitud.FechaCese, motivo, solicitud.Observacion, usuario);

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Comprueba que el administrador gestione la planta de un trabajador existente antes de
    /// tocarlo. Si no, responde como si no existiera (404): no se confirma que exista en otra
    /// planta — filtrar el listado no basta si por id directo se llega igual a la ficha.
    /// </summary>
    private async Task GarantizarAccesoTrabajadorAsync(long plantaId, long id, CancellationToken cancelacion)
    {
        if (!await alcance.PuedeVerPlantaAsync(plantaId, cancelacion).ConfigureAwait(false))
        {
            throw new RecursoNoEncontradoException("el trabajador", id);
        }
    }

    /// <summary>
    /// Un administrador solo puede registrar o mover un trabajador a una planta que gestiona.
    /// A diferencia del acceso por id, aquí es una regla de negocio explícita: la planta
    /// destino la eligió en el formulario, no es un id que adivinó.
    /// </summary>
    private async Task GarantizarPlantaAsignableAsync(long plantaId, CancellationToken cancelacion)
    {
        if (!await alcance.PuedeVerPlantaAsync(plantaId, cancelacion).ConfigureAwait(false))
        {
            throw new ReglaNegocioException(
                "No puede registrar ni asignar un trabajador a una planta que no gestiona.");
        }
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

        await ValidarFormatoDocumentoAsync(solicitud.TipoDocumentoId, documento, cancelacion)
            .ConfigureAwait(false);

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
    /// Comprueba el número de documento contra las reglas de su tipo en el catálogo.
    /// </summary>
    /// <remarks>
    /// El formulario ya acota el campo con <c>maxlength</c> e <c>inputmode</c>, pero eso
    /// es comodidad para quien escribe, no un control: se desactiva desde las herramientas
    /// del navegador y no interviene en absoluto en la carga masiva por CSV. La regla del
    /// proyecto es explícita —la validación va en Application, no en la vista—, así que
    /// esta es la comprobación que de verdad decide.
    /// </remarks>
    private async Task ValidarFormatoDocumentoAsync(
        long tipoDocumentoId,
        string documento,
        CancellationToken cancelacion)
    {
        var tipo = await catalogos
            .TipoDocumentoPorIdAsync(tipoDocumentoId, cancelacion)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            throw new ReglaNegocioException(
                "El tipo de documento indicado no existe o está dado de baja en el catálogo.");
        }

        var problema = ValidadorDocumentoIdentidad.Validar(
            tipo.Codigo, tipo.LongitudExacta, tipo.LongitudMaxima, tipo.SoloDigitos, documento);

        if (problema is not null)
        {
            throw new ReglaNegocioException(problema);
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

            Reasignar(
                trabajador,
                trabajador.AsignacionesMarcador,
                a => string.Equals(a.IdMarcador, idMarcador, StringComparison.Ordinal),
                a => a.IdMarcador = idMarcador,
                desde => new AsignacionMarcador { IdMarcador = idMarcador, FechaInicio = desde },
                hoy,
                usuario);
        }

        if (solicitud.TurnoId is { } turnoId)
        {
            Reasignar(
                trabajador,
                trabajador.AsignacionesTurno,
                a => a.TurnoId == turnoId,
                a => a.TurnoId = turnoId,
                desde => new AsignacionTurno { TurnoId = turnoId, FechaInicio = desde },
                hoy,
                usuario);
        }

        if (solicitud.MontoSueldoBasico is { } sueldoBasico)
        {
            Reasignar(
                trabajador,
                trabajador.AsignacionesSueldo,
                a => a.SueldoBasicoMensual == sueldoBasico,
                a => a.SueldoBasicoMensual = sueldoBasico,
                desde => new AsignacionSueldo { SueldoBasicoMensual = sueldoBasico, FechaInicio = desde },
                hoy,
                usuario);
        }

        // Última palabra antes de guardar. Las tres asignaciones comparten mecánica, así
        // que comparten la comprobación: si alguna quedó solapada o con un rango imposible,
        // la actualización entera se rechaza en vez de dejar al trabajador con dos turnos
        // vigentes el mismo día.
        ValidadorVigencias.GarantizarLineaDeTiempoSana(trabajador.AsignacionesMarcador, "ID de marcador");
        ValidadorVigencias.GarantizarLineaDeTiempoSana(trabajador.AsignacionesTurno, "turno");
        ValidadorVigencias.GarantizarLineaDeTiempoSana(trabajador.AsignacionesSueldo, "sueldo básico");
    }

    /// <summary>
    /// Cierra la asignación vigente y abre una nueva cuando el valor cambia.
    /// </summary>
    /// <remarks>
    /// El corte es <b>hoy</b>: la vigente termina hoy y la nueva empieza mañana. Las
    /// asignaciones no se editan porque reinterpretarían marcaciones ya ocurridas.
    ///
    /// <para>Con una excepción, y es la que arregla un defecto real: una asignación que
    /// empieza <b>después de hoy</b> todavía no ha regido ni un solo día, así que no es
    /// historia y se corrige en sitio. Cerrarla «hoy» la dejaba con fecha de fin anterior a
    /// su fecha de inicio —un rango imposible— y eso es exactamente lo que pasaba al
    /// corregir dos veces el mismo día un turno recién cambiado, que es lo normal cuando
    /// alguien se equivoca de turno al preparar la rotación.</para>
    /// </remarks>
    private static void Reasignar<T>(
        Trabajador trabajador,
        ICollection<T> asignaciones,
        Func<T, bool> conservaElMismoValor,
        Action<T> reescribirValor,
        Func<DateOnly, T> abrirDesde,
        DateOnly hoy,
        string usuario)
        where T : EntidadBase, IVigenciaPorFecha
    {
        var vigente = asignaciones.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            // Un trabajador cesado no recibe asignaciones nuevas: sus vigencias se cerraron
            // el día del cese. Reabrir una desde la fecha de ingreso —que es lo que hacía
            // guardar su ficha— generaba una asignación que solapaba con toda su historia.
            if (trabajador.FechaCese is not null)
            {
                return;
            }

            var nueva = abrirDesde(trabajador.FechaIngreso);
            nueva.CreadoPor = usuario;
            asignaciones.Add(nueva);
            return;
        }

        if (conservaElMismoValor(vigente))
        {
            return;
        }

        if (vigente.FechaInicio > hoy)
        {
            reescribirValor(vigente);
            vigente.ModificadoPor = usuario;
            return;
        }

        vigente.FechaFin = hoy;
        vigente.ModificadoPor = usuario;

        var siguiente = abrirDesde(hoy.AddDays(1));
        siguiente.CreadoPor = usuario;
        asignaciones.Add(siguiente);
    }
}
