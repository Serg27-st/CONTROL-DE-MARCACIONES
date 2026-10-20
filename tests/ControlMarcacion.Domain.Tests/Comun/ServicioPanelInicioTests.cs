using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Inicio;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Comun;

/// <summary>
/// El panel de la portada.
/// </summary>
/// <remarks>
/// Es un orquestador puro: no toca la base, solo compone lo que devuelven los servicios de
/// cada módulo. Lo que estas pruebas fijan es <b>qué periodo elige</b>, <b>cuándo declara
/// que hay algo pendiente</b> y que <b>un módulo caído no tumba la portada entera</b>.
/// </remarks>
public sealed class ServicioPanelInicioTests
{
    private readonly IServicioPeriodos periodos = Substitute.For<IServicioPeriodos>();
    private readonly IServicioImportacion importacion = Substitute.For<IServicioImportacion>();
    private readonly IServicioCalculoTareo tareo = Substitute.For<IServicioCalculoTareo>();
    private readonly IServicioSolicitudes solicitudes = Substitute.For<IServicioSolicitudes>();
    private readonly IServicioReportes reportes = Substitute.For<IServicioReportes>();

    private static readonly DateOnly Inicio = new(2026, 7, 1);
    private static readonly DateOnly Fin = new(2026, 7, 31);

    private ServicioPanelInicio Servicio =>
        new(periodos, importacion, tareo, solicitudes, reportes);

    public ServicioPanelInicioTests()
    {
        // Por omisión: sin periodos, sin solicitudes. Cada prueba siembra lo suyo.
        periodos.ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        solicitudes.ListarAsync(Arg.Any<FiltroSolicitudes>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoPaginado<SolicitudListaDto>([], 0, 1, 1));
    }

    [Fact]
    public async Task Sin_periodos_devuelve_un_panel_vacio_pero_con_las_solicitudes()
    {
        SembrarSolicitudesPendientes(3);

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.HayPeriodo.ShouldBeFalse();
        panel.Periodo.ShouldBeNull();
        panel.Marcaciones.ShouldBeNull();
        panel.Tareo.ShouldBeNull();

        // Las solicitudes no dependen del periodo: si hay tres esperando, se dicen.
        panel.SolicitudesPendientes.ShouldBe(3);
        panel.HayPendientes.ShouldBeTrue();
    }

    /// <summary>
    /// El periodo elegido es el primero de la lista, que viene ordenada por fecha de
    /// inicio descendente. Es el mismo criterio que usa la pantalla de Tareo al entrar
    /// sin elegir nada; si divergieran, la portada enlazaría a otro mes del que resume.
    /// </summary>
    [Fact]
    public async Task Toma_el_periodo_mas_reciente_y_pide_la_cobertura_de_su_rango()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 20, diasTranscurridos: 20);
        SembrarTareo();

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Periodo!.Codigo.ShouldBe("2026-07");

        // La cobertura se pide por el rango del periodo, no por mes calendario.
        await importacion.Received(1)
            .CoberturaDeRangoAsync(Inicio, Fin, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Marca_pendiente_cuando_faltan_dias_por_cargar()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 12, diasTranscurridos: 15);
        SembrarTareo();

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Marcaciones!.DiasSinMarcacion.ShouldBe(3);
        panel.Marcaciones.HayHuecos.ShouldBeTrue();
        panel.HayPendientes.ShouldBeTrue();
    }

    /// <summary>
    /// Todos los días tienen marcaciones, pero las del M-Z faltan en cuatro: la portada no
    /// puede decir «todo cargado».
    /// </summary>
    /// <remarks>
    /// Antes solo miraba los días sin ninguna marcación, y con dos aparatos en la planta un
    /// archivo olvidado no se notaba: el día tenía las marcaciones del otro.
    /// </remarks>
    [Fact]
    public async Task Marca_pendiente_cuando_a_un_aparato_le_faltan_dias_aunque_todos_tengan_marcaciones()
    {
        SembrarPeriodo();
        importacion.CoberturaDeRangoAsync(
                Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new CoberturaDeRango(
                Inicio, Fin, 1500, DiasConMarcacion: 15, DiasTranscurridos: 15,
                DiasIncompletos: 4, AparatosIncompletos: ["Detergentes M-Z"]));
        SembrarTareo();

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Marcaciones!.DiasSinMarcacion.ShouldBe(0);
        panel.Marcaciones.DiasPorRevisar.ShouldBe(4);
        panel.Marcaciones.HayHuecos.ShouldBeTrue();
        panel.HayPendientes.ShouldBeTrue();
    }

    [Fact]
    public async Task Marca_pendiente_cuando_el_tareo_esta_desactualizado()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 15, diasTranscurridos: 15);
        SembrarTareo(new EstadoCalculoTareo(
            CalculadoMasReciente: new DateTime(2026, 7, 20, 9, 0, 0),
            UltimoCambioRelevante: new DateTime(2026, 7, 21, 8, 0, 0),
            MotivoUltimoCambio: "una carga de marcaciones"));

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Tareo!.Calculo.Desactualizado.ShouldBeTrue();
        panel.HayPendientes.ShouldBeTrue();
    }

    /// <summary>
    /// Todo cubierto, tareo al día y nada esperando: la portada puede decir que el mes va
    /// bien. Es la única combinación en la que debe hacerlo.
    /// </summary>
    [Fact]
    public async Task Sin_huecos_ni_solicitudes_y_con_el_tareo_al_dia_no_hay_pendientes()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 15, diasTranscurridos: 15);
        SembrarTareo(new EstadoCalculoTareo(
            CalculadoMasReciente: new DateTime(2026, 7, 21, 9, 0, 0),
            UltimoCambioRelevante: new DateTime(2026, 7, 20, 8, 0, 0),
            MotivoUltimoCambio: "una carga de marcaciones"));

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Tareo!.Calculo.EstaAlDia.ShouldBeTrue();
        panel.Marcaciones!.HayHuecos.ShouldBeFalse();
        panel.SolicitudesPendientes.ShouldBe(0);
        panel.Tareo.PendientesDeValidar.ShouldBe(0);
        panel.HayPendientes.ShouldBeFalse();
    }

    /// <summary>
    /// Un día que el sistema no pudo decidir cuenta como pendiente, aunque todo lo demás
    /// esté en orden.
    /// </summary>
    /// <remarks>
    /// Es la razón de ser de la tarjeta: esos días <b>no son faltas</b> —no aparecen en el
    /// conteo de faltas— y cerrar el mes sin mirarlos es cerrarlo a medias. Sin esto la
    /// portada diría «nada pendiente» sobre un periodo con media planta sin archivo cargado.
    /// </remarks>
    [Fact]
    public async Task Los_dias_pendientes_de_validar_cuentan_como_pendientes()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 15, diasTranscurridos: 15);
        SembrarTareo(
            new EstadoCalculoTareo(
                CalculadoMasReciente: new DateTime(2026, 7, 21, 9, 0, 0),
                UltimoCambioRelevante: new DateTime(2026, 7, 20, 8, 0, 0),
                MotivoUltimoCambio: "una carga de marcaciones"),
            pendientesDeValidar: 37);

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.Tareo!.PendientesDeValidar.ShouldBe(37);
        panel.Tareo.Faltas.ShouldBe(0);
        panel.HayPendientes.ShouldBeTrue();
    }

    /// <summary>
    /// Si falta un parámetro legal, el estado del cálculo revienta con ReglaNegocio. La
    /// portada tiene que seguir sirviendo: es el punto de partida del usuario, y dejarla
    /// en blanco por un dato de normativa ausente lo deja sin por dónde empezar.
    /// </summary>
    [Fact]
    public async Task Un_fallo_al_resolver_el_estado_del_calculo_no_tumba_la_portada()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 15, diasTranscurridos: 15);

        tareo.ResumirAsync(Arg.Any<FiltroTareo>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenTareo(
                new Dictionary<EstadoTareo, int> { [EstadoTareo.Falta] = 4 },
                Dias: 100, Trabajadores: 5,
                MinutosOrdinarios: 0, MinutosExtra25: 0, MinutosExtra35: 0,
                MinutosNocturnos: 0, MinutosTardanza: 0, MinutosSalidaAnticipada: 0));

        reportes.ObtenerMetadatosAsync(
                Arg.Any<FiltroReporte>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<MetadatosReporte>>(_ =>
                throw new ReglaNegocioException("Falta la RMV vigente."));

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        // El resumen sí llegó; lo único que se pierde es el sello de vigencia.
        panel.Tareo.ShouldNotBeNull();
        panel.Tareo!.Faltas.ShouldBe(4);
        panel.Tareo.DiasCalculados.ShouldBe(100);

        // Sin poder comprobarlo, se asume lo conservador: no está al día.
        panel.Tareo.Calculo.SinCalcular.ShouldBeTrue();
        panel.Tareo.Calculo.EstaAlDia.ShouldBeFalse();
    }

    [Fact]
    public async Task Cuenta_las_solicitudes_pendientes_sin_traerselas()
    {
        SembrarPeriodo();
        SembrarCobertura(diasConMarcacion: 15, diasTranscurridos: 15);
        SembrarTareo();
        SembrarSolicitudesPendientes(7);

        var panel = await Servicio.ObtenerAsync("coordinador@gca.local");

        panel.SolicitudesPendientes.ShouldBe(7);

        // Se pide una página mínima: interesa el total, no las filas.
        await solicitudes.Received(1).ListarAsync(
            Arg.Is<FiltroSolicitudes>(f =>
                f != null
                && f.Estado == Domain.Solicitudes.EstadoSolicitud.Pendiente
                && f.TamanioPagina == 1),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------

    private void SembrarPeriodo() =>
        periodos.ListarAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new List<PeriodoListaDto>
            {
                new(1, "2026-07", "Julio 2026", Inicio, Fin,
                    EstadoPeriodo.Abierto, null, null, null, null, null, true, 155)
            });

    private void SembrarCobertura(int diasConMarcacion, int diasTranscurridos) =>
        importacion.CoberturaDeRangoAsync(
                Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new CoberturaDeRango(Inicio, Fin, 1500, diasConMarcacion, diasTranscurridos));

    private void SembrarTareo(EstadoCalculoTareo? calculo = null, int pendientesDeValidar = 0)
    {
        tareo.ResumirAsync(Arg.Any<FiltroTareo>(), Arg.Any<CancellationToken>())
            .Returns(new ResumenTareo(
                new Dictionary<EstadoTareo, int>
                {
                    [EstadoTareo.Falta] = 0,
                    [EstadoTareo.Tardanza] = 0,
                    [EstadoTareo.PendienteValidacion] = pendientesDeValidar
                },
                Dias: 155, Trabajadores: 5,
                MinutosOrdinarios: 27360, MinutosExtra25: 7000, MinutosExtra35: 7090,
                MinutosNocturnos: 10560, MinutosTardanza: 25, MinutosSalidaAnticipada: 0));

        reportes.ObtenerMetadatosAsync(
                Arg.Any<FiltroReporte>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MetadatosReporte(
                Alcance: "Periodo 2026-07",
                FechaInicio: Inicio,
                FechaFin: Fin,
                PeriodoCodigo: "2026-07",
                PeriodoNombre: "Julio 2026",
                PeriodoAbierto: true,
                FiltrosDescripcion: "sin filtros",
                GeneradoEn: new DateTime(2026, 7, 21, 10, 0, 0),
                GeneradoPor: "coordinador@gca.local",
                Calculo: calculo ?? new EstadoCalculoTareo(
                    new DateTime(2026, 7, 21, 9, 0, 0),
                    new DateTime(2026, 7, 20, 8, 0, 0),
                    "una carga de marcaciones")));
    }

    private void SembrarSolicitudesPendientes(int total) =>
        solicitudes.ListarAsync(Arg.Any<FiltroSolicitudes>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoPaginado<SolicitudListaDto>([], total, 1, 1));
}
