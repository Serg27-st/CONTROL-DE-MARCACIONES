using ControlMarcacion.Application.Justificaciones.Dto;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// Paginación del listado de justificaciones.
/// </summary>
/// <remarks>
/// Es la tabla que más crece del sistema: aprobar una solicitud genera una justificación
/// por cada día cubierto, así que unas vacaciones de 30 días son 30 filas de una sola vez.
/// Antes el listado devolvía todo lo que hubiera, y sus tres filtros son opcionales.
/// </remarks>
public sealed class PaginacionJustificacionesTests : IDisposable
{
    private const long PeriodoJulio2026 = 1;

    private readonly MarcacionDbContext contexto;
    private readonly ServicioJustificaciones servicio;

    public PaginacionJustificacionesTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        servicio = new ServicioJustificaciones(contexto, new AlcanceSinRestriccion());
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public async Task El_listado_devuelve_una_pagina_y_no_la_tabla_entera()
    {
        await SembrarAsync(120);

        var pagina1 = await servicio.ListarAsync(new FiltroJustificaciones(), CancellationToken.None);

        pagina1.Elementos.Count.ShouldBe(50);
        pagina1.TotalElementos.ShouldBe(120);
        pagina1.TotalPaginas.ShouldBe(3);
        pagina1.HayPaginaAnterior.ShouldBeFalse();
        pagina1.HayPaginaSiguiente.ShouldBeTrue();
    }

    [Fact]
    public async Task La_ultima_pagina_trae_el_resto_y_no_ofrece_siguiente()
    {
        await SembrarAsync(120);

        var pagina3 = await servicio.ListarAsync(
            new FiltroJustificaciones { Pagina = 3 }, CancellationToken.None);

        pagina3.Elementos.Count.ShouldBe(20);
        pagina3.HayPaginaSiguiente.ShouldBeFalse();
        pagina3.HayPaginaAnterior.ShouldBeTrue();
    }

    [Fact]
    public async Task Ninguna_fila_se_repite_ni_se_pierde_entre_paginas()
    {
        // El desempate por Id existe justamente para esto: varias justificaciones comparten
        // fecha —las genera la misma aprobación— y sin un orden total, SQL Server puede
        // devolver la misma fila en dos páginas y omitir otra.
        await SembrarAsync(120);

        var vistos = new List<long>();

        for (var pagina = 1; pagina <= 3; pagina++)
        {
            var resultado = await servicio.ListarAsync(
                new FiltroJustificaciones { Pagina = pagina }, CancellationToken.None);

            vistos.AddRange(resultado.Elementos.Select(j => j.Id));
        }

        vistos.Count.ShouldBe(120);
        vistos.Distinct().Count().ShouldBe(120);
    }

    [Fact]
    public async Task Una_pagina_mas_alla_del_total_devuelve_vacio_sin_fallar()
    {
        await SembrarAsync(10);

        var resultado = await servicio.ListarAsync(
            new FiltroJustificaciones { Pagina = 99 }, CancellationToken.None);

        resultado.Elementos.ShouldBeEmpty();
        resultado.TotalElementos.ShouldBe(10);
    }

    [Fact]
    public async Task Una_pagina_invalida_se_trata_como_la_primera()
    {
        // La página llega de la barra de direcciones: sin filtro, un 0 o un negativo
        // producirían un Skip negativo y una excepción de SQL Server.
        await SembrarAsync(10);

        foreach (var pagina in new[] { 0, -5 })
        {
            var resultado = await servicio.ListarAsync(
                new FiltroJustificaciones { Pagina = pagina }, CancellationToken.None);

            resultado.Pagina.ShouldBe(1);
            resultado.Elementos.Count.ShouldBe(10);
        }
    }

    [Fact]
    public async Task El_total_respeta_el_filtro_y_no_cuenta_la_tabla_completa()
    {
        await SembrarAsync(30, OrigenJustificacion.Solicitud);
        await SembrarAsync(12, OrigenJustificacion.Manual, desdeDia: 1000);

        var manuales = await servicio.ListarAsync(
            new FiltroJustificaciones { Origen = OrigenJustificacion.Manual }, CancellationToken.None);

        manuales.TotalElementos.ShouldBe(12);
        manuales.Elementos.Count.ShouldBe(12);
        manuales.TotalPaginas.ShouldBe(1);
    }

    private async Task SembrarAsync(
        int cantidad,
        OrigenJustificacion origen = OrigenJustificacion.Solicitud,
        int desdeDia = 0)
    {
        // Varias justificaciones comparten fecha a propósito: es lo que pasa al aprobar
        // una solicitud de varios trabajadores para el mismo día.
        var justificaciones = Enumerable.Range(0, cantidad).Select(i => new Justificacion
        {
            TrabajadorId = i + 1,
            PeriodoId = PeriodoJulio2026,
            Fecha = new DateOnly(2026, 7, 1).AddDays((desdeDia + i) % 5),
            Origen = origen,
            Tipo = TipoJustificacion.Ausencia,
            Motivo = "Prueba de paginación",
            RegistradoPorUsuario = "test",
            RegistradoEn = new DateTime(2026, 7, 1, 8, 0, 0)
        });

        await contexto.Justificaciones.AddRangeAsync(justificaciones, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
