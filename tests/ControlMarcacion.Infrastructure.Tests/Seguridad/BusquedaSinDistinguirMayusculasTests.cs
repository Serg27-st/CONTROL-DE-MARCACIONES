using ControlMarcacion.Application.Auditoria.Dto;
using ControlMarcacion.Domain.Auditoria;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Seguridad;

/// <summary>
/// Los buscadores por texto encuentran sin importar la caja de lo que se teclea.
/// </summary>
/// <remarks>
/// El filtro por usuario de la bitácora comparaba con <c>Contains</c> a secas, que sobre
/// SQLite se traduce a <c>instr()</c> —byte a byte—: «SIST» no encontraba a
/// <c>sistemas@gca.pe</c> aunque «sist» devolviera 27 movimientos. Con SQL Server
/// funcionaba solo, porque su colación por omisión no distingue caja, así que el cambio de
/// motor lo rompió en silencio.
///
/// ⚠️ Este test corre sobre InMemory, que compara con la semántica de .NET y por tanto
/// <b>tampoco</b> reproduce la colación de SQL Server. Eso lo hace útil precisamente aquí:
/// en .NET <c>Contains</c> también distingue caja, de modo que si alguien quita el
/// <c>ToUpper()</c> de la columna o el <c>BusquedaTexto.Normalizar</c> del término, esta
/// prueba se pone roja. Lo que NO cubre es la traducción a SQL; para eso hay que consultar
/// la base real.
/// </remarks>
public sealed class BusquedaSinDistinguirMayusculasTests : IDisposable
{
    private readonly MarcacionDbContext contexto;
    private readonly ConsultaAuditoria consulta;

    public BusquedaSinDistinguirMayusculasTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();
        consulta = new ConsultaAuditoria(contexto);
    }

    public void Dispose() => contexto.Dispose();

    [Theory]
    [InlineData("sistemas")]        // tal como está guardado
    [InlineData("SISTEMAS")]        // todo en mayúsculas
    [InlineData("Sistemas")]        // capitalizado
    [InlineData("SiStEmAs")]        // mezclado
    [InlineData("  sistemas  ")]    // con espacios de sobra
    public async Task El_filtro_por_usuario_encuentra_escriba_como_escriba(string tecleado)
    {
        await SembrarAsync("sistemas@gca.pe");
        await SembrarAsync("coordinador@gca.pe");

        var pagina = await consulta.ListarAsync(
            new FiltroAuditoria { Usuario = tecleado }, CancellationToken.None);

        pagina.TotalElementos.ShouldBe(1);
        pagina.Elementos[0].Usuario.ShouldBe("sistemas@gca.pe");
    }

    /// <summary>
    /// Y al revés: un usuario guardado en mayúsculas se encuentra tecleando en minúscula.
    /// Los dos lados se pliegan, no solo el término.
    /// </summary>
    [Fact]
    public async Task Tambien_encuentra_un_usuario_guardado_en_mayusculas()
    {
        await SembrarAsync("RRHH@GCA.PE");

        var pagina = await consulta.ListarAsync(
            new FiltroAuditoria { Usuario = "rrhh" }, CancellationToken.None);

        pagina.TotalElementos.ShouldBe(1);
    }

    /// <summary>Buscar sigue acotando: no devuelve la bitácora entera.</summary>
    [Fact]
    public async Task El_filtro_sigue_descartando_lo_que_no_coincide()
    {
        await SembrarAsync("sistemas@gca.pe");
        await SembrarAsync("coordinador@gca.pe");

        var pagina = await consulta.ListarAsync(
            new FiltroAuditoria { Usuario = "zzz" }, CancellationToken.None);

        pagina.TotalElementos.ShouldBe(0);
    }

    private async Task SembrarAsync(string usuario)
    {
        await contexto.RegistrosAuditoria.AddAsync(new RegistroAuditoria
        {
            Tabla = "trabajador",
            ClaveRegistro = "1",
            Operacion = OperacionAuditada.Modificacion,
            Usuario = usuario,
            FechaHora = new DateTime(2026, 7, 15, 10, 0, 0),
            Detalle = "[]"
        });

        await contexto.SaveChangesAsync();
    }
}
