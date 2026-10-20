using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// Qué días quedan esperando una decisión humana, y por qué.
/// </summary>
/// <remarks>
/// La regla que gobierna todas estas pruebas: <b>ante la duda, no decidir</b>. Un dato que
/// no llegó no es una falta, una marca que no encaja no se descarta en silencio, y una
/// sospecha sobre el turno no reescribe el turno.
/// </remarks>
public class IncidenciasDelDiaTests
{
    /// <summary>Miércoles 1 de julio de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private static Turno Diurno() => new()
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

    private static Turno Nocturno() => new()
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

    // ------------------------------------------------------------------
    // Sin cobertura de carga: el archivo que no llegó
    // ------------------------------------------------------------------

    /// <summary>
    /// <b>El defecto más caro del sistema.</b> Un día sin archivo cargado producía una falta
    /// por cada trabajador de ese aparato, idéntica a una falta real en el Excel que va a
    /// planilla, y con una observación que afirmaba algo que el sistema no sabía.
    /// </summary>
    [Fact]
    public void Un_dia_sin_archivo_cargado_no_es_falta()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            SinCoberturaDeCarga = true
        });

        resultado.Estado.ShouldBe(EstadoTareo.PendienteValidacion);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SinCoberturaDeCarga);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.Observacion.ShouldContain("No se cargó el archivo");
    }

    /// <summary>
    /// Con el archivo cargado, un día sin marcar sigue siendo falta: la cobertura no es una
    /// amnistía.
    /// </summary>
    [Fact]
    public void Con_el_archivo_cargado_un_dia_sin_marcar_sigue_siendo_falta()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            SinCoberturaDeCarga = false
        });

        resultado.Estado.ShouldBe(EstadoTareo.Falta);
        resultado.TipoIncidencia.ShouldBeNull();
        resultado.RequiereValidacion.ShouldBeFalse();
    }

    /// <summary>
    /// Si la persona marcó, el archivo de su aparato llegó: la falta de cobertura no puede
    /// tapar una jornada que sí se calculó.
    /// </summary>
    [Fact]
    public void Una_jornada_completa_no_se_vuelve_pendiente_por_la_cobertura()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            SinCoberturaDeCarga = true,
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 0, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ]
        });

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MinutosTrabajados.ShouldBe(720);
    }

    // ------------------------------------------------------------------
    // Marca impar: qué falta exactamente
    // ------------------------------------------------------------------

    [Fact]
    public void Una_entrada_sin_salida_pide_regularizar_la_salida()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            Marcaciones = [new DateTime(2026, 7, 1, 7, 2, 0)]
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        resultado.RequiereValidacion.ShouldBeTrue();
    }

    /// <summary>
    /// El caso rector: el marcador tiene la salida y la persona olvidó marcar al llegar.
    /// </summary>
    [Fact]
    public void Una_salida_sin_entrada_pide_regularizar_el_ingreso()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            Marcaciones = [new DateTime(2026, 7, 1, 19, 3, 0)]
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.IngresoNoRegistrado);
    }

    // ------------------------------------------------------------------
    // Marcas fuera de la ventana del turno
    // ------------------------------------------------------------------

    /// <summary>
    /// Se descartaban sin dejar rastro: ni en la observación, ni en ninguna bandeja. Solo
    /// quedaban en la tabla de marcaciones crudas, que nadie consulta a mano.
    /// </summary>
    [Fact]
    public void Las_marcas_fuera_de_la_ventana_se_declaran_con_su_hora()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            MarcacionesFueraVentana =
            [
                new DateTime(2026, 7, 1, 5, 30, 0),
                new DateTime(2026, 7, 1, 5, 34, 0)
            ]
        });

        resultado.MarcacionesFueraVentana.ShouldBe(2);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.FueraDeTurno);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.Observacion.ShouldContain("05:30");
        resultado.Observacion.ShouldContain("05:34");
    }

    /// <summary>
    /// Con la jornada ya resuelta, las marcas sobrantes se declaran pero no mandan a nadie a
    /// revisar un día correcto: una bandeja con ruido no se revisa.
    /// </summary>
    [Fact]
    public void Un_dia_completo_con_marcas_sobrantes_las_declara_sin_pedir_revision()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 0, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ],
            MarcacionesFueraVentana = [new DateTime(2026, 7, 1, 23, 50, 0)]
        });

        resultado.Estado.ShouldBe(EstadoTareo.Puntual);
        resultado.MarcacionesFueraVentana.ShouldBe(1);
        resultado.RequiereValidacion.ShouldBeFalse();
        resultado.Observacion.ShouldContain("fuera de la ventana");
    }

    /// <summary>
    /// La falta de archivo manda sobre todo lo demás: sin datos del aparato, ninguna otra
    /// lectura del día significa nada.
    /// </summary>
    [Fact]
    public void La_falta_de_cobertura_tiene_precedencia_sobre_las_marcas_sueltas()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            SinCoberturaDeCarga = true,
            MarcacionesFueraVentana = [new DateTime(2026, 7, 1, 5, 30, 0)]
        });

        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SinCoberturaDeCarga);
    }

    // ------------------------------------------------------------------
    // Turno discrepante
    // ------------------------------------------------------------------

    /// <summary>
    /// La detección existía, estaba probada y <b>ningún archivo del sistema la llamaba</b>.
    /// Una noche calculada contra el turno diurno da tardanza enorme o sobretiempo
    /// inventado, y nada lo señalaba.
    /// </summary>
    [Fact]
    public void Un_dia_que_encaja_en_otro_turno_queda_señalado_sin_recalcularse()
    {
        // Marcas de una noche completa, con el turno DIURNO asignado.
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            TurnosCandidatos = [Diurno(), Nocturno()],
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 19, 0, 0),
                new DateTime(2026, 7, 2, 7, 0, 0)
            ]
        });

        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.TurnoDiscrepante);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.Observacion.ShouldContain("NOC");

        // Y lo esencial: el cálculo NO cambió de turno. Corregir la rotación es una decisión
        // con autor; si el motor reescribiera el turno, el tareo dejaría de ser
        // reconstruible desde el padrón.
        resultado.MinutosTardanza.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Un_dia_coherente_con_su_turno_no_genera_sospecha()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            TurnosCandidatos = [Diurno(), Nocturno()],
            Marcaciones =
            [
                new DateTime(2026, 7, 1, 7, 0, 0),
                new DateTime(2026, 7, 1, 19, 0, 0)
            ]
        });

        resultado.TipoIncidencia.ShouldBeNull();
        resultado.RequiereValidacion.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Lo ya resuelto no vuelve a la bandeja
    // ------------------------------------------------------------------

    [Fact]
    public void Un_dia_ya_justificado_no_genera_incidencia()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Miercoles,
            Turno = Diurno(),
            Marcaciones = [new DateTime(2026, 7, 1, 7, 2, 0)],
            Justificacion = new JustificacionAplicable
            {
                Id = 12,
                Tipo = TipoJustificacion.MarcacionIncompleta,
                ComputaComoAsistencia = false,
                Motivo = "Se verificó con el supervisor"
            }
        });

        resultado.TipoIncidencia.ShouldBeNull();
        resultado.RequiereValidacion.ShouldBeFalse();
    }
}
