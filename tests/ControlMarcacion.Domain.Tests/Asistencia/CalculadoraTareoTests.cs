using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Pruebas del motor de cálculo del tareo.
/// </summary>
/// <remarks>
/// Todas las fechas son literales: nunca <c>DateTime.Now</c>. Un test que depende del reloj
/// falla el primer lunes feriado y nadie sabe por qué.
///
/// Los escenarios salen del archivo real de julio 2026: jornadas 06:00–19:00, sábados y
/// domingos trabajados, salidas a las 21:00 y 22:00, y un 11 % de días con una sola marca.
/// </remarks>
public class CalculadoraTareoTests
{
    // OJO: estos dos turnos llevan refrigerio 60 y NO reflejan la configuración de GCA,
    // donde el refrigerio es tiempo pagado y el catálogo lo tiene en 0. Se conservan así a
    // propósito: son los que ejercitan la rama que descuenta el refrigerio, que sigue
    // siendo una configuración válida y editable por turno.
    //
    // La configuración real está cubierta aparte, en la región "Configuración de GCA" del
    // final de este archivo, con turnos de refrigerio 0.
    private static Turno TurnoDiurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(19, 0),
        MinutosRefrigerio = 60,
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
        MinutosRefrigerio = 60,
        MinutosTolerancia = 5,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    /// <summary>Miércoles 1 de julio de 2026: día laborable normal.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    /// <summary>Domingo 5 de julio de 2026.</summary>
    private static readonly DateOnly Domingo = new(2026, 7, 5);

    private static EntradaCalculo Entrada(
        DateOnly fecha,
        Turno? turno,
        params DateTime[] marcaciones) =>
        new() { Fecha = fecha, Turno = turno, Marcaciones = marcaciones };

    // ------------------------------------------------------------------
    // Jornada normal
    // ------------------------------------------------------------------

    [Fact]
    public void Jornada_completa_puntual_descuenta_el_refrigerio()
    {
        // 07:00 a 19:00 = 720 min brutos, menos 60 de refrigerio = 660 efectivos.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTrabajados.ShouldBe(660);
        resultado.MinutosTardanza.ShouldBe(0);
    }

    [Fact]
    public void Reparte_la_jornada_en_ordinarias_y_extras_al_25_y_al_35()
    {
        // R-P0-03.1: DIU regular 07:00–17:22; ordinario hasta el fin teórico.
        // 07:00 a 21:00 = 840 - 60 refrigerio = 780 efectivos: 622 ordinarios + 158 extras.
        // De esos 158: los primeros 120 al 25 %, los 38 restantes al 35 %.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, ConFin(TurnoDiurno(), 17, 22),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 21, 0, 0)));

        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(38);
        resultado.MinutosExtraTotales.ShouldBe(158);
    }

    [Fact]
    public void Una_jornada_corta_no_genera_horas_extras()
    {
        // 07:00 a 12:00 = 300 brutos - 60 refrigerio = 240 efectivos, todos ordinarios.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 12, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(240);
        resultado.MinutosOrdinarios.ShouldBe(240);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    // Tolerancia y tardanza
    // ------------------------------------------------------------------

    [Fact]
    public void La_tolerancia_configurada_no_perdona_tardanza_de_planilla()
    {
        // La configuración histórica conserva 5, pero planilla cuenta desde las 07:00.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 5, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Tardanza);
        resultado.MinutosTardanza.ShouldBe(5);
    }

    [Fact]
    public void Pasado_el_limite_la_tardanza_se_cuenta_desde_la_hora_del_turno()
    {
        // Llega 07:20. Superó la tolerancia, así que la tardanza son los 20 minutos
        // completos desde las 07:00, no los 15 que exceden la tolerancia.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 20, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Tardanza);
        resultado.MinutosTardanza.ShouldBe(20);
    }

    [Fact]
    public void La_tardanza_recorta_los_minutos_trabajados()
    {
        // Entra 07:20 y sale 19:00 => 700 brutos - 60 refrigerio = 640.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 20, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(640);
    }

    // ------------------------------------------------------------------
    // Entrada anticipada: el caso masivo del archivo real
    // ------------------------------------------------------------------

    /// <summary>
    /// En el archivo real hay 738 jornadas que marcan entre las 06:00 y las 06:59 para un
    /// turno de 07:00. Si el cómputo arrancara en la marca, cada una generaría una hora
    /// extra diaria que nadie autorizó (D.S. 007-2002-TR, art. 9).
    /// </summary>
    [Fact]
    public void Marcar_antes_del_turno_no_genera_sobretiempo()
    {
        var temprano = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 6, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        var puntual = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        temprano.MinutosTrabajados.ShouldBe(puntual.MinutosTrabajados);
        temprano.MinutosExtraTotales.ShouldBe(puntual.MinutosExtraTotales);
    }

    [Fact]
    public void La_entrada_anticipada_queda_registrada_y_explicada()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 6, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        // La hora real no se pierde: se guarda tal cual y se explica en la observación.
        resultado.EntradaReal.ShouldBe(new DateTime(2026, 7, 1, 6, 0, 0));
        resultado.Observacion.ShouldContain("antes del inicio del turno");
    }

    [Fact]
    public void Salir_despues_del_turno_si_genera_sobretiempo()
    {
        // Caso real del 06/07: salida a las 22:06. Aquí 07:00 a 22:00 = 900 - 60 = 840.
        // R-P0-03.1: DIU regular 07:00–17:22; 622 ordinarios, el resto es sobretiempo.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, ConFin(TurnoDiurno(), 17, 22),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 22, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(840);
        resultado.MinutosExtraTotales.ShouldBe(218);
    }

    // ------------------------------------------------------------------
    // Marcación impar: el 11 % de las jornadas reales
    // ------------------------------------------------------------------

    [Fact]
    public void Una_sola_marcacion_deja_el_dia_incompleto_sin_calcular_horas()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void La_marca_impar_cercana_al_inicio_se_interpreta_como_entrada()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 1, 0)));

        resultado.EntradaReal.ShouldNotBeNull();
        resultado.SalidaReal.ShouldBeNull();
    }

    [Fact]
    public void La_marca_impar_cercana_al_fin_se_interpreta_como_salida()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 18, 50, 0)));

        resultado.EntradaReal.ShouldBeNull();
        resultado.SalidaReal.ShouldNotBeNull();
    }

    // ------------------------------------------------------------------
    // Sin marcaciones, sin turno, sin vínculo
    // ------------------------------------------------------------------

    [Fact]
    public void Sin_marcaciones_en_dia_laborable_es_falta()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(Miercoles, TurnoDiurno()));

        resultado.Estado.ShouldBe(EstadoTareo.Falta);
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    [Fact]
    public void Sin_turno_asignado_no_es_falta_sino_sin_horario()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(Miercoles, turno: null));

        resultado.Estado.ShouldBe(EstadoTareo.SinHorario);
    }

    [Fact]
    public void Fuera_del_vinculo_laboral_no_se_calcula_nada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            TieneVinculo = false
        });

        resultado.Estado.ShouldBe(EstadoTareo.SinVinculo);
    }

    // ------------------------------------------------------------------
    // Domingo y feriado
    // ------------------------------------------------------------------

    [Fact]
    public void Domingo_sin_marcaciones_es_descanso_no_falta()
    {
        // El turno diurno aplica de lunes a sábado: el domingo no correspondía trabajar.
        var resultado = CalculadoraTareo.Calcular(Entrada(Domingo, TurnoDiurno()));

        resultado.Estado.ShouldBe(EstadoTareo.Descanso);
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    /// <summary>
    /// El domingo 05/07/2026 se trabajó de 07:00 a 19:06 en el archivo real. El día tiene
    /// que quedar marcado como no laborable para que reciba la sobretasa del 100 %.
    /// </summary>
    [Fact]
    public void Domingo_trabajado_se_marca_como_descanso_semanal()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Domingo, TurnoDiurno(),
            new DateTime(2026, 7, 5, 7, 0, 0),
            new DateTime(2026, 7, 5, 19, 0, 0)));

        // El concepto importa: descanso semanal trabajado va por el D. Leg. 713 art. 3,
        // que es una tasa distinta de la del feriado aunque hoy ambas paguen 100 %.
        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.DescansoSemanal);
        resultado.EsDiaNoLaborable.ShouldBeTrue();
        resultado.MinutosTrabajados.ShouldBe(660);
        resultado.Observacion.ShouldContain("descanso");
    }

    /// <summary>
    /// Si un feriado cae en domingo, el concepto que manda es el de feriado: no se
    /// acumulan las dos sobretasas.
    /// </summary>
    [Fact]
    public void Un_feriado_en_domingo_se_liquida_como_feriado_no_como_descanso()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Domingo,
            Turno = TurnoDiurno(),
            EsFeriado = true,
            Marcaciones =
            [
                new DateTime(2026, 7, 5, 7, 0, 0),
                new DateTime(2026, 7, 5, 19, 0, 0)
            ]
        });

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
    }

    [Fact]
    public void Un_dia_laborable_normal_no_tiene_tipo_de_dia_no_laborable()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Ninguno);
        resultado.EsDiaNoLaborable.ShouldBeFalse();
    }

    /// <summary>
    /// En un día no laborable no hay hora de entrada exigible, así que llegar "tarde"
    /// no genera tardanza.
    /// </summary>
    [Fact]
    public void Un_domingo_trabajado_no_genera_tardanza()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Domingo, TurnoDiurno(),
            new DateTime(2026, 7, 5, 9, 30, 0),
            new DateTime(2026, 7, 5, 19, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
    }

    [Fact]
    public void Feriado_sin_marcaciones_es_feriado_no_falta()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            EsFeriado = true
        });

        resultado.Estado.ShouldBe(EstadoTareo.Feriado);
    }

    [Fact]
    public void Feriado_trabajado_se_marca_con_el_concepto_de_feriado()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            EsFeriado = true,
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 0, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ]
        });

        resultado.TipoDiaNoLaborable.ShouldBe(TipoDiaNoLaborable.Feriado);
        resultado.Observacion.ShouldContain("Feriado");
    }

    // ------------------------------------------------------------------
    // Turno nocturno: el caso que más se rompe
    // ------------------------------------------------------------------

    [Fact]
    public void El_turno_nocturno_cruza_medianoche_y_computa_la_jornada_completa()
    {
        // 19:00 del miércoles a 07:00 del jueves = 720 brutos - 60 = 660 efectivos.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocturno(),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 7, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTrabajados.ShouldBe(660);
    }

    /// <summary>
    /// De 19:00 a 07:00, la franja nocturna legal (22:00–06:00) aporta 8 horas exactas.
    /// </summary>
    [Fact]
    public void El_nocturno_cuenta_solo_los_minutos_dentro_de_la_franja_legal()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocturno(),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 7, 0, 0)));

        resultado.MinutosNocturnos.ShouldBe(480);
    }

    [Fact]
    public void Una_jornada_diurna_no_genera_minutos_nocturnos()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.MinutosNocturnos.ShouldBe(0);
    }

    [Fact]
    public void Una_salida_tardia_genera_los_minutos_nocturnos_que_correspondan()
    {
        // Sale 23:30: de 22:00 a 23:30 son 90 minutos en franja nocturna.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 23, 30, 0)));

        resultado.MinutosNocturnos.ShouldBe(90);
    }

    // ------------------------------------------------------------------
    // Desglose del sobretiempo nocturno por tramo
    //
    // El tareo final de GCA separa el sobretiempo en bloque diurno y bloque nocturno, cada
    // uno con sus tramos 25 / 35. El sobretiempo es la cola de la jornada, así que el tramo
    // se ubica en el reloj y se cruza contra la franja legal 22:00–06:00.
    // ------------------------------------------------------------------

    /// <summary>
    /// 07:00 a 23:30: 990 brutos − 60 refrigerio = 930 efectivos. Ordinarias 480, extras 450:
    /// las 2 primeras horas (25 %) van de 16:00 a 18:00 y no tocan la franja; las 330
    /// restantes (35 %) van de 18:00 a 23:30 y aportan los 90 min de 22:00 en adelante.
    /// </summary>
    [Fact]
    public void El_sobretiempo_nocturno_se_imputa_al_tramo_que_realmente_cayo_en_la_franja()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 23, 30, 0)));

        resultado.MinutosExtra25Nocturnos.ShouldBe(0);
        resultado.MinutosExtra35Nocturnos.ShouldBe(90);
    }

    /// <summary>
    /// R-P0-03.1: NOC regular 19:00–05:22 (refrigerio pagado, 0 min), salida 08:00. Las
    /// extras arrancan a las 05:22:
    /// del 25 % (05:22–07:22) solo 05:22–06:00 cae en la franja, y el 35 % (07:22–08:00) ya
    /// es de día.
    /// </summary>
    [Fact]
    public void En_el_turno_nocturno_el_veinticinco_cae_dentro_de_la_franja_y_el_treinta_y_cinco_fuera()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, ConFin(TurnoNocturnoGca(), 5, 22),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 8, 0, 0)));

        resultado.MinutosExtra25.ShouldBe(120);
        resultado.MinutosExtra35.ShouldBe(38);
        resultado.MinutosExtra25Nocturnos.ShouldBe(38);
        resultado.MinutosExtra35Nocturnos.ShouldBe(0);
    }

    /// <summary>
    /// Los tramos nocturnos son un subconjunto de la nocturnidad total: lo que resta es la
    /// base de la bonificación nocturna, que es lo que el tareo final pide como
    /// "B.NOCT. (HORAS)".
    /// </summary>
    [Fact]
    public void La_nocturnidad_ordinaria_es_la_nocturnidad_total_menos_el_sobretiempo()
    {
        // R-P0-03.1: NOC regular 19:00–05:22; las extras (05:22–07:00) tienen 38 min nocturnos.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, ConFin(TurnoNocturnoGca(), 5, 22),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 7, 0, 0)));

        resultado.MinutosNocturnos.ShouldBe(480);
        resultado.MinutosOrdinariosNocturnos.ShouldBe(442);
    }

    [Fact]
    public void Una_jornada_diurna_sin_sobretiempo_no_genera_tramos_nocturnos()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 16, 0, 0)));

        resultado.MinutosExtra25Nocturnos.ShouldBe(0);
        resultado.MinutosExtra35Nocturnos.ShouldBe(0);
        resultado.MinutosOrdinariosNocturnos.ShouldBe(0);
    }

    /// <summary>
    /// Los tramos nocturnos nunca superan a su tramo diurno equivalente: son parte de él,
    /// no un concepto que se suma aparte. Si esto se rompe, el Excel paga horas de más.
    /// </summary>
    [Fact]
    public void Los_tramos_nocturnos_nunca_exceden_al_tramo_del_que_son_subconjunto()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocturno(),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 9, 30, 0)));

        resultado.MinutosExtra25Nocturnos.ShouldBeLessThanOrEqualTo(resultado.MinutosExtra25);
        resultado.MinutosExtra35Nocturnos.ShouldBeLessThanOrEqualTo(resultado.MinutosExtra35);

        (resultado.MinutosExtra25Nocturnos + resultado.MinutosExtra35Nocturnos)
            .ShouldBeLessThanOrEqualTo(resultado.MinutosNocturnos);
    }

    // ------------------------------------------------------------------
    // Salida anticipada
    // ------------------------------------------------------------------

    [Fact]
    public void La_anticipacion_configurada_no_perdona_salida_anticipada_de_planilla()
    {
        // La configuración histórica conserva 5, pero planilla cuenta hasta las 19:00.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 18, 56, 0)));

        resultado.MinutosSalidaAnticipada.ShouldBe(4);
    }

    [Fact]
    public void Salir_antes_de_la_ventana_queda_registrado()
    {
        // Se va a las 17:00 de un turno que termina a las 19:00: 120 minutos antes.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 17, 0, 0)));

        resultado.MinutosSalidaAnticipada.ShouldBe(120);
        resultado.Observacion.ShouldContain("salida anticipada");
    }

    // ------------------------------------------------------------------
    // Idempotencia y trazabilidad
    // ------------------------------------------------------------------

    [Fact]
    public void Calcular_dos_veces_la_misma_entrada_da_el_mismo_resultado()
    {
        var entrada = Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 6, 47, 49),
            new DateTime(2026, 7, 1, 19, 4, 44));

        var primero = CalculadoraTareo.Calcular(entrada);
        var segundo = CalculadoraTareo.Calcular(entrada);

        segundo.ShouldBe(primero);
    }

    // ------------------------------------------------------------------
    // Justificaciones: marcador + justificación = tareo final
    // ------------------------------------------------------------------

    private static JustificacionAplicable Justificacion(
        TipoJustificacion tipo,
        bool computaComoAsistencia = true,
        int? minutos = null) =>
        new()
        {
            Id = 99,
            Tipo = tipo,
            ComputaComoAsistencia = computaComoAsistencia,
            MinutosJustificados = minutos,
            Motivo = "Descanso médico del 01 al 03"
        };

    [Fact]
    public void Una_justificacion_convierte_la_falta_en_ausencia_justificada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = Justificacion(TipoJustificacion.Ausencia)
        });

        resultado.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
    }

    /// <summary>
    /// Es la evidencia que hace auditable el sistema: se puede ver que el día era Falta y
    /// qué lo cambió, sin tener que reconstruirlo.
    /// </summary>
    [Fact]
    public void La_justificacion_conserva_el_estado_anterior_y_su_identificador()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = Justificacion(TipoJustificacion.Ausencia)
        });

        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Falta));
        resultado.JustificacionId.ShouldBe(99);
        resultado.Observacion.ShouldContain("Descanso médico");
    }

    [Fact]
    public void Una_ausencia_que_computa_como_asistencia_paga_la_jornada_ordinaria()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = Justificacion(TipoJustificacion.Ausencia, computaComoAsistencia: true)
        });

        resultado.MinutosOrdinarios.ShouldBe(480);
    }

    /// <summary>
    /// Una licencia sin goce justifica la falta pero no la paga: el día queda en cero.
    /// </summary>
    [Fact]
    public void Una_ausencia_sin_goce_justifica_pero_deja_el_dia_en_cero()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = Justificacion(TipoJustificacion.Ausencia, computaComoAsistencia: false)
        });

        resultado.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        resultado.MinutosOrdinarios.ShouldBe(0);
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    /// <summary>
    /// Justificar una ausencia no puede inventar sobretiempo: se paga la jornada ordinaria
    /// y nada más.
    /// </summary>
    [Fact]
    public void Justificar_una_ausencia_nunca_genera_horas_extras()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Justificacion = Justificacion(TipoJustificacion.Ausencia)
        });

        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    [Fact]
    public void Justificar_la_tardanza_completa_la_borra_y_el_dia_queda_puntual()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 20, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.Tardanza)
        });

        resultado.MinutosTardanza.ShouldBe(0);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Tardanza));
    }

    /// <summary>
    /// La observación debe decir cuánto quedó, no solo cuánto había: un dato a medio
    /// explicar es tan malo como uno sin explicar.
    /// </summary>
    [Fact]
    public void La_observacion_de_una_tardanza_parcial_dice_cuanto_quedo()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 20, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.Tardanza, minutos: 12)
        });

        resultado.Observacion.ShouldContain("se perdonaron 12 min, quedan 8 min");
    }

    [Fact]
    public void Una_tardanza_justificada_en_parte_conserva_el_resto()
    {
        // 20 minutos de tardanza, se perdonan 12: quedan 8 y el día sigue en tardanza.
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 20, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.Tardanza, minutos: 12)
        });

        resultado.MinutosTardanza.ShouldBe(8);
        resultado.Estado.ShouldBe(EstadoTareo.Tardanza);
    }

    /// <summary>
    /// Perdonar la tardanza no regala horas: el trabajador estuvo lo que estuvo.
    /// </summary>
    [Fact]
    public void Justificar_la_tardanza_no_altera_los_minutos_trabajados()
    {
        var conJustificacion = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 20, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.Tardanza)
        });

        conJustificacion.MinutosTrabajados.ShouldBe(640);
    }

    [Fact]
    public void Justificar_una_marcacion_impar_la_convierte_en_ausencia_justificada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones = [new DateTime(2026, 7, 1, 7, 0, 0)],
            Justificacion = Justificacion(TipoJustificacion.MarcacionIncompleta)
        });

        resultado.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        resultado.EstadoAnterior.ShouldBe(nameof(EstadoTareo.Incompleto));
    }

    [Fact]
    public void Justificar_la_salida_anticipada_la_descuenta()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 0, 0),
                new DateTime(2026, 7, 1, 17, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.SalidaAnticipada)
        });

        resultado.MinutosSalidaAnticipada.ShouldBe(0);
    }

    /// <summary>
    /// La marcación cruda es inmutable: la justificación nunca la reescribe.
    /// </summary>
    [Fact]
    public void La_justificacion_no_altera_las_horas_que_reporto_el_marcador()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = TurnoDiurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 20, 0),
                new DateTime(2026, 7, 1, 17, 0, 0)
            ],
            Justificacion = Justificacion(TipoJustificacion.Tardanza)
        });

        resultado.EntradaReal.ShouldBe(new DateTime(2026, 7, 1, 7, 20, 0));
        resultado.SalidaReal.ShouldBe(new DateTime(2026, 7, 1, 17, 0, 0));
    }

    [Fact]
    public void Sin_justificacion_el_resultado_no_lleva_rastro_de_ninguna()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.JustificacionId.ShouldBeNull();
        resultado.EstadoAnterior.ShouldBeNull();
    }

    [Fact]
    public void Todo_resultado_lleva_una_observacion_legible()
    {
        EntradaCalculo[] casos =
        [
            Entrada(Miercoles, TurnoDiurno()),
            Entrada(Miercoles, turno: null),
            Entrada(Miercoles, TurnoDiurno(), new DateTime(2026, 7, 1, 7, 0, 0)),
            Entrada(Domingo, TurnoDiurno()),
            Entrada(Miercoles, TurnoDiurno(),
                new DateTime(2026, 7, 1, 7, 0, 0), new DateTime(2026, 7, 1, 19, 0, 0))
        ];

        foreach (var caso in casos)
        {
            CalculadoraTareo.Calcular(caso).Observacion.ShouldNotBeNullOrWhiteSpace();
        }
    }

    // ------------------------------------------------------------------
    // Marcas de más: baño, rebote del lector y doble fichada
    // ------------------------------------------------------------------
    //
    // Hasta aquí ningún test pasaba más de dos marcaciones, así que el comportamiento con
    // marcas intermedias estaba sostenido solo por `marcaciones[0]` y `marcaciones[^1]`.
    // Estos tests lo fijan, y separan los dos casos que se confunden con facilidad:
    //
    //   • marca INTERMEDIA (el baño): ocurre a media jornada y se ignora sin más
    //   • REBOTE (marcar dos veces seguidas): ocurre pegado a otra marca y no es un evento
    //
    // La diferencia importa en un solo punto, pero es el que produce el error: si TODAS
    // las marcas del día son el mismo rebote, el día no tiene jornada.

    [Fact]
    public void Una_marca_intermedia_no_altera_la_jornada()
    {
        var soloExtremos = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        var conVisitaAlBano = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 12, 30, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        conVisitaAlBano.EntradaReal.ShouldBe(soloExtremos.EntradaReal);
        conVisitaAlBano.SalidaReal.ShouldBe(soloExtremos.SalidaReal);
        conVisitaAlBano.MinutosTrabajados.ShouldBe(soloExtremos.MinutosTrabajados);
        conVisitaAlBano.Estado.ShouldBe(soloExtremos.Estado);
    }

    [Fact]
    public void Las_marcas_intermedias_quedan_declaradas_en_la_observacion()
    {
        // Quien audita ve seis marcas en el detalle del día y una jornada de 07:00 a 19:00.
        // Sin esta frase no puede distinguir "el sistema las ignoró a propósito" de "el
        // sistema perdió marcas".
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 10, 15, 0),
            new DateTime(2026, 7, 1, 12, 30, 0),
            new DateTime(2026, 7, 1, 15, 45, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(660);
        resultado.Observacion.ShouldContain("3 marca(s) intermedia(s)");
    }

    [Fact]
    public void Un_rebote_al_entrar_no_agrava_la_tardanza()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 20, 0),
            new DateTime(2026, 7, 1, 7, 20, 40),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        // La tardanza se mide desde la PRIMERA marca. Tomar la segunda castigaría a quien
        // marca dos veces con minutos que no llegó tarde.
        resultado.EntradaReal.ShouldBe(new DateTime(2026, 7, 1, 7, 20, 0));
        resultado.MinutosTardanza.ShouldBe(20);
    }

    [Fact]
    public void Un_rebote_al_salir_conserva_el_sobretiempo_completo()
    {
        var conRebote = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 40, 0),
            new DateTime(2026, 7, 1, 19, 41, 0)));

        var sinRebote = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 41, 0)));

        // ⚠️ Este test es el que impide "simplificar" el motor tomando la salida de la
        // lista consolidada: ahí la salida sería 19:40 y se perderían minutos trabajados.
        // Consolidar cuenta pasos por el lector; no mueve las horas.
        conRebote.SalidaReal.ShouldBe(new DateTime(2026, 7, 1, 19, 41, 0));
        conRebote.MinutosTrabajados.ShouldBe(sinRebote.MinutosTrabajados);
        conRebote.MinutosExtraTotales.ShouldBe(sinRebote.MinutosExtraTotales);
    }

    [Fact]
    public void Dos_marcas_seguidas_y_salida_olvidada_dejan_el_dia_incompleto()
    {
        // El caso que motivó el debouncing. Sin él, estas dos marcas se leían como entrada
        // y salida: el día salía con jornada y tardanza de 20 min, y por tanto NO aparecía
        // en el filtro de días Incompletos, que es donde RRHH busca lo que hay que
        // justificar. Una salida olvidada se disfrazaba de día trabajado.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurnoGca(),
            new DateTime(2026, 7, 1, 7, 20, 0),
            new DateTime(2026, 7, 1, 7, 21, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.SalidaReal.ShouldBeNull();
        resultado.Observacion.ShouldContain("Se descartó 1 marca(s) repetida(s)");
    }

    [Fact]
    public void Un_racimo_de_rebotes_no_se_encadena_mas_alla_de_la_ventana()
    {
        // 07:00 → 07:01:30 → 07:03. Cada marca está a menos de 2 min de la ANTERIOR, pero
        // 07:03 sí está a 3 min de la última ACEPTADA (07:00), así que cuenta como evento.
        // Comparando contra la anterior en bruto, una cadena de marcas a 1 min de distancia
        // se comería la jornada entera sin límite.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurno(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 7, 1, 30),
            new DateTime(2026, 7, 1, 7, 3, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTrabajados.ShouldBe(660);
        resultado.Observacion.ShouldContain("1 marca(s) intermedia(s)");
    }

    [Fact]
    public void Un_dia_de_descanso_con_solo_rebotes_no_computa_jornada()
    {
        // La consolidación se aplica también en la rama de feriado y descanso: si no, un
        // domingo con dos marcas pegadas generaría una jornada con sobretasa del 100 %.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Domingo, TurnoDiurnoGca(),
            new DateTime(2026, 7, 5, 8, 0, 0),
            new DateTime(2026, 7, 5, 8, 1, 30)));

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.MinutosTrabajados.ShouldBe(0);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    // Configuración de GCA: el refrigerio es tiempo pagado
    // ------------------------------------------------------------------
    //
    // GCA confirmó que en los cinco turnos el refrigerio se computa como hora de labor y
    // no se descuenta, así que el catálogo lo tiene en 0. Estos son los números que de
    // verdad salen a planilla; los tests de más arriba usan turnos con refrigerio 60 para
    // cubrir la rama que sí descuenta.

    private static Turno TurnoDiurnoGca() => new()
    {
        Id = 1,
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

    private static Turno TurnoNocturnoGca() => new()
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

    [Fact]
    public void Con_el_refrigerio_pagado_la_jornada_de_doce_horas_computa_720_minutos()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, ConFin(TurnoDiurnoGca(), 17, 22),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 19, 0, 0)));

        // Las 12 horas completas, sin resta alguna.
        resultado.MinutosTrabajados.ShouldBe(720);

        // R-P0-03.1: DIU regular 07:00–17:22 (622 min); 17:22–19:00 son 98 min de
        // sobretiempo, todos dentro de las dos primeras horas al 25 %.
        resultado.MinutosOrdinarios.ShouldBe(622);
        resultado.MinutosExtraTotales.ShouldBe(98);
        resultado.MinutosExtra25.ShouldBe(98);
        resultado.MinutosExtra35.ShouldBe(0);
    }

    [Fact]
    public void Con_el_refrigerio_pagado_el_nocturno_computa_toda_la_franja_legal()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocturnoGca(),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 7, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(720);

        // Los 480 minutos de la franja 22:00–06:00 son ahora todos tiempo trabajado: sin
        // refrigerio que descontar, no hay diferencia entre estar presente y estar
        // laborando, y la columna de horas nocturnas del tareo deja de sobrar minutos.
        resultado.MinutosNocturnos.ShouldBe(480);
        resultado.MinutosNocturnos.ShouldBeLessThanOrEqualTo(resultado.MinutosTrabajados);
    }

    [Fact]
    public void Con_el_refrigerio_pagado_una_salida_temprana_no_inventa_sobretiempo()
    {
        // Sale a las 15:00: 480 minutos exactos, la jornada ordinaria y ni un minuto extra.
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoDiurnoGca(),
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 15, 0, 0)));

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    // Gracia de salida
    // ------------------------------------------------------------------
    //
    // La entrada ya se computaba desde la hora del turno; la salida, no: salir 10 minutos
    // después del fin —la cola del lector— daba 10 minutos de sobretiempo. La gracia es el
    // simétrico, por turno y en 0 por omisión, para que nada cambie hasta que GCA la fije.

    /// <summary>Turno nocturno de 22:00 a 06:00, el del ejemplo de rotación.</summary>
    private static Turno TurnoNocheCorta(int graciaSalida) => new()
    {
        Id = 9,
        Codigo = "N8",
        Nombre = "Nocturno de 8 horas",
        HoraInicio = new TimeOnly(22, 0),
        HoraFin = new TimeOnly(6, 0),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 15,
        MinutosAnticipacionEntrada = 15,
        MinutosAnticipacionSalida = 5,
        MinutosGraciaSalida = graciaSalida,
        MinutosJornadaOrdinaria = 480,
        DiasAplicables = DiasSemana.LunesASabado
    };

    private static Turno ConFin(Turno turno, int hora, int minuto)
    {
        turno.HoraFin = new TimeOnly(hora, minuto);
        return turno;
    }

    private static ResultadoCalculo NocheCorta(int graciaSalida, TimeOnly salida) =>
        CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocheCorta(graciaSalida),
            new DateTime(2026, 7, 1, 21, 50, 0),
            new DateOnly(2026, 7, 2).ToDateTime(salida)));

    // ------------------------------------------------------------------
    // Turno que sale de una cuadrilla
    // ------------------------------------------------------------------

    /// <summary>
    /// Con una cuadrilla la ficha no dice qué turno tuvo cada día: la observación lo dice.
    /// </summary>
    [Fact]
    public void El_origen_del_turno_encabeza_la_observacion()
    {
        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, TurnoNocturnoGca(),
            new DateTime(2026, 7, 1, 19, 0, 0),
            new DateTime(2026, 7, 2, 7, 0, 0)) with { OrigenTurno = "Cuadrilla A: turno NOC." });

        resultado.Observacion.ShouldStartWith("Cuadrilla A: turno NOC. ");
        resultado.MinutosTrabajados.ShouldBe(720);
    }

    [Fact]
    public void Sin_turno_el_motivo_conocido_reemplaza_al_generico()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = null,
            MotivoSinTurno = "La cuadrilla no tenía ciclo programado ese día."
        });

        resultado.Estado.ShouldBe(EstadoTareo.SinHorario);
        resultado.Observacion.ShouldBe("La cuadrilla no tenía ciclo programado ese día.");
    }

    [Fact]
    public void Un_turno_nuevo_nace_sin_gracia_de_salida()
    {
        new Turno().MinutosGraciaSalida.ShouldBe(0);
    }

    /// <summary>
    /// R-P0-03.1: sin gracia rige el umbral de 15 min de lunes a viernes; diez minutos tras
    /// el fin no alcanzan y no se computan.
    /// </summary>
    [Fact]
    public void Sin_gracia_salir_diez_minutos_despues_del_fin_no_alcanza_el_umbral()
    {
        var resultado = NocheCorta(graciaSalida: 0, new TimeOnly(6, 10));

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosExtra25.ShouldBe(0);
        resultado.Observacion.ShouldContain("umbral");
    }

    /// <summary>
    /// El ejemplo completo: entra 21:50, sale 06:10, con 15 minutos de gracia.
    /// </summary>
    /// <remarks>
    /// La entrada se computa desde las 22:00 y la salida hasta las 06:00: la jornada exacta,
    /// toda en franja nocturna, puntual y sin sobretiempo. Las marcas reales se conservan y
    /// la observación dice cuántos minutos absorbió la gracia, para que quien audita vea que
    /// no se perdieron sino que se decidió no computarlos.
    /// </remarks>
    [Fact]
    public void Con_gracia_salir_diez_minutos_despues_no_genera_sobretiempo()
    {
        var resultado = NocheCorta(graciaSalida: 15, new TimeOnly(6, 10));

        resultado.MinutosTrabajados.ShouldBe(480);
        resultado.MinutosOrdinarios.ShouldBe(480);
        resultado.MinutosExtraTotales.ShouldBe(0);
        resultado.MinutosNocturnos.ShouldBe(480);
        resultado.MinutosTardanza.ShouldBe(0);
        resultado.Estado.ShouldBe(EstadoTareo.Puntual);

        resultado.EntradaReal.ShouldBe(new DateTime(2026, 7, 1, 21, 50, 0));
        resultado.SalidaReal.ShouldBe(new DateTime(2026, 7, 2, 6, 10, 0));
        resultado.Observacion.ShouldContain("10 min");
        resultado.Observacion.ShouldContain("gracia de salida");
    }

    /// <summary>La gracia es inclusiva, igual que la tolerancia de tardanza.</summary>
    [Fact]
    public void Salir_justo_en_el_limite_de_la_gracia_tampoco_genera_sobretiempo()
    {
        var resultado = NocheCorta(graciaSalida: 15, new TimeOnly(6, 15));

        resultado.MinutosExtraTotales.ShouldBe(0);
    }

    /// <summary>
    /// Pasada la gracia se computa todo desde el fin del turno, no desde el fin de la gracia.
    /// </summary>
    /// <remarks>
    /// La gracia es un margen para la cola del lector, no un corrimiento del horario: quien
    /// se quedó 16 minutos trabajó 16 minutos de sobretiempo, no 1.
    /// </remarks>
    [Fact]
    public void Pasada_la_gracia_cuenta_todo_el_sobretiempo_desde_el_fin_del_turno()
    {
        var resultado = NocheCorta(graciaSalida: 15, new TimeOnly(6, 16));

        resultado.MinutosTrabajados.ShouldBe(496);
        resultado.MinutosExtra25.ShouldBe(16);
        resultado.Observacion.ShouldNotContain("gracia de salida");
    }

    /// <summary>
    /// Las salidas reales a las 21:06 de un turno que cierra a las 19:00 siguen contando
    /// enteras: la gracia no puede comerse sobretiempo de verdad.
    /// </summary>
    [Fact]
    public void La_gracia_no_toca_el_sobretiempo_real_de_dos_horas()
    {
        // R-P0-03.1: DIU regular 07:00–17:22.
        var turno = ConFin(TurnoDiurnoGca(), 17, 22);
        turno.MinutosGraciaSalida = 15;

        var resultado = CalculadoraTareo.Calcular(Entrada(
            Miercoles, turno,
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 21, 6, 0)));

        resultado.MinutosTrabajados.ShouldBe(846);

        // R-P0-03: todo lo posterior a las 17:22 (846 - 622).
        resultado.MinutosExtraTotales.ShouldBe(224);
    }
}
