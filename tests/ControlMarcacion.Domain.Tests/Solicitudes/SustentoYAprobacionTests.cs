using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Solicitudes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Solicitudes;

/// <summary>
/// Cuándo el listado puede ofrecer «Aprobar».
/// </summary>
/// <remarks>
/// <see cref="SolicitudListaDto.PuedeAprobarse"/> tiene que coincidir con lo que
/// <c>ServicioSolicitudes.AprobarAsync</c> acepta, que exige dos cosas: que la solicitud
/// esté <b>pendiente</b> y que tenga <b>sustento</b> si su tipo lo requiere. Si las dos
/// reglas se separan, el listado ofrece un botón que el servidor rechaza — que es
/// justamente lo que hacía antes.
///
/// El servidor sigue comprobándolo por su cuenta: esto solo evita ofrecer la acción.
/// </remarks>
public sealed class SustentoYAprobacionTests
{
    [Fact]
    public void Sin_sustento_cuando_el_tipo_lo_exige_no_se_puede_aprobar()
    {
        var solicitud = Crear(requiereSustento: true, sustento: null);

        solicitud.FaltaSustento.ShouldBeTrue();
        solicitud.AdmiteDecision.ShouldBeTrue();
        solicitud.PuedeAprobarse.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_sustento_en_blanco_cuenta_como_ausente(string sustento)
    {
        var solicitud = Crear(requiereSustento: true, sustento: sustento);

        solicitud.FaltaSustento.ShouldBeTrue();
        solicitud.PuedeAprobarse.ShouldBeFalse();
    }

    [Fact]
    public void Con_sustento_registrado_se_puede_aprobar()
    {
        var solicitud = Crear(requiereSustento: true, sustento: "Certificado médico N.° 4471");

        solicitud.FaltaSustento.ShouldBeFalse();
        solicitud.PuedeAprobarse.ShouldBeTrue();
    }

    /// <summary>
    /// Un tipo que no exige sustento se aprueba sin él: no todos lo requieren —solo
    /// descanso médico, licencias y subsidios en el catálogo sembrado—.
    /// </summary>
    [Fact]
    public void Si_el_tipo_no_exige_sustento_su_ausencia_no_bloquea_nada()
    {
        var solicitud = Crear(requiereSustento: false, sustento: null);

        solicitud.FaltaSustento.ShouldBeFalse();
        solicitud.PuedeAprobarse.ShouldBeTrue();
    }

    /// <summary>
    /// Tener sustento no basta: una solicitud ya resuelta no vuelve a decidirse. Es la
    /// otra mitad de la regla de AprobarAsync.
    /// </summary>
    [Theory]
    [InlineData(EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Rechazada)]
    [InlineData(EstadoSolicitud.Anulada)]
    public void Una_solicitud_ya_resuelta_no_se_puede_aprobar(EstadoSolicitud estado)
    {
        var solicitud = Crear(requiereSustento: true, sustento: "Certificado", estado: estado);

        solicitud.AdmiteDecision.ShouldBeFalse();
        solicitud.PuedeAprobarse.ShouldBeFalse();
    }

    /// <summary>
    /// El aviso de «Falta» solo tiene sentido mientras se pueda hacer algo: en una
    /// solicitud ya rechazada la carencia es historia, no una tarea pendiente.
    /// </summary>
    [Fact]
    public void El_sustento_faltante_de_una_solicitud_rechazada_ya_no_es_accionable()
    {
        var solicitud = Crear(
            requiereSustento: true, sustento: null, estado: EstadoSolicitud.Rechazada);

        solicitud.FaltaSustento.ShouldBeTrue();
        solicitud.AdmiteDecision.ShouldBeFalse();
    }

    private static SolicitudListaDto Crear(
        bool requiereSustento,
        string? sustento,
        EstadoSolicitud estado = EstadoSolicitud.Pendiente) =>
        new(
            Id: 1,
            TrabajadorId: 1,
            Trabajador: "HUAMAN ROJAS, MARIA ELENA",
            TipoAusencia: "Descanso médico",
            ComportamientoEtiqueta: "Subsidiada",
            FechaInicio: new DateOnly(2026, 7, 1),
            FechaFin: new DateOnly(2026, 7, 3),
            HoraInicio: null,
            HoraFin: null,
            Estado: estado,
            RequiereSustento: requiereSustento,
            Sustento: sustento,
            Observacion: null,
            MotivoAnulacion: null,
            SubtipoVacaciones: null,
            PeriodoVacacional: null,
            JustificacionesGeneradas: 0);
}
