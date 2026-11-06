using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// Contratos directos FER-004: el catálogo de feriados se consulta con la fecha de inicio
/// de la jornada nocturna, no con la fecha calendario de su salida.
/// </summary>
public sealed class FeriadoFechaInicioAcceptanceTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;

    private static readonly DateOnly DiaPrevio = new(2026, 7, 27);
    private static readonly DateOnly Feriado = new(2026, 7, 28);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;
    private readonly long equipoId;
    private readonly long turnoId;

    public FeriadoFechaInicioAcceptanceTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador
        {
            Codigo = "FER004-EQ",
            Nombre = "Equipo FER-004",
            PlantaId = PlantaP01,
            Activo = true
        };
        var turno = new Turno
        {
            Codigo = "FER004-NOC",
            Nombre = "Nocturno FER-004",
            HoraInicio = new TimeOnly(19, 0),
            HoraFin = new TimeOnly(5, 22),
            MinutosJornadaOrdinaria = 622,
            DiasAplicables = DiasSemana.Todos,
            Activo = true
        };
        turno.Versiones.Add(new VersionTurno
        {
            FechaInicio = VersionTurno.VigenciaOriginal,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            MinutosRefrigerio = turno.MinutosRefrigerio,
            MinutosTolerancia = turno.MinutosTolerancia,
            MinutosAnticipacionEntrada = turno.MinutosAnticipacionEntrada,
            MinutosAnticipacionSalida = turno.MinutosAnticipacionSalida,
            MinutosGraciaSalida = turno.MinutosGraciaSalida,
            MinutosJornadaOrdinaria = turno.MinutosJornadaOrdinaria,
            DiasAplicables = turno.DiasAplicables,
            CreadoPor = "prueba"
        });

        contexto.EquiposMarcador.Add(equipo);
        contexto.Turnos.Add(turno);
        contexto.Feriados.Add(new Feriado
        {
            Fecha = Feriado,
            Descripcion = "Fiestas Patrias",
            PlantaId = null,
            Activo = true
        });
        contexto.SaveChanges();

        equipoId = equipo.Id;
        turnoId = turno.Id;
        servicio = new ServicioCalculoTareo(
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

    [Fact]
    public async Task FER004_01_cruce_hacia_feriado_conserva_clasificacion_del_dia_de_inicio()
    {
        var trabajadorId = await CrearTrabajadorYMarcasAsync(DiaPrevio, "74004001");

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "prueba", CancellationToken.None);

        var resultado = await DiaAsync(trabajadorId, DiaPrevio);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.MinutosTrabajados.ShouldBe(622);
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public async Task FER004_02_inicio_en_feriado_conserva_clasificacion_hasta_dia_siguiente()
    {
        var trabajadorId = await CrearTrabajadorYMarcasAsync(Feriado, "74004002");

        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "prueba", CancellationToken.None);

        var resultado = await DiaAsync(trabajadorId, Feriado);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.MinutosTrabajados.ShouldBe(622);
        // El tareo conserva la distribución física; FER-002 hace que la liquidación no
        // vuelva a pagar estos ordinarios y valore una sola base como feriado trabajado.
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    private async Task<long> CrearTrabajadorYMarcasAsync(DateOnly fechaInicio, string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "FERIADO",
            ApellidoMaterno = "CRUCE",
            Nombres = "PRUEBA",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };
        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = turnoId,
            FechaInicio = new DateOnly(2020, 1, 1)
        });
        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = documento,
            FechaInicio = new DateOnly(2020, 1, 1)
        });
        trabajador.AbrirVinculo("prueba");

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();

        var carga = new ImportacionMarcador
        {
            NombreArchivo = $"fer004-{fechaInicio:yyyyMMdd}.txt",
            EquipoId = equipoId,
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "prueba",
            FilasLeidas = 2,
            FilasInsertadas = 2
        };
        contexto.Importaciones.Add(carga);
        await contexto.SaveChangesAsync();

        contexto.MarcacionesCrudas.AddRange(
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = documento,
                FechaHora = fechaInicio.ToDateTime(new TimeOnly(19, 0)),
                ImportacionId = carga.Id
            },
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = documento,
                FechaHora = fechaInicio.AddDays(1).ToDateTime(new TimeOnly(5, 22)),
                ImportacionId = carga.Id
            });
        await contexto.SaveChangesAsync();

        return trabajador.Id;
    }

    private Task<TareoDiario> DiaAsync(long trabajadorId, DateOnly fecha) =>
        contexto.TareosDiarios.AsNoTracking()
            .SingleAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == fecha);
}
