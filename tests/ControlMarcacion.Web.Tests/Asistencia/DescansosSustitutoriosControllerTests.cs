using System.Security.Claims;
using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Application.Seguridad;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Web.Controllers;
using ControlMarcacion.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Web.Tests.Asistencia;

public sealed class DescansosSustitutoriosControllerTests
{
    [Fact]
    public async Task GET_permitido_carga_listado_y_formulario()
    {
        var fake = new ServicioFake();
        var controlador = CrearControlador(fake);

        var indice = await controlador.Index(CancellationToken.None);
        ((ViewResult)indice).Model.ShouldBeOfType<ListaDescansosSustitutoriosVm>();

        var crear = await controlador.Crear(CancellationToken.None);
        ((ViewResult)crear).Model.ShouldBeOfType<CrearDescansoSustitutorioVm>();
        fake.Listados.ShouldBe(1);
        fake.CargasTrabajadores.ShouldBe(1);
    }

    [Fact]
    public async Task POST_valido_otorga_y_redirige()
    {
        var fake = new ServicioFake();
        var controlador = CrearControlador(fake);

        var resultado = await controlador.Guardar(new CrearDescansoSustitutorioVm
        {
            Datos = Solicitud()
        }, CancellationToken.None);

        ((RedirectToActionResult)resultado).ActionName.ShouldBe("Index");
        fake.Otorgamientos.ShouldBe(1);
        fake.UltimoUsuario.ShouldBe("coordinador.prueba");
    }

    [Fact]
    public async Task POST_duplicado_muestra_error_y_no_redirige()
    {
        var fake = new ServicioFake
        {
            ErrorAlOtorgar = new ReglaNegocioException("Ya existe un descanso vigente.")
        };
        var controlador = CrearControlador(fake);

        var resultado = await controlador.Guardar(new CrearDescansoSustitutorioVm
        {
            Datos = Solicitud()
        }, CancellationToken.None);

        ((ViewResult)resultado).ViewName.ShouldBe("Crear");
        controlador.ModelState.IsValid.ShouldBeFalse();
        fake.CargasTrabajadores.ShouldBe(1);
    }

    [Fact]
    public async Task ModelState_invalido_no_invoca_escritura()
    {
        var fake = new ServicioFake();
        var controlador = CrearControlador(fake);
        controlador.ModelState.AddModelError("Datos.TrabajadorId", "Seleccione el trabajador.");

        var resultado = await controlador.Guardar(new CrearDescansoSustitutorioVm
        {
            Datos = Solicitud()
        }, CancellationToken.None);

        resultado.ShouldBeOfType<ViewResult>();
        fake.Otorgamientos.ShouldBe(0);
        fake.CargasTrabajadores.ShouldBe(1);
    }

    [Fact]
    public async Task anulacion_invoca_servicio_con_version_y_motivo()
    {
        var fake = new ServicioFake();
        var controlador = CrearControlador(fake);

        var resultado = await controlador.Anular(new AnularDescansoSustitutorioRequest
        {
            Id = 7,
            Version = 2,
            Motivo = "Registro equivocado"
        }, CancellationToken.None);

        ((RedirectToActionResult)resultado).ActionName.ShouldBe("Index");
        fake.Anulaciones.ShouldBe(1);
        fake.UltimaAnulacion!.Version.ShouldBe(2);
    }

    [Fact]
    public void permisos_quedan_limitados_a_coordinador_y_administrador()
    {
        var atributo = typeof(DescansosSustitutoriosController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        var roles = atributo.Roles.ShouldNotBeNull();
        roles.ShouldBe($"{RolesSistema.Coordinador},{RolesSistema.Administrador}");
        roles.ShouldNotContain(RolesSistema.Gerencia);
        roles.ShouldNotContain(RolesSistema.TrabajadorSocial);
    }

    private static RegistrarDescansoSustitutorioRequest Solicitud() => new()
    {
        TrabajadorId = 1,
        FechaFeriadoOrigen = new DateOnly(2026, 7, 28),
        FechaDescanso = new DateOnly(2026, 7, 30)
    };

    private static DescansosSustitutoriosController CrearControlador(ServicioFake servicio)
    {
        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "coordinador.prueba")], "prueba");
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identidad)
        };
        return new DescansosSustitutoriosController(servicio)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = contexto
            },
            TempData = new TempDataDictionary(contexto, new ProveedorTempDataFake())
        };
    }

    private sealed class ProveedorTempDataFake : ITempDataProvider
    {
        private readonly Dictionary<string, object> datos = [];

        public IDictionary<string, object> LoadTempData(HttpContext context) => datos;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            datos.Clear();
            foreach (var par in values)
            {
                datos[par.Key] = par.Value;
            }
        }
    }

    private sealed class ServicioFake : IServicioDescansosSustitutorios
    {
        public int Listados { get; private set; }
        public int CargasTrabajadores { get; private set; }
        public int Otorgamientos { get; private set; }
        public int Anulaciones { get; private set; }
        public string? UltimoUsuario { get; private set; }
        public AnularDescansoSustitutorioRequest? UltimaAnulacion { get; private set; }
        public Exception? ErrorAlOtorgar { get; init; }

        public Task<IReadOnlyList<DescansoSustitutorioDto>> ListarAsync(
            CancellationToken cancelacion = default)
        {
            Listados++;
            return Task.FromResult<IReadOnlyList<DescansoSustitutorioDto>>([]);
        }

        public Task<IReadOnlyList<OpcionDto>> TrabajadoresAsync(
            CancellationToken cancelacion = default)
        {
            CargasTrabajadores++;
            return Task.FromResult<IReadOnlyList<OpcionDto>>([new(1, "PRUEBA, UNO")]);
        }

        public Task<long> OtorgarAsync(
            RegistrarDescansoSustitutorioRequest solicitud,
            string usuario,
            CancellationToken cancelacion = default)
        {
            Otorgamientos++;
            UltimoUsuario = usuario;
            if (ErrorAlOtorgar is not null)
            {
                throw ErrorAlOtorgar;
            }

            return Task.FromResult(1L);
        }

        public Task AnularAsync(
            AnularDescansoSustitutorioRequest solicitud,
            string usuario,
            CancellationToken cancelacion = default)
        {
            Anulaciones++;
            UltimaAnulacion = solicitud;
            UltimoUsuario = usuario;
            return Task.CompletedTask;
        }
    }
}
