using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Qué día se paga y cuál no, cuando hay una justificación de por medio.
/// </summary>
/// <remarks>
/// Hasta ahora la decisión colgaba de <c>ComputaComoAsistencia</c>, una casilla editable
/// desde la pantalla de tipos de ausencia. Con esa casilla en verdadero —que es como venía
/// sembrada la licencia sin goce— el motor pagaba ocho horas de un día expresamente no
/// remunerado. Estas pruebas fijan que la decisión la tome el <b>comportamiento</b> del
/// tipo, que es su definición legal y no una preferencia de pantalla.
/// </remarks>
public class PagoDelDiaJustificadoTests
{
    /// <summary>Miércoles 1 de julio de 2026: día laborable normal.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private static Turno TurnoDiurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
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
        HoraFin = new TimeOnly(5, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static ResultadoCalculo DiaSinMarcar(
        ComportamientoAusencia? comportamiento,
        bool computaComoAsistencia = true) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = new JustificacionAplicable
            {
                Id = 77,
                Tipo = TipoJustificacion.Ausencia,
                ComputaComoAsistencia = computaComoAsistencia,
                Comportamiento = comportamiento,
                Motivo = "Justificación de prueba"
            }
        });

    /// <summary>
    /// El caso que estaba mal en producción: <c>LSG</c> se sembró con la casilla en
    /// verdadero y el motor le pagaba la jornada ordinaria completa.
    /// </summary>
    [Fact]
    public void Una_licencia_sin_goce_no_paga_el_dia_aunque_compute_como_asistencia()
    {
        var resultado = DiaSinMarcar(
            ComportamientoAusencia.JustificadaNoRemunerada,
            computaComoAsistencia: true);

        resultado.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(0);
    }

    [Fact]
    public void Unas_vacaciones_siguen_pagando_la_jornada_ordinaria()
    {
        var resultado = DiaSinMarcar(ComportamientoAusencia.JustificadaRemunerada);

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(0);
    }

    /// <summary>
    /// AUS-PAG-01: la licencia pagada acredita ocho horas efectivas, no los 622 minutos
    /// de reloj que separan el inicio y el fin del turno DIU vigente.
    /// </summary>
    [Fact]
    public void Una_licencia_pagada_completa_en_Diu_acredita_480_y_no_622()
    {
        var resultado = DiaSinMarcar(ComportamientoAusencia.JustificadaRemunerada);

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosTrabajados.ShouldNotBe(622);
    }

    /// <summary>
    /// AUS-PAG-03/04/05: NOC acredita la misma base de ocho horas. La ausencia por sí
    /// sola no inventa sobretiempo ni bono nocturno.
    /// </summary>
    [Fact]
    public void Una_licencia_pagada_completa_en_Noc_acredita_480_sin_HE_ni_nocturnidad()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoNocturno(),
            Justificacion = new JustificacionAplicable
            {
                Id = 79,
                Tipo = TipoJustificacion.Ausencia,
                ComputaComoAsistencia = true,
                Comportamiento = ComportamientoAusencia.JustificadaRemunerada,
                Motivo = "Licencia con goce completa"
            }
        });

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosTrabajados.ShouldNotBe(622);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
        resultado.MinutosExtra100.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(0);
    }

    /// <summary>
    /// El descanso médico lo paga EsSalud y no el empleador, pero para el tareo el día
    /// sigue contando con su jornada: esta prueba existe para que el arreglo de la licencia
    /// sin goce no se lleve por delante al subsidio, que es un comportamiento distinto.
    /// </summary>
    [Fact]
    public void Un_dia_subsidiado_sigue_computando_la_jornada_ordinaria()
    {
        var resultado = DiaSinMarcar(ComportamientoAusencia.Subsidiada);

        resultado.MinutosOrdinarios.ShouldBe(480);
    }

    /// <summary>
    /// Una justificación sin tipo de ausencia —completar una marca impar, perdonar una
    /// tardanza— no tiene nada que diga que el día se paga, así que no se paga.
    /// </summary>
    /// <remarks>
    /// Es la mitad de dominio del otro defecto: el orquestador resolvía
    /// <c>TipoAusenciaId == null</c> como "sí computa", y una marca impar justificada
    /// terminaba con 480 minutos planos. En un turno de doce horas eso borra cuatro horas
    /// de jornada y todo el sobretiempo del día.
    /// </remarks>
    [Fact]
    public void Una_justificacion_sin_tipo_de_ausencia_no_paga_la_jornada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones = [new DateTime(2026, 7, 1, 7, 0, 0)],
            Justificacion = new JustificacionAplicable
            {
                Id = 78,
                Tipo = TipoJustificacion.MarcacionIncompleta,
                ComputaComoAsistencia = false,
                Comportamiento = null,
                Motivo = "Olvidó marcar la salida"
            }
        });

        resultado.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(0);
    }

    [Fact]
    public void El_dia_no_pagado_conserva_su_trazabilidad()
    {
        var resultado = DiaSinMarcar(ComportamientoAusencia.JustificadaNoRemunerada);

        resultado.JustificacionId.ShouldBe(77);
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Falta));
        resultado.Observacion.ShouldContain("JUSTIFICADO");
    }
}
