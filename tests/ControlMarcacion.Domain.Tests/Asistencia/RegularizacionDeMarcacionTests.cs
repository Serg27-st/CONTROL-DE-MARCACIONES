using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// La hora que el marcador no registró, aportada por quien autoriza.
/// </summary>
/// <remarks>
/// Es el caso rector del sistema: el equipo registra la salida de las 17:03, la persona
/// olvidó marcar al llegar y el supervisor verifica que entró 07:58.
///
/// <b>La marcación original no se toca.</b> La hora regularizada es un dato aparte, con
/// autor y motivo, que el cálculo suma encima — la diferencia entre corregir un tareo y
/// falsificar un registro biométrico.
/// </remarks>
public class RegularizacionDeMarcacionTests
{
    /// <summary>Miércoles 1 de julio de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private static Turno Oficina() => new()
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

    private static Turno Diurno12h() => new()
    {
        Id = 2,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(19, 0),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno DiurnoRegular()
    {
        var turno = Diurno12h();
        turno.HoraFin = new TimeOnly(17, 22);
        return turno;
    }

    private static JustificacionAplicable Regularizacion(
        DateTime? entrada = null,
        DateTime? salida = null) =>
        new()
        {
            Id = 31,
            Tipo = TipoJustificacion.MarcacionRegularizada,
            ComputaComoAsistencia = false,
            EntradaRegularizada = entrada,
            SalidaRegularizada = salida,
            Motivo = "Olvido de marcación, verificado por el supervisor"
        };

    /// <summary>
    /// El enunciado, literal: turno 08:00–17:00, el marcador tiene 17:03 y nada más, el
    /// supervisor verifica que entró 07:58.
    /// </summary>
    [Fact]
    public void La_entrada_verificada_completa_la_jornada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Marcaciones = [new DateTime(2026, 7, 1, 17, 3, 0)],
            Justificacion = Regularizacion(entrada: new DateTime(2026, 7, 1, 7, 58, 0))
        });

        // Se computa desde la hora del turno. R-P0-03.1: ordinario hasta el fin teórico
        // (17:00) y los 3 min posteriores no alcanzan el umbral de 15: 08:00 a 17:00 = 540.
        resultado.MinutosTrabajados.ShouldBe(540);
        resultado.MinutosOrdinarios.ShouldBe(540);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
    }

    /// <summary>
    /// <b>Lo que no se puede negociar:</b> la marcación del equipo sigue siendo la del
    /// equipo. La regularización se lee en la observación, no suplantando el dato duro.
    /// </summary>
    [Fact]
    public void La_marcacion_original_no_se_toca()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Marcaciones = [new DateTime(2026, 7, 1, 17, 3, 0)],
            Justificacion = Regularizacion(entrada: new DateTime(2026, 7, 1, 7, 58, 0))
        });

        // El marcador solo registró la salida: eso es lo que el día conserva.
        resultado.EntradaReal.ShouldBeNull();
        resultado.SalidaReal.ShouldBe(new DateTime(2026, 7, 1, 17, 3, 0));

        resultado.Observacion.ShouldContain("REGULARIZADO");
        resultado.Observacion.ShouldContain("entrada verificada 01/07 07:58");
        resultado.Observacion.ShouldContain("se conservan sin cambios");
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Incompleto));
        resultado.JustificacionId.ShouldBe(31);
    }

    /// <summary>
    /// La regularización devuelve el sobretiempo que la ausencia justificada borraba.
    /// </summary>
    /// <remarks>
    /// En el turno de doce horas de GCA, declarar el día ausencia justificada pagaba 480
    /// planos: cuatro horas de jornada y las cuatro de sobretiempo se evaporaban. Aquí el
    /// día se calcula como la jornada que realmente ocurrió.
    /// </remarks>
    [Fact]
    public void La_jornada_regularizada_conserva_su_sobretiempo()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = DiurnoRegular(),
            Marcaciones = [new DateTime(2026, 7, 1, 7, 0, 0)],
            Justificacion = Regularizacion(salida: new DateTime(2026, 7, 1, 19, 0, 0))
        });

        resultado.MinutosTrabajados.ShouldBe(720);

        // R-P0-03.1: DIU regular 07:00–17:22; 17:22–19:00 = 98 min al 25 %.
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(98);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    /// <summary>
    /// El día en que se olvidaron las dos marcas: nada lo delata, así que no es una
    /// incidencia detectable, pero sí se regulariza aportando las dos horas.
    /// </summary>
    [Fact]
    public void Se_pueden_aportar_las_dos_horas()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Justificacion = Regularizacion(
                entrada: new DateTime(2026, 7, 1, 8, 0, 0),
                salida: new DateTime(2026, 7, 1, 17, 0, 0))
        });

        resultado.MinutosTrabajados.ShouldBe(540);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Falta));
    }

    /// <summary>
    /// La hora verificada no borra la tardanza: si entró tarde, entró tarde. Regularizar es
    /// completar el dato que falta, no perdonarlo — para eso está la justificación de
    /// tardanza, que es otra decisión y deja otro rastro.
    /// </summary>
    [Fact]
    public void Una_entrada_verificada_tarde_sigue_siendo_tardanza()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Marcaciones = [new DateTime(2026, 7, 1, 17, 0, 0)],
            Justificacion = Regularizacion(entrada: new DateTime(2026, 7, 1, 8, 40, 0))
        });

        resultado.MinutosTardanza.ShouldBe(40);
        resultado.Estado.ShouldBe(EstadoTareo.Tardanza);
    }

    /// <summary>
    /// Una regularización que no alcanza para armar una jornada deja el día como estaba y lo
    /// dice. No se inventa el otro extremo.
    /// </summary>
    [Fact]
    public void Una_regularizacion_a_medias_no_inventa_el_otro_extremo()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Justificacion = Regularizacion(entrada: new DateTime(2026, 7, 1, 8, 0, 0))
        });

        resultado.Estado.ShouldBe(EstadoTareo.Falta);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.Observacion.ShouldContain("REGULARIZACIÓN INCOMPLETA");
    }

    /// <summary>
    /// Una hora incoherente —salida anterior a la entrada— tampoco se fuerza.
    /// </summary>
    [Fact]
    public void Una_hora_incoherente_no_produce_jornada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Oficina(),
            Marcaciones = [new DateTime(2026, 7, 1, 8, 0, 0)],
            Justificacion = Regularizacion(salida: new DateTime(2026, 7, 1, 6, 0, 0))
        });

        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.Observacion.ShouldContain("REGULARIZACIÓN INCOMPLETA");
    }
}
