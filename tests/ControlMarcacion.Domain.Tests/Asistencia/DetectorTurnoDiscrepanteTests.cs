using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Detección de días cuyas marcas contradicen el turno que el padrón creía vigente.
/// </summary>
/// <remarks>
/// El escenario es el de julio de 2026: la cuadrilla figura en diurno y el lunes 20 trabajó
/// de noche porque rotó antes de lo anotado. Todas las fechas son literales.
/// </remarks>
public sealed class DetectorTurnoDiscrepanteTests
{
    private static readonly DateOnly Lunes20 = new(2026, 7, 20);

    private static Turno Diurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(19, 0),
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno Nocturno() => new()
    {
        Id = 2,
        Codigo = "NOC",
        Nombre = "Nocturno",
        HoraInicio = new TimeOnly(19, 0),
        HoraFin = new TimeOnly(7, 0),
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static DiscrepanciaTurno? Detectar(Turno asignado, params DateTime[] marcas) =>
        DetectorTurnoDiscrepante.Detectar(Lunes20, asignado, [Diurno(), Nocturno()], marcas);

    /// <summary>
    /// El caso que motiva todo: figura en diurno y trabajó la noche entera.
    /// </summary>
    /// <remarks>
    /// Calculado contra el turno diurno, ese día sale con una tardanza de doce horas y un
    /// sobretiempo que nunca ocurrió, sin que nada lo señale. El detector lo manda a revisar.
    /// </remarks>
    [Fact]
    public void Una_noche_trabajada_figurando_en_diurno_se_detecta()
    {
        var discrepancia = Detectar(
            Diurno(),
            new DateTime(2026, 7, 20, 19, 0, 0),
            new DateTime(2026, 7, 21, 7, 0, 0));

        discrepancia.ShouldNotBeNull();
        discrepancia.TurnoQueEncaja.Codigo.ShouldBe("NOC");
        discrepancia.DesvioQueEncaja.ShouldBe(0);
        discrepancia.DesvioProgramado.ShouldBe(1440);
        discrepancia.Explicacion.ShouldContain("NOC");
    }

    [Fact]
    public void Un_dia_coherente_con_su_turno_no_se_señala()
    {
        Detectar(
            Diurno(),
            new DateTime(2026, 7, 20, 7, 0, 0),
            new DateTime(2026, 7, 20, 19, 0, 0))
            .ShouldBeNull();
    }

    /// <summary>
    /// Una tardanza grande sigue siendo una tardanza, no otro turno.
    /// </summary>
    /// <remarks>
    /// Es la confusión que haría inútil la bandeja: si llegar seis horas tarde se propusiera
    /// como «turno equivocado», el descuento por tardanza se convertiría en una sugerencia de
    /// reescribir el horario.
    /// </remarks>
    [Fact]
    public void Una_tardanza_grande_no_se_confunde_con_otro_turno()
    {
        Detectar(
            Diurno(),
            new DateTime(2026, 7, 20, 13, 0, 0),
            new DateTime(2026, 7, 20, 19, 0, 0))
            .ShouldBeNull();
    }

    /// <summary>
    /// Las salidas a las 21:06 del archivo real son sobretiempo, no un turno distinto.
    /// </summary>
    [Fact]
    public void Un_dia_con_sobretiempo_largo_no_se_señala()
    {
        Detectar(
            Diurno(),
            new DateTime(2026, 7, 20, 7, 0, 0),
            new DateTime(2026, 7, 20, 21, 6, 0))
            .ShouldBeNull();
    }

    /// <summary>
    /// Con una sola marca el día ya sale «Incompleto»: no hay de dónde deducir el turno.
    /// </summary>
    [Fact]
    public void Con_una_sola_marca_no_se_deduce_nada()
    {
        Detectar(Diurno(), new DateTime(2026, 7, 20, 19, 0, 0)).ShouldBeNull();
    }

    /// <summary>
    /// Si las marcas no encajan en ningún turno, no se propone «el menos malo».
    /// </summary>
    [Fact]
    public void Unas_marcas_dispersas_no_encajan_en_ningun_turno()
    {
        Detectar(
            Diurno(),
            new DateTime(2026, 7, 20, 10, 0, 0),
            new DateTime(2026, 7, 20, 14, 0, 0))
            .ShouldBeNull();
    }

    /// <summary>El turno asignado dentro de los candidatos no se propone a sí mismo.</summary>
    [Fact]
    public void El_turno_asignado_no_se_propone_a_si_mismo()
    {
        DetectorTurnoDiscrepante.Detectar(
            Lunes20,
            Diurno(),
            [Diurno()],
            [new DateTime(2026, 7, 20, 7, 0, 0), new DateTime(2026, 7, 20, 19, 0, 0)])
            .ShouldBeNull();
    }

    /// <summary>
    /// El detector no toca el cálculo: solo mira las marcas y los horarios.
    /// </summary>
    /// <remarks>
    /// Se fija aquí porque es la garantía que permite activarlo sin un ADR: señala días, no
    /// reescribe turnos ni minutos. Corregir la rotación sigue siendo una decisión con autor.
    /// </remarks>
    [Fact]
    public void Detectar_no_modifica_el_turno_que_recibe()
    {
        var asignado = Diurno();

        Detectar(asignado,
            new DateTime(2026, 7, 20, 19, 0, 0),
            new DateTime(2026, 7, 21, 7, 0, 0));

        asignado.HoraInicio.ShouldBe(new TimeOnly(7, 0));
        asignado.HoraFin.ShouldBe(new TimeOnly(19, 0));
    }
}
