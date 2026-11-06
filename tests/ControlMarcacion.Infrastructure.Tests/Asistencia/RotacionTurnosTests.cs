using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Repositorios;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// Rotación de turnos: los obreros pasan de diurno a nocturno cada una o dos semanas.
/// </summary>
/// <remarks>
/// Va sobre la base y no sobre <c>CalculadoraTareo</c> porque lo que se prueba es el
/// <b>emparejamiento</b> de marcaciones con jornadas, que vive en el servicio: decidir qué
/// marcas pertenecen a qué día es justo lo que se rompe en la semana del cambio.
///
/// <para>Turnos sembrados: DIU (1) de 07:00 a 19:00 y NOC (2) de 19:00 a 07:00, ambos de
/// lunes a sábado. Periodo 1 = julio 2026, del 1 al 31. El 1 de julio de 2026 es
/// miércoles.</para>
/// </remarks>
public sealed class RotacionTurnosTests : IDisposable
{
    /// <summary>Aparato de las marcaciones de prueba.</summary>
    private const long EquipoDePrueba = 1;

    private const long PlantaP01 = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long Diurno = 1;
    private const long Nocturno = 2;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;

    public RotacionTurnosTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(), NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose() => contexto.Dispose();

    /// <summary>
    /// El defecto que motivó todo esto: una marca no puede pertenecer a dos días.
    /// </summary>
    /// <remarks>
    /// El obrero cierra su última noche el miércoles 8 (19:00) y sale el jueves 9 a las
    /// 07:00. El jueves ya está asignado a diurno, cuya ventana de entrada abre a las 06:45
    /// — así que esa misma marca de las 07:00 caía también dentro del jueves y el día salía
    /// «Incompleto» con una entrada que nunca ocurrió. Multiplicado por todo el personal que
    /// rota, y por cada rotación.
    /// </remarks>
    [Fact]
    public async Task La_salida_de_la_ultima_noche_no_reaparece_como_entrada_del_dia_siguiente()
    {
        var id = await CrearAsync(
            (Nocturno, new DateOnly(2020, 1, 1), new DateOnly(2026, 7, 8)),
            (Diurno, new DateOnly(2026, 7, 9), null));

        // Entra el miércoles 19:00, sale el jueves 07:00. El jueves descansa tras noches.
        await MarcarAsync(id,
            new DateTime(2026, 7, 8, 19, 0, 0),
            new DateTime(2026, 7, 9, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var miercoles = await LeerAsync(id, new DateOnly(2026, 7, 8));
        var jueves = await LeerAsync(id, new DateOnly(2026, 7, 9));

        // El miércoles conserva su jornada completa: la marca es suya.
        miercoles.MinutosTrabajados.ShouldBe(720);
        miercoles.SalidaReal.ShouldBe(new DateTime(2026, 7, 9, 7, 0, 0));
        miercoles.Estado.ShouldBe(EstadoTareo.Puntual);

        // Y el jueves ya no se la queda prestada.
        jueves.EntradaReal.ShouldBeNull();
        jueves.SalidaReal.ShouldBeNull();
        jueves.Estado.ShouldBe(EstadoTareo.Falta);
    }

    /// <summary>
    /// Si el obrero sale de la noche con sobretiempo, la marca tardía tampoco se recicla.
    /// </summary>
    [Fact]
    public async Task La_salida_nocturna_con_sobretiempo_tampoco_se_cuenta_dos_veces()
    {
        var id = await CrearAsync(
            (Nocturno, new DateOnly(2020, 1, 1), new DateOnly(2026, 7, 8)),
            (Diurno, new DateOnly(2026, 7, 9), null));

        // Sale a las 08:30 del jueves: 90 minutos después del cierre del turno.
        await MarcarAsync(id,
            new DateTime(2026, 7, 8, 19, 0, 0),
            new DateTime(2026, 7, 9, 8, 30, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var miercoles = await LeerAsync(id, new DateOnly(2026, 7, 8));
        var jueves = await LeerAsync(id, new DateOnly(2026, 7, 9));

        miercoles.MinutosTrabajados.ShouldBe(810);
        jueves.EntradaReal.ShouldBeNull();
    }

    /// <summary>
    /// El día 1 del periodo está tan protegido como los demás.
    /// </summary>
    /// <remarks>
    /// Sin sembrar el estado con el día anterior al periodo, el corte solo funcionaría a
    /// partir del día 2 y el primero de cada mes volvería a heredar la marca de la noche
    /// del último día del mes anterior.
    /// </remarks>
    [Fact]
    public async Task El_primer_dia_del_periodo_no_hereda_la_salida_de_la_noche_anterior()
    {
        // Martes 30 de junio de noche; desde el miércoles 1 de julio pasa a diurno.
        var id = await CrearAsync(
            (Nocturno, new DateOnly(2020, 1, 1), new DateOnly(2026, 6, 30)),
            (Diurno, new DateOnly(2026, 7, 1), null));

        await MarcarAsync(id,
            new DateTime(2026, 6, 30, 19, 0, 0),
            new DateTime(2026, 7, 1, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var primero = await LeerAsync(id, new DateOnly(2026, 7, 1));

        primero.EntradaReal.ShouldBeNull();
        primero.Estado.ShouldBe(EstadoTareo.Falta);
    }

    /// <summary>
    /// Regresión: el turno nocturno continuo nunca tuvo el problema y sigue sin tenerlo.
    /// </summary>
    [Fact]
    public async Task El_nocturno_continuo_conserva_su_jornada_completa()
    {
        var id = await CrearAsync((Nocturno, new DateOnly(2020, 1, 1), null));

        // Tres noches seguidas: lunes 6, martes 7 y miércoles 8 de julio.
        await MarcarAsync(id,
            new DateTime(2026, 7, 6, 19, 0, 0), new DateTime(2026, 7, 7, 7, 0, 0),
            new DateTime(2026, 7, 7, 19, 0, 0), new DateTime(2026, 7, 8, 7, 0, 0),
            new DateTime(2026, 7, 8, 19, 0, 0), new DateTime(2026, 7, 9, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        foreach (var dia in new[] { 6, 7, 8 })
        {
            var tareo = await LeerAsync(id, new DateOnly(2026, 7, dia));

            tareo.MinutosTrabajados.ShouldBe(720, $"la noche del {dia}/07 debe dar 12 h");
            tareo.Estado.ShouldBe(EstadoTareo.Puntual);
        }
    }

    /// <summary>
    /// Regresión: el diurno continuo tampoco se ve recortado por el corte nuevo.
    /// </summary>
    [Fact]
    public async Task El_diurno_continuo_conserva_su_jornada_completa()
    {
        var id = await CrearAsync((Diurno, new DateOnly(2020, 1, 1), null));

        await MarcarAsync(id,
            new DateTime(2026, 7, 6, 7, 0, 0), new DateTime(2026, 7, 6, 19, 0, 0),
            new DateTime(2026, 7, 7, 7, 0, 0), new DateTime(2026, 7, 7, 19, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        (await LeerAsync(id, new DateOnly(2026, 7, 6))).MinutosTrabajados.ShouldBe(720);
        (await LeerAsync(id, new DateOnly(2026, 7, 7))).MinutosTrabajados.ShouldBe(720);
    }

    /// <summary>
    /// La rotación completa de un mes se calcula con el turno de cada día.
    /// </summary>
    [Fact]
    public async Task Una_rotacion_quincenal_usa_el_turno_vigente_de_cada_dia()
    {
        var id = await CrearAsync(
            (Diurno, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
            (Nocturno, new DateOnly(2026, 7, 16), null));

        // Lunes 13 en diurno y lunes 20 en nocturno.
        await MarcarAsync(id,
            new DateTime(2026, 7, 13, 7, 0, 0), new DateTime(2026, 7, 13, 19, 0, 0),
            new DateTime(2026, 7, 20, 19, 0, 0), new DateTime(2026, 7, 21, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var enDiurno = await LeerAsync(id, new DateOnly(2026, 7, 13));
        var enNocturno = await LeerAsync(id, new DateOnly(2026, 7, 20));

        enDiurno.TurnoId.ShouldBe(Diurno);
        enDiurno.MinutosTrabajados.ShouldBe(720);

        enNocturno.TurnoId.ShouldBe(Nocturno);
        enNocturno.MinutosTrabajados.ShouldBe(720);
    }

    /// <summary>
    /// Recalcular dos veces un periodo con rotación da exactamente lo mismo.
    /// </summary>
    /// <remarks>
    /// El corte por jornada anterior introduce estado que se arrastra de un día al
    /// siguiente, así que la idempotencia hay que volver a demostrarla: se reconstruye
    /// desde cero en cada corrida, no se acumula entre corridas.
    /// </remarks>
    [Fact]
    public async Task Recalcular_dos_veces_con_rotacion_da_el_mismo_resultado()
    {
        var id = await CrearAsync(
            (Nocturno, new DateOnly(2020, 1, 1), new DateOnly(2026, 7, 8)),
            (Diurno, new DateOnly(2026, 7, 9), null));

        await MarcarAsync(id,
            new DateTime(2026, 7, 8, 19, 0, 0), new DateTime(2026, 7, 9, 7, 0, 0),
            new DateTime(2026, 7, 10, 7, 0, 0), new DateTime(2026, 7, 10, 19, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var primera = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.TrabajadorId == id)
            .OrderBy(t => t.Fecha)
            .Select(t => new { t.Fecha, t.MinutosTrabajados, t.Estado, t.EntradaReal, t.SalidaReal })
            .ToListAsync(CancellationToken.None);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var segunda = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.TrabajadorId == id)
            .OrderBy(t => t.Fecha)
            .Select(t => new { t.Fecha, t.MinutosTrabajados, t.Estado, t.EntradaReal, t.SalidaReal })
            .ToListAsync(CancellationToken.None);

        segunda.ShouldBe(primera);
        segunda.Count.ShouldBe(31);
    }

    /// <summary>
    /// Red de seguridad: ante un solapamiento en la base, manda la asignación más reciente.
    /// </summary>
    /// <remarks>
    /// Guardar esto ya no es posible —<c>ValidadorVigencias</c> lo rechaza—, pero puede
    /// haber quedado de antes o entrar por un arreglo manual. Lo que no puede pasar es que
    /// el resultado dependa del orden en que la base devuelva las filas.
    /// </remarks>
    [Fact]
    public async Task Ante_asignaciones_solapadas_gana_la_que_empezo_despues()
    {
        var id = await CrearAsync(
            (Diurno, new DateOnly(2026, 7, 1), null),
            (Nocturno, new DateOnly(2026, 7, 6), null));

        await MarcarAsync(id,
            new DateTime(2026, 7, 6, 19, 0, 0),
            new DateTime(2026, 7, 7, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var lunes = await LeerAsync(id, new DateOnly(2026, 7, 6));

        lunes.TurnoId.ShouldBe(Nocturno);
        lunes.MinutosTrabajados.ShouldBe(720);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Una rotación anotada días después, con «Rige desde», corrige el tareo al recalcular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El obrero figura de día, pero desde el lunes 20 trabaja de noche. Sin la rotación
    /// anotada, el martes junta la salida de la primera noche (07:00) con la entrada de la
    /// segunda (19:00) y sale como <b>una jornada diurna completa que nunca ocurrió</b>:
    /// puntual, 720 minutos y cero nocturnos. No aparece en ningún filtro de problemas.
    /// </para>
    /// <para>
    /// Antes de «Rige desde» no había arreglo: el cambio de turno recién regía al día
    /// siguiente de guardarlo. Ahora se anota la rotación el día en que ocurrió y basta con
    /// recalcular.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Una_rotacion_anotada_con_fecha_pasada_corrige_el_tareo_al_recalcular()
    {
        var id = await CrearAsync((Diurno, new DateOnly(2020, 1, 1), null));

        // Descansa el domingo 19 y entra a la noche el lunes 20.
        await MarcarAsync(id,
            new DateTime(2026, 7, 20, 19, 0, 0),
            new DateTime(2026, 7, 21, 7, 0, 0),
            new DateTime(2026, 7, 21, 19, 0, 0),
            new DateTime(2026, 7, 22, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var martesFantasma = await LeerAsync(id, new DateOnly(2026, 7, 21));
        martesFantasma.EntradaReal.ShouldBe(new DateTime(2026, 7, 21, 7, 0, 0));
        martesFantasma.SalidaReal.ShouldBe(new DateTime(2026, 7, 21, 19, 0, 0));
        martesFantasma.MinutosNocturnos.ShouldBe(0);

        // Se anota la rotación desde la ficha, con la fecha en que ocurrió.
        var fichas = FichasDePersonal();
        var ficha = await fichas.ObtenerAsync(id, CancellationToken.None);

        await fichas.ActualizarAsync(
            new GuardarTrabajadorRequest
            {
                Id = id,
                TipoDocumentoId = ficha.TipoDocumentoId,
                NumeroDocumento = ficha.NumeroDocumento,
                ApellidoPaterno = ficha.ApellidoPaterno,
                ApellidoMaterno = ficha.ApellidoMaterno,
                Nombres = ficha.Nombres,
                Sexo = ficha.Sexo,
                EmpresaId = ficha.EmpresaId,
                TipoPlanilla = ficha.TipoPlanilla,
                PlantaId = ficha.PlantaId,
                PuestoId = ficha.PuestoId,
                FechaIngreso = ficha.FechaIngreso,
                TurnoId = Nocturno,
                TurnoVigenteDesde = new DateOnly(2026, 7, 20)
            },
            "coordinador",
            CancellationToken.None);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var lunes = await LeerAsync(id, new DateOnly(2026, 7, 20));
        var martes = await LeerAsync(id, new DateOnly(2026, 7, 21));

        lunes.EntradaReal.ShouldBe(new DateTime(2026, 7, 20, 19, 0, 0));
        lunes.SalidaReal.ShouldBe(new DateTime(2026, 7, 21, 7, 0, 0));
        lunes.MinutosTrabajados.ShouldBe(720);
        lunes.MinutosNocturnos.ShouldBe(480);
        lunes.Estado.ShouldBe(EstadoTareo.Puntual);

        martes.EntradaReal.ShouldBe(new DateTime(2026, 7, 21, 19, 0, 0));
        martes.SalidaReal.ShouldBe(new DateTime(2026, 7, 22, 7, 0, 0));
        martes.MinutosNocturnos.ShouldBe(480);
    }

    // ------------------------------------------------------------------
    // Rotación por cuadrilla
    // ------------------------------------------------------------------

    /// <summary>
    /// Quien integra una cuadrilla se calcula cada día con el turno que el ciclo le da al
    /// grupo, sin que nadie haya tocado su ficha en la rotación.
    /// </summary>
    [Fact]
    public async Task El_integrante_de_una_cuadrilla_se_calcula_con_el_turno_de_su_ciclo()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        await Cuadrillas().ProgramarAsync(
            Ciclo(cuadrilla, new DateOnly(2026, 7, 6), (Diurno, 14), (Nocturno, 14)), "test", CancellationToken.None);

        var id = await CrearEnCuadrillaAsync(cuadrilla);

        await MarcarAsync(id,
            new DateTime(2026, 7, 13, 7, 0, 0),
            new DateTime(2026, 7, 13, 19, 0, 0),
            new DateTime(2026, 7, 20, 19, 0, 0),
            new DateTime(2026, 7, 21, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var dia = await LeerAsync(id, new DateOnly(2026, 7, 13));
        var noche = await LeerAsync(id, new DateOnly(2026, 7, 20));

        dia.TurnoId.ShouldBe(Diurno);
        dia.MinutosTrabajados.ShouldBe(720);
        dia.MinutosNocturnos.ShouldBe(0);

        noche.TurnoId.ShouldBe(Nocturno);
        noche.MinutosTrabajados.ShouldBe(720);
        noche.MinutosNocturnos.ShouldBe(480);
        noche.Estado.ShouldBe(EstadoTareo.Puntual);
        noche.Observacion.ShouldStartWith("Cuadrilla A: turno NOC.");

        // Idempotente: el turno sale de datos con vigencia, no del reloj.
        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var otraVez = await LeerAsync(id, new DateOnly(2026, 7, 20));
        otraVez.TurnoId.ShouldBe(noche.TurnoId);
        otraVez.MinutosTrabajados.ShouldBe(noche.MinutosTrabajados);
        otraVez.Observacion.ShouldBe(noche.Observacion);
        (await contexto.TareosDiarios.CountAsync(t => t.TrabajadorId == id)).ShouldBe(31);
    }

    /// <summary>
    /// La cuadrilla rotó un día después de lo programado: se corre la rotación y basta con
    /// recalcular. Es una sola edición para todo el grupo.
    /// </summary>
    [Fact]
    public async Task Correr_la_rotacion_de_la_cuadrilla_y_recalcular_corrige_a_todos_sus_integrantes()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        await Cuadrillas().ProgramarAsync(
            Ciclo(cuadrilla, new DateOnly(2026, 7, 6), (Diurno, 14), (Nocturno, 14)), "test", CancellationToken.None);

        var id = await CrearEnCuadrillaAsync(cuadrilla);

        // El lunes 20 todavía trabajó de día: la rotación real fue el martes.
        await MarcarAsync(id,
            new DateTime(2026, 7, 20, 7, 0, 0),
            new DateTime(2026, 7, 20, 19, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);
        (await LeerAsync(id, new DateOnly(2026, 7, 20))).Estado.ShouldBe(EstadoTareo.Incompleto);

        await Cuadrillas().CorrerRotacionAsync(
            new CorrerRotacionRequest { CuadrillaId = cuadrilla, Desde = new DateOnly(2026, 7, 20), Dias = 1 },
            "test",
            CancellationToken.None);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var lunes = await LeerAsync(id, new DateOnly(2026, 7, 20));
        lunes.TurnoId.ShouldBe(Diurno);
        lunes.MinutosTrabajados.ShouldBe(720);
        lunes.Estado.ShouldBe(EstadoTareo.Puntual);
    }

    /// <summary>
    /// El ciclo decide el turno de <b>cada</b> día del periodo, no solo de los que se
    /// muestrean.
    /// </summary>
    /// <remarks>
    /// Las demás pruebas de cuadrilla comprueban días sueltos —uno diurno, uno nocturno— y
    /// eso deja pasar un corrimiento de la frontera: un ciclo que rota el 21 en vez del 20
    /// sigue dando DIU el 13 y NOC el 21. Aquí se recorre julio entero y se compara con el
    /// ciclo esperado día a día, que es lo que hace visible el día de más o de menos.
    ///
    /// <para>Ciclo desde el lunes 6: DIU los 14 días del 6 al 19, NOC del 20 en adelante.
    /// Antes del 6 no hay ciclo programado, así que esos días quedan sin horario.</para>
    /// </remarks>
    [Fact]
    public async Task El_ciclo_asigna_el_turno_de_cada_dia_del_periodo_y_la_frontera_cae_donde_debe()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        await Cuadrillas().ProgramarAsync(
            Ciclo(cuadrilla, new DateOnly(2026, 7, 6), (Diurno, 14), (Nocturno, 14)), "test", CancellationToken.None);

        var id = await CrearEnCuadrillaAsync(cuadrilla);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var porFecha = await contexto.TareosDiarios
            .AsNoTracking()
            .Where(t => t.TrabajadorId == id)
            .ToDictionaryAsync(t => t.Fecha, t => t.TurnoId, CancellationToken.None);

        porFecha.Count.ShouldBe(31);

        for (var fecha = new DateOnly(2026, 7, 1); fecha <= new DateOnly(2026, 7, 31); fecha = fecha.AddDays(1))
        {
            var esperado = fecha.Day switch
            {
                < 6 => (long?)null,          // el ciclo todavía no regía
                <= 19 => Diurno,             // 6 al 19: los 14 días del primer tramo
                _ => Nocturno                // 20 en adelante: el segundo tramo
            };

            porFecha[fecha].ShouldBe(esperado, $"el {fecha:dd/MM/yyyy} salió con el turno equivocado");
        }
    }

    [Fact]
    public async Task El_servicio_conserva_como_ordinario_el_domingo_programado_como_trabajo()
    {
        var turnoHotel = new Turno
        {
            Codigo = "HOTEL",
            Nombre = "Hotel diurno",
            HoraInicio = new TimeOnly(7, 0),
            HoraFin = new TimeOnly(16, 0),
            MinutosRefrigerio = 60,
            MinutosTolerancia = 10,
            MinutosAnticipacionEntrada = 15,
            MinutosAnticipacionSalida = 5,
            MinutosJornadaOrdinaria = 480,
            DiasAplicables = DiasSemana.Todos,
            Activo = true,
            CreadoPor = "test"
        };
        turnoHotel.Versiones.Add(
            VersionTurno.De(turnoHotel, VersionTurno.VigenciaOriginal, "test"));
        await contexto.Turnos.AddAsync(turnoHotel, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var cuadrilla = await CrearCuadrillaAsync();
        var domingo = new DateOnly(2026, 7, 5);
        await Cuadrillas().ProgramarAsync(
            Ciclo(cuadrilla, domingo, (turnoHotel.Id, 1), (Nocturno, 1)),
            "test",
            CancellationToken.None);
        var trabajador = await CrearEnCuadrillaAsync(cuadrilla);

        await MarcarAsync(
            trabajador,
            domingo.ToDateTime(new TimeOnly(7, 8)),
            domingo.ToDateTime(new TimeOnly(16, 8)));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var tareo = await LeerAsync(trabajador, domingo);
        tareo.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        tareo.MinutosTrabajados.ShouldBe(480);
        tareo.MinutosOrdinarios.ShouldBe(480);
        tareo.MinutosTardanza.ShouldBe(0);
        tareo.MinutosExtra25.ShouldBe(0);
        tareo.MinutosExtra35.ShouldBe(0);
        tareo.Observacion.ShouldNotBeNull();
        tareo.Observacion!.ShouldContain("recuperó 8 min");
    }

    [Fact]
    public async Task Una_cuadrilla_sin_ciclo_deja_el_dia_sin_horario_y_dice_por_que()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        var id = await CrearEnCuadrillaAsync(cuadrilla);

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var dia = await LeerAsync(id, new DateOnly(2026, 7, 13));
        dia.Estado.ShouldBe(EstadoTareo.SinHorario);
        dia.Observacion!.ShouldContain("cuadrilla que no tenía ciclo programado");
    }

    [Fact]
    public async Task ROT_MARK_03_la_salida_del_NOC_anterior_no_se_reutiliza_en_el_descanso()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        await ProgramarConDescansoAsync(cuadrilla, new DateOnly(2026, 7, 6), Nocturno, Diurno);
        var id = await CrearEnCuadrillaAsync(cuadrilla);

        await MarcarAsync(
            id,
            new DateTime(2026, 7, 6, 19, 0, 0),
            new DateTime(2026, 7, 7, 7, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var trabajo = await LeerAsync(id, new DateOnly(2026, 7, 6));
        var descanso = await LeerAsync(id, new DateOnly(2026, 7, 7));

        trabajo.MinutosTrabajados.ShouldBe(720);
        descanso.EsDescansoProgramado.ShouldBeTrue();
        descanso.Estado.ShouldBe(EstadoTareo.Descanso);
        descanso.MinutosTrabajados.ShouldBe(0);
    }

    [Fact]
    public async Task ROT_MARK_06_descanso_puede_cerrar_en_D_mas_1_antes_de_la_siguiente_jornada()
    {
        var cuadrilla = await CrearCuadrillaAsync();
        await ProgramarConDescansoAsync(cuadrilla, new DateOnly(2026, 7, 7), null, Diurno);
        var id = await CrearEnCuadrillaAsync(cuadrilla);

        await MarcarAsync(
            id,
            new DateTime(2026, 7, 7, 20, 0, 0),
            new DateTime(2026, 7, 8, 2, 0, 0),
            new DateTime(2026, 7, 8, 7, 0, 0),
            new DateTime(2026, 7, 8, 19, 0, 0));

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var descanso = await LeerAsync(id, new DateOnly(2026, 7, 7));
        var siguiente = await LeerAsync(id, new DateOnly(2026, 7, 8));

        descanso.MinutosTrabajados.ShouldBe(360);
        descanso.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        siguiente.MinutosTrabajados.ShouldBe(720);
        siguiente.EntradaReal.ShouldBe(new DateTime(2026, 7, 8, 7, 0, 0));
    }

    private ServicioCuadrillas Cuadrillas() => new(contexto);

    private Task ProgramarConDescansoAsync(
        long cuadrillaId,
        DateOnly desde,
        long? turnoAnterior,
        long turnoSiguiente)
    {
        var tramos = new List<TramoProgramadoRequest>();
        if (turnoAnterior is { } turno)
        {
            tramos.Add(new TramoProgramadoRequest
                { Tipo = TipoTramoRotacion.Trabajo, TurnoId = turno, Dias = 1 });
        }

        tramos.Add(new TramoProgramadoRequest
            { Tipo = TipoTramoRotacion.Descanso, Dias = 1 });
        tramos.Add(new TramoProgramadoRequest
            { Tipo = TipoTramoRotacion.Trabajo, TurnoId = turnoSiguiente, Dias = 1 });

        return Cuadrillas().ProgramarAsync(
            new ProgramarCuadrillaRequest
            {
                CuadrillaId = cuadrillaId,
                Desde = desde,
                Tramos = tramos
            },
            "test",
            CancellationToken.None);
    }

    private async Task<long> CrearCuadrillaAsync() =>
        await Cuadrillas().GuardarAsync(
            new GuardarCuadrillaRequest { Codigo = "A", Nombre = "Cuadrilla A", PlantaId = PlantaP01 },
            "test",
            CancellationToken.None);

    private static ProgramarCuadrillaRequest Ciclo(
        long cuadrilla,
        DateOnly desde,
        params (long Turno, int Dias)[] tramos) =>
        new()
        {
            CuadrillaId = cuadrilla,
            Desde = desde,
            Tramos = [.. tramos.Select(t => new TramoProgramadoRequest { TurnoId = t.Turno, Dias = t.Dias })]
        };

    private async Task<long> CrearEnCuadrillaAsync(long cuadrillaId)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = "44444449",
            ApellidoPaterno = "CUADRILLA",
            ApellidoMaterno = "PRUEBA",
            Nombres = "OBRERO",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            CuadrillaId = cuadrillaId,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    /// <summary>La ficha de Personal, armada sobre la misma base que el cálculo.</summary>
    private ServicioTrabajadores FichasDePersonal()
    {
        var alcance = new AlcanceSinRestriccion();

        return new ServicioTrabajadores(
            new TrabajadorRepository(contexto),
            new ConsultaTrabajadores(contexto, alcance),
            new ConsultaCatalogos(contexto, new AlcanceSinRestriccion()),
            alcance,
            new ServicioMarcadores(contexto),
            new ServicioPeriodos(contexto));
    }

    private async Task<long> CrearAsync(params (long TurnoId, DateOnly Desde, DateOnly? Hasta)[] turnos)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = "44444443",
            ApellidoPaterno = "ROTACION",
            ApellidoMaterno = "PRUEBA",
            Nombres = "OBRERO",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        foreach (var (turnoId, desde, hasta) in turnos)
        {
            trabajador.AsignacionesTurno.Add(new AsignacionTurno
            {
                TurnoId = turnoId,
                FechaInicio = desde,
                FechaFin = hasta
            });
        }

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task MarcarAsync(long trabajadorId, params DateTime[] momentos)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "rotacion.txt",
            HashContenido = new string('d', 64),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = momentos.Length,
            FilasInsertadas = momentos.Length
        };

        await contexto.Importaciones.AddAsync(carga, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        await AsegurarNumeroAsignadoAsync(trabajadorId);

        foreach (var momento in momentos)
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                EquipoId = EquipoDePrueba,
                IdMarcador = NumeroDe(trabajadorId),
                FechaHora = momento,
                ImportacionId = carga.Id
            }, CancellationToken.None);
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<TareoDiario> LeerAsync(long trabajadorId, DateOnly fecha) =>
        await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == fecha, CancellationToken.None);

    /// <summary>Número de cada trabajador en el aparato de prueba: uno distinto por persona.</summary>
    private static string NumeroDe(long trabajadorId) => trabajadorId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// La marcación se atribuye al calcular, por el número que el trabajador tiene en el
    /// aparato: sin esta asignación sus marcaciones no serían de nadie.
    /// </summary>
    private async Task AsegurarNumeroAsignadoAsync(long trabajadorId)
    {
        if (await contexto.AsignacionesMarcador.AnyAsync(a => a.TrabajadorId == trabajadorId, CancellationToken.None))
        {
            return;
        }

        await contexto.AsignacionesMarcador.AddAsync(new AsignacionMarcador
        {
            TrabajadorId = trabajadorId,
            EquipoId = EquipoDePrueba,
            IdMarcador = NumeroDe(trabajadorId),
            FechaInicio = new DateOnly(2020, 1, 1)
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
