using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
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

        servicio = new ServicioCalculoTareo(contexto);
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

        foreach (var momento in momentos)
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                TrabajadorId = trabajadorId,
                IdMarcador = "1",
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
}
