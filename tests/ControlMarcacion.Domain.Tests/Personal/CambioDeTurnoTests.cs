using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
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
/// El corte es «hoy»: la asignación vigente se cierra hoy y la nueva empieza mañana. Por
/// eso estos tests comparan contra <see cref="DateOnly.FromDateTime"/> de la fecha del
/// sistema en vez de contra una fecha literal — es el propio servicio el que la lee, y esa
/// es justamente una de las limitaciones anotadas: todavía no se puede elegir desde cuándo
/// rige la rotación.
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

    public CambioDeTurnoTests()
    {
        catalogos
            .TipoDocumentoPorIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new TipoDocumentoOpcionDto(1, "DNI", "DNI", 8, null, true));

        repositorio
            .ExisteDocumentoAsync(
                Arg.Any<long>(), Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(false);
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

    private ServicioTrabajadores Servicio(Trabajador trabajador)
    {
        repositorio
            .ObtenerPorIdAsync(trabajador.Id, Arg.Any<CancellationToken>())
            .Returns(trabajador);

        return new ServicioTrabajadores(repositorio, consulta, catalogos);
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

    private static GuardarTrabajadorRequest Solicitud(long turnoId) => new()
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
        FechaIngreso = new DateOnly(2020, 1, 1),
        TurnoId = turnoId
    };
}
