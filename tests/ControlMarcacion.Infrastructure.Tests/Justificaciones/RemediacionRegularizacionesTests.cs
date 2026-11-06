using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

public sealed class RemediacionRegularizacionesTests : IDisposable
{
    private static readonly DateOnly Fecha = new(2026, 7, 1);
    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly MarcacionDbContext contexto;
    private readonly ServicioJustificaciones justificaciones;
    private readonly ServicioSolicitudes solicitudes;

    public RemediacionRegularizacionesTests()
    {
        conexion.Open();
        contexto = new MarcacionDbContext(new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();
        justificaciones = new ServicioJustificaciones(contexto, new AlcanceSinRestriccion());
        solicitudes = new ServicioSolicitudes(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    [Fact]
    public async Task REG_APR_001_manual_nace_pendiente_y_exige_otro_aprobador()
    {
        var trabajador = await CrearTrabajadorAsync("89000001");
        var id = await justificaciones.CrearManualAsync(new CrearJustificacionManualRequest
        {
            TrabajadorId = trabajador.Id,
            Fecha = Fecha,
            Tipo = TipoJustificacion.Tardanza,
            MinutosJustificados = 10,
            Motivo = "Movilidad verificada por supervisión"
        }, "solicitante", CancellationToken.None);

        var creada = await contexto.Justificaciones.AsNoTracking().SingleAsync(j => j.Id == id);
        creada.Estado.ShouldBe(EstadoJustificacion.Solicitada);
        creada.ResueltaPorUsuario.ShouldBeNull();

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            justificaciones.ResolverAsync(id, true, "Aprobada", "solicitante", CancellationToken.None));

        await justificaciones.ResolverAsync(id, true, "Sustento conforme", "coordinador", CancellationToken.None);
        var aprobada = await contexto.Justificaciones.AsNoTracking().SingleAsync(j => j.Id == id);
        aprobada.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        aprobada.ResueltaPorUsuario.ShouldBe("coordinador");
    }

    [Fact]
    public async Task REG_TURNO_001_aprobacion_crea_excepcion_de_un_dia_y_preserva_historia()
    {
        var trabajador = await CrearTrabajadorAsync("89000002");
        var turnoAlterno = await contexto.Turnos.AsNoTracking().Where(t => t.Id != 1 && t.Activo).Select(t => t.Id).FirstAsync();

        var id = await solicitudes.GuardarCambioTurnoAsync(new GuardarCambioTurnoRequest
        {
            TrabajadorId = trabajador.Id,
            FechaEfectiva = Fecha,
            TurnoId = turnoAlterno,
            Motivo = "Cobertura puntual autorizada por operación"
        }, "solicitante", CancellationToken.None);

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            solicitudes.ResolverCambioTurnoAsync(id, true, null, "solicitante", CancellationToken.None));

        await solicitudes.ResolverCambioTurnoAsync(id, true, "Cobertura confirmada", "coordinador", CancellationToken.None);

        var tramos = await contexto.AsignacionesTurno.AsNoTracking()
            .Where(a => a.TrabajadorId == trabajador.Id).OrderBy(a => a.FechaInicio).ToListAsync();
        tramos.Single(a => a.EstaVigenteEn(Fecha.AddDays(-1))).TurnoId.ShouldBe(1);
        var excepcion = tramos.Single(a => a.EstaVigenteEn(Fecha));
        excepcion.TurnoId.ShouldBe(turnoAlterno);
        excepcion.FechaInicio.ShouldBe(Fecha);
        excepcion.FechaFin.ShouldBe(Fecha);
        tramos.Single(a => a.EstaVigenteEn(Fecha.AddDays(1))).TurnoId.ShouldBe(1);

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            solicitudes.ResolverCambioTurnoAsync(id, true, null, "otro", CancellationToken.None));
    }

    private async Task<Trabajador> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = 1,
            NumeroDocumento = documento,
            ApellidoPaterno = "QA",
            ApellidoMaterno = "REMEDIACION",
            Nombres = "PRUEBA",
            Sexo = Sexo.Femenino,
            EmpresaId = 2,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = 3,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };
        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = 1,
            FechaInicio = new DateOnly(2020, 1, 1)
        });
        trabajador.AbrirVinculo("test");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync();
        return trabajador;
    }
}
