using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

public sealed class DescansoSustitutorioAcceptanceTests
{
    private static readonly DateOnly Feriado = new(2026, 7, 28);
    private static readonly DateOnly Descanso = new(2026, 7, 30);

    [Fact]
    public void FER003_03_dia_completo_no_crea_minutos_ni_incidencias()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Descanso,
            TieneVinculo = true,
            EsDescansoSustitutorio = true,
            FechaFeriadoCompensado = Feriado
        });

        resultado.Estado.ShouldBe(EstadoTareo.DescansoSustitutorio);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
        resultado.RequiereValidacion.ShouldBeFalse();
        resultado.TipoIncidencia.ShouldBeNull();
    }

    [Fact]
    public void FER003_04_fecha_anterior_o_igual_es_rechazada()
    {
        Should.Throw<ReglaNegocioException>(() =>
            DescansoSustitutorio.Otorgar(1, Feriado, Feriado, "coordinador", DateTime.Now));
        Should.Throw<ReglaNegocioException>(() =>
            DescansoSustitutorio.Otorgar(1, Feriado, Feriado.AddDays(-1), "coordinador", DateTime.Now));
    }

    [Fact]
    public void FER003_07_anulacion_conserva_historia_y_libera_el_efecto()
    {
        var entidad = DescansoSustitutorio.Otorgar(
            1, Feriado, Descanso, "coordinador", new DateTime(2026, 7, 20, 8, 0, 0));

        entidad.Anular("Registro equivocado", "administrador", new DateTime(2026, 7, 21, 9, 0, 0));

        entidad.Estado.ShouldBe(EstadoDescansoSustitutorio.Anulado);
        entidad.Activo.ShouldBeFalse();
        entidad.AnuladoPorUsuario.ShouldBe("administrador");
        entidad.MotivoAnulacion.ShouldBe("Registro equivocado");
        entidad.Version.ShouldBe(1);
        entidad.FechaFeriadoOrigen.ShouldBe(Feriado);
        entidad.FechaDescanso.ShouldBe(Descanso);
    }

    [Fact]
    public void marcaciones_en_el_dia_sustitutorio_exigen_revision_sin_inventar_jornada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Descanso,
            TieneVinculo = true,
            EsDescansoSustitutorio = true,
            FechaFeriadoCompensado = Feriado,
            Marcaciones =
            [
                Descanso.ToDateTime(new TimeOnly(7, 0)),
                Descanso.ToDateTime(new TimeOnly(17, 0))
            ]
        });

        resultado.Estado.ShouldBe(EstadoTareo.PendienteValidacion);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.TrabajoEnDescansoSustitutorio);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.MinutosTrabajados.ShouldBe(0);
    }
}
