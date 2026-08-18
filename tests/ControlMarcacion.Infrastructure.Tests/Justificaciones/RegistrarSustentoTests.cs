using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// Registrar el sustento que faltaba.
/// </summary>
/// <remarks>
/// Es la contraparte de que el sustento ya no sea obligatorio al dar de alta. Antes, el
/// mensaje de <c>AprobarAsync</c> —«regístrelo antes de aprobar»— apuntaba a algo que el
/// sistema no permitía: no había edición de solicitudes, así que la única salida era
/// anular y volver a crearla.
///
/// Lo que estas pruebas fijan es que la acción sea <b>estrecha</b> (no toca lo que el
/// aprobador ya evaluó) y que solo valga sobre solicitudes pendientes.
/// </remarks>
public sealed class RegistrarSustentoTests : IDisposable
{
    private const long TipoDescansoMedico = 2;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioSolicitudes servicio;

    public RegistrarSustentoTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        servicio = new ServicioSolicitudes(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task Registra_el_sustento_de_una_solicitud_pendiente()
    {
        var id = await SembrarAsync(EstadoSolicitud.Pendiente, sustento: null);

        await servicio.RegistrarSustentoAsync(
            id, "  Certificado médico N.° 4471  ", null, "coordinador@gca.local", CancellationToken.None);

        var guardada = await contexto.SolicitudesAusencia.SingleAsync(s => s.Id == id);

        guardada.Sustento.ShouldBe("Certificado médico N.° 4471");
        guardada.ModificadoPor.ShouldBe("coordinador@gca.local");
    }

    /// <summary>
    /// La acción es estrecha a propósito: agrega el papel que llegó tarde, no reabre lo
    /// que el aprobador ya evaluó.
    /// </summary>
    [Fact]
    public async Task No_toca_trabajador_ni_tipo_ni_fechas()
    {
        var id = await SembrarAsync(EstadoSolicitud.Pendiente, sustento: null);
        var antes = await contexto.SolicitudesAusencia.AsNoTracking().SingleAsync(s => s.Id == id);

        await servicio.RegistrarSustentoAsync(
            id, "Certificado médico", null, "coordinador@gca.local", CancellationToken.None);

        var despues = await contexto.SolicitudesAusencia.AsNoTracking().SingleAsync(s => s.Id == id);

        despues.TrabajadorId.ShouldBe(antes.TrabajadorId);
        despues.TipoAusenciaId.ShouldBe(antes.TipoAusenciaId);
        despues.FechaInicio.ShouldBe(antes.FechaInicio);
        despues.FechaFin.ShouldBe(antes.FechaFin);
        despues.Estado.ShouldBe(EstadoSolicitud.Pendiente);
    }

    [Fact]
    public async Task Guarda_la_ruta_del_documento_cuando_se_indica()
    {
        var id = await SembrarAsync(EstadoSolicitud.Pendiente, sustento: null);

        await servicio.RegistrarSustentoAsync(
            id, "Certificado médico", @"\\servidor\rrhh\4471.pdf",
            "coordinador@gca.local", CancellationToken.None);

        var guardada = await contexto.SolicitudesAusencia.SingleAsync(s => s.Id == id);
        guardada.RutaDocumentoSustento.ShouldBe(@"\\servidor\rrhh\4471.pdf");
    }

    /// <summary>
    /// Una solicitud aprobada ya generó justificaciones que heredaron el texto del
    /// sustento. Cambiarlo después las dejaría explicando algo distinto de la solicitud
    /// que las originó, que es justo lo que una auditoría busca.
    /// </summary>
    [Theory]
    [InlineData(EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Rechazada)]
    [InlineData(EstadoSolicitud.Anulada)]
    public async Task No_se_puede_registrar_sobre_una_solicitud_ya_resuelta(EstadoSolicitud estado)
    {
        var id = await SembrarAsync(estado, sustento: null);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.RegistrarSustentoAsync(
                id, "Certificado médico", null, "coordinador@gca.local", CancellationToken.None));

        error.Message.ShouldContain(estado.ToString().ToLowerInvariant());

        var guardada = await contexto.SolicitudesAusencia.SingleAsync(s => s.Id == id);
        guardada.Sustento.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public async Task Rechaza_un_sustento_que_no_explica_nada(string sustento)
    {
        var id = await SembrarAsync(EstadoSolicitud.Pendiente, sustento: null);

        await Should.ThrowAsync<ReglaNegocioException>(
            () => servicio.RegistrarSustentoAsync(
                id, sustento, null, "coordinador@gca.local", CancellationToken.None));

        var guardada = await contexto.SolicitudesAusencia.SingleAsync(s => s.Id == id);
        guardada.Sustento.ShouldBeNull();
    }

    [Fact]
    public async Task Una_solicitud_que_no_existe_da_recurso_no_encontrado()
    {
        await Should.ThrowAsync<RecursoNoEncontradoException>(
            () => servicio.RegistrarSustentoAsync(
                999, "Certificado médico", null, "coordinador@gca.local", CancellationToken.None));
    }

    private async Task<long> SembrarAsync(EstadoSolicitud estado, string? sustento)
    {
        var solicitud = new SolicitudAusencia
        {
            TrabajadorId = 1,
            TipoAusenciaId = TipoDescansoMedico,
            FechaInicio = new DateOnly(2026, 7, 1),
            FechaFin = new DateOnly(2026, 7, 3),
            Estado = estado,
            Sustento = sustento,
            CreadoPor = "trabajador.social@gca.local"
        };

        await contexto.SolicitudesAusencia.AddAsync(solicitud);
        await contexto.SaveChangesAsync();

        return solicitud.Id;
    }
}
