using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Asistencia;

/// <summary>
/// NH-03: una justificación parcial no debe declarar resuelto un extremo de marcación que
/// continúa faltando ni retirar el día de la bandeja de validación.
/// </summary>
public sealed class IncidenciaConJustificacionParcialAcceptanceTests
{
    private static readonly DateOnly Fecha = new(2026, 7, 1);

    [Fact]
    public void INC_01_permiso_parcial_no_oculta_una_salida_no_registrada()
    {
        var resultado = CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Fecha,
            Turno = TurnoDiurno(),
            Marcaciones = [Fecha.ToDateTime(new TimeOnly(7, 0))],
            Justificacion = new JustificacionAplicable
            {
                Id = 90,
                Tipo = TipoJustificacion.PermisoParcial,
                ComputaComoAsistencia = true,
                Comportamiento = ComportamientoAusencia.PermisoParcial,
                MinutosJustificados = 60,
                InicioPermiso = new TimeOnly(12, 0),
                FinPermiso = new TimeOnly(13, 0),
                Motivo = "Permiso aprobado"
            }
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.MinutosTrabajados.ShouldBe(0);
    }

    [Fact]
    public void INC_02_tardanza_justificada_no_oculta_una_salida_no_registrada()
    {
        var resultado = CalcularConUnaEntrada(new JustificacionAplicable
        {
            Id = 91,
            Tipo = TipoJustificacion.Tardanza,
            ComputaComoAsistencia = true,
            MinutosJustificados = 5,
            Motivo = "Tardanza autorizada"
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        resultado.RequiereValidacion.ShouldBeTrue();
    }

    [Fact]
    public void INC_03_regularizacion_parcial_no_oculta_el_extremo_que_sigue_faltando()
    {
        var resultado = CalcularConUnaEntrada(new JustificacionAplicable
        {
            Id = 92,
            Tipo = TipoJustificacion.MarcacionRegularizada,
            ComputaComoAsistencia = false,
            EntradaRegularizada = Fecha.ToDateTime(new TimeOnly(7, 0)),
            Motivo = "Solo se verificó la entrada"
        });

        resultado.Estado.ShouldBe(EstadoTareo.Incompleto);
        resultado.TipoIncidencia.ShouldBe(TipoIncidencia.SalidaNoRegistrada);
        resultado.RequiereValidacion.ShouldBeTrue();
        resultado.Observacion.ShouldContain("REGULARIZACIÓN INCOMPLETA");
    }

    private static ResultadoCalculo CalcularConUnaEntrada(JustificacionAplicable justificacion) =>
        CalculadoraTareo.Calcular(new EntradaCalculo
        {
            Fecha = Fecha,
            Turno = TurnoDiurno(),
            Marcaciones = [Fecha.ToDateTime(new TimeOnly(7, 0))],
            Justificacion = justificacion
        });

    private static Turno TurnoDiurno() => new()
    {
        Id = 1,
        Codigo = "DIU",
        Nombre = "Diurno",
        HoraInicio = new TimeOnly(7, 0),
        HoraFin = new TimeOnly(17, 22),
        MinutosRefrigerio = 0,
        MinutosTolerancia = 5,
        MinutosJornadaOrdinaria = 622,
        DiasAplicables = DiasSemana.LunesASabado
    };
}
