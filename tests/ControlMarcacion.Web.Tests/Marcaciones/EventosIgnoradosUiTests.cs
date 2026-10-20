using ControlMarcacion.Application.Marcaciones.Dto;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Marcaciones;

public sealed class EventosIgnoradosUiTests
{
    [Fact]
    public void BKIGN_09_eventos_ignorados_no_convierten_una_importacion_completa_en_error()
    {
        var resultado = new ResultadoImportacion(
            1,
            "BKtransaction.dat",
            "Marcador",
            FilasLeidas: 981,
            FilasInsertadas: 840,
            FilasDuplicadas: 11,
            FilasRechazadas: 0,
            Rechazadas: [],
            IdsSinAsignar: [],
            FilasIgnoradas: 130);

        resultado.FilasAceptadas.ShouldBe(851);
        resultado.FilasIgnoradas.ShouldBe(130);
        resultado.HuboProblemas.ShouldBeFalse();
    }

    [Fact]
    public void BKIGN_10_resultado_e_historial_muestran_ignorados_separados_de_errores()
    {
        var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var resultado = File.ReadAllText(
            Path.Combine(raiz, "src", "ControlMarcacion.Web", "Views", "Marcaciones", "Resultado.cshtml"));
        var historial = File.ReadAllText(
            Path.Combine(raiz, "src", "ControlMarcacion.Web", "Views", "Marcaciones", "Index.cshtml"));

        resultado.ShouldContain("Eventos de equipo ignorados");
        resultado.ShouldContain("@r.FilasIgnoradas");
        resultado.ShouldContain("<th>Errores</th>");
        historial.ShouldContain("<th>Ignoradas</th>");
        historial.ShouldContain("i.FilasIgnoradas");
        historial.ShouldContain("<th>Errores</th>");
    }
}
