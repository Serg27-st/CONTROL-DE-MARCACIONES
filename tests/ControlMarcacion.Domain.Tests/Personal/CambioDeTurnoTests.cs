using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>
/// Cambio de turno desde la ficha del trabajador: es la operación de la rotación.
/// </summary>
/// <remarks>
/// <para>
/// Sin «Rige desde» el corte es «hoy»: la asignación vigente se cierra hoy y la nueva
/// empieza mañana. Por eso esos tests comparan contra <see cref="DateOnly.FromDateTime"/>
/// de la fecha del sistema en vez de contra una fecha literal: es el propio servicio el que
/// la lee.
/// </para>
/// <para>
/// Con «Rige desde» la rotación se registra el día en que ocurrió, y esos tests sí usan
/// fechas literales: la semana de julio de 2026 en que la cuadrilla rotó tras el descanso
/// del domingo 19.
/// </para>
/// </remarks>
public sealed class CambioDeTurnoTests
{
    private const long Diurno = 1;
    private const long Nocturno = 2;
    private const long Sabatino = 3;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private readonly ITrabajadorRepository repositorio = Substitute.For<ITrabajadorRepository>();
    private readonly IConsultaTrabajadores consulta = Substitute.For<IConsultaTrabajadores>();
    private readonly IConsultaCatalogos catalogos = Substitute.For<IConsultaCatalogos>();
    private readonly IProveedorAlcancePlantas alcance = Substitute.For<IProveedorAlcancePlantas>();
    private readonly IServicioPeriodos periodos = Substitute.For<IServicioPeriodos>();

    public CambioDeTurnoTests()
    {
        // Sin periodos cerrados salvo que el test los declare.
        periodos
            .ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PeriodoListaDto>());

        // Sin restricción de plantas: estos tests son sobre la rotación de turno, no sobre el
        // aislamiento por planta. null = ve todas (como un COORDINADOR).
        alcance
            .PlantasVisiblesAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<long>?)null);

        catalogos
            .TipoDocumentoPorIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new TipoDocumentoOpcionDto(1, "DNI", "DNI", 8, null, true));

        repositorio
            .ExisteDocumentoAsync(
                Arg.Any<long>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        consulta
            .ContextosLaboralesEnFechaAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(llamada => (IReadOnlyDictionary<long, ContextoLaboralFechaDto>)llamada
                .ArgAt<IReadOnlyCollection<long>>(0)
                .ToDictionary(
                    id => id,
                    id => new ContextoLaboralFechaDto(
                        id, 2, 3, new DateOnly(2020, 1, 1), null)));
    }

    [Fact]
    public async Task Cambiar_de_turno_cierra_el_vigente_hoy_y_abre_el_nuevo_manana()
    {
        var trabajador = Obrero(Diurno);
        var servicio = Servicio(trabajador);

        await servicio.ActualizarAsync(Solicitud(Nocturno), "coordinador", CancellationToken.None);

        var asignaciones = trabajador.AsignacionesTurno.OrderBy(a => a.FechaInicio).ToList();

        asignaciones.Count.ShouldBe(2);
        asignaciones[0].TurnoId.ShouldBe(Diurno);
        asignaciones[0].FechaFin.ShouldBe(Hoy);
        asignaciones[1].TurnoId.ShouldBe(Nocturno);
        asignaciones[1].FechaInicio.ShouldBe(Hoy.AddDays(1));
        asignaciones[1].FechaFin.ShouldBeNull();
    }

    [Fact]
    public async Task Guardar_sin_cambiar_el_turno_no_abre_una_asignacion_nueva()
    {
        var trabajador = Obrero(Diurno);
        var servicio = Servicio(trabajador);

        await servicio.ActualizarAsync(Solicitud(Diurno), "coordinador", CancellationToken.None);

        trabajador.AsignacionesTurno.Count.ShouldBe(1);
        trabajador.AsignacionesTurno.Single().FechaFin.ShouldBeNull();
    }

    /// <summary>
    /// Corregir el turno el mismo día en que se cambió no deja un rango imposible.
    /// </summary>
    /// <remarks>
    /// Es el error humano normal cuando se prepara una rotación: se elige el turno
    /// equivocado y se corrige acto seguido. La asignación que se acaba de crear empieza
    /// <b>mañana</b>, así que cerrarla «hoy» la dejaba con fecha de fin anterior a su fecha
    /// de inicio. Como todavía no ha regido ni un día, no es historia: se corrige en sitio.
    /// </remarks>
    [Fact]
    public async Task Corregir_el_turno_el_mismo_dia_no_deja_una_vigencia_imposible()
    {
        var trabajador = Obrero(Diurno);
        var servicio = Servicio(trabajador);

        await servicio.ActualizarAsync(Solicitud(Nocturno), "coordinador", CancellationToken.None);
        await servicio.ActualizarAsync(Solicitud(Sabatino), "coordinador", CancellationToken.None);

        var asignaciones = trabajador.AsignacionesTurno.OrderBy(a => a.FechaInicio).ToList();

        // Siguen siendo dos, no tres: la segunda se corrigió en vez de duplicarse.
        asignaciones.Count.ShouldBe(2);
        asignaciones[1].TurnoId.ShouldBe(Sabatino);
        asignaciones[1].FechaInicio.ShouldBe(Hoy.AddDays(1));
        asignaciones[1].FechaFin.ShouldBeNull();

        // Y ninguna termina antes de empezar.
        foreach (var asignacion in asignaciones)
        {
            if (asignacion.FechaFin is { } fin)
            {
                fin.ShouldBeGreaterThanOrEqualTo(asignacion.FechaInicio);
            }
        }
    }

    [Fact]
    public async Task Una_rotacion_repetida_no_acumula_asignaciones_solapadas()
    {
        var trabajador = Obrero(Diurno);
        var servicio = Servicio(trabajador);

        // Cuatro rotaciones seguidas. Al ocurrir todas el mismo día, cada una corrige la
        // anterior: lo que importa es que la línea de tiempo siga siendo válida.
        foreach (var turno in new[] { Nocturno, Diurno, Nocturno, Diurno })
        {
            await servicio.ActualizarAsync(Solicitud(turno), "coordinador", CancellationToken.None);
        }

        Should.NotThrow(() =>
            ValidadorVigencias.GarantizarLineaDeTiempoSana(trabajador.AsignacionesTurno, "turno"));

        trabajador.AsignacionesTurno.Count(a => a.FechaFin is null).ShouldBe(1);
    }

    /// <summary>
    /// Guardar la ficha de un trabajador cesado no le reabre una asignación de turno.
    /// </summary>
    /// <remarks>
    /// El cese cierra las asignaciones vigentes. Como después no queda ninguna abierta, el
    /// servicio abría una nueva desde la fecha de ingreso —solapando con toda su historia—
    /// cada vez que alguien guardaba la ficha para corregir un teléfono.
    /// </remarks>
    [Fact]
    public async Task Guardar_la_ficha_de_un_cesado_no_le_reabre_el_turno()
    {
        var trabajador = Obrero(Diurno);
        trabajador.DarDeBaja(new DateOnly(2026, 6, 30), MotivoCese.RenunciaVoluntaria, null, "coordinador");

        var servicio = Servicio(trabajador);

        await servicio.ActualizarAsync(Solicitud(Diurno), "coordinador", CancellationToken.None);

        trabajador.AsignacionesTurno.Count.ShouldBe(1);
        trabajador.AsignacionesTurno.Single().FechaFin.ShouldBe(new DateOnly(2026, 6, 30));
    }

    // ------------------------------------------------------------------
    // Con «Rige desde»: la rotación se registra el día en que ocurrió
    // ------------------------------------------------------------------

    private static readonly DateOnly InicioNoche = new(2026, 7, 6);
    private static readonly DateOnly LunesRotacion = new(2026, 7, 20);

    /// <summary>
    /// La rotación anotada tarde rige desde el día en que ocurrió, no desde mañana.
    /// </summary>
    /// <remarks>
    /// Es el defecto que motiva el campo: sin fecha, los días entre la rotación y el día en
    /// que alguien la anotaba se calculaban con el turno viejo. La fecha futura también vale:
    /// es preparar la rotación de la semana que viene.
    /// </remarks>
    [Theory]
    [InlineData(2026, 7, 6)]
    [InlineData(2031, 3, 3)]
    public async Task Una_rotacion_con_fecha_rige_desde_ese_dia(int anio, int mes, int dia)
    {
        var fecha = new DateOnly(anio, mes, dia);
        var trabajador = Obrero(Diurno);

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Nocturno, desde: fecha), "coordinador", CancellationToken.None);

        var asignaciones = Ordenadas(trabajador);

        asignaciones.Count.ShouldBe(2);
        asignaciones[0].TurnoId.ShouldBe(Diurno);
        asignaciones[0].FechaFin.ShouldBe(fecha.AddDays(-1));
        asignaciones[1].TurnoId.ShouldBe(Nocturno);
        asignaciones[1].FechaInicio.ShouldBe(fecha);
        asignaciones[1].FechaFin.ShouldBeNull();
    }

    /// <summary>
    /// La cuadrilla rotó un día antes de lo anotado: la frontera retrocede.
    /// </summary>
    [Fact]
    public async Task Si_rotaron_un_dia_antes_la_noche_se_acorta_y_el_dia_empieza_antes()
    {
        var trabajador = ConRotacionAnotada();

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Diurno, desde: LunesRotacion.AddDays(-1)), "coordinador", CancellationToken.None);

        var asignaciones = Ordenadas(trabajador);

        asignaciones.Count.ShouldBe(3);
        asignaciones[1].TurnoId.ShouldBe(Nocturno);
        asignaciones[1].FechaInicio.ShouldBe(InicioNoche);
        asignaciones[1].FechaFin.ShouldBe(new DateOnly(2026, 7, 18));
        asignaciones[2].TurnoId.ShouldBe(Diurno);
        asignaciones[2].FechaInicio.ShouldBe(new DateOnly(2026, 7, 19));
        asignaciones[2].FechaFin.ShouldBeNull();
    }

    /// <summary>
    /// La cuadrilla rotó un día después de lo anotado: la noche se alarga.
    /// </summary>
    /// <remarks>
    /// Con el mismo turno y una fecha posterior, «desde esa fecha, ese turno» solo es
    /// cierto si los días entre medio vuelven al turno anterior. Si se ignorara la fecha
    /// —ese turno ya regía desde antes—, el lunes 20 seguiría calculándose de día.
    /// </remarks>
    [Fact]
    public async Task Si_rotaron_un_dia_despues_la_noche_se_alarga_hasta_el_dia_previo()
    {
        var trabajador = ConRotacionAnotada();

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Diurno, desde: LunesRotacion.AddDays(1)), "coordinador", CancellationToken.None);

        var asignaciones = Ordenadas(trabajador);

        asignaciones.Count.ShouldBe(3);
        asignaciones[1].TurnoId.ShouldBe(Nocturno);
        asignaciones[1].FechaFin.ShouldBe(LunesRotacion);
        asignaciones[2].TurnoId.ShouldBe(Diurno);
        asignaciones[2].FechaInicio.ShouldBe(new DateOnly(2026, 7, 21));
    }

    /// <summary>
    /// Volver a guardar la ficha con la misma fecha y el mismo turno no toca nada.
    /// </summary>
    [Fact]
    public async Task La_misma_fecha_y_el_mismo_turno_no_cambian_nada()
    {
        var trabajador = ConRotacionAnotada();

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Diurno, desde: LunesRotacion), "coordinador", CancellationToken.None);

        trabajador.AsignacionesTurno.Count.ShouldBe(3);
        trabajador.AsignacionesTurno.ShouldAllBe(a => a.ModificadoPor == null);
    }

    /// <summary>
    /// Un turno equivocado desde el día en que empezó se corrige en sitio.
    /// </summary>
    [Fact]
    public async Task Otro_turno_desde_el_inicio_de_la_vigente_la_corrige_en_sitio()
    {
        var trabajador = ConRotacionAnotada();

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Sabatino, desde: LunesRotacion), "coordinador", CancellationToken.None);

        var asignaciones = Ordenadas(trabajador);

        asignaciones.Count.ShouldBe(3);
        asignaciones[2].TurnoId.ShouldBe(Sabatino);
        asignaciones[2].FechaInicio.ShouldBe(LunesRotacion);
        asignaciones[1].FechaFin.ShouldBe(LunesRotacion.AddDays(-1));
    }

    /// <summary>
    /// Una fecha que se comería entera la asignación anterior se rechaza sin tocar nada.
    /// </summary>
    /// <remarks>
    /// Borrar un tramo de la historia de rotaciones desde la ficha sería perderlo sin
    /// rastro. Con varias rotaciones sin anotar se registran una por una, en orden.
    /// </remarks>
    [Fact]
    public async Task Una_fecha_que_borraria_la_asignacion_anterior_se_rechaza()
    {
        var trabajador = ConRotacionAnotada();

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Diurno, desde: InicioNoche), "coordinador", CancellationToken.None));

        error.Message.ShouldContain("desaparecería entera");
        Ordenadas(trabajador)[1].FechaFin.ShouldBe(LunesRotacion.AddDays(-1));
        await repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Atrasar el inicio del turno sin un turno anterior que cubra los días se rechaza.
    /// </summary>
    [Fact]
    public async Task Atrasar_el_inicio_sin_turno_anterior_dejaria_dias_sin_turno_y_se_rechaza()
    {
        var trabajador = Obrero(Diurno);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Diurno, desde: LunesRotacion), "coordinador", CancellationToken.None));

        error.Message.ShouldContain("quedarían sin turno");
        trabajador.AsignacionesTurno.Single().FechaInicio.ShouldBe(new DateOnly(2020, 1, 1));
    }

    [Fact]
    public async Task El_turno_no_puede_regir_antes_del_ingreso()
    {
        var trabajador = Obrero(Diurno);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Nocturno, desde: new DateOnly(2019, 12, 31)), "coordinador", CancellationToken.None));

        error.Message.ShouldContain("anterior a la fecha de ingreso");
        trabajador.AsignacionesTurno.Count.ShouldBe(1);
    }

    /// <summary>
    /// Un periodo cerrado no admite escrituras, tampoco un cambio de turno sobre sus días.
    /// </summary>
    [Fact]
    public async Task Un_cambio_con_fecha_dentro_de_un_periodo_cerrado_se_rechaza()
    {
        CerrarPeriodo("2026-07", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        var trabajador = Obrero(Diurno);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Nocturno, desde: LunesRotacion), "coordinador", CancellationToken.None));

        error.Message.ShouldContain("2026-07");
        error.Message.ShouldContain("posterior al 31/07/2026");
        await repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_periodo_cerrado_anterior_a_la_fecha_no_estorba()
    {
        CerrarPeriodo("2026-06", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        var trabajador = Obrero(Diurno);

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Nocturno, desde: InicioNoche), "coordinador", CancellationToken.None);

        Ordenadas(trabajador)[1].FechaInicio.ShouldBe(InicioNoche);
        await repositorio.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Correr la frontera hacia atrás cambia días de la asignación anterior: si esos días
    /// están en un periodo cerrado, tampoco se puede.
    /// </summary>
    [Fact]
    public async Task Adelantar_la_rotacion_hacia_un_periodo_cerrado_se_rechaza()
    {
        CerrarPeriodo("2026-06", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));
        var trabajador = ConRotacionAnotada(inicioNoche: new DateOnly(2026, 6, 15), rotacion: new DateOnly(2026, 7, 1));

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Diurno, desde: new DateOnly(2026, 6, 29)), "coordinador", CancellationToken.None));

        error.Message.ShouldContain("2026-06");
    }

    // ------------------------------------------------------------------
    // Cuadrilla en lugar de turno fijo
    // ------------------------------------------------------------------

    private const long CuadrillaA = 40;
    private const long CuadrillaDeOtraPlanta = 41;

    private void DeclararCuadrillas() =>
        catalogos
            .CuadrillaPorIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(llamada => llamada.ArgAt<long>(0) switch
            {
                CuadrillaA => new CuadrillaOpcionDto(CuadrillaA, "A", "Cuadrilla A", 3, "PLANTA 1", true),
                CuadrillaDeOtraPlanta => new CuadrillaOpcionDto(CuadrillaDeOtraPlanta, "Z", "Cuadrilla Z", 9, "OTRA", true),
                _ => null
            });

    /// <summary>
    /// Pasar a una cuadrilla es un cambio de asignación como cualquier otro: con fecha, rige
    /// desde ese día, y el turno fijo se cierra el día previo.
    /// </summary>
    [Fact]
    public async Task Pasar_a_una_cuadrilla_con_fecha_cierra_el_turno_fijo_el_dia_previo()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);

        await Servicio(trabajador).ActualizarAsync(
            Solicitud(Diurno, desde: LunesRotacion) with { CuadrillaId = CuadrillaA },
            "coordinador",
            CancellationToken.None);

        var asignaciones = Ordenadas(trabajador);

        asignaciones.Count.ShouldBe(2);
        asignaciones[0].TurnoId.ShouldBe(Diurno);
        asignaciones[0].FechaFin.ShouldBe(LunesRotacion.AddDays(-1));

        // Manda la cuadrilla: el turno fijo que el formulario seguía enviando se ignora.
        asignaciones[1].CuadrillaId.ShouldBe(CuadrillaA);
        asignaciones[1].TurnoId.ShouldBeNull();
        asignaciones[1].FechaInicio.ShouldBe(LunesRotacion);
    }

    [Fact]
    public async Task Una_cuadrilla_de_otra_planta_se_rechaza()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).ActualizarAsync(
                Solicitud(Diurno) with { CuadrillaId = CuadrillaDeOtraPlanta },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("solo pueden integrarla trabajadores de esa unidad");
        trabajador.AsignacionesTurno.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sumar_varios_a_una_cuadrilla_les_abre_la_asignacion_desde_la_fecha()
    {
        DeclararCuadrillas();
        var primero = Obrero(Diurno);
        var segundo = Obrero(Nocturno);
        segundo.Id = 8;

        var servicio = Servicio(primero);
        repositorio.ObtenerPorIdAsync(8, Arg.Any<CancellationToken>()).Returns(segundo);

        var sumados = await servicio.AsignarCuadrillaAsync(
            new AsignarCuadrillaRequest { CuadrillaId = CuadrillaA, Desde = LunesRotacion, TrabajadorIds = [7, 8] },
            "coordinador",
            CancellationToken.None);

        sumados.ShouldBe(2);
        Ordenadas(primero)[^1].CuadrillaId.ShouldBe(CuadrillaA);
        Ordenadas(segundo)[^1].CuadrillaId.ShouldBe(CuadrillaA);
        Ordenadas(segundo)[0].FechaFin.ShouldBe(LunesRotacion.AddDays(-1));
        await repositorio.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PL3EB_CUA_01_usa_la_planta_historica_y_no_la_actual()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);
        trabajador.PlantaId = 9;

        var sumados = await Servicio(trabajador).AsignarCuadrillaAsync(
            new AsignarCuadrillaRequest
            {
                CuadrillaId = CuadrillaA,
                Desde = LunesRotacion,
                TrabajadorIds = [trabajador.Id]
            },
            "coordinador",
            CancellationToken.None);

        sumados.ShouldBe(1);
        Ordenadas(trabajador)[^1].CuadrillaId.ShouldBe(CuadrillaA);
    }

    [Fact]
    public async Task PL3EB_CUA_02_rechaza_fecha_anterior_al_primer_ingreso()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);
        consulta
            .ContextosLaboralesEnFechaAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, ContextoLaboralFechaDto>());

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).AsignarCuadrillaAsync(
                new AsignarCuadrillaRequest
                {
                    CuadrillaId = CuadrillaA,
                    Desde = new DateOnly(2019, 12, 31),
                    TrabajadorIds = [trabajador.Id]
                },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("no tenía un vínculo laboral vigente");
    }

    [Fact]
    public async Task PL3EB_CUA_03_rechaza_fecha_posterior_al_cese()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);
        trabajador.DarDeBaja(
            new DateOnly(2026, 6, 30), MotivoCese.RenunciaVoluntaria, null, "coordinador");
        consulta
            .ContextosLaboralesEnFechaAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, ContextoLaboralFechaDto>());

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            Servicio(trabajador).AsignarCuadrillaAsync(
                new AsignarCuadrillaRequest
                {
                    CuadrillaId = CuadrillaA,
                    Desde = LunesRotacion,
                    TrabajadorIds = [trabajador.Id]
                },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("no tenía un vínculo laboral vigente");
    }

    [Fact]
    public async Task PL3EB_CUA_04_reingreso_posterior_no_reemplaza_el_contexto_del_vinculo_antiguo()
    {
        DeclararCuadrillas();
        var trabajador = Obrero(Diurno);
        var asignacionAntigua = trabajador.AsignacionesTurno.Single();
        asignacionAntigua.FechaFin = new DateOnly(2026, 12, 31);
        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = Nocturno,
            FechaInicio = new DateOnly(2027, 2, 1)
        });
        trabajador.PlantaId = 9;
        trabajador.FechaIngreso = new DateOnly(2027, 2, 1);
        consulta
            .ContextosLaboralesEnFechaAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                LunesRotacion,
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, ContextoLaboralFechaDto>
            {
                [trabajador.Id] = new(
                    100, 1, 3, new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31))
            });

        var sumados = await Servicio(trabajador).AsignarCuadrillaAsync(
            new AsignarCuadrillaRequest
            {
                CuadrillaId = CuadrillaA,
                Desde = LunesRotacion,
                TrabajadorIds = [trabajador.Id]
            },
            "coordinador",
            CancellationToken.None);

        sumados.ShouldBe(1);
        var ordenadas = Ordenadas(trabajador);
        ordenadas.Single(a => a.CuadrillaId == CuadrillaA).FechaFin
            .ShouldBe(new DateOnly(2026, 12, 31));
        ordenadas.Single(a => a.FechaInicio == new DateOnly(2027, 2, 1)).TurnoId
            .ShouldBe(Nocturno);
    }

    /// <summary>
    /// Todo o nada: media cuadrilla sumada dejaría al grupo rotando partido.
    /// </summary>
    [Fact]
    public async Task Si_uno_no_puede_sumarse_no_se_suma_nadie_y_el_aviso_dice_quien()
    {
        DeclararCuadrillas();
        var primero = Obrero(Diurno);
        var cesado = Obrero(Nocturno);
        cesado.Id = 8;
        cesado.ApellidoPaterno = "CESADO";
        cesado.DarDeBaja(new DateOnly(2026, 6, 30), MotivoCese.RenunciaVoluntaria, null, "coordinador");

        var servicio = Servicio(primero);
        repositorio.ObtenerPorIdAsync(8, Arg.Any<CancellationToken>()).Returns(cesado);
        consulta
            .ContextosLaboralesEnFechaAsync(
                Arg.Any<IReadOnlyCollection<long>>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, ContextoLaboralFechaDto>
            {
                [primero.Id] = new(primero.Id, 2, 3, new DateOnly(2020, 1, 1), null)
            });

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.AsignarCuadrillaAsync(
                new AsignarCuadrillaRequest { CuadrillaId = CuadrillaA, Desde = LunesRotacion, TrabajadorIds = [7, 8] },
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("CESADO");
        error.Message.ShouldContain("no tenía un vínculo laboral vigente");
        await repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------

    private ServicioTrabajadores Servicio(Trabajador trabajador)
    {
        repositorio
            .ObtenerPorIdAsync(trabajador.Id, Arg.Any<CancellationToken>())
            .Returns(trabajador);

        return new ServicioTrabajadores(
            repositorio, consulta, catalogos, alcance, Substitute.For<IServicioMarcadores>(), periodos);
    }

    private void CerrarPeriodo(string codigo, DateOnly inicio, DateOnly fin) =>
        periodos
            .ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new PeriodoListaDto(
                    1, codigo, codigo, inicio, fin, EstadoPeriodo.Cerrado,
                    new DateTime(2026, 8, 3, 9, 0, 0), "coordinador", null, null, null, true, 0)
            });

    private static List<AsignacionTurno> Ordenadas(Trabajador trabajador) =>
        [.. trabajador.AsignacionesTurno.OrderBy(a => a.FechaInicio)];

    /// <summary>
    /// Diurno desde el ingreso, noche desde <paramref name="inicioNoche"/> y de vuelta a día
    /// desde <paramref name="rotacion"/>, ya anotado.
    /// </summary>
    private static Trabajador ConRotacionAnotada(DateOnly? inicioNoche = null, DateOnly? rotacion = null)
    {
        var noche = inicioNoche ?? InicioNoche;
        var dia = rotacion ?? LunesRotacion;

        var trabajador = Obrero(Diurno);
        var inicial = trabajador.AsignacionesTurno.Single();
        inicial.FechaFin = noche.AddDays(-1);

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = Nocturno,
            FechaInicio = noche,
            FechaFin = dia.AddDays(-1)
        });

        trabajador.AsignacionesTurno.Add(new AsignacionTurno { TurnoId = Diurno, FechaInicio = dia });

        return trabajador;
    }

    private static Trabajador Obrero(long turnoId)
    {
        var trabajador = new Trabajador
        {
            Id = 7,
            TipoDocumentoId = 1,
            NumeroDocumento = "44444444",
            ApellidoPaterno = "ROTACION",
            ApellidoMaterno = "PRUEBA",
            Nombres = "OBRERO",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = turnoId,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("semilla");

        return trabajador;
    }

    private static GuardarTrabajadorRequest Solicitud(long turnoId, DateOnly? desde = null) => new()
    {
        TurnoVigenteDesde = desde,
        Id = 7,
        TipoDocumentoId = 1,
        NumeroDocumento = "44444444",
        ApellidoPaterno = "ROTACION",
        ApellidoMaterno = "PRUEBA",
        Nombres = "OBRERO",
        Sexo = Sexo.Masculino,
        EmpresaId = 2,
        TipoPlanilla = TipoPlanilla.Obrero,
        PlantaId = 3,
        PuestoId = 1,
        FechaIngreso = new DateOnly(2020, 1, 1),
        TurnoId = turnoId
    };
}
