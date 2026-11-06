using ControlMarcacion.Domain.Catalogos;
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
/// AUS-008: anular una solicitud deja sin efecto sus justificaciones, pero conserva las
/// filas que prueban qué días estuvieron justificados, quién las anuló y por qué.
/// </summary>
public sealed class AnulacionPreservaEvidenciaSolicitudesTests : IDisposable
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

    public AnulacionPreservaEvidenciaSolicitudesTests()
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
    public async Task AUS008_01_anular_conserva_y_cancela_las_justificaciones_derivadas()
    {
        var solicitudId = await CrearYAprobarAsync(
            "88800801",
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(1));

        var originales = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.SolicitudAusenciaId == solicitudId)
            .OrderBy(j => j.Fecha)
            .Select(j => new { j.Id, j.Fecha, j.TrabajadorId, j.TipoAusenciaId, j.CreadoEn })
            .ToListAsync();

        await servicio.AnularAsync(
            solicitudId,
            "  Corrección autorizada  ",
            "coordinador",
            CancellationToken.None);

        contexto.ChangeTracker.Clear();

        var solicitud = await contexto.SolicitudesAusencia
            .AsNoTracking()
            .SingleAsync(s => s.Id == solicitudId);
        var conservadas = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.SolicitudAusenciaId == solicitudId)
            .OrderBy(j => j.Fecha)
            .ToListAsync();

        solicitud.Estado.ShouldBe(EstadoSolicitud.Anulada);
        solicitud.MotivoAnulacion.ShouldBe("Corrección autorizada");
        conservadas.Count.ShouldBe(2);
        conservadas.Select(j => j.Id).ShouldBe(originales.Select(j => j.Id));
        conservadas.Select(j => j.Fecha).ShouldBe(originales.Select(j => j.Fecha));
        conservadas.Select(j => j.TrabajadorId).ShouldBe(originales.Select(j => j.TrabajadorId));
        conservadas.Select(j => j.TipoAusenciaId).ShouldBe(originales.Select(j => j.TipoAusenciaId));
        conservadas.Select(j => j.CreadoEn).ShouldBe(originales.Select(j => j.CreadoEn));
        conservadas.ShouldAllBe(j => j.Estado == EstadoJustificacion.Cancelada);
        conservadas.ShouldAllBe(j => j.ResueltaPorUsuario == "coordinador");
        conservadas.ShouldAllBe(j => j.ResueltaEn != null);
        conservadas.ShouldAllBe(j => j.MotivoResolucion == "Corrección autorizada");
        conservadas.ShouldAllBe(j => j.ModificadoPor == "coordinador");
        conservadas.ShouldAllBe(j => j.Version == 1);
    }

    [Fact]
    public async Task AUS008_02_la_evidencia_cancelada_no_bloquea_una_solicitud_correctiva()
    {
        var trabajadorId = await CrearTrabajadorAsync("88800802");
        var primeraId = await CrearSolicitudPendienteAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio);

        await servicio.AprobarAsync(primeraId, "coordinador", CancellationToken.None);
        await servicio.AnularAsync(
            primeraId,
            "Fechas equivocadas",
            "coordinador",
            CancellationToken.None);

        var segundaId = await CrearSolicitudPendienteAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio);

        var resultado = await servicio.AprobarAsync(
            segundaId,
            "coordinador",
            CancellationToken.None);

        resultado.JustificacionesCreadas.ShouldBe(1);

        contexto.ChangeTracker.Clear();

        var historial = await contexto.Justificaciones
            .AsNoTracking()
            .Where(j => j.TrabajadorId == trabajadorId && j.Fecha == PrimeroDeJulio)
            .OrderBy(j => j.Id)
            .ToListAsync();

        historial.Count.ShouldBe(2);
        historial[0].SolicitudAusenciaId.ShouldBe(primeraId);
        historial[0].Estado.ShouldBe(EstadoJustificacion.Cancelada);
        historial[1].SolicitudAusenciaId.ShouldBe(segundaId);
        historial[1].Estado.ShouldBe(EstadoJustificacion.Aprobada);
    }

    [Fact]
    public async Task AUS008_03_periodo_cerrado_rechaza_toda_la_anulacion()
    {
        var solicitudId = await CrearYAprobarAsync(
            "88800803",
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(1));

        var periodo = await contexto.Periodos.SingleAsync(p => p.Id == PeriodoJulio2026);
        periodo.Estado = EstadoPeriodo.Cerrado;
        periodo.Version++;
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.ChangeTracker.Clear();

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.AnularAsync(
                solicitudId,
                "Corrección fuera de cierre",
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("periodo cerrado", Case.Insensitive);

        contexto.ChangeTracker.Clear();

        (await contexto.SolicitudesAusencia.AsNoTracking().SingleAsync(s => s.Id == solicitudId))
            .Estado.ShouldBe(EstadoSolicitud.Aprobada);
        (await contexto.Justificaciones.AsNoTracking()
                .Where(j => j.SolicitudAusenciaId == solicitudId)
                .ToListAsync())
            .ShouldAllBe(j => j.Estado == EstadoJustificacion.Aprobada);
    }

    [Fact]
    public async Task AUS008_04_doble_post_no_vuelve_a_mutar_la_evidencia()
    {
        var solicitudId = await CrearYAprobarAsync(
            "88800804",
            PrimeroDeJulio,
            PrimeroDeJulio);

        await servicio.AnularAsync(
            solicitudId,
            "Primera anulación",
            "coordinador",
            CancellationToken.None);

        contexto.ChangeTracker.Clear();

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.AnularAsync(
                solicitudId,
                "Segundo envío",
                "otro-coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("solo se puede anular", Case.Insensitive);

        contexto.ChangeTracker.Clear();

        var evidencia = await contexto.Justificaciones
            .AsNoTracking()
            .SingleAsync(j => j.SolicitudAusenciaId == solicitudId);

        evidencia.Estado.ShouldBe(EstadoJustificacion.Cancelada);
        evidencia.MotivoResolucion.ShouldBe("Primera anulación");
        evidencia.ResueltaPorUsuario.ShouldBe("coordinador");
        evidencia.Version.ShouldBe(1);
    }

    [Fact]
    public async Task AUS008_06_evidencia_preexistente_incompleta_se_rechaza_sin_normalizarla()
    {
        var solicitudId = await CrearYAprobarAsync(
            "88800806",
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(1));

        // Simula una inconsistencia histórica previa a AUS-007/AUS-008. El saneamiento no
        // pertenece a la operación normal de anulación y no debe inventar la fila perdida.
        var perdida = await contexto.Justificaciones
            .SingleAsync(j => j.SolicitudAusenciaId == solicitudId
                              && j.Fecha == PrimeroDeJulio.AddDays(1));
        contexto.Justificaciones.Remove(perdida);
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.ChangeTracker.Clear();

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.AnularAsync(
                solicitudId,
                "Intento sobre historia incompleta",
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("evidencia", Case.Insensitive);
        error.Message.ShouldContain("incompleta", Case.Insensitive);

        contexto.ChangeTracker.Clear();

        (await contexto.SolicitudesAusencia.AsNoTracking().SingleAsync(s => s.Id == solicitudId))
            .Estado.ShouldBe(EstadoSolicitud.Aprobada);
        var sobreviviente = await contexto.Justificaciones
            .AsNoTracking()
            .SingleAsync(j => j.SolicitudAusenciaId == solicitudId);
        sobreviviente.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        sobreviviente.Version.ShouldBe(0);
    }

    private async Task<long> CrearYAprobarAsync(string documento, DateOnly inicio, DateOnly fin)
    {
        var trabajadorId = await CrearTrabajadorAsync(documento);
        var solicitudId = await CrearSolicitudPendienteAsync(trabajadorId, inicio, fin);

        await servicio.AprobarAsync(solicitudId, "coordinador", CancellationToken.None);

        return solicitudId;
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "EVIDENCIA",
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
