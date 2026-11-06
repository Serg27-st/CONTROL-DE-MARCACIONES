using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
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
/// Depende de <see cref="IConsultaCatalogos"/> y de <see cref="IServicioMarcadores"/> —
/// contratos públicos de Application, no las tablas— para comprobar que el número que se
/// asigna es de un aparato de la planta del trabajador, y para darlo de alta en el
/// catálogo cuando la ficha lo pide. Y de <see cref="IServicioPeriodos"/> para no dejar
/// registrar un cambio de turno con fecha dentro de un periodo cerrado.
/// </remarks>
public sealed class ServicioTrabajadores(
    ITrabajadorRepository repositorio,
    IConsultaTrabajadores consulta,
    IConsultaCatalogos catalogos,
    IProveedorAlcancePlantas alcance,
    IServicioMarcadores marcadores,
    IServicioPeriodos periodos)
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

    /// <summary>Ver <see cref="IConsultaTrabajadores.IntegrantesDeCuadrillaAsync"/>.</summary>
    public Task<IReadOnlyList<IntegranteCuadrillaDto>> IntegrantesDeCuadrillaAsync(
        long cuadrillaId,
        DateOnly fecha,
        CancellationToken cancelacion = default) =>
        consulta.IntegrantesDeCuadrillaAsync(cuadrillaId, fecha, cancelacion);

    /// <summary>Ver <see cref="IConsultaTrabajadores.CandidatosParaCuadrillaAsync"/>.</summary>
    public Task<IReadOnlyList<CandidatoCuadrillaDto>> CandidatosParaCuadrillaAsync(
        long cuadrillaId,
        long plantaId,
        DateOnly fecha,
        CancellationToken cancelacion = default) =>
        consulta.CandidatosParaCuadrillaAsync(cuadrillaId, plantaId, fecha, cancelacion);

    /// <summary>
    /// Suma varios trabajadores a una cuadrilla desde una fecha, de una vez.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es exactamente lo que hace elegir la cuadrilla con «Turno rige desde» en la ficha de
    /// cada uno (<see cref="ReasignarTurno"/>), con las mismas reglas: la fecha no puede ser
    /// anterior al ingreso ni caer en un periodo cerrado, y solo mueve la frontera con la
    /// asignación anterior.
    /// </para>
    /// <para>
    /// Es <b>todo o nada</b>: si una persona no puede sumarse, no se suma nadie y el mensaje
    /// dice quién y por qué. Media cuadrilla sumada y media no dejaría al grupo rotando
    /// partido sin que la pantalla lo mostrara.
    /// </para>
    /// </remarks>
    /// <returns>Cuántas personas cambiaron de asignación; las que ya la tenían no cuentan.</returns>
    public async Task<int> AsignarCuadrillaAsync(
        AsignarCuadrillaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.Desde is not { } desde)
        {
            throw new ReglaNegocioException("Indique desde qué fecha integran la cuadrilla.");
        }

        if (solicitud.TrabajadorIds.Count == 0)
        {
            throw new ReglaNegocioException("Marque al menos un trabajador para sumar a la cuadrilla.");
        }

        var cuadrilla = await catalogos.CuadrillaPorIdAsync(solicitud.CuadrillaId, cancelacion).ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("la cuadrilla", solicitud.CuadrillaId);

        var trabajadorIds = solicitud.TrabajadorIds.Distinct().ToList();
        var contextosLaborales = await consulta
            .ContextosLaboralesEnFechaAsync(trabajadorIds, desde, cancelacion)
            .ConfigureAwait(false);

        var cambiados = 0;
        TramoAfectado? afectado = null;

        foreach (var id in trabajadorIds)
        {
            var trabajador = await repositorio.ObtenerPorIdAsync(id, cancelacion).ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el trabajador", id);

            var nombre = $"{trabajador.ApellidoPaterno} {trabajador.ApellidoMaterno}, {trabajador.Nombres}";

            try
            {
                if (!contextosLaborales.TryGetValue(id, out var laboral))
                {
                    throw new ReglaNegocioException(
                        $"no tenía un vínculo laboral vigente el {desde:dd/MM/yyyy}.");
                }

                if (laboral.PlantaId is not { } plantaHistorica)
                {
                    throw new ReglaNegocioException(
                        $"no tenía una unidad laboral definida el {desde:dd/MM/yyyy}.");
                }

                await GarantizarAccesoTrabajadorAsync(plantaHistorica, id, cancelacion).ConfigureAwait(false);
                await GarantizarCuadrillaDeLaPlantaAsync(cuadrilla.Id, plantaHistorica, cancelacion)
                    .ConfigureAwait(false);

                var tramo = ReasignarCuadrillaEnVinculo(
                    trabajador, cuadrilla.Id, desde, laboral.FechaCese, usuario);
                ValidadorVigencias.GarantizarLineaDeTiempoSana(trabajador.AsignacionesTurno, "turno");

                if (tramo is not null)
                {
                    cambiados++;
                    afectado = afectado is null
                        ? tramo
                        : new TramoAfectado(
                            tramo.Desde < afectado.Desde ? tramo.Desde : afectado.Desde,
                            tramo.Hasta is null || afectado.Hasta is null
                                ? null
                                : tramo.Hasta > afectado.Hasta ? tramo.Hasta : afectado.Hasta);
                    repositorio.Actualizar(trabajador);
                }
            }
            catch (ReglaNegocioException excepcion)
            {
                throw new ReglaNegocioException(
                    $"No se sumó a nadie a la cuadrilla {cuadrilla.Nombre}: {nombre}: {excepcion.Message}");
            }
        }

        await GarantizarPeriodosAbiertosAsync(afectado, cancelacion).ConfigureAwait(false);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        return cambiados;
    }

    /// <summary>
    /// La cuadrilla existe, está activa y es de la planta del trabajador.
    /// </summary>
    /// <remarks>
    /// Una cuadrilla de otra planta rotaría con turnos que quizá no están habilitados en la
    /// suya, y sus integrantes saldrían en la pantalla de otra planta.
    /// </remarks>
    private async Task GarantizarCuadrillaDeLaPlantaAsync(
        long cuadrillaId,
        long plantaId,
        CancellationToken cancelacion)
    {
        var cuadrilla = await catalogos.CuadrillaPorIdAsync(cuadrillaId, cancelacion).ConfigureAwait(false);

        if (cuadrilla is null || !cuadrilla.Activo)
        {
            throw new ReglaNegocioException("La cuadrilla indicada no existe o está dada de baja.");
        }

        if (cuadrilla.PlantaId != plantaId)
        {
            throw new ReglaNegocioException(
                $"La cuadrilla {cuadrilla.Nombre} es de {cuadrilla.Planta}: solo pueden integrarla " +
                "trabajadores de esa unidad.");
        }
    }

    public async Task<long> CrearAsync(
        GuardarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // Un administrador solo da de alta trabajadores en las plantas que gestiona.
        await GarantizarPlantaAsignableAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);

        var equipoAsignado = await ValidarAsync(solicitud, null, solicitud.FechaCese, usuario, cancelacion)
            .ConfigureAwait(false);

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

        var turnoAfecta = AplicarAsignaciones(trabajador, solicitud, equipoAsignado, usuario);
        await GarantizarPeriodosAbiertosAsync(turnoAfecta, cancelacion).ConfigureAwait(false);

        // El área también tiene vigencia por fecha: la primera rige desde el ingreso.
        SincronizarAreaVigente(trabajador, solicitud.AreaId, solicitud.FechaIngreso, usuario);

        // El vínculo laboral se abre junto con la ficha: si se creara recién al primer
        // cese, los trabajadores que nunca cesan no tendrían historial y la pantalla
        // mostraría un padrón a medias.
        trabajador.AbrirVinculo(usuario);

        // Un alta puede nacer ya cesada —la carga masiva de personal histórico lo hace—, y
        // en ese caso las asignaciones que se acaban de crear tienen que cerrarse en la
        // misma fecha. Si no, el ID de marcador queda ocupado por alguien que ya no está.
        trabajador.CerrarAsignacionesSiNaceCesado(usuario);
        trabajador.ValidarPeriodosLaborales();

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
        var equipoAsignado = await ValidarAsync(solicitud, trabajador, trabajador.FechaCese, usuario, cancelacion)
            .ConfigureAwait(false);

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

        // La planta plana es solo la proyección actual. Si hay un vínculo abierto, el
        // cambio se registra primero en su línea temporal y rige desde hoy (o desde el
        // inicio si se trata de un alta futura). Una ficha legacy sin vínculo se
        // materializa más abajo con la planta solicitada; una persona cesada conserva su
        // historia cerrada y solo actualiza la proyección previa a un eventual reingreso.
        var vinculoAbierto = trabajador.PeriodosLaborales.SingleOrDefault(p => p.EstaAbierto);
        if (trabajador.PlantaId != solicitud.PlantaId
            && trabajador.FechaCese is null
            && vinculoAbierto is not null)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Now);
            var desde = hoy < vinculoAbierto.FechaIngreso ? vinculoAbierto.FechaIngreso : hoy;
            trabajador.CambiarPlanta(solicitud.PlantaId, desde, usuario);
        }
        else
        {
            trabajador.PlantaId = solicitud.PlantaId;
        }

        // Área y Sección ya no se capturan en el formulario de Personal (Centro y
        // Centro de costo cumplen ese rol). Si la solicitud no las trae, se
        // conserva el valor existente en vez de borrarlo: ServicioReportes las usa
        // todavía como respaldo del tareo final para los trabajadores que aún no
        // tienen Centro de costo asignado.
        if (solicitud.AreaId is { } areaId)
        {
            trabajador.AreaId = areaId;

            // Y se anota en la línea de tiempo, para que un reporte de un mes anterior siga
            // mostrando el área que la persona tenía entonces. Rige desde hoy: no hay campo
            // «área rige desde» porque el formulario de Personal ya no captura el área —entra
            // por la carga masiva del padrón—, así que no hay dónde fecharlo.
            SincronizarAreaVigente(trabajador, areaId, DateOnly.FromDateTime(DateTime.Now), usuario);
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
        var turnoAfecta = AplicarAsignaciones(trabajador, solicitud, equipoAsignado, usuario);
        await GarantizarPeriodosAbiertosAsync(turnoAfecta, cancelacion).ConfigureAwait(false);
        trabajador.ValidarPeriodosLaborales();

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
        trabajador.ValidarPeriodosLaborales();

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Abre un nuevo vínculo laboral para una persona cesada, sin alterar su historia.
    /// </summary>
    /// <remarks>
    /// La regla temporal vive en <see cref="Trabajador.Reingresar"/>. Este caso de uso
    /// solo aplica alcance, valida los catálogos elegidos y persiste el agregado completo
    /// en un único <c>SaveChanges</c>.
    /// </remarks>
    public async Task ReingresarAsync(
        ReingresarTrabajadorRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var trabajador = await repositorio
            .ObtenerPorIdAsync(solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false)
            ?? throw new RecursoNoEncontradoException("el trabajador", solicitud.TrabajadorId);

        // La proyección plana conserva la última planta del vínculo cerrado. Se reutiliza
        // aquí exclusivamente para el control de acceso por id que ya protege el resto de
        // las escrituras de Personal; la disponibilidad del reingreso se decide abajo con
        // PeriodoLaboral, dentro del dominio.
        await GarantizarAccesoTrabajadorAsync(
                trabajador.PlantaId, solicitud.TrabajadorId, cancelacion)
            .ConfigureAwait(false);
        await GarantizarPlantaAsignableAsync(solicitud.PlantaId, cancelacion).ConfigureAwait(false);

        var empresas = await catalogos.EmpresasAsync(cancelacion).ConfigureAwait(false);
        if (!empresas.Any(e => e.Id == solicitud.EmpresaId))
        {
            throw new ReglaNegocioException(
                "La empresa indicada no existe o está dada de baja en el catálogo.");
        }

        var empresaDePlanta = await catalogos
            .EmpresaDePlantaAsync(solicitud.PlantaId, cancelacion)
            .ConfigureAwait(false);
        if (empresaDePlanta is null)
        {
            throw new ReglaNegocioException(
                "La unidad indicada no existe o está dada de baja en el catálogo.");
        }

        if (empresaDePlanta != solicitud.EmpresaId)
        {
            throw new ReglaNegocioException(
                "La unidad indicada no pertenece a la empresa seleccionada.");
        }

        trabajador.Reingresar(
            solicitud.FechaIngreso,
            solicitud.EmpresaId,
            solicitud.PlantaId,
            usuario);
        trabajador.ValidarPeriodosLaborales();

        repositorio.Actualizar(trabajador);
        await repositorio.GuardarReingresoAsync(cancelacion).ConfigureAwait(false);
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
                "No puede registrar ni asignar un trabajador a una unidad que no gestiona.");
        }
    }

    private async Task<long?> ValidarAsync(
        GuardarTrabajadorRequest solicitud,
        Trabajador? existente,
        DateOnly? fechaCeseVigente,
        string usuario,
        CancellationToken cancelacion)
    {
        var idExistente = existente?.Id;

        if (fechaCeseVigente is { } cese && cese < solicitud.FechaIngreso)
        {
            throw new ReglaNegocioException(
                "La fecha de cese no puede ser anterior a la fecha de ingreso.");
        }

        if ((solicitud.TurnoId is not null || solicitud.CuadrillaId is not null)
            && solicitud.TurnoVigenteDesde is { } turnoDesde
            && turnoDesde < solicitud.FechaIngreso)
        {
            throw new ReglaNegocioException(
                $"El turno no puede regir desde el {turnoDesde:dd/MM/yyyy}: es anterior a la fecha " +
                $"de ingreso ({solicitud.FechaIngreso:dd/MM/yyyy}).");
        }

        if (solicitud.CuadrillaId is { } cuadrillaId)
        {
            await GarantizarCuadrillaDeLaPlantaAsync(cuadrillaId, solicitud.PlantaId, cancelacion)
                .ConfigureAwait(false);
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
            return null;
        }

        var idMarcador = solicitud.IdMarcador.Trim();

        if (solicitud.MarcadorVigenteDesde is { } pedido && pedido < solicitud.FechaIngreso)
        {
            throw new ReglaNegocioException(
                $"El número del marcador no puede regir desde el {pedido:dd/MM/yyyy}: es anterior " +
                $"a la fecha de ingreso ({solicitud.FechaIngreso:dd/MM/yyyy}).");
        }

        var (equipoId, faltaEnCatalogo) = await ResolverAparatoDelNumeroAsync(
                idMarcador, solicitud.PlantaId, solicitud.EquipoId, solicitud.RegistrarMarcadorSiFalta,
                cancelacion)
            .ConfigureAwait(false);

        // El ID del marcador se reasigna con el tiempo, pero dos personas no pueden
        // tenerlo a la vez: las marcaciones de ese periodo serían inatribuibles.
        //
        // Se mira desde la fecha en que empezaría a regir para esta persona, no desde su
        // ingreso: un número que otro trabajador dejó al cesar se puede reutilizar.
        //
        // ⚠️ La ocupación se mira DENTRO DEL EQUIPO. El mismo código en dos aparatos son
        // dos identidades distintas, y tratarlas como una sola impediría registrar a la
        // segunda mitad de la planta — que es exactamente el problema que este modelo vino
        // a resolver.
        var marcadorOcupado = await repositorio
            .IdMarcadorEnUsoAsync(
                equipoId,
                idMarcador,
                InicioDelNumero(solicitud, existente, equipoId, idMarcador),
                fechaCeseVigente,
                idExistente,
                cancelacion)
            .ConfigureAwait(false);

        if (marcadorOcupado)
        {
            throw new ReglaNegocioException(
                $"El número '{idMarcador}' de ese aparato ya está asignado a otro trabajador en " +
                "fechas que se cruzan con estas. Revise la ficha de quien lo tiene: si cesó, " +
                "registre su baja; si es un error, corrija su número primero.");
        }

        // Se registra al final, cuando todo lo demás ya pasó: un número dado de alta por
        // una ficha que después se rechaza quedaría en el catálogo sin que nadie lo pidiera.
        if (faltaEnCatalogo)
        {
            await marcadores
                .GuardarAsync(
                    new GuardarMarcadorRequest
                    {
                        Codigo = idMarcador,
                        EquipoId = equipoId,
                        Descripcion = "Registrado desde la ficha del trabajador",
                        Activo = true
                    },
                    usuario,
                    cancelacion)
                .ConfigureAwait(false);
        }

        return equipoId;
    }

    /// <summary>
    /// Fecha desde la que el número regiría para esta persona si se guarda la ficha.
    /// </summary>
    /// <remarks>
    /// Tiene que coincidir con lo que hace <see cref="ReasignarMarcador"/>: la fecha pedida
    /// si la hay; si no, el ingreso para la primera asignación, el inicio de la vigente si
    /// el número no cambia, y mañana para un cambio.
    /// </remarks>
    private static DateOnly InicioDelNumero(
        GuardarTrabajadorRequest solicitud,
        Trabajador? existente,
        long equipoId,
        string idMarcador)
    {
        if (solicitud.MarcadorVigenteDesde is { } pedido)
        {
            return pedido;
        }

        var vigente = existente?.AsignacionesMarcador.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            return solicitud.FechaIngreso;
        }

        return vigente.EquipoId == equipoId && string.Equals(vigente.IdMarcador, idMarcador, StringComparison.Ordinal)
            ? vigente.FechaInicio
            : DateOnly.FromDateTime(DateTime.Now).AddDays(1);
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
    /// Aparato al que pertenece el número, y si ese número todavía no está en su catálogo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Si la ficha o la fila declaran el aparato, <b>manda</b>, aunque el número exista en
    /// un solo aparato y sea otro: una fila que decía «14 en M-Z», con el catálogo teniendo
    /// solo el 14 de A-L, quedaba asignada al de A-L sin aviso, y sus marcaciones iban a
    /// otra persona.
    /// </para>
    /// <para>
    /// Sin aparato declarado solo se infiere cuando no hay nada entre qué elegir: el número
    /// existe en un único aparato de la planta, o —si hay que registrarlo— la planta tiene
    /// un único aparato. Con dos, elegir por el usuario sería adivinar a cuál de las dos
    /// personas que llevan ese número se le está asignando.
    /// </para>
    /// <para>
    /// Un número que no está en el catálogo se rechaza, salvo que la solicitud pida
    /// registrarlo: la ficha lo pide, porque quien la llena copia el número de la lista de
    /// usuarios del aparato.
    /// </para>
    /// </remarks>
    private async Task<(long EquipoId, bool FaltaEnCatalogo)> ResolverAparatoDelNumeroAsync(
        string idMarcador,
        long plantaId,
        long? equipoPedido,
        bool registrarSiFalta,
        CancellationToken cancelacion)
    {
        var candidatos = await catalogos
            .MarcadoresPorCodigoAsync(idMarcador, plantaId, cancelacion)
            .ConfigureAwait(false);

        if (equipoPedido is { } pedido)
        {
            var enElAparato = candidatos.FirstOrDefault(c => c.EquipoId == pedido);

            if (enElAparato is not null)
            {
                return (GarantizarActivo(enElAparato), false);
            }

            if (!registrarSiFalta)
            {
                throw new ReglaNegocioException(
                    candidatos.Count == 0
                        ? $"El identificador de marcador '{idMarcador}' no está registrado en el equipo " +
                          "indicado. Regístrelo en Catálogos → Marcadores."
                        : $"El identificador de marcador '{idMarcador}' no está registrado en el equipo " +
                          "indicado; en esta unidad existe en " +
                          $"{string.Join(", ", candidatos.Select(c => c.Equipo))}. Regístrelo en el " +
                          "equipo correcto o corrija el equipo de la fila.");
            }

            var aparatos = await AparatosDeLaPlantaAsync(plantaId, cancelacion).ConfigureAwait(false);

            return aparatos.Any(a => a.Id == pedido)
                ? (pedido, true)
                : throw new ReglaNegocioException(
                    "El aparato marcador indicado no pertenece a la unidad del trabajador o está " +
                    "dado de baja.");
        }

        if (candidatos.Count == 1)
        {
            return (GarantizarActivo(candidatos[0]), false);
        }

        if (candidatos.Count > 1)
        {
            throw new ReglaNegocioException(
                $"En esta unidad hay {candidatos.Count} marcadores con el código '{idMarcador}' " +
                $"({string.Join(", ", candidatos.Select(c => c.Equipo))}). Indique de cuál de " +
                "ellos es el trabajador: el código por sí solo identifica a personas distintas " +
                "en cada equipo.");
        }

        if (!registrarSiFalta)
        {
            throw new ReglaNegocioException(
                $"El identificador de marcador '{idMarcador}' no existe en el catálogo de esta " +
                "unidad. Regístrelo primero en Catálogos → Marcadores, indicando a qué equipo " +
                "pertenece.");
        }

        var deLaPlanta = await AparatosDeLaPlantaAsync(plantaId, cancelacion).ConfigureAwait(false);

        return deLaPlanta.Count switch
        {
            1 => (deLaPlanta[0].Id, true),
            0 => throw new ReglaNegocioException(
                "La unidad no tiene ningún aparato marcador registrado. Regístrelo en Equipos."),
            _ => throw new ReglaNegocioException(
                $"Indique en qué aparato está registrado el número '{idMarcador}': la unidad tiene " +
                $"{deLaPlanta.Count} ({string.Join(", ", deLaPlanta.Select(a => a.Nombre))}).")
        };

        long GarantizarActivo(MarcadorCatalogoDto marcador) =>
            marcador.Activo
                ? marcador.EquipoId
                : throw new ReglaNegocioException(
                    $"El marcador '{idMarcador}' del equipo {marcador.Equipo} está dado de baja " +
                    "y no puede asignarse.");
    }

    private async Task<List<EquipoOpcionDto>> AparatosDeLaPlantaAsync(long plantaId, CancellationToken cancelacion) =>
        [.. (await catalogos.EquiposActivosAsync(cancelacion).ConfigureAwait(false)).Where(e => e.PlantaId == plantaId)];

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
        var abiertos = trabajador.PeriodosLaborales.Where(p => p.EstaAbierto).ToList();

        if (abiertos.Count > 1)
        {
            throw new ReglaNegocioException(
                "El trabajador tiene más de un periodo laboral abierto.");
        }

        var abierto = abiertos.SingleOrDefault();

        if (abierto is null)
        {
            // Guardar datos administrativos de una persona cesada no es un reingreso.
            // Solo una ficha vigente sin historia (legacy) puede materializar el vínculo
            // que le falta; el reingreso requiere la operación explícita del dominio.
            if (trabajador.FechaCese is not null)
            {
                return;
            }

            trabajador.AbrirVinculo(usuario);
            return;
        }

        if (abierto.FechaIngreso == trabajador.FechaIngreso && abierto.EmpresaId == trabajador.EmpresaId)
        {
            return;
        }

        abierto.CorregirFechaIngreso(trabajador.FechaIngreso, usuario);
        abierto.EmpresaId = trabajador.EmpresaId;
        abierto.ModificadoPor = usuario;
    }

    /// <summary>
    /// Crea las asignaciones de marcador, turno y sueldo básico cuando cambian.
    /// </summary>
    /// <remarks>
    /// Sin fecha, turno y sueldo no se editan: se cierra la vigente y se abre una nueva,
    /// porque reescribirla reinterpretaría días ya ocurridos con datos que no regían
    /// entonces. El número de marcador admite corrección en sitio (ver
    /// <see cref="ReasignarMarcador"/>), y el turno con fecha registra la rotación el día en
    /// que ocurrió (ver <see cref="ReasignarTurno"/>).
    /// </remarks>
    /// <returns>
    /// Los días a los que el cambio de turno con fecha les cambia el turno, o nulo si no
    /// hubo cambio con fecha. Quien llama comprueba que ninguno caiga en un periodo cerrado.
    /// </returns>
    /// <summary>
    /// Deja la línea de tiempo del área consistente con el área que la ficha tiene ahora.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Misma mecánica que las otras vigencias: no se edita, se cierra la vigente y se abre
    /// otra. Sin esto, mover a alguien de área reescribía el pasado — un resumen de julio
    /// emitido en setiembre lo mostraba en el área que tiene ahora, y la columna <c>AREA</c>
    /// del tareo final cambiaba sola en un mes ya entregado a planilla.
    /// </para>
    /// <para>
    /// Una fecha igual o anterior al inicio de la vigente la <b>corrige en sitio</b>: el área
    /// estaba mal desde el principio y abrir otra dejaría una vigencia de cero días. Es el
    /// caso normal cuando se corrige una ficha el mismo día que se creó.
    /// </para>
    /// <para>
    /// ⚠️ No se cierra al cesar, a diferencia del marcador, el turno y el sueldo: aquellos se
    /// cierran para liberar el número y dejar de calcular, y el área no gobierna nada del
    /// cálculo. Dejarla abierta hace que el reporte del mes del cese siga resolviéndola.
    /// </para>
    /// </remarks>
    private static void SincronizarAreaVigente(
        Trabajador trabajador,
        long? areaId,
        DateOnly desde,
        string usuario)
    {
        if (areaId is not { } area)
        {
            return;
        }

        var vigente = trabajador.AsignacionesArea
            .Where(a => a.FechaFin is null)
            .OrderByDescending(a => a.FechaInicio)
            .FirstOrDefault();

        if (vigente is not null && vigente.AreaId == area)
        {
            return;
        }

        if (vigente is not null)
        {
            if (desde <= vigente.FechaInicio)
            {
                vigente.AreaId = area;
                vigente.ModificadoPor = usuario;
                return;
            }

            vigente.FechaFin = desde.AddDays(-1);
            vigente.ModificadoPor = usuario;
        }

        trabajador.AsignacionesArea.Add(new AsignacionArea
        {
            AreaId = area,
            FechaInicio = desde,
            CreadoPor = usuario
        });
    }

    private static TramoAfectado? AplicarAsignaciones(
        Trabajador trabajador,
        GuardarTrabajadorRequest solicitud,
        long? equipoId,
        string usuario)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        // equipoId lo resuelve ValidarAsync contra el catálogo, no llega del formulario sin
        // comprobar: es el aparato del marcador que se está asignando, y sin él la ingesta
        // no podría atribuir ninguna marcación de esta persona.
        if (!string.IsNullOrWhiteSpace(solicitud.IdMarcador) && equipoId is { } equipo)
        {
            ReasignarMarcador(
                trabajador, equipo, solicitud.IdMarcador.Trim(), solicitud.MarcadorVigenteDesde, hoy, usuario);
        }

        TramoAfectado? turnoAfecta = null;

        // Turno fijo o cuadrilla: van en la misma línea de tiempo, así que pasar de uno a
        // otro es un cambio de asignación como cualquier otro. Si llegan los dos, manda la
        // cuadrilla: el formulario envía siempre el turno fijo que tenía la persona, y exigir
        // vaciarlo para elegir una cuadrilla sería un paso manual que se olvida.
        var cuadrillaId = solicitud.CuadrillaId;
        var turnoId = cuadrillaId is null ? solicitud.TurnoId : null;

        if (turnoId is not null || cuadrillaId is not null)
        {
            if (solicitud.TurnoVigenteDesde is { } turnoDesde)
            {
                turnoAfecta = ReasignarTurno(trabajador, turnoId, cuadrillaId, turnoDesde, usuario);
            }
            else
            {
                Reasignar(
                    trabajador,
                    trabajador.AsignacionesTurno,
                    a => a.Asigna(turnoId, cuadrillaId),
                    a =>
                    {
                        a.TurnoId = turnoId;
                        a.CuadrillaId = cuadrillaId;
                    },
                    desde => new AsignacionTurno { TurnoId = turnoId, CuadrillaId = cuadrillaId, FechaInicio = desde },
                    hoy,
                    usuario);
            }
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

        return turnoAfecta;
    }

    /// <summary>
    /// Días a los que un cambio les cambia el valor. <see cref="Hasta"/> nulo: de ahí en
    /// adelante.
    /// </summary>
    private sealed record TramoAfectado(DateOnly Desde, DateOnly? Hasta);

    /// <summary>
    /// Asigna una cuadrilla dentro del vínculo que contiene la fecha efectiva, sin tocar una
    /// asignación de un reingreso posterior. El tramo nuevo conserva el final que ya tenía la
    /// asignación reemplazada o, si faltaba, termina con el vínculo histórico.
    /// </summary>
    private static TramoAfectado? ReasignarCuadrillaEnVinculo(
        Trabajador trabajador,
        long cuadrillaId,
        DateOnly desde,
        DateOnly? finVinculo,
        string usuario)
    {
        var asignaciones = trabajador.AsignacionesTurno;
        var aplicable = asignaciones.SingleOrDefault(a =>
            a.FechaInicio <= desde && (a.FechaFin is null || a.FechaFin >= desde));

        if (aplicable is not null)
        {
            if (aplicable.Asigna(null, cuadrillaId))
            {
                return null;
            }

            var finOriginal = aplicable.FechaFin;
            if (aplicable.FechaInicio == desde)
            {
                aplicable.TurnoId = null;
                aplicable.CuadrillaId = cuadrillaId;
                aplicable.ModificadoPor = usuario;
                return new TramoAfectado(desde, finOriginal);
            }

            aplicable.FechaFin = desde.AddDays(-1);
            aplicable.ModificadoPor = usuario;
            asignaciones.Add(new AsignacionTurno
            {
                CuadrillaId = cuadrillaId,
                FechaInicio = desde,
                FechaFin = finOriginal,
                CreadoPor = usuario
            });
            return new TramoAfectado(desde, finOriginal);
        }

        var siguiente = asignaciones
            .Where(a => a.FechaInicio > desde)
            .OrderBy(a => a.FechaInicio)
            .FirstOrDefault();
        var hasta = finVinculo;
        if (siguiente is not null)
        {
            var antesDeSiguiente = siguiente.FechaInicio.AddDays(-1);
            hasta = hasta is null || antesDeSiguiente < hasta ? antesDeSiguiente : hasta;
        }

        asignaciones.Add(new AsignacionTurno
        {
            CuadrillaId = cuadrillaId,
            FechaInicio = desde,
            FechaFin = hasta,
            CreadoPor = usuario
        });
        return new TramoAfectado(desde, hasta);
    }

    /// <summary>
    /// Asigna el turno desde la fecha indicada: la rotación se registra el día en que
    /// ocurrió, aunque se anote después.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La fecha dice <b>desde cuándo rige el turno elegido</b>, y el resultado es siempre el
    /// mismo: desde esa fecha, ese turno. Según dónde caiga respecto del inicio de la
    /// asignación vigente:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Posterior, con otro turno</b>: es la rotación. La vigente se cierra el día
    /// previo y el nuevo turno rige desde la fecha.</item>
    /// <item><b>Igual, con otro turno</b>: el turno vigente estaba mal desde que empezó; se
    /// corrige en sitio y la auditoría guarda el anterior.</item>
    /// <item><b>Anterior</b>: la rotación ocurrió antes de lo anotado —la cuadrilla rotó un
    /// día antes—, o el turno vigente estaba mal y el correcto empezó antes. La vigente
    /// empieza en la fecha, con el turno elegido, y la anterior se acorta hasta el día
    /// previo.</item>
    /// <item><b>Posterior, con el mismo turno</b>: la rotación ocurrió después de lo
    /// anotado. La vigente empieza en la fecha y la anterior se alarga hasta el día previo,
    /// así los días entre medio siguen con el turno que tenían.</item>
    /// </list>
    /// <para>
    /// Solo se mueve la frontera con la asignación <b>inmediatamente anterior</b>. Si la
    /// fecha se la comiera entera, o si al atrasar el inicio quedaran días sin ningún turno,
    /// se rechaza en vez de adivinar: con varias rotaciones sin anotar, se registran en
    /// orden, una por una.
    /// </para>
    /// </remarks>
    /// <returns>Los días cuyo turno cambia, o nulo si no cambia ninguno.</returns>
    private static TramoAfectado? ReasignarTurno(
        Trabajador trabajador,
        long? turnoId,
        long? cuadrillaId,
        DateOnly desde,
        string usuario)
    {
        var asignaciones = trabajador.AsignacionesTurno;
        var vigente = asignaciones.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            // Mismo criterio que Reasignar: un cesado no recibe asignaciones nuevas.
            if (trabajador.FechaCese is not null)
            {
                return null;
            }

            asignaciones.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                CuadrillaId = cuadrillaId,
                FechaInicio = desde,
                CreadoPor = usuario
            });
            return new TramoAfectado(desde, null);
        }

        var inicioVigente = vigente.FechaInicio;
        var mismoTurno = vigente.Asigna(turnoId, cuadrillaId);

        // La asignación cerrada más reciente es la que termina justo antes de la vigente.
        var anterior = asignaciones
            .Where(a => a.FechaFin is not null)
            .OrderByDescending(a => a.FechaInicio)
            .FirstOrDefault();

        if (desde == inicioVigente)
        {
            if (mismoTurno)
            {
                return null;
            }

            vigente.TurnoId = turnoId;
            vigente.CuadrillaId = cuadrillaId;
            vigente.ModificadoPor = usuario;
            return new TramoAfectado(desde, null);
        }

        if (desde > inicioVigente && !mismoTurno)
        {
            vigente.FechaFin = desde.AddDays(-1);
            vigente.ModificadoPor = usuario;

            asignaciones.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                CuadrillaId = cuadrillaId,
                FechaInicio = desde,
                CreadoPor = usuario
            });
            return new TramoAfectado(desde, null);
        }

        if (desde > inicioVigente)
        {
            // Mismo turno, más tarde: los días del inicio anotado al día previo a la fecha
            // vuelven al turno anterior, que tiene que venir pegado a la vigente.
            if (anterior?.FechaFin is not { } finAnterior || finAnterior.AddDays(1) != inicioVigente)
            {
                throw new ReglaNegocioException(
                    $"Ese turno ya rige desde el {inicioVigente:dd/MM/yyyy}. Para que empiece recién el " +
                    $"{desde:dd/MM/yyyy}, los días del {inicioVigente:dd/MM/yyyy} al " +
                    $"{desde.AddDays(-1):dd/MM/yyyy} quedarían sin turno. Si en esos días tuvo otro, " +
                    $"regístrelo primero con «Rige desde» {inicioVigente:dd/MM/yyyy}.");
            }

            anterior.FechaFin = desde.AddDays(-1);
            anterior.ModificadoPor = usuario;

            vigente.FechaInicio = desde;
            vigente.ModificadoPor = usuario;
            return new TramoAfectado(inicioVigente, desde.AddDays(-1));
        }

        // La fecha es anterior al inicio de la vigente: la frontera con la anterior retrocede.
        if (anterior?.FechaFin is { } fin && fin >= desde)
        {
            if (anterior.FechaInicio >= desde)
            {
                throw new ReglaNegocioException(
                    $"Con esa fecha, la asignación de turno anterior —del " +
                    $"{anterior.FechaInicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}— desaparecería entera. " +
                    "Desde la ficha solo se corre la frontera con la asignación anterior: si hubo " +
                    "varias rotaciones sin anotar, regístrelas en orden, una por una.");
            }

            anterior.FechaFin = desde.AddDays(-1);
            anterior.ModificadoPor = usuario;
        }

        vigente.FechaInicio = desde;
        vigente.TurnoId = turnoId;
        vigente.CuadrillaId = cuadrillaId;
        vigente.ModificadoPor = usuario;

        // Con el mismo turno solo cambian los días que se le quitaron a la anterior; con
        // otro turno, además todos los de la vigente.
        return new TramoAfectado(desde, mismoTurno ? inicioVigente.AddDays(-1) : null);
    }

    /// <summary>
    /// Rechaza un cambio de turno que alcance días de un periodo cerrado.
    /// </summary>
    /// <remarks>
    /// Un periodo cerrado no admite escrituras, y cambiar el turno de sus días es escribir
    /// en él aunque no se toque ningún tareo: el turno es con lo que se calcularía, y la
    /// ficha pasaría a decir algo distinto de lo que se pagó. Solo se comprueba el cambio
    /// con fecha: sin ella el turno cambia desde mañana, igual que antes.
    /// </remarks>
    private async Task GarantizarPeriodosAbiertosAsync(TramoAfectado? tramo, CancellationToken cancelacion)
    {
        if (tramo is null)
        {
            return;
        }

        var cerrados = (await periodos.ListarAsync(incluirInactivos: false, cancelacion).ConfigureAwait(false))
            .Where(p => !p.EstaAbierto
                        && p.FechaFin >= tramo.Desde
                        && (tramo.Hasta is null || p.FechaInicio <= tramo.Hasta.Value))
            .OrderBy(p => p.FechaInicio)
            .ToList();

        if (cerrados.Count == 0)
        {
            return;
        }

        var cuales = cerrados.Count == 1
            ? $"del periodo {cerrados[0].Codigo}, que está cerrado y no admite cambios"
            : $"de los periodos {string.Join(", ", cerrados.Select(p => p.Codigo))}, que están " +
              "cerrados y no admiten cambios";

        throw new ReglaNegocioException(
            $"El cambio de turno alcanzaría días {cuales}. Elija una fecha " +
            $"posterior al {cerrados[^1].FechaFin:dd/MM/yyyy}, o reabra el periodo indicando el motivo.");
    }

    /// <summary>
    /// Asigna el número del aparato, con la fecha desde la que rige.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es distinto de turno y sueldo porque el número decide <b>de quién es cada marcación</b>,
    /// y la marcación se atribuye al calcular con la asignación vigente en su fecha. Por eso
    /// la fecha la puede indicar quien llena la ficha, y hay tres casos:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Primera asignación</b>: rige desde la fecha pedida o, sin fecha, desde el
    /// ingreso. Así las marcaciones ya cargadas de alguien que se registró tarde cuentan al
    /// recalcular.</item>
    /// <item><b>Corrección</b> (fecha igual o anterior al inicio del número vigente): el
    /// vigente estaba mal desde que empezó, así que se corrige en sitio —la auditoría guarda
    /// el valor anterior— y al recalcular sus marcaciones pasan a la persona correcta.</item>
    /// <item><b>Cambio</b> (fecha posterior, o sin fecha): el número vigente se cierra el día
    /// anterior y el nuevo rige desde esa fecha; lo anterior sigue siendo del número viejo.
    /// Sin fecha, el corte es hoy, igual que turno y sueldo.</item>
    /// </list>
    /// <para>
    /// Si el número no cambia, una fecha anterior solo adelanta su inicio: ya lo tenía
    /// desde antes. <see cref="ValidadorVigencias"/> rechaza después cualquier resultado que
    /// se cruce con una asignación anterior.
    /// </para>
    /// </remarks>
    private static void ReasignarMarcador(
        Trabajador trabajador,
        long equipoId,
        string idMarcador,
        DateOnly? desde,
        DateOnly hoy,
        string usuario)
    {
        var asignaciones = trabajador.AsignacionesMarcador;
        var vigente = asignaciones.FirstOrDefault(a => a.FechaFin is null);

        if (vigente is null)
        {
            // Mismo criterio que Reasignar: un cesado no recibe asignaciones nuevas.
            if (trabajador.FechaCese is not null)
            {
                return;
            }

            asignaciones.Add(new AsignacionMarcador
            {
                EquipoId = equipoId,
                IdMarcador = idMarcador,
                FechaInicio = desde ?? trabajador.FechaIngreso,
                CreadoPor = usuario
            });

            return;
        }

        if (vigente.EquipoId == equipoId && string.Equals(vigente.IdMarcador, idMarcador, StringComparison.Ordinal))
        {
            if (desde is { } antes && antes < vigente.FechaInicio)
            {
                vigente.FechaInicio = antes;
                vigente.ModificadoPor = usuario;
            }

            return;
        }

        // Sin fecha, un número que todavía no ha regido ni un día se corrige en sitio; si
        // ya rigió, el cambio corta hoy.
        var inicioDelCambio = desde ?? (vigente.FechaInicio > hoy ? vigente.FechaInicio : hoy.AddDays(1));

        if (inicioDelCambio <= vigente.FechaInicio)
        {
            vigente.EquipoId = equipoId;
            vigente.IdMarcador = idMarcador;
            vigente.FechaInicio = inicioDelCambio;
            vigente.ModificadoPor = usuario;
            return;
        }

        vigente.FechaFin = inicioDelCambio.AddDays(-1);
        vigente.ModificadoPor = usuario;

        asignaciones.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = idMarcador,
            FechaInicio = inicioDelCambio,
            CreadoPor = usuario
        });
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
