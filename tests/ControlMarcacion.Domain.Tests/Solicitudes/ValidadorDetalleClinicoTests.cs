using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Solicitudes;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Solicitudes;

/// <summary>
/// Pruebas del detalle clínico exigible a una solicitud.
/// </summary>
/// <remarks>
/// Qué tipos lo exigen se decide por el comportamiento del catálogo, no por una lista de
/// códigos. Los tipos se construyen igual que están sembrados en <c>DatosSemilla</c>.
/// </remarks>
public class ValidadorDetalleClinicoTests
{
    private static TipoAusencia DescansoMedico() => new()
    {
        Id = 2,
        Codigo = "DM",
        Nombre = "Descanso médico",
        Comportamiento = ComportamientoAusencia.Subsidiada,
        MaximoDiasPorAnioCalendario = 20
    };

    private static TipoAusencia SubsidioAccidente() => new()
    {
        Id = 9,
        Codigo = "SUBA",
        Nombre = "Subsidio por accidente",
        Comportamiento = ComportamientoAusencia.Subsidiada
    };

    private static TipoAusencia Vacaciones() => new()
    {
        Id = 1,
        Codigo = "VAC",
        Nombre = "Vacaciones",
        Comportamiento = ComportamientoAusencia.JustificadaRemunerada
    };

    [Fact]
    public void Los_tipos_subsidiados_exigen_detalle_clinico()
    {
        ValidadorDetalleClinico.Exige(DescansoMedico()).ShouldBeTrue();
        ValidadorDetalleClinico.Exige(SubsidioAccidente()).ShouldBeTrue();
    }

    [Fact]
    public void Un_tipo_no_subsidiado_no_lo_exige()
    {
        ValidadorDetalleClinico.Exige(Vacaciones()).ShouldBeFalse();
    }

    [Fact]
    public void Descanso_medico_completo_es_valido()
    {
        var problema = ValidadorDetalleClinico.Validar(
            DescansoMedico(),
            Contingencia.EnfermedadComun,
            "Lumbalgia aguda",
            CentroAtencion.Essalud);

        problema.ShouldBeNull();
    }

    [Fact]
    public void Descanso_medico_sin_contingencia_se_rechaza()
    {
        var problema = ValidadorDetalleClinico.Validar(
            DescansoMedico(), null, "Lumbalgia aguda", CentroAtencion.Essalud);

        problema.ShouldNotBeNull();
        problema.ShouldContain("contingencia");
    }

    [Fact]
    public void Descanso_medico_sin_centro_de_atencion_se_rechaza()
    {
        var problema = ValidadorDetalleClinico.Validar(
            DescansoMedico(), Contingencia.EnfermedadComun, "Lumbalgia aguda", null);

        problema.ShouldNotBeNull();
        problema.ShouldContain("centro de atención");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Descanso_medico_sin_diagnostico_se_rechaza(string? diagnostico)
    {
        var problema = ValidadorDetalleClinico.Validar(
            DescansoMedico(), Contingencia.EnfermedadComun, diagnostico, CentroAtencion.Essalud);

        problema.ShouldNotBeNull();
        problema.ShouldContain("diagnóstico");
    }

    [Fact]
    public void Diagnostico_mas_largo_que_la_columna_se_rechaza()
    {
        var largo = new string('a', ValidadorDetalleClinico.LargoMaximoDiagnostico + 1);

        var problema = ValidadorDetalleClinico.Validar(
            DescansoMedico(), Contingencia.EnfermedadComun, largo, CentroAtencion.Essalud);

        problema.ShouldNotBeNull();
        problema.ShouldContain("250");
    }

    [Fact]
    public void Vacaciones_sin_detalle_clinico_es_valido()
    {
        ValidadorDetalleClinico.Validar(Vacaciones(), null, null, null).ShouldBeNull();
    }

    [Fact]
    public void Depurar_conserva_el_detalle_en_un_tipo_subsidiado()
    {
        var (contingencia, diagnostico, centro) = ValidadorDetalleClinico.Depurar(
            DescansoMedico(), Contingencia.AccidenteLaboral, "  Fractura de radio  ",
            CentroAtencion.Particular);

        contingencia.ShouldBe(Contingencia.AccidenteLaboral);
        diagnostico.ShouldBe("Fractura de radio");
        centro.ShouldBe(CentroAtencion.Particular);
    }

    [Fact]
    public void Depurar_borra_el_detalle_si_el_tipo_no_lo_admite()
    {
        // Es la protección contra el caso real: una solicitud registrada como descanso
        // médico que se corrige a vacaciones no puede quedarse con el diagnóstico guardado.
        var (contingencia, diagnostico, centro) = ValidadorDetalleClinico.Depurar(
            Vacaciones(), Contingencia.EnfermedadComun, "Lumbalgia aguda", CentroAtencion.Essalud);

        contingencia.ShouldBeNull();
        diagnostico.ShouldBeNull();
        centro.ShouldBeNull();
    }
}
