using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// Un día sin archivo cargado no puede salir como falta, y con el archivo cargado sí.
/// </summary>
/// <remarks>
/// <para>
/// Es el defecto más caro que tenía el sistema. En Detergentes hay dos aparatos —apellidos
/// A-L y M-Z—; cargar uno y olvidar el otro dejaba el mes «cubierto» en el calendario y
/// sacaba una falta por cada persona del aparato olvidado, con una observación que afirmaba
/// lo que el sistema no sabía: «no registró ninguna marcación».
/// </para>
/// <para>
/// Va contra el <b>ServicioImportacion real</b>, no contra un doble: lo que se prueba es
/// justamente que el motor y el calendario compartan una sola regla de cobertura. Con un
/// doble, la prueba pasaría aunque cada uno contara los días a su manera.
/// </para>
/// </remarks>
public sealed class FaltaMasivaPorArchivoSinCargarTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const long EmpresaConsulting = 1;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    /// <summary>Miércoles 1 de julio de 2026: los dos marcan.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    /// <summary>Jueves 2: solo se cargó el archivo del A-L.</summary>
    private static readonly DateOnly Jueves = new(2026, 7, 2);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo calculo;
    private readonly long aparatoAL;
    private readonly long aparatoMZ;

    public FaltaMasivaPorArchivoSinCargarTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var al = new EquipoMarcador { Codigo = "DET-AL", Nombre = "Detergentes A-L", PlantaId = PlantaDetergentes, Activo = true };
        var mz = new EquipoMarcador { Codigo = "DET-MZ", Nombre = "Detergentes M-Z", PlantaId = PlantaDetergentes, Activo = true };

        contexto.EquiposMarcador.AddRange(al, mz);
        contexto.SaveChanges();

        aparatoAL = al.Id;
        aparatoMZ = mz.Id;

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });

        var importacion = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));

        calculo = new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), importacion, NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// El jueves solo se cargó el A-L: quien marca en el M-Z no tiene falta, tiene un día
    /// que nadie puede juzgar todavía.
    /// </summary>
    [Fact]
    public async Task El_dia_sin_archivo_de_su_aparato_queda_pendiente_y_no_en_falta()
    {
        var deAL = await CrearTrabajadorAsync("11111101", aparatoAL, "10");
        var deMZ = await CrearTrabajadorAsync("11111102", aparatoMZ, "10");

        // Miércoles: los dos aparatos entregaron archivo y los dos trabajaron.
        await MarcarAsync(aparatoAL, "10", Miercoles);
        await MarcarAsync(aparatoMZ, "10", Miercoles);

        // Jueves: solo llegó el archivo del A-L, y su gente marcó. Del M-Z no hay nada.
        await MarcarAsync(aparatoAL, "10", Jueves);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var juevesAL = await DiaAsync(deAL, Jueves);
        var juevesMZ = await DiaAsync(deMZ, Jueves);

        // Quien marcó, trabajó.
        juevesAL.Estado.ShouldBe(EstadoTareo.Puntual);

        // Y quien está en el aparato olvidado NO tiene falta.
        juevesMZ.Estado.ShouldBe(EstadoTareo.PendienteValidacion);
        juevesMZ.TipoIncidencia.ShouldBe(TipoIncidencia.SinCoberturaDeCarga);
        juevesMZ.RequiereValidacion.ShouldBeTrue();
        juevesMZ.Observacion.ShouldNotBeNull();
        juevesMZ.Observacion.ShouldContain("No se cargó el archivo");
    }

    /// <summary>
    /// La cobertura no es una amnistía: con el archivo de su aparato cargado, quien no marcó
    /// sigue teniendo falta.
    /// </summary>
    [Fact]
    public async Task Con_el_archivo_de_su_aparato_cargado_el_que_no_marco_tiene_falta()
    {
        var presente = await CrearTrabajadorAsync("11111103", aparatoAL, "20");
        var ausente = await CrearTrabajadorAsync("11111104", aparatoAL, "21");

        await MarcarAsync(aparatoAL, "20", Miercoles);
        await MarcarAsync(aparatoMZ, "99", Miercoles);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        (await DiaAsync(presente, Miercoles)).Estado.ShouldBe(EstadoTareo.Puntual);

        var falta = await DiaAsync(ausente, Miercoles);
        falta.Estado.ShouldBe(EstadoTareo.Falta);
        falta.TipoIncidencia.ShouldBeNull();
    }

    /// <summary>
    /// Cargar el archivo que faltaba y recalcular resuelve la incidencia sola, sin que nadie
    /// tenga que regularizar nada. Es la salida correcta, y por eso la observación la nombra
    /// antes que la regularización.
    /// </summary>
    [Fact]
    public async Task Cargar_el_archivo_que_faltaba_resuelve_el_dia()
    {
        var deMZ = await CrearTrabajadorAsync("11111105", aparatoMZ, "30");

        await MarcarAsync(aparatoAL, "30", Jueves);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);
        (await DiaAsync(deMZ, Jueves)).Estado.ShouldBe(EstadoTareo.PendienteValidacion);

        // Llega el archivo olvidado.
        await MarcarAsync(aparatoMZ, "30", Jueves);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        var resuelto = await DiaAsync(deMZ, Jueves);
        resuelto.Estado.ShouldBe(EstadoTareo.Puntual);
        resuelto.RequiereValidacion.ShouldBeFalse();
        resuelto.TipoIncidencia.ShouldBeNull();
        resuelto.MinutosTrabajados.ShouldBe(720);
    }

    // ------------------------------------------------------------------

    private async Task<TareoDiario> DiaAsync(long trabajadorId, DateOnly fecha) =>
        await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == fecha);

    private async Task<long> CrearTrabajadorAsync(string documento, long equipoId, string numero)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "COBERTURA",
            Nombres = "APARATO",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaConsulting,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaDetergentes,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = TurnoDiurno,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = numero,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    /// <summary>Jornada completa de 07:00 a 19:00 en ese aparato y ese día.</summary>
    private async Task MarcarAsync(long equipoId, string numero, DateOnly fecha)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = $"{equipoId}-{fecha:yyyyMMdd}.txt",
            EquipoId = equipoId,
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 2,
            FilasInsertadas = 2
        };

        contexto.Importaciones.Add(carga);
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.MarcacionesCrudas.AddRange(
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = fecha.ToDateTime(new TimeOnly(7, 0)),
                ImportacionId = carga.Id
            },
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = fecha.ToDateTime(new TimeOnly(19, 0)),
                ImportacionId = carga.Id
            });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
