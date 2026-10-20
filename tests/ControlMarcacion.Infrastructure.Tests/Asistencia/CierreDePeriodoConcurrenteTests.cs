using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// «Periodo cerrado, nada se escribe» cuando el cierre ocurre <b>mientras</b> se escribe.
/// </summary>
/// <remarks>
/// <para>
/// Comprobar que el periodo está abierto y guardar eran dos operaciones separadas. En el
/// recálculo median <b>minutos</b> —lee el estado al empezar y escribe decenas de miles de
/// filas al final—, así que bastaba con que alguien cerrara el mes mientras otro recalculaba
/// para meter tareo en un periodo cerrado, sin un solo error a la vista.
/// </para>
/// <para>
/// La carrera se reproduce sin hilos ni esperas: se deja el periodo <b>ya seguido</b> por el
/// contexto del servicio antes de que otro lo cierre. EF resuelve por identidad —devuelve la
/// instancia que ya tenía, sin refrescar sus valores—, así que el servicio ve exactamente lo
/// que veía quien empezó a recalcular antes del cierre. Es el mismo estado que produciría la
/// concurrencia real, y encima es determinista.
/// </para>
/// </remarks>
public sealed class CierreDePeriodoConcurrenteTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo calculo;
    private readonly long equipoId;

    public CierreDePeriodoConcurrenteTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = NuevoContexto();
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador { Codigo = "P01-A", Nombre = "Planta 1", PlantaId = PlantaP01, Activo = true };
        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        equipoId = equipo.Id;

        calculo = new ServicioCalculoTareo(
            contexto,
            new AlcanceSinRestriccion(),
            CoberturaDePrueba.TodoCargado(),
            NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// <b>La prueba que importa.</b> Si el periodo se cierra después de que el recálculo
    /// leyó su estado, no se escribe ni una fila de tareo.
    /// </summary>
    [Fact]
    public async Task Un_cierre_a_mitad_del_recalculo_no_deja_escribir_el_tareo()
    {
        var trabajadorId = await CrearTrabajadorAsync("66666601");
        await MarcarAsync(trabajadorId);

        // El servicio ya tiene el periodo en su seguimiento, abierto: es lo que veía quien
        // lanzó el recálculo un segundo antes del cierre.
        await contexto.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);

        await CerrarDesdeOtraSesionAsync();

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "admin", CancellationToken.None));

        error.Message.ShouldContain("se cerró mientras");
        error.Message.ShouldContain("recalculaba el tareo");

        // Lo esencial: el lote entero se perdió. Ni un día quedó escrito en el periodo que
        // acababa de cerrarse.
        await using var verificacion = NuevoContexto();
        (await verificacion.TareosDiarios.CountAsync(CancellationToken.None)).ShouldBe(0);
    }

    /// <summary>
    /// Dos cierres a la vez: el segundo se entera en vez de pisar al primero.
    /// </summary>
    /// <remarks>
    /// Antes los dos pasaban la comprobación y los dos escribían: ganaba el último, así que
    /// <c>CerradoPor</c> quedaba a nombre de quien llegó después mientras la auditoría
    /// mostraba dos cierres del mismo periodo.
    /// </remarks>
    [Fact]
    public async Task Dos_cierres_simultaneos_no_se_pisan()
    {
        await using var segunda = NuevoContexto();
        var servicioSegundo = new ServicioPeriodos(segunda);

        // La segunda sesión ya tenía el periodo cargado y abierto.
        await segunda.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);

        await new ServicioPeriodos(contexto)
            .CerrarAsync(PeriodoJulio2026, "coordinador", CancellationToken.None);

        var error = await Should.ThrowAsync<ReglaNegocioException>(
            servicioSegundo.CerrarAsync(PeriodoJulio2026, "otro", CancellationToken.None));

        error.Message.ShouldContain("cerraba el periodo");

        await using var verificacion = NuevoContexto();
        var periodo = await verificacion.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);

        periodo.CerradoPor.ShouldBe("coordinador");
        periodo.Version.ShouldBe(1);
    }

    /// <summary>
    /// Dos recálculos simultáneos <b>sí</b> conviven: afirmar la versión no es consumirla.
    /// </summary>
    /// <remarks>
    /// Es la mitad menos obvia del diseño y la que evita que el guardián se vuelva ruido. Si
    /// recalcular incrementara la versión, un administrador recalculando sus plantas y un
    /// coordinador recalculando todo chocarían sin que nada malo hubiera pasado — y quien
    /// viera ese aviso aprendería a ignorarlo, que es como se pierde el aviso de verdad.
    /// </remarks>
    [Fact]
    public async Task Recalcular_no_consume_la_version_del_periodo()
    {
        var trabajadorId = await CrearTrabajadorAsync("66666602");
        await MarcarAsync(trabajadorId);

        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "admin", CancellationToken.None);
        await calculo.RecalcularPeriodoAsync(PeriodoJulio2026, "coordinador", CancellationToken.None);

        await using var verificacion = NuevoContexto();
        var periodo = await verificacion.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);

        periodo.Version.ShouldBe(0);

        // Y el recálculo siguió siendo idempotente: un día por trabajador, no dos.
        (await verificacion.TareosDiarios.CountAsync(t => t.Fecha == Miercoles, CancellationToken.None))
            .ShouldBe(1);
    }

    /// <summary>
    /// Cerrar sí la consume: es lo que invalida cualquier escritura en vuelo.
    /// </summary>
    [Fact]
    public async Task Cerrar_y_reabrir_incrementan_la_version()
    {
        var servicio = new ServicioPeriodos(contexto);

        await servicio.CerrarAsync(PeriodoJulio2026, "coordinador", CancellationToken.None);
        await servicio.ReabrirAsync(
            PeriodoJulio2026, "Faltaba cargar un archivo", "coordinador", CancellationToken.None);

        await using var verificacion = NuevoContexto();
        var periodo = await verificacion.Periodos.FirstAsync(p => p.Id == PeriodoJulio2026, CancellationToken.None);

        periodo.Version.ShouldBe(2);
        periodo.EstaAbierto.ShouldBeTrue();
    }

    // ------------------------------------------------------------------

    private MarcacionDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);

    /// <summary>Cierra el periodo desde otra sesión, como haría otra persona.</summary>
    private async Task CerrarDesdeOtraSesionAsync()
    {
        await using var otra = NuevoContexto();

        await new ServicioPeriodos(otra)
            .CerrarAsync(PeriodoJulio2026, "coordinador", CancellationToken.None);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "CIERRE",
            Nombres = "CONCURRENTE",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesTurno.Add(new AsignacionTurno
        {
            TurnoId = TurnoDiurno,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = documento,
            FechaInicio = new DateOnly(2020, 1, 1)
        });

        trabajador.AbrirVinculo("prueba");
        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }

    private async Task MarcarAsync(long trabajadorId)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "prueba.txt",
            EquipoId = equipoId,
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = 2,
            FilasInsertadas = 2
        };

        contexto.Importaciones.Add(carga);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var numero = await contexto.AsignacionesMarcador
            .Where(a => a.TrabajadorId == trabajadorId)
            .Select(a => a.IdMarcador)
            .FirstAsync(CancellationToken.None);

        contexto.MarcacionesCrudas.AddRange(
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = Miercoles.ToDateTime(new TimeOnly(7, 0)),
                ImportacionId = carga.Id
            },
            new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = Miercoles.ToDateTime(new TimeOnly(19, 0)),
                ImportacionId = carga.Id
            });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
