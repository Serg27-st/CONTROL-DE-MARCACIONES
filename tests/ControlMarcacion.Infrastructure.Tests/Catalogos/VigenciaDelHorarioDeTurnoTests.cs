using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Catalogos;

/// <summary>
/// El horario del turno tiene vigencia por fecha: editarlo no reescribe los meses anteriores.
/// </summary>
/// <remarks>
/// <para>
/// El defecto que cierran estas pruebas era silencioso y caro: <c>Turno</c> guardaba un solo
/// horario, válido para siempre. Corregir el horario de DIU en octubre y recalcular julio
/// —por una justificación tardía, por un archivo que faltaba— recalculaba julio con el
/// horario de octubre, y las tardanzas y el sobretiempo de un mes ya pagado cambiaban sin que
/// nada en ese mes hubiera cambiado. Todo lo demás del sistema ya tenía vigencia por fecha; el
/// contenido del turno era el único que no.
/// </para>
/// <para>
/// Van sobre <b>SQLite</b> y no sobre InMemory porque parte de la garantía es el índice único
/// filtrado de «una sola versión abierta por turno», y ese proveedor ignora los índices.
/// Turnos sembrados: DIU (1) 07:00–19:00 y NOC (2) 19:00–07:00. Periodo 1 = julio de 2026.
/// </para>
/// </remarks>
public sealed class VigenciaDelHorarioDeTurnoTests : IDisposable
{
    private const long Diurno = 1;
    private const long PlantaP01 = 3;
    private const long PeriodoJulio2026 = 1;

    /// <summary>
    /// El aparato de las marcaciones de prueba. Se crea en el fixture y no se siembra: sobre
    /// SQLite la clave foránea se exige de verdad, a diferencia de InMemory.
    /// </summary>
    private readonly long equipoDePrueba;

    private static readonly DateOnly Lunes13 = new(2026, 7, 13);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioTurnos turnos;
    private readonly ServicioCalculoTareo calculo;

    public VigenciaDelHorarioDeTurnoTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador
        {
            Codigo = "P01-A", Nombre = "Planta 1", PlantaId = PlantaP01, Activo = true
        };

        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        equipoDePrueba = equipo.Id;

        turnos = new ServicioTurnos(contexto);
        calculo = new ServicioCalculoTareo(
            contexto,
            new AlcanceSinRestriccion(),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// La prueba que fija el defecto: cambiar el horario hoy y recalcular un mes anterior
    /// tiene que dar exactamente lo mismo que antes del cambio.
    /// </summary>
    [Fact]
    public async Task Cambiar_el_horario_hoy_no_mueve_el_tareo_de_un_mes_anterior()
    {
        var id = await CrearTrabajadorConTurnoAsync(Diurno);

        // Llegó puntual a las 07:00 y salió a las 19:00: jornada exacta del turno.
        await MarcarAsync(id, Lunes13.ToDateTime(new TimeOnly(7, 0)), Lunes13.ToDateTime(new TimeOnly(19, 0)));

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);
        var antes = await LeerAsync(id, Lunes13);

        antes.MinutosTardanza.ShouldBe(0);
        antes.MinutosTrabajados.ShouldBe(720);

        // En setiembre alguien mueve el turno diurno a las 08:00. Rige desde hoy.
        await CambiarHorarioAsync(Diurno, new TimeOnly(8, 0), new TimeOnly(20, 0), rigeDesde: null);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);
        var despues = await LeerAsync(id, Lunes13);

        // Sin vigencia, julio se recalculaba con el horario de setiembre: la entrada de las
        // 07:00 pasaba a ser una hora ANTES del turno y el día salía con otra jornada.
        despues.MinutosTardanza.ShouldBe(antes.MinutosTardanza);
        despues.MinutosTrabajados.ShouldBe(antes.MinutosTrabajados);
        despues.MinutosExtra25.ShouldBe(antes.MinutosExtra25);
        despues.MinutosExtra35.ShouldBe(antes.MinutosExtra35);
        despues.Estado.ShouldBe(antes.Estado);
    }

    /// <summary>
    /// La frontera cae donde dice «rige desde»: antes el horario viejo, desde esa fecha el
    /// nuevo, dentro del mismo periodo.
    /// </summary>
    [Fact]
    public async Task El_horario_nuevo_rige_desde_su_fecha_y_los_dias_previos_conservan_el_viejo()
    {
        var id = await CrearTrabajadorConTurnoAsync(Diurno);

        var martes14 = new DateOnly(2026, 7, 14);

        // Los dos días entra 07:00 y sale 19:00.
        await MarcarAsync(
            id,
            Lunes13.ToDateTime(new TimeOnly(7, 0)),
            Lunes13.ToDateTime(new TimeOnly(19, 0)),
            martes14.ToDateTime(new TimeOnly(7, 0)),
            martes14.ToDateTime(new TimeOnly(19, 0)));

        // Desde el martes el turno empieza a las 06:00: ese día entrar a las 07:00 es una
        // hora tarde, pero el lunes sigue siendo puntual.
        await CambiarHorarioAsync(Diurno, new TimeOnly(6, 0), new TimeOnly(18, 0), rigeDesde: martes14);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        (await LeerAsync(id, Lunes13)).MinutosTardanza.ShouldBe(0);
        (await LeerAsync(id, martes14)).MinutosTardanza.ShouldBe(60);
    }

    /// <summary>Renombrar el turno no parte su historia: el cálculo no mira el nombre.</summary>
    [Fact]
    public async Task Renombrar_el_turno_no_abre_una_version_nueva()
    {
        var actual = await turnos.ObtenerAsync(Diurno, CancellationToken.None);
        actual.ShouldNotBeNull();

        await turnos.GuardarAsync(Igual(actual) with { Nombre = "Diurno planta" }, "coordinador", CancellationToken.None);

        (await VersionesDe(Diurno)).Count.ShouldBe(1);
    }

    /// <summary>
    /// El horario estaba mal desde el principio: se corrige la versión vigente en sitio en
    /// vez de abrir otra con vigencia de cero días.
    /// </summary>
    [Fact]
    public async Task Una_fecha_anterior_al_inicio_de_la_version_vigente_la_corrige_en_sitio()
    {
        await CambiarHorarioAsync(
            Diurno,
            new TimeOnly(6, 30),
            new TimeOnly(18, 30),
            rigeDesde: VersionTurno.VigenciaOriginal.AddDays(-1));

        var versiones = await VersionesDe(Diurno);

        versiones.Count.ShouldBe(1);
        versiones[0].HoraInicio.ShouldBe(new TimeOnly(6, 30));
        versiones[0].FechaInicio.ShouldBe(VersionTurno.VigenciaOriginal);
    }

    /// <summary>
    /// Un periodo cerrado no admite que le cambien el horario bajo los pies: al siguiente
    /// recálculo cambiaría un mes ya pagado.
    /// </summary>
    [Fact]
    public async Task No_se_puede_abrir_una_version_que_alcance_un_periodo_cerrado()
    {
        var periodo = await contexto.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);
        periodo.Estado = EstadoPeriodo.Cerrado;
        await contexto.SaveChangesAsync(CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            CambiarHorarioAsync(Diurno, new TimeOnly(8, 0), new TimeOnly(20, 0), rigeDesde: Lunes13));

        error.Message.ShouldContain("cerrado");
        (await VersionesDe(Diurno)).Count.ShouldBe(1);
    }

    /// <summary>
    /// Cada turno sembrado nace con su versión abierta y con el mismo horario que el turno.
    /// </summary>
    /// <remarks>
    /// Es el guardia contra la deriva entre las dos filas. <c>Turno</c> guarda lo que se edita
    /// en el catálogo y <c>VersionTurno</c> lo que usa el cálculo: si se separan, la pantalla
    /// muestra un horario y el tareo usa otro, sin que nada falle.
    /// </remarks>
    [Fact]
    public async Task Todo_turno_tiene_una_version_abierta_con_su_mismo_horario()
    {
        var activos = await contexto.Turnos.AsNoTracking()
            .Where(t => t.Activo)
            .ToListAsync(CancellationToken.None);
        var abiertas = await contexto.VersionesTurno.AsNoTracking()
            .Where(v => v.Activo && v.FechaFin == null)
            .ToListAsync(CancellationToken.None);

        activos.ShouldNotBeEmpty();
        abiertas.Count.ShouldBe(activos.Count);

        foreach (var turno in activos)
        {
            var abierta = abiertas.Where(v => v.TurnoId == turno.Id).ShouldHaveSingleItem();
            abierta.MismoHorarioQue(turno).ShouldBeTrue($"el turno {turno.Codigo} derivó de su versión abierta");
        }
    }

    // ------------------------------------------------------------------

    private async Task<List<VersionTurno>> VersionesDe(long turnoId) =>
        await contexto.VersionesTurno.AsNoTracking()
            .Where(v => v.TurnoId == turnoId && v.Activo)
            .OrderBy(v => v.FechaInicio)
            .ToListAsync(CancellationToken.None);

    private async Task CambiarHorarioAsync(long turnoId, TimeOnly inicio, TimeOnly fin, DateOnly? rigeDesde)
    {
        var actual = await turnos.ObtenerAsync(turnoId, CancellationToken.None);
        actual.ShouldNotBeNull();

        await turnos.GuardarAsync(
            Igual(actual) with { HoraInicio = inicio, HoraFin = fin, RigeDesde = rigeDesde },
            "coordinador",
            CancellationToken.None);
    }

    /// <summary>El turno tal como está, para cambiarle un solo campo con <c>with</c>.</summary>
    private static GuardarTurnoRequest Igual(TurnoListaDto turno) =>
        new()
        {
            Id = turno.Id,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            MinutosRefrigerio = turno.MinutosRefrigerio,
            MinutosTolerancia = turno.MinutosTolerancia,
            MinutosAnticipacionEntrada = turno.MinutosAnticipacionEntrada,
            MinutosAnticipacionSalida = turno.MinutosAnticipacionSalida,
            MinutosGraciaSalida = turno.MinutosGraciaSalida,
            MinutosJornadaOrdinaria = turno.MinutosJornadaOrdinaria,
            DiasAplicables = turno.DiasAplicables,
            Activo = turno.Activo
        };

    private async Task<long> CrearTrabajadorConTurnoAsync(long turnoId)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = "44444451",
            ApellidoPaterno = "HORARIO",
            ApellidoMaterno = "PRUEBA",
            Nombres = "OBRERO",
            Sexo = Sexo.Masculino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = turnoId,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        await contexto.AsignacionesMarcador.AddAsync(new AsignacionMarcador
        {
            TrabajadorId = trabajador.Id,
            EquipoId = equipoDePrueba,
            IdMarcador = "77",
            FechaInicio = new DateOnly(2020, 1, 1)
        }, CancellationToken.None);

        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task MarcarAsync(long trabajadorId, params DateTime[] momentos)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "horario.txt",
            EquipoId = equipoDePrueba,
            HashContenido = new string('h', 64),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = momentos.Length,
            FilasInsertadas = momentos.Length
        };

        await contexto.Importaciones.AddAsync(carga, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var numero = await contexto.AsignacionesMarcador
            .Where(a => a.TrabajadorId == trabajadorId)
            .Select(a => a.IdMarcador)
            .FirstAsync(CancellationToken.None);

        foreach (var momento in momentos)
        {
            await contexto.MarcacionesCrudas.AddAsync(new MarcacionCruda
            {
                EquipoId = equipoDePrueba,
                IdMarcador = numero,
                FechaHora = momento,
                ImportacionId = carga.Id
            }, CancellationToken.None);
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<Domain.Asistencia.TareoDiario> LeerAsync(long trabajadorId, DateOnly fecha) =>
        await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == fecha, CancellationToken.None);
}
