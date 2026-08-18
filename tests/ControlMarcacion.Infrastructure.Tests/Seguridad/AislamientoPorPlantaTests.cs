using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Seguridad;

/// <summary>
/// Aislamiento por planta (Hallazgo 1): un ADMINISTRADOR solo ve —y solo toca— trabajadores de
/// las plantas que gestiona. COORDINADOR y GERENCIA (alcance «sin restricción») ven todo.
/// </summary>
/// <remarks>
/// El control vive en la consulta de Application/Infrastructure, no en la vista: por eso se
/// prueba directamente sobre los servicios, y no solo que el listado se recorte —también que el
/// acceso por id directo a un trabajador de otra planta responda como si no existiera (404),
/// que es la diferencia entre un control real y ocultar un botón.
/// </remarks>
public sealed class AislamientoPorPlantaTests : IDisposable
{
    private const long PlantaDetergentes = 1;   // Consulting Advisers
    private const long PlantaSulfonacion = 2;    // CA Outsourcing

    private readonly MarcacionDbContext contexto;

    public AislamientoPorPlantaTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => contexto.Dispose();

    // ------------------------------------------------------------------
    // Personal (ConsultaTrabajadores)
    // ------------------------------------------------------------------

    [Fact]
    public async Task El_listado_de_personal_solo_trae_los_de_las_plantas_del_administrador()
    {
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");
        await SembrarTrabajadorAsync(2, PlantaDetergentes, "BBB");
        await SembrarTrabajadorAsync(3, PlantaSulfonacion, "CCC");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaDetergentes));

        var pagina = await consulta.ListarAsync(new FiltroTrabajadores(), CancellationToken.None);

        pagina.TotalElementos.ShouldBe(2);
        pagina.Elementos.Select(e => e.Id).ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public async Task Sin_restriccion_el_listado_de_personal_trae_todas_las_plantas()
    {
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");
        await SembrarTrabajadorAsync(2, PlantaSulfonacion, "BBB");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion());

        var pagina = await consulta.ListarAsync(new FiltroTrabajadores(), CancellationToken.None);

        pagina.TotalElementos.ShouldBe(2);
    }

    [Fact]
    public async Task Un_administrador_sin_plantas_no_ve_ningun_trabajador()
    {
        // Fail-closed: la lista vacía significa «no ve ninguna». El alta exige al menos una
        // planta justamente para que esto no le pase a una cuenta real, pero el filtro tiene
        // que ser estricto por su cuenta.
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceDePlantas());

        var pagina = await consulta.ListarAsync(new FiltroTrabajadores(), CancellationToken.None);

        pagina.TotalElementos.ShouldBe(0);
    }

    [Fact]
    public async Task La_ficha_de_un_trabajador_de_otra_planta_no_se_puede_abrir_por_id()
    {
        await SembrarTrabajadorAsync(3, PlantaSulfonacion, "CCC");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaDetergentes));

        // Acceso por id directo (IDOR): filtrar el listado no basta si por la URL se llega
        // igual a la ficha. Detalle, panel e historial responden null → 404 en el controlador.
        (await consulta.ObtenerDetalleAsync(3, CancellationToken.None)).ShouldBeNull();
        (await consulta.ObtenerPanelAsync(3, CancellationToken.None)).ShouldBeNull();
        (await consulta.ObtenerHistorialAsync(3, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task La_ficha_de_un_trabajador_de_la_planta_propia_si_se_abre_por_id()
    {
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceDePlantas(PlantaDetergentes));

        (await consulta.ObtenerDetalleAsync(1, CancellationToken.None)).ShouldNotBeNull();
    }

    // ------------------------------------------------------------------
    // Solicitudes (ServicioSolicitudes)
    // ------------------------------------------------------------------

    [Fact]
    public async Task El_listado_de_solicitudes_solo_trae_las_de_las_plantas_del_administrador()
    {
        SembrarTipoAusencia();
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");
        await SembrarTrabajadorAsync(2, PlantaSulfonacion, "BBB");
        await SembrarSolicitudAsync(10, trabajadorId: 1);
        await SembrarSolicitudAsync(20, trabajadorId: 2);

        var servicio = new ServicioSolicitudes(contexto, new AlcanceDePlantas(PlantaDetergentes));

        var pagina = await servicio.ListarAsync(new FiltroSolicitudes(), CancellationToken.None);

        pagina.TotalElementos.ShouldBe(1);
        pagina.Elementos.Single().Id.ShouldBe(10);
    }

    [Fact]
    public async Task Una_solicitud_de_otra_planta_no_se_puede_abrir_por_id()
    {
        SembrarTipoAusencia();
        await SembrarTrabajadorAsync(2, PlantaSulfonacion, "BBB");
        await SembrarSolicitudAsync(20, trabajadorId: 2);

        var servicio = new ServicioSolicitudes(contexto, new AlcanceDePlantas(PlantaDetergentes));

        (await servicio.ObtenerAsync(20, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task El_desplegable_de_trabajadores_de_solicitudes_se_acota_a_las_plantas_propias()
    {
        await SembrarTrabajadorAsync(1, PlantaDetergentes, "AAA");
        await SembrarTrabajadorAsync(2, PlantaSulfonacion, "BBB");

        var servicio = new ServicioSolicitudes(contexto, new AlcanceDePlantas(PlantaDetergentes));

        var opciones = await servicio.TrabajadoresAsync(CancellationToken.None);

        opciones.Select(o => o.Id).ShouldBe([1]);
    }

    // ------------------------------------------------------------------

    private async Task SembrarTrabajadorAsync(long id, long plantaId, string apellido)
    {
        contexto.Trabajadores.Add(new Trabajador
        {
            Id = id,
            TipoDocumentoId = 1,
            NumeroDocumento = $"1000000{id}",
            ApellidoPaterno = apellido,
            ApellidoMaterno = "PRUEBA",
            Nombres = "TEST",
            Sexo = Sexo.Masculino,
            EmpresaId = 1,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = plantaId,
            PuestoId = 1,
            FechaIngreso = new DateOnly(2024, 1, 1)
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private void SembrarTipoAusencia()
    {
        if (!contexto.TiposAusencia.Any(t => t.Id == 1))
        {
            contexto.TiposAusencia.Add(new TipoAusencia
            {
                Id = 1,
                Codigo = "PER",
                Nombre = "Permiso",
                Comportamiento = ComportamientoAusencia.JustificadaNoRemunerada
            });
        }
    }

    private async Task SembrarSolicitudAsync(long id, long trabajadorId)
    {
        contexto.SolicitudesAusencia.Add(new SolicitudAusencia
        {
            Id = id,
            TrabajadorId = trabajadorId,
            TipoAusenciaId = 1,
            FechaInicio = new DateOnly(2026, 7, 10),
            FechaFin = new DateOnly(2026, 7, 10),
            Estado = EstadoSolicitud.Pendiente,
            CreadoPor = "test"
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
