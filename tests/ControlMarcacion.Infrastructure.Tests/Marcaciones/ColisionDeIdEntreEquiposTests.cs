using ControlMarcacion.Application.Marcaciones.Dto;
using ControlMarcacion.Application.Personal.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Marcaciones;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using ControlMarcacion.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Marcaciones;

/// <summary>
/// Dos aparatos en la misma planta, ambos numerando a su gente desde el 1.
/// </summary>
/// <remarks>
/// Es el caso real de Planta Detergentes: un marcador para apellidos A-L y otro para M-Z,
/// con usuarios del 1 al 100 en los dos. Antes de modelar el equipo, el código era la única
/// identidad del sistema y el usuario 14 de un aparato y el 14 del otro se resolvían al
/// mismo trabajador: sus marcaciones se fundían en una jornada sola —entrada del primero,
/// salida del segundo— y la fila resultante parecía correcta. Uno cobraba sobretiempo
/// ajeno y el otro desaparecía del tareo.
///
/// Estas pruebas fijan el comportamiento que lo impide. Si alguien vuelve a resolver por el
/// código a secas, o quita el equipo del índice único, fallan aquí y no en la planilla.
/// </remarks>
public sealed class ColisionDeIdEntreEquiposTests : IDisposable
{
    private const long PlantaDetergentes = 1;
    private const long PuestoOperario = 1;
    private const long EmpresaConsulting = 1;
    private const long TipoDocumentoDni = 1;

    private const long EquipoAL = 1;
    private const long EquipoMZ = 2;

    private const string SerieAL = "VGU6251600671";
    private const string SerieMZ = "VGU6251600672";

    /// <summary>El mismo código en los dos aparatos, dos personas distintas.</summary>
    private const string CodigoCompartido = "14";

    private static readonly DateOnly Dia = new(2026, 7, 6);

    private readonly MarcacionDbContext contexto;
    private readonly ServicioImportacion servicio;

    public ColisionDeIdEntreEquiposTests()
    {
        var opciones = new DbContextOptionsBuilder<MarcacionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        contexto = new MarcacionDbContext(opciones);
        contexto.Database.EnsureCreated();

        contexto.EquiposMarcador.AddRange(
            new EquipoMarcador
            {
                Id = EquipoAL,
                Codigo = "DET-AL",
                Nombre = "Detergentes A-L",
                Serie = SerieAL,
                PlantaId = PlantaDetergentes
            },
            new EquipoMarcador
            {
                Id = EquipoMZ,
                Codigo = "DET-MZ",
                Nombre = "Detergentes M-Z",
                Serie = SerieMZ,
                PlantaId = PlantaDetergentes
            });

        contexto.SaveChanges();

        var configuracion = Options.Create(new OpcionesMarcador { DiasAntiguedadMaxima = 100_000 });

        servicio = new ServicioImportacion(
            contexto,
            new LectorMarcadorAutomatico(
                new LectorTxtZkteco(configuracion), new LectorRespaldoZkteco(configuracion)),
            configuracion,
            new ServicioEquipos(contexto));
    }

    public void Dispose() => contexto.Dispose();

    /// <summary>
    /// La prueba que da sentido a todo el modelo: cada archivo va a su gente.
    /// </summary>
    [Fact]
    public async Task Cada_equipo_atribuye_sus_marcaciones_a_su_propio_trabajador()
    {
        var flores = await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");
        var quispe = await CrearTrabajadorAsync(EquipoMZ, CodigoCompartido, "41300114", "QUISPE");

        // Mismo código, misma planta, mismo día. Solo cambia el aparato.
        await ImportarAsync(EquipoAL, SerieAL, new TimeOnly(6, 48), new TimeOnly(19, 5));
        await ImportarAsync(EquipoMZ, SerieMZ, new TimeOnly(7, 15), new TimeOnly(19, 40));

        var crudas = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .OrderBy(m => m.FechaHora)
            .ToListAsync(CancellationToken.None);

        crudas.Count.ShouldBe(4);

        // La marcación no guarda de quién es: se atribuye con la misma regla que usa el
        // motor al calcular.
        var atribucion = new AtribucionMarcaciones(await contexto.AsignacionesMarcador
            .AsNoTracking()
            .Select(a => new VigenciaIdMarcador(a.TrabajadorId, a.EquipoId, a.IdMarcador, a.FechaInicio, a.FechaFin))
            .ToListAsync(CancellationToken.None));

        long? DuenoDe(MarcacionCruda m) => atribucion.Resolver(m.EquipoId, m.IdMarcador, m.FechaHora);

        // Ninguna marcación cruzó de un trabajador al otro.
        crudas.Where(m => m.EquipoId == EquipoAL).Select(DuenoDe).ShouldAllBe(dueno => dueno == flores);
        crudas.Where(m => m.EquipoId == EquipoMZ).Select(DuenoDe).ShouldAllBe(dueno => dueno == quispe);

        // Y cada uno conserva SUS horas, que es lo que la fusión destruía: antes FLORES
        // habría salido con la entrada suya y la salida de QUISPE.
        var deFlores = crudas.Where(m => DuenoDe(m) == flores).ToList();
        deFlores.Count.ShouldBe(2);
        deFlores[0].FechaHora.TimeOfDay.ShouldBe(new TimeSpan(6, 48, 0));
        deFlores[^1].FechaHora.TimeOfDay.ShouldBe(new TimeSpan(19, 5, 0));

        var deQuispe = crudas.Where(m => DuenoDe(m) == quispe).ToList();
        deQuispe.Count.ShouldBe(2);
        deQuispe[0].FechaHora.TimeOfDay.ShouldBe(new TimeSpan(7, 15, 0));
        deQuispe[^1].FechaHora.TimeOfDay.ShouldBe(new TimeSpan(19, 40, 0));
    }

    /// <summary>
    /// Dos marcaciones del mismo código y el mismo instante, pero de aparatos distintos,
    /// son dos hechos distintos y las dos se guardan.
    /// </summary>
    /// <remarks>
    /// Con la clave de deduplicación anterior —trabajador e instante— la segunda se habría
    /// descartado como repetida y contado entre las "duplicadas" del resumen, sin un solo
    /// error a la vista.
    /// </remarks>
    [Fact]
    public async Task Dos_equipos_pueden_registrar_el_mismo_codigo_en_el_mismo_instante()
    {
        await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");
        await CrearTrabajadorAsync(EquipoMZ, CodigoCompartido, "41300114", "QUISPE");

        var misma = new TimeOnly(6, 50);

        var primera = await ImportarAsync(EquipoAL, SerieAL, misma);
        var segunda = await ImportarAsync(EquipoMZ, SerieMZ, misma);

        primera.FilasInsertadas.ShouldBe(1);
        segunda.FilasInsertadas.ShouldBe(1);
        segunda.FilasDuplicadas.ShouldBe(0);

        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(2);
    }

    /// <summary>
    /// Declarar el equipo equivocado no atribuye el archivo a otra gente: se rechaza.
    /// </summary>
    [Fact]
    public async Task Un_archivo_cuya_serie_no_coincide_con_el_equipo_declarado_se_rechaza()
    {
        await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");

        // El archivo lo generó el A-L, pero se declara el M-Z.
        var bytes = RespaldoDePrueba.Archivo(
            SerieAL,
            new RespaldoDePrueba.Marca(14, Dia.ToDateTime(new TimeOnly(6, 48))));

        using var archivo = new MemoryStream(bytes);

        var error = await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.ImportarAsync(
                archivo,
                "BKtransaction.dat",
                PlantaDetergentes,
                EquipoMZ,
                "coordinador",
                CancellationToken.None));

        error.Message.ShouldContain(SerieAL);
        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(0);
    }

    /// <summary>El equipo tiene que ser de la planta declarada.</summary>
    [Fact]
    public async Task Declarar_una_planta_que_no_es_la_del_equipo_se_rechaza()
    {
        await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");

        var bytes = RespaldoDePrueba.Archivo(
            SerieAL,
            new RespaldoDePrueba.Marca(14, Dia.ToDateTime(new TimeOnly(6, 48))));

        using var archivo = new MemoryStream(bytes);

        const long otraPlanta = 3;

        await Should.ThrowAsync<ReglaNegocioException>(() =>
            servicio.ImportarAsync(
                archivo,
                "BKtransaction.dat",
                otraPlanta,
                EquipoAL,
                "coordinador",
                CancellationToken.None));

        (await contexto.MarcacionesCrudas.CountAsync(CancellationToken.None)).ShouldBe(0);
    }

    /// <summary>
    /// El listado de personal dice de qué aparato es cada ID.
    /// </summary>
    /// <remarks>
    /// El testeo integral encontró dos personas con «ID 14» en la misma planta y nada que
    /// dijera cuál era de qué aparato: la columna prometía identificar y no lo hacía. Si
    /// alguien quita el equipo de la proyección, esto falla aquí y no frente a un operador
    /// que corrige marcaciones mirando la pantalla.
    ///
    /// La tabla muestra el <b>código</b> del aparato en su propia columna y deja el nombre
    /// en el título de la celda: el nombre no cabe en trece columnas. Los dos salen de la
    /// misma asignación vigente, así que se comprueban juntos — un código sin su nombre
    /// dejaría el título vacío y la columna sin explicar.
    /// </remarks>
    [Fact]
    public async Task El_listado_de_personal_distingue_por_aparato_a_quienes_comparten_ID()
    {
        await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");
        await CrearTrabajadorAsync(EquipoMZ, CodigoCompartido, "41300114", "QUISPE");

        var consulta = new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion());

        var listado = await consulta.ListarAsync(new FiltroTrabajadores(), CancellationToken.None);

        var conElMismoCodigo = listado.Elementos
            .Where(t => t.IdMarcador == CodigoCompartido)
            .ToList();

        conElMismoCodigo.Count.ShouldBe(2);

        var flores = conElMismoCodigo.Single(t => t.ApellidosYNombres.StartsWith("FLORES", StringComparison.Ordinal));
        flores.Equipo.ShouldBe("Detergentes A-L");
        flores.EquipoCodigo.ShouldBe("DET-AL");

        var quispe = conElMismoCodigo.Single(t => t.ApellidosYNombres.StartsWith("QUISPE", StringComparison.Ordinal));
        quispe.Equipo.ShouldBe("Detergentes M-Z");
        quispe.EquipoCodigo.ShouldBe("DET-MZ");
    }

    /// <summary>
    /// Sin número asignado, las dos columnas quedan vacías en vez de mostrar media fila.
    /// </summary>
    /// <remarks>
    /// Es el estado normal mientras se completa el padrón, y la pantalla lo pinta como «—».
    /// Un código de aparato sin número diría que esa persona marca en un aparato cuando el
    /// sistema todavía no sabe con qué número.
    /// </remarks>
    [Fact]
    public async Task Quien_no_tiene_numero_asignado_sale_sin_aparato()
    {
        await CrearTrabajadorAsync(EquipoAL, CodigoCompartido, "41300014", "FLORES");

        var sinNumero = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = "41300999",
            ApellidoPaterno = "TORRES",
            ApellidoMaterno = "PRUEBA",
            Nombres = "SIN NUMERO",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaConsulting,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaDetergentes,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        await contexto.Trabajadores.AddAsync(sinNumero, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var listado = await new ConsultaTrabajadores(contexto, new AlcanceSinRestriccion())
            .ListarAsync(new FiltroTrabajadores(), CancellationToken.None);

        var fila = listado.Elementos.Single(t => t.NumeroDocumento == "41300999");
        fila.IdMarcador.ShouldBeNull();
        fila.EquipoCodigo.ShouldBeNull();
        fila.Equipo.ShouldBeNull();
    }

    private async Task<ResultadoImportacion> ImportarAsync(
        long equipoId,
        string serie,
        params TimeOnly[] horas)
    {
        var marcas = horas
            .Select(h => new RespaldoDePrueba.Marca(14, Dia.ToDateTime(h)))
            .ToArray();

        using var archivo = new MemoryStream(RespaldoDePrueba.Archivo(serie, marcas));

        return await servicio.ImportarAsync(
            archivo,
            "BKtransaction.dat",
            PlantaDetergentes,
            equipoId,
            "coordinador",
            CancellationToken.None);
    }

    private async Task<long> CrearTrabajadorAsync(
        long equipoId,
        string idMarcador,
        string documento,
        string apellido)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = apellido,
            ApellidoMaterno = "PRUEBA",
            Nombres = "COLISION",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaConsulting,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaDetergentes,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        trabajador.AsignacionesMarcador.Add(new AsignacionMarcador
        {
            EquipoId = equipoId,
            IdMarcador = idMarcador,
            FechaInicio = Dia.AddDays(-30)
        });

        await contexto.Trabajadores.AddAsync(trabajador, CancellationToken.None);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }
}
