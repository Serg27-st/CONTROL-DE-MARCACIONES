using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Importa de punta a punta el respaldo binario <b>real</b> del marcador nuevo.
/// </summary>
/// <remarks>
/// Los demás tests del formato usan respaldos fabricados, que prueban las reglas pero no
/// que el archivo de verdad se entienda. Este usa <c>Recursos/BKtransaction.dat</c> tal
/// como salió del equipo —serie <c>VGU6251600671</c>, plataforma <c>ZAM70</c>— y comprueba
/// lo único que importa al final: que las marcaciones <b>quedan en la base</b> atribuidas a
/// quien corresponde.
///
/// La muestra se versiona porque <b>no contiene datos personales</b>: son nueve registros
/// de una sesión de pruebas con los identificadores 1, 2 y 3, sin nombres, documentos ni
/// plantillas biométricas.
/// </remarks>
public sealed class ImportacionRespaldoRealTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long PuestoOperario = 1;
    private const long EmpresaOutsourcing = 2;
    private const long TipoDocumentoDni = 1;

    /// <summary>Equipo del que sale el respaldo de muestra.</summary>
    private const long EquipoDePrueba = 1;

    /// <summary>Serie que trae la cabecera del archivo real.</summary>
    private const string SerieDelEquipo = "VGU6251600671";

    /// <summary>Fecha de las marcaciones del archivo real.</summary>
    private static readonly DateOnly DiaDelRespaldo = new(2026, 8, 24);

    private static readonly string RutaRespaldoReal =
        Path.Combine(AppContext.BaseDirectory, "Recursos", "BKtransaction.dat");

    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;

    public ImportacionRespaldoRealTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        // El aparato del que sale el archivo. Con la serie declarada, la ingesta contrasta
        // la de la cabecera contra ella: si el respaldo de muestra viniera de otro equipo,
        // la importación se rechazaría en vez de atribuirlo a este padrón.
        contexto.EquiposMarcador.Add(new EquipoMarcador
        {
            Id = EquipoDePrueba,
            Codigo = "P01-A",
            Nombre = "Marcador de prueba",
            Serie = SerieDelEquipo,
            PlantaId = PlantaP01
        });

        contexto.SaveChanges();

        // Ventana de antigüedad amplia: lo que se prueba es la ingesta, no el rechazo por
        // fecha vieja, y con los 400 días por omisión este test empezaría a fallar solo
        // cuando agosto de 2026 quedara fuera.
        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });

        servicio = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose() => contexto.Dispose();

    [Fact]
    public void El_archivo_de_muestra_esta_donde_se_espera()
    {
        File.Exists(RutaRespaldoReal).ShouldBeTrue(
            $"Falta la muestra del respaldo en {RutaRespaldoReal}.");

        new FileInfo(RutaRespaldoReal).Length.ShouldBe(315);
    }

    /// <summary>
    /// La prueba que cierra el circuito: del binario del equipo a filas consultables.
    /// </summary>
    /// <remarks>
    /// Aquí se asignan los tres identificadores —incluido el del master— para comprobar que
    /// el parser extrae <b>los siete registros con identificador</b>. Que el master no
    /// debería estar en el padrón es una decisión de configuración, y tiene su propia
    /// prueba en <see cref="El_master_del_equipo_no_entra_al_tareo_si_no_esta_en_el_padron"/>.
    /// </remarks>
    [Fact]
    public async Task Las_marcaciones_del_respaldo_real_quedan_insertadas()
    {
        var master = await CrearTrabajadorAsync("1", "44444401", "MASTER");
        var dos = await CrearTrabajadorAsync("2", "44444402", "DOS");
        var tres = await CrearTrabajadorAsync("3", "44444403", "TRES");

        using var archivo = File.OpenRead(RutaRespaldoReal);

        var resultado = await servicio.ImportarAsync(
            archivo, "BKtransaction.dat", PlantaP01, EquipoDePrueba, "coordinador", CancellationToken.None);

        // Nueve registros en el archivo: siete marcaciones y dos que el equipo escribió
        // para sí mismo, sin identificador.
        resultado.FilasLeidas.ShouldBe(9);
        resultado.FilasInsertadas.ShouldBe(7);
        resultado.FilasIgnoradas.ShouldBe(2);
        resultado.FilasRechazadas.ShouldBe(0);
        resultado.IdsSinAsignar.ShouldBeEmpty();

        var crudas = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .OrderBy(m => m.FechaHora)
            .ToListAsync(CancellationToken.None);

        crudas.Count.ShouldBe(7);
        crudas.ShouldAllBe(m => m.FechaHora.Year == 2026 && m.FechaHora.Month == 8);

        // Primera y última del archivo, atribuidas con la regla del motor.
        var atribucion = await AtribucionAsync();
        long? DuenoDe(MarcacionCruda m) => atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora);

        crudas[0].IdMarcador.ShouldBe("2");
        DuenoDe(crudas[0]).ShouldBe(dos);
        crudas[0].FechaHora.ShouldBe(new DateTime(2026, 8, 24, 15, 13, 56));

        crudas[^1].IdMarcador.ShouldBe("1");
        DuenoDe(crudas[^1]).ShouldBe(master);
        crudas[^1].FechaHora.ShouldBe(new DateTime(2026, 8, 24, 16, 4, 46));

        // Tres identificadores distintos: tres marcaciones de cada usuario y una del master.
        crudas.Count(m => DuenoDe(m) == dos).ShouldBe(3);
        crudas.Count(m => DuenoDe(m) == tres).ShouldBe(3);
        crudas.Count(m => DuenoDe(m) == master).ShouldBe(1);
    }

    [Fact]
    public async Task BKIGN_02_y_09_una_carga_solo_con_eventos_ignorados_completa_sin_marcaciones_ni_errores()
    {
        var datos = RespaldoDePrueba.Archivo(
            new RespaldoDePrueba.Marca(0, new DateTime(2026, 10, 1, 8, 0, 0), Evento: 27),
            new RespaldoDePrueba.Marca(0, new DateTime(2026, 10, 1, 8, 1, 0), Verificacion: 200, Evento: 751),
            new RespaldoDePrueba.Marca(0, new DateTime(2026, 10, 1, 8, 2, 0), Verificacion: 200, Evento: 752));
        using var archivo = new MemoryStream(datos);

        var resultado = await servicio.ImportarAsync(
            archivo, "solo-eventos.dat", PlantaP01, EquipoDePrueba, "coordinador", CancellationToken.None);

        resultado.FilasLeidas.ShouldBe(3);
        resultado.FilasAceptadas.ShouldBe(0);
        resultado.FilasIgnoradas.ShouldBe(3);
        resultado.FilasRechazadas.ShouldBe(0);
        resultado.Rechazadas.ShouldBeEmpty();
        resultado.HuboProblemas.ShouldBeFalse();
        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(0);
        (await contexto.ErroresImportacion.CountAsync(CancellationToken.None)).ShouldBe(0);

        var recargado = await servicio.ObtenerResultadoAsync(resultado.ImportacionId, CancellationToken.None);
        recargado.ShouldNotBeNull();
        recargado.FilasIgnoradas.ShouldBe(3);
        recargado.HuboProblemas.ShouldBeFalse();

        var historial = await servicio.HistorialAsync(cancelacion: CancellationToken.None);
        historial.Single(x => x.Id == resultado.ImportacionId).FilasIgnoradas.ShouldBe(3);
    }

    /// <summary>
    /// Cargar dos veces el mismo archivo no duplica marcaciones.
    /// </summary>
    /// <remarks>
    /// Vale la pena comprobarlo sobre el binario y no solo sobre el texto: la
    /// deduplicación mira trabajador y fecha, y si el parser devolviera fechas
    /// ligeramente distintas entre dos lecturas del mismo archivo, la segunda carga
    /// entraría entera como nueva.
    /// </remarks>
    [Fact]
    public async Task Reimportar_el_mismo_respaldo_no_duplica_nada()
    {
        await CrearTrabajadorAsync("1", "44444401", "UNO");
        await CrearTrabajadorAsync("2", "44444402", "DOS");
        await CrearTrabajadorAsync("3", "44444403", "TRES");

        using (var primera = File.OpenRead(RutaRespaldoReal))
        {
            await servicio.ImportarAsync(primera, "BKtransaction.dat", PlantaP01, EquipoDePrueba, "coordinador", CancellationToken.None);
        }

        using var segunda = File.OpenRead(RutaRespaldoReal);
        var repetida = await servicio.ImportarAsync(
            segunda, "BKtransaction.dat", PlantaP01, EquipoDePrueba, "coordinador", CancellationToken.None);

        repetida.FilasInsertadas.ShouldBe(0);
        repetida.FilasDuplicadas.ShouldBe(7);

        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(7);
    }

    /// <summary>
    /// El escenario real: el <b>master no es un trabajador</b>.
    /// </summary>
    /// <remarks>
    /// En el equipo de prueba, <c>2</c> y <c>3</c> son usuarios y <c>1</c> es el master —la
    /// cuenta de administrador del propio marcador—. No hace falta ninguna regla en el
    /// código para dejarlo fuera del tareo: <b>basta con no asignarle ese ID a nadie</b>, y
    /// el padrón decide, que es el mecanismo que el sistema ya tiene. Sus registros se
    /// guardan —la marcación cruda es lo que dijo el aparato— y se reportan como ID sin
    /// asignar; al calcular no se atribuyen a nadie.
    ///
    /// Es también la red de seguridad contra un mapeo de campos equivocado: si el
    /// identificador se leyera del byte que no es, saldrían números que no corresponden a
    /// nadie y acabarían todos en esta misma lista. Por eso es lo primero que hay que mirar
    /// al cargar un respaldo de producción.
    /// </remarks>
    [Fact]
    public async Task El_master_del_equipo_no_entra_al_tareo_si_no_esta_en_el_padron()
    {
        var dos = await CrearTrabajadorAsync("2", "44444402", "DOS");
        var tres = await CrearTrabajadorAsync("3", "44444403", "TRES");

        using var archivo = File.OpenRead(RutaRespaldoReal);

        var resultado = await servicio.ImportarAsync(
            archivo, "BKtransaction.dat", PlantaP01, EquipoDePrueba, "coordinador", CancellationToken.None);

        // Las siete se guardan —la marcación cruda no depende del padrón—, y la del master
        // se avisa como ID sin asignar.
        resultado.FilasInsertadas.ShouldBe(7);
        resultado.IdsSinAsignar.Select(x => x.IdMarcador).ShouldBe(["1"]);
        resultado.IdsSinAsignar[0].Marcaciones.ShouldBe(1);
        resultado.HuboProblemas.ShouldBeTrue();

        var crudas = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        crudas.Count.ShouldBe(7);

        // Pero no es de nadie: al calcular, la del master no se atribuye y no entra al tareo.
        var atribucion = await AtribucionAsync();
        crudas.Where(m => m.IdMarcador == "1")
            .ShouldAllBe(m => atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora) == null);
        crudas.Where(m => m.IdMarcador != "1")
            .ShouldAllBe(m => atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora) == dos
                              || atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora) == tres);
    }

    /// <summary>La regla con que el motor atribuye cada marcación al calcular.</summary>
    private async Task<AtribucionMarcaciones> AtribucionAsync() =>
        new(await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Select(a => new VigenciaIdMarcador(a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(CancellationToken.None));

    private async Task<long> CrearTrabajadorAsync(string idMarcador, string documento, string nombres)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "RESPALDO",
            Nombres = nombres,
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = EquipoDePrueba,
            IdMarcador = idMarcador,
            FechaInicio = DiaDelRespaldo.AddDays(-30)
        });

        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }
}
