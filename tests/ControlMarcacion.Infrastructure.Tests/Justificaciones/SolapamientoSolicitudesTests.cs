using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Comun;
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
/// AUS-006: el solape se decide contra las solicitudes persistidas del mismo trabajador.
/// Pendiente y aprobada reservan el rango; rechazada y anulada lo liberan.
/// </summary>
public sealed class SolapamientoSolicitudesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long TipoLicenciaConGoce = 3;

    private static readonly DateOnly PrimeroDeJulio = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioSolicitudes servicio;

    public SolapamientoSolicitudesTests()
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

    [Theory]
    [InlineData(EstadoSolicitud.Pendiente, true)]
    [InlineData(EstadoSolicitud.Aprobada, true)]
    [InlineData(EstadoSolicitud.Rechazada, false)]
    [InlineData(EstadoSolicitud.Anulada, false)]
    public async Task AUS006_01_los_estados_vigentes_bloquean_y_los_terminales_liberan_el_rango(
        EstadoSolicitud estadoExistente,
        bool debeBloquear)
    {
        var trabajadorId = await CrearTrabajadorAsync($"66006{(int)estadoExistente:00}");
        await CrearSolicitudAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(2),
            estadoExistente);

        var nueva = CrearRequest(
            trabajadorId,
            PrimeroDeJulio.AddDays(1),
            PrimeroDeJulio.AddDays(3));

        if (debeBloquear)
        {
            var error = await Should.ThrowAsync<ReglaNegocioException>(
                servicio.GuardarAsync(nueva, "coordinador", CancellationToken.None));

            error.Message.ShouldContain("se cruza", Case.Insensitive);
            (await contexto.SolicitudesAusencia.AsNoTracking().CountAsync()).ShouldBe(1);
            return;
        }

        var nuevaId = await servicio.GuardarAsync(
            nueva,
            "coordinador",
            CancellationToken.None);

        nuevaId.ShouldBeGreaterThan(0);
        (await contexto.SolicitudesAusencia.AsNoTracking().CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task AUS006_02_el_extremo_compartido_se_solapa_y_el_dia_siguiente_no()
    {
        var trabajadorId = await CrearTrabajadorAsync("66006020");
        await CrearSolicitudAsync(
            trabajadorId,
            PrimeroDeJulio,
            PrimeroDeJulio.AddDays(2),
            EstadoSolicitud.Pendiente);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicio.GuardarAsync(
                CrearRequest(
                    trabajadorId,
                    PrimeroDeJulio.AddDays(2),
                    PrimeroDeJulio.AddDays(2)),
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain("se cruza", Case.Insensitive);

        var siguienteId = await servicio.GuardarAsync(
            CrearRequest(
                trabajadorId,
                PrimeroDeJulio.AddDays(3),
                PrimeroDeJulio.AddDays(3)),
            "coordinador",
            CancellationToken.None);

        siguienteId.ShouldBeGreaterThan(0);
        (await contexto.SolicitudesAusencia.AsNoTracking().CountAsync()).ShouldBe(2);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "SOLAPE",
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

    private async Task CrearSolicitudAsync(
        long trabajadorId,
        DateOnly inicio,
        DateOnly fin,
        EstadoSolicitud estado)
    {
        contexto.SolicitudesAusencia.Add(new SolicitudAusencia
        {
            TrabajadorId = trabajadorId,
            TipoAusenciaId = TipoLicenciaConGoce,
            FechaInicio = inicio,
            FechaFin = fin,
            Estado = estado,
            CreadoPor = "admin"
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private static GuardarSolicitudRequest CrearRequest(
        long trabajadorId,
        DateOnly inicio,
        DateOnly fin) =>
        new()
        {
            TrabajadorId = trabajadorId,
            TipoAusenciaId = TipoLicenciaConGoce,
            FechaInicio = inicio,
            FechaFin = fin
        };
}
