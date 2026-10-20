using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// El permiso por horas se descuenta del extremo de la jornada donde realmente cae.
/// </summary>
/// <remarks>
/// Antes todo permiso se registraba como una tardanza justificada. Un permiso de 15:00 a
/// 17:00 descontaba de la tardanza —que ese día valía cero— y dejaba intactos los 120
/// minutos de salida anticipada: el permiso estaba aprobado y el día seguía marcando el
/// desvío. Solo funcionaba por casualidad cuando el permiso arrancaba con el turno.
///
/// Las horas son literales y el turno es 08:00–17:00 con refrigerio 0, que es el ejemplo
/// con el que se discutió la regla.
/// </remarks>
public class PermisoParcialTests
{
    /// <summary>Miércoles 1 de julio de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private static Turno TurnoDeOficina() => new()
    {
        Id = 1,
        Codigo = "OFI",
        Nombre = "Oficina",
        HoraInicio = new TimeOnly(8, 0),
        HoraFin = new TimeOnly(17, 0),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno TurnoNocturno() => new()
    {
        Id = 2,
        Codigo = "NOC",
        Nombre = "Nocturno",
        HoraInicio = new TimeOnly(19, 0),
        HoraFin = new TimeOnly(7, 0),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static JustificacionAplicable Permiso(TimeOnly desde, TimeOnly hasta) =>
        new()
        {
            Id = 55,
            Tipo = TipoJustificacion.PermisoParcial,
            ComputaComoAsistencia = true,
            Comportamiento = ComportamientoAusencia.PermisoParcial,
            MinutosJustificados = (int)(hasta.ToTimeSpan() - desde.ToTimeSpan()).TotalMinutes,
            InicioPermiso = desde,
            FinPermiso = hasta,
            Motivo = "Trámite personal autorizado"
        };

    private static ResultadoCalculo Calcular(
        Turno turno,
        JustificacionAplicable? permiso,
        params DateTime[] marcaciones) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = turno,
            Marcaciones = marcaciones,
            Justificacion = permiso
        });

    /// <summary>
    /// El caso del enunciado: turno 08:00–17:00, permiso 08:00–10:00, entrada 10:02.
    /// De los 122 minutos de tardanza, el permiso cubre 120 y quedan 2.
    /// </summary>
    [Fact]
    public void Un_permiso_al_inicio_descuenta_de_la_tardanza()
    {
        var sinPermiso = Calcular(
            TurnoDeOficina(), null,
            new DateTime(2026, 7, 1, 10, 2, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        sinPermiso.MinutosTardanza.ShouldBe(122);

        var conPermiso = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(8, 0), new TimeOnly(10, 0)),
            new DateTime(2026, 7, 1, 10, 2, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        conPermiso.MinutosTardanza.ShouldBe(2);
        conPermiso.MinutosSalidaAnticipada.ShouldBe(0);
    }

    /// <summary>
    /// El caso que estaba roto: el permiso es de 15:00 a 17:00 y la persona se va a las
    /// 15:00. Antes el descuento iba a la tardanza, que valía cero, y los 120 minutos de
    /// salida anticipada quedaban registrados pese al permiso aprobado.
    /// </summary>
    [Fact]
    public void Un_permiso_al_final_descuenta_de_la_salida_anticipada()
    {
        var sinPermiso = Calcular(
            TurnoDeOficina(), null,
            new DateTime(2026, 7, 1, 8, 0, 0),
            new DateTime(2026, 7, 1, 15, 0, 0));

        sinPermiso.MinutosSalidaAnticipada.ShouldBe(120);

        var conPermiso = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(15, 0), new TimeOnly(17, 0)),
            new DateTime(2026, 7, 1, 8, 0, 0),
            new DateTime(2026, 7, 1, 15, 0, 0));

        conPermiso.MinutosSalidaAnticipada.ShouldBe(0);
        conPermiso.MinutosTardanza.ShouldBe(0);
    }

    /// <summary>
    /// Un permiso en mitad de la jornada no descuenta nada, y la observación lo dice: el
    /// motor toma la primera y la última marca, así que ese tramo no produjo desvío que
    /// perdonar. Quien audita tiene que poder distinguir "no había nada que descontar" de
    /// "el permiso no se aplicó".
    /// </summary>
    [Fact]
    public void Un_permiso_en_mitad_de_la_jornada_no_descuenta_nada_y_lo_declara()
    {
        var resultado = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(12, 0), new TimeOnly(13, 0)),
            new DateTime(2026, 7, 1, 8, 0, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        resultado.MinutosTardanza.ShouldBe(0);
        resultado.MinutosSalidaAnticipada.ShouldBe(0);
        resultado.Observacion.ShouldContain("no hubo minutos que descontar");
    }

    /// <summary>
    /// En un turno que cruza medianoche las 05:00 del permiso son las del día siguiente.
    /// Anclarlas al día de inicio dejaría la ventana fuera de la jornada y el permiso no
    /// descontaría nada.
    /// </summary>
    [Fact]
    public void En_turno_nocturno_el_permiso_se_ancla_al_dia_que_corresponde()
    {
        var resultado = Calcular(
            TurnoNocturno(), Permiso(new TimeOnly(5, 0), new TimeOnly(7, 0)),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 5, 0, 0));

        resultado.MinutosSalidaAnticipada.ShouldBe(0);
    }

    /// <summary>
    /// El permiso perdona el desvío; no reescribe la jornada. Los minutos trabajados
    /// siguen siendo los que hubo entre la primera y la última marca.
    /// </summary>
    /// <remarks>
    /// ⚠️ Si GCA confirma que un permiso con goce debe pagar las horas permitidas, esta
    /// prueba es la que hay que cambiar — a propósito: el cambio tiene que ser una
    /// decisión explícita y no un efecto colateral.
    /// </remarks>
    [Fact]
    public void Un_permiso_no_inventa_minutos_trabajados_ni_sobretiempo()
    {
        var resultado = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(15, 0), new TimeOnly(17, 0)),
            new DateTime(2026, 7, 1, 8, 0, 0),
            new DateTime(2026, 7, 1, 15, 0, 0));

        resultado.MinutosTrabajados.ShouldBe(420);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void El_permiso_deja_su_rastro_en_el_dia()
    {
        var resultado = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(8, 0), new TimeOnly(10, 0)),
            new DateTime(2026, 7, 1, 10, 2, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        resultado.JustificacionId.ShouldBe(55);
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Tardanza));
        resultado.Observacion.ShouldContain("PERMISO PARCIAL de 08:00 a 10:00");
        resultado.Observacion.ShouldContain("120 min de tardanza");
        resultado.Observacion.ShouldContain("Trámite personal autorizado");
    }

    /// <summary>
    /// Un permiso que cubre el inicio entero deja el día puntual, igual que una tardanza
    /// perdonada del todo.
    /// </summary>
    [Fact]
    public void Un_permiso_que_cubre_toda_la_tardanza_deja_el_dia_puntual()
    {
        var resultado = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(8, 0), new TimeOnly(10, 0)),
            new DateTime(2026, 7, 1, 10, 0, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        resultado.MinutosTardanza.ShouldBe(0);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
    }

    /// <summary>
    /// Un permiso de dos horas no justifica un día entero sin marcar: sin marcas no hay
    /// tramo que perdonar y el día sigue siendo falta.
    /// </summary>
    [Fact]
    public void Un_permiso_por_horas_no_convierte_una_falta_en_dia_justificado()
    {
        var resultado = Calcular(
            TurnoDeOficina(), Permiso(new TimeOnly(8, 0), new TimeOnly(10, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Falta);
        resultado.MinutosTrabajados.ShouldBe(0);
    }
}
