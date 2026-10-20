using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Domain.Tests.Personal;

/// <summary>Contratos PL3C-VIG-01..07 de resolución canónica de vigencia.</summary>
public sealed class PeriodoLaboralFase3CVigenciaTests
{
    [Fact]
    public void PL3C_VIG_01_resuelve_el_periodo_vigente_exacto()
    {
        var trabajador = ConDosEpisodios();

        trabajador.ObtenerPeriodoVigente(new DateOnly(2024, 3, 1))!
            .FechaIngreso.ShouldBe(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void PL3C_VIG_02_antes_del_ingreso_no_hay_periodo()
    {
        ConDosEpisodios().ObtenerPeriodoVigente(new DateOnly(2023, 12, 31)).ShouldBeNull();
    }

    [Fact]
    public void PL3C_VIG_03_despues_del_cese_no_hay_periodo()
    {
        var trabajador = ConPeriodo(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30));

        trabajador.ObtenerPeriodoVigente(new DateOnly(2024, 7, 1)).ShouldBeNull();
    }

    [Fact]
    public void PL3C_VIG_04_periodo_abierto_aplica_en_fecha_posterior()
    {
        ConDosEpisodios().ObtenerPeriodoVigente(new DateOnly(2028, 2, 1))!
            .FechaIngreso.ShouldBe(new DateOnly(2025, 1, 1));
    }

    [Fact]
    public void PL3C_VIG_05_dos_episodios_conservan_el_hueco()
    {
        var trabajador = ConDosEpisodios();

        trabajador.ObtenerPeriodoVigente(new DateOnly(2024, 3, 1)).ShouldNotBeNull();
        trabajador.ObtenerPeriodoVigente(new DateOnly(2024, 10, 1)).ShouldBeNull();
        trabajador.ObtenerPeriodoVigente(new DateOnly(2025, 2, 1)).ShouldNotBeNull();
    }

    [Fact]
    public void PL3C_VIG_06_no_selecciona_un_periodo_futuro()
    {
        ConDosEpisodios().ObtenerPeriodoVigente(new DateOnly(2024, 12, 31)).ShouldBeNull();
    }

    [Fact]
    public void PL3C_VIG_07_multiples_periodos_vigentes_son_error()
    {
        var trabajador = ConPeriodo(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31));
        trabajador.PeriodosLaborales.Add(new PeriodoLaboral
        {
            EmpresaId = 2,
            FechaIngreso = new DateOnly(2024, 6, 1),
            FechaCese = new DateOnly(2025, 1, 31)
        });

        Should.Throw<ReglaNegocioException>(() =>
            trabajador.ObtenerPeriodoVigente(new DateOnly(2024, 7, 1)));
    }

    private static Trabajador ConDosEpisodios()
    {
        var trabajador = ConPeriodo(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30));
        trabajador.PeriodosLaborales.Add(new PeriodoLaboral
        {
            EmpresaId = 2,
            FechaIngreso = new DateOnly(2025, 1, 1)
        });
        return trabajador;
    }

    private static Trabajador ConPeriodo(DateOnly inicio, DateOnly? fin) => new()
    {
        PeriodosLaborales =
        [
            new PeriodoLaboral { EmpresaId = 1, FechaIngreso = inicio, FechaCese = fin }
        ]
    };
}

