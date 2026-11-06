using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// AUS-007: aprobar una solicitud es una sola decisión para todo su rango. No puede quedar
/// aprobada si uno de sus días no recibió la justificación que materializa esa decisión.
/// </summary>
public sealed class AprobacionAtomicaSolicitudesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TipoVacaciones = 1;

    private static readonly DateOnly PrimeroDeJulio = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioSolicitudes servicio;

    public AprobacionAtomicaSolicitudesTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        servicio = new ServicioSolicitudes(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task AUS007_01_un_choque_intermedio_rechaza_toda_la_aprobacion()
    {
        var trabajadorId = await CrearTrabajadorAsync("77700701");
        var solicitudId = await CrearSolicitudPendienteAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(2));

        contexto.Justificaciones.Add(new Justificacion
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = PrimeroDeJulio.AddDays(1),
            Origen = OrigenJustificacion.Manual,
            TipoAusenciaId = TipoVacaciones,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Ausencia ya resuelta manualmente",
            RegistradoPorUsuario = "admin-previo",
            RegistradoEn = new DateTime(2026, 7, 2, 8, 0, 0),
            CreadoPor = "admin-previo"
        });
        await contexto.SaveChangesAsync(CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.AprobarAsync(solicitudId, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("no se aprobó", Case.Insensitive);
        error.Message.ShouldContain("justificación", Case.Insensitive);

        contexto.ChangeTracker.Clear();

        var solicitud = await contexto.SolicitudesAusencia
            .AsNoTracking()
            .SingleAsync(s => s.Id == solicitudId);

        solicitud.Estado.ShouldBe(EstadoSolicitud.Pendiente);
        (await contexto.Justificaciones.AsNoTracking().CountAsync()).ShouldBe(1);
        (await contexto.Justificaciones.AsNoTracking()
                .CountAsync(j => j.SolicitudAusenciaId == solicitudId))
            .ShouldBe(0);
    }

    [Fact]
    public async Task AUS007_02_un_dia_sin_periodo_rechaza_toda_la_aprobacion()
    {
        var trabajadorId = await CrearTrabajadorAsync("77700702");
        var solicitudId = await CrearSolicitudPendienteAsync(
            trabajadorId,
            PrimeroDeJulio.AddDays(-1),
            PrimeroDeJulio);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.AprobarAsync(solicitudId, "coordinador", CancellationToken.None));

        error.Message.ShouldContain("no se aprobó", Case.Insensitive);
        error.Message.ShouldContain("ningún periodo", Case.Insensitive);

        contexto.ChangeTracker.Clear();

        (await contexto.SolicitudesAusencia.AsNoTracking()
                .SingleAsync(s => s.Id == solicitudId))
            .Estado.ShouldBe(EstadoSolicitud.Pendiente);
        (await contexto.Justificaciones.AsNoTracking()
                .CountAsync(j => j.SolicitudAusenciaId == solicitudId))
            .ShouldBe(0);
    }

    [Fact]
    public async Task AUS007_03_un_rango_valido_se_aprueba_completo()
    {
        var trabajadorId = await CrearTrabajadorAsync("77700703");
        var solicitudId = await CrearSolicitudPendienteAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(2));

        var resultado = await servicio.AprobarAsync(
            solicitudId,
            "coordinador",
            CancellationToken.None);

        resultado.JustificacionesCreadas.ShouldBe(3);
        resultado.HuboOmisiones.ShouldBeFalse();

        contexto.ChangeTracker.Clear();

        (await contexto.SolicitudesAusencia.AsNoTracking()
                .SingleAsync(s => s.Id == solicitudId))
            .Estado.ShouldBe(EstadoSolicitud.Aprobada);
        (await contexto.Justificaciones.AsNoTracking()
                .CountAsync(j => j.SolicitudAusenciaId == solicitudId))
            .ShouldBe(3);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "ATOMICIDAD",
            Nombres = "SOLICITUD",
            Sexo = Sexo.Femenino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task<long> CrearSolicitudPendienteAsync(
        long trabajadorId,
        DateOnly inicio,
        DateOnly fin)
    {
        var solicitud = new SolicitudAusencia
        {
            TrabajadorId = trabajadorId,
            TipoAusenciaId = TipoVacaciones,
            FechaInicio = inicio,
            FechaFin = fin,
            Estado = EstadoSolicitud.Pendiente,
            CreadoPor = "admin"
        };

        contexto.SolicitudesAusencia.Add(solicitud);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return solicitud.Id;
    }
}
