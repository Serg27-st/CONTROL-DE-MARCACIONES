using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Personal;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Web.Controllers;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Marcaciones;

public sealed class VinculacionesBiometricasControllerTests
{
    [Fact]
    public void Acceso_queda_limitado_a_coordinador()
    {
        var atributo = typeof(VinculacionesBiometricasController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Single();

        atributo.Roles.ShouldBe(RolesSistema.Coordinador);
    }

    [Fact]
    public async Task Archivo_renombrado_se_rechaza_antes_del_lector()
    {
        var lector = new LectorFake();
        var confirmacion = new ConfirmacionFake();
        var controlador = CrearControlador(lector, confirmacion);

        var resultado = await controlador.Previsualizar(new VinculacionBiometricaIndexVm
        {
            PlantaId = 10,
            EquipoId = 20,
            FechaInicio = DateOnly.FromDateTime(DateTime.Today),
            Archivo = Archivo("usuarios.dat")
        }, CancellationToken.None);

        ((ViewResult)resultado).ViewName.ShouldBe("Index");
        lector.Lecturas.ShouldBe(0);
        confirmacion.Preparaciones.ShouldBe(0);
    }

    [Fact]
    public async Task UserU_valido_prepara_lote_y_solo_pendiente_es_confirmable()
    {
        var lector = new LectorFake();
        var confirmacion = new ConfirmacionFake();
        var controlador = CrearControlador(lector, confirmacion);

        var resultado = await controlador.Previsualizar(new VinculacionBiometricaIndexVm
        {
            PlantaId = 10,
            EquipoId = 20,
            FechaInicio = DateOnly.FromDateTime(DateTime.Today),
            Archivo = Archivo("user.u")
        }, CancellationToken.None);

        var vm = ((ViewResult)resultado).Model.ShouldBeOfType<PrevisualizacionVinculacionBiometricaVm>();
        vm.TotalUsuarios.ShouldBe(1);
        vm.HuellaSha256.ShouldBe(new string('A', 64));
        vm.Filas.Single().EsConfirmable.ShouldBeTrue();
        lector.Lecturas.ShouldBe(1);
        confirmacion.Preparaciones.ShouldBe(1);
    }

    [Fact]
    public async Task Confirmacion_envia_solo_identificadores_versionados()
    {
        var confirmacion = new ConfirmacionFake();
        var controlador = CrearControlador(new LectorFake(), confirmacion);

        var resultado = await controlador.Confirmar(new RevisarVinculacionesBiometricasVm
        {
            ConciliacionId = ConfirmacionFake.Lote,
            Version = 1,
            Decisiones = [7, 7]
        }, CancellationToken.None);

        ((ViewResult)resultado).ViewName.ShouldBe("Resultado");
        confirmacion.UltimaConfirmacion.ShouldNotBeNull();
        confirmacion.UltimaConfirmacion.Decisiones.ShouldBe([7]);
    }

    private static VinculacionesBiometricasController CrearControlador(
        LectorFake lector,
        ConfirmacionFake confirmacion) =>
        new(new CatalogosFake(), new TrabajadoresFake(), lector, confirmacion);

    private static FormFile Archivo(string nombre)
    {
        var contenido = new MemoryStream([1, 2, 3]);
        return new FormFile(contenido, 0, contenido.Length, "Archivo", nombre);
    }

    private sealed class LectorFake : ILectorUsuariosZkteco
    {
        public int Lecturas { get; private set; }

        public ResultadoLecturaUsuariosZkteco Leer(
            Stream contenido,
            EquipoOrigenUsuariosZkteco equipo,
            OpcionesLecturaUsuariosZkteco? opciones = null)
        {
            Lecturas++;
            return new(
                equipo,
                EstadoArchivoUsuariosZkteco.Valido,
                [new(1, 1, "15", "12345678", false, false, EstadoRegistroUsuarioZkteco.Leido,
                    EstadoCandidatoIdentidadZkteco.NoVerificado, [])],
                [],
                "user.u/table=1/fields=1..11",
                new string('A', 64),
                3,
                DateTime.UtcNow);
        }
    }

    private sealed class ConfirmacionFake : IServicioConfirmacionVinculacionBiometrica
    {
        public static readonly Guid Lote = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public int Preparaciones { get; private set; }
        public ConfirmarVinculacionesBiometricasRequest? UltimaConfirmacion { get; private set; }

        public Task<PropuestaConfirmacionBiometricaDto> PrepararAsync(
            PrepararConfirmacionBiometricaRequest solicitud,
            CancellationToken cancelacion = default)
        {
            Preparaciones++;
            return Task.FromResult(new PropuestaConfirmacionBiometricaDto(
                Lote,
                1,
                DateTime.Now.AddMinutes(30),
                [new(7, 1, "15", 30, "12345678",
                    BaseConfirmacionIdentidadBiometrica.ReglaOperativaUserU,
                    NaturalezaVigenciaBiometrica.Declarada,
                    solicitud.FechaInicio,
                    solicitud.FechaFin,
                    "Pendiente",
                    null,
                    [DiagnosticoConciliacionBiometrica.DocumentoCoincideExactamentePorReglaOperativa.ToString()])]));
        }

        public Task<ResultadoConfirmacionBiometricaLote> ConfirmarLoteAsync(
            ConfirmarVinculacionesBiometricasRequest solicitud,
            CancellationToken cancelacion = default)
        {
            UltimaConfirmacion = solicitud;
            return Task.FromResult(new ResultadoConfirmacionBiometricaLote(
                solicitud.ConciliacionId,
                solicitud.Version,
                solicitud.Decisiones.Select(id =>
                    new ResultadoDecisionConfirmacionBiometrica(id, "Creada", null, 90)).ToArray()));
        }
    }

    private sealed class CatalogosFake : IConsultaCatalogos
    {
        public Task<IReadOnlyList<EquipoOpcionDto>> EquiposActivosAsync(CancellationToken cancelacion = default) =>
            Task.FromResult<IReadOnlyList<EquipoOpcionDto>>([new(20, "EQ-01", "Principal", 10, "Planta 1")]);

        public Task<IReadOnlyList<TipoDocumentoOpcionDto>> TiposDocumentoAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<TipoDocumentoOpcionDto?> TipoDocumentoPorIdAsync(long id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> EmpresasAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> PlantasAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<long?> EmpresaDePlantaAsync(long id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> AreasAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> SeccionesAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> CentrosAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> CentrosCostoAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> PuestosAsync(long? id = null, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OpcionDto>> TurnosAsync(long? id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CuadrillaOpcionDto>> CuadrillasActivasAsync(CancellationToken c = default) => throw new NotSupportedException();
        public Task<CuadrillaOpcionDto?> CuadrillaPorIdAsync(long id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MarcadorCatalogoDto>> MarcadoresPorCodigoAsync(string codigo, long planta, CancellationToken c = default) => throw new NotSupportedException();
    }

    private sealed class TrabajadoresFake : IConsultaTrabajadores
    {
        public Task<TrabajadorDetalleDto?> ObtenerDetalleAsync(long id, CancellationToken c = default) => Task.FromResult<TrabajadorDetalleDto?>(null);
        public Task<ResultadoPaginado<TrabajadorListaDto>> ListarAsync(FiltroTrabajadores f, CancellationToken c = default) => throw new NotSupportedException();
        public Task<TrabajadorPanelDto?> ObtenerPanelAsync(long id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TrabajadorBusquedaRapidaDto>> BuscarRapidoAsync(string? t, CancellationToken c = default) => throw new NotSupportedException();
        public Task<HistorialTrabajadorDto?> ObtenerHistorialAsync(long id, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<long, ContextoLaboralFechaDto>> ContextosLaboralesEnFechaAsync(IReadOnlyCollection<long> ids, DateOnly f, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IntegranteCuadrillaDto>> IntegrantesDeCuadrillaAsync(long id, DateOnly f, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CandidatoCuadrillaDto>> CandidatosParaCuadrillaAsync(long id, long planta, DateOnly f, CancellationToken c = default) => throw new NotSupportedException();
    }
}
