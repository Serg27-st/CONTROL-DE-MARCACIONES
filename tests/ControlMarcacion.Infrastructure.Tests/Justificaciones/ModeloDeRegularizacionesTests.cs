using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Justificaciones;

/// <summary>
/// El modelo que sostiene las regularizaciones, sobre <b>SQLite real</b>.
/// </summary>
/// <remarks>
/// Son pruebas de esquema, no de lógica: la fase que las llena todavía no existe. Lo que
/// fijan es que las piezas del modelo digan lo que creemos que dicen, porque tres de ellas
/// son el tipo de detalle que falla en silencio — un índice filtrado cuyo texto viaja
/// literal al SQL, un enum que se guarda como texto y un valor por omisión que EF genera
/// vacío.
///
/// InMemory no sirve aquí: ignora los índices y no tiene esquema que comprobar.
/// </remarks>
public sealed class ModeloDeRegularizacionesTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;

    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;

    public ModeloDeRegularizacionesTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// Una justificación nace aprobada. Es lo que hace que todo lo que ya existía siga
    /// contando: las que vienen de una solicitud ya pasaron por su propia autorización.
    /// </summary>
    [Fact]
    public async Task Una_justificacion_nace_aprobada()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444421");

        contexto.Justificaciones.Add(Fila(trabajadorId));
        await contexto.SaveChangesAsync(CancellationToken.None);

        var guardada = await contexto.Justificaciones.AsNoTracking().FirstAsync();

        guardada.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        guardada.Version.ShouldBe(0);
        guardada.EntradaRegularizada.ShouldBeNull();
        guardada.SalidaRegularizada.ShouldBeNull();
    }

    /// <summary>
    /// El estado se guarda como <b>texto</b>, y con exactamente la grafía que espera el
    /// filtro del índice único.
    /// </summary>
    /// <remarks>
    /// ⚠️ Es el acoplamiento frágil de esta migración: <c>HasFilter</c> lleva
    /// <c>'Rechazada'</c> escrito a mano, así que renombrar el valor del enum haría que el
    /// filtro dejara de coincidir <b>sin un solo error</b> — y un rechazo volvería a
    /// bloquear el día para siempre. Esta prueba es la que lo delata.
    /// </remarks>
    [Fact]
    public async Task El_estado_se_persiste_como_texto_con_la_grafia_que_usa_el_indice()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444422");

        var fila = Fila(trabajadorId);
        fila.Estado = EstadoJustificacion.Rechazada;
        fila.MotivoResolucion = "El sustento no corresponde a la fecha";

        contexto.Justificaciones.Add(fila);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var comoTexto = await contexto.Database
            .SqlQuery<string>($"SELECT estado AS Value FROM justificacion")
            .ToListAsync(CancellationToken.None);

        comoTexto.ShouldHaveSingleItem().ShouldBe("Rechazada");
    }

    /// <summary>
    /// Una regularización rechazada se conserva —es la evidencia de que se pidió y de por
    /// qué no se concedió— pero no bloquea el día: la persona puede volver a pedirla.
    /// </summary>
    [Fact]
    public async Task Una_justificacion_rechazada_no_bloquea_el_dia()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444423");

        var rechazada = Fila(trabajadorId);
        rechazada.Estado = EstadoJustificacion.Rechazada;
        rechazada.MotivoResolucion = "Sin sustento";

        contexto.Justificaciones.Add(rechazada);
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.Justificaciones.Add(Fila(trabajadorId));
        await contexto.SaveChangesAsync(CancellationToken.None);

        (await contexto.Justificaciones.CountAsync()).ShouldBe(2);
    }

    /// <summary>
    /// Lo que el filtro no puede relajar: dos justificaciones vigentes el mismo día siguen
    /// siendo imposibles.
    /// </summary>
    [Fact]
    public async Task Dos_justificaciones_vigentes_el_mismo_dia_siguen_rechazandose()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444424");

        contexto.Justificaciones.Add(Fila(trabajadorId));
        await contexto.SaveChangesAsync(CancellationToken.None);

        contexto.Justificaciones.Add(Fila(trabajadorId));

        await Should.ThrowAsync<DbUpdateException>(
            contexto.SaveChangesAsync(CancellationToken.None));
    }

    /// <summary>
    /// Dos personas resolviendo la misma regularización: la segunda no pisa a la primera.
    /// </summary>
    /// <remarks>
    /// Es el escenario del que no había defensa ninguna: SQLite no tiene <c>rowversion</c>,
    /// así que el token es un entero que incrementa quien escribe. EF lo mete en el
    /// <c>WHERE</c> del <c>UPDATE</c> y la segunda escritura no encuentra a quién actualizar.
    /// </remarks>
    [Fact]
    public async Task Dos_resoluciones_simultaneas_no_se_pisan()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444425");

        contexto.Justificaciones.Add(Fila(trabajadorId));
        await contexto.SaveChangesAsync(CancellationToken.None);

        // Dos sesiones distintas sobre la misma base, cada una con su seguimiento de cambios.
        await using var primera = NuevoContexto();
        await using var segunda = NuevoContexto();

        var paraAprobar = await primera.Justificaciones.FirstAsync(CancellationToken.None);
        var paraRechazar = await segunda.Justificaciones.FirstAsync(CancellationToken.None);

        Resolver(paraAprobar, EstadoJustificacion.Aprobada, "coordinador", "Verificado con el supervisor");
        await primera.SaveChangesAsync(CancellationToken.None);

        Resolver(paraRechazar, EstadoJustificacion.Rechazada, "otro", "No corresponde");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            segunda.SaveChangesAsync(CancellationToken.None));

        var final = await contexto.Justificaciones.AsNoTracking().FirstAsync(CancellationToken.None);

        final.Estado.ShouldBe(EstadoJustificacion.Aprobada);
        final.ResueltaPorUsuario.ShouldBe("coordinador");
        final.Version.ShouldBe(1);
    }

    /// <summary>
    /// Las dos horas conviven en la misma fila: es el día en que se olvidaron las dos
    /// marcas, que el motor no puede detectar pero sí se regulariza.
    /// </summary>
    [Fact]
    public async Task Una_regularizacion_guarda_las_dos_horas_y_la_incidencia_que_resuelve()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444426");

        var fila = Fila(trabajadorId);
        fila.Tipo = TipoJustificacion.MarcacionRegularizada;
        fila.TipoIncidencia = TipoIncidencia.IngresoNoRegistrado;
        fila.EntradaRegularizada = new DateTime(2026, 7, 1, 7, 58, 0);
        fila.SalidaRegularizada = new DateTime(2026, 7, 1, 17, 3, 0);

        contexto.Justificaciones.Add(fila);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var guardada = await contexto.Justificaciones.AsNoTracking().FirstAsync(CancellationToken.None);

        guardada.Tipo.ShouldBe(TipoJustificacion.MarcacionRegularizada);
        guardada.TipoIncidencia.ShouldBe(TipoIncidencia.IngresoNoRegistrado);
        guardada.EntradaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 7, 58, 0));
        guardada.SalidaRegularizada.ShouldBe(new DateTime(2026, 7, 1, 17, 3, 0));
    }

    /// <summary>
    /// El día del tareo guarda qué hay que resolver, aparte de cómo quedó.
    /// </summary>
    [Fact]
    public async Task El_dia_del_tareo_guarda_su_incidencia_aparte_del_estado()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444427");

        contexto.TareosDiarios.Add(new TareoDiario
        {
            PeriodoId = PeriodoJulio2026,
            TrabajadorId = trabajadorId,
            Fecha = Miercoles,
            Estado = EstadoTareo.PendienteValidacion,
            TipoIncidencia = TipoIncidencia.SinCoberturaDeCarga,
            RequiereValidacion = true,
            MarcacionesFueraVentana = 2,
            Observacion = "No se cargó el archivo de este aparato para esta fecha.",
            CalculadoEn = new DateTime(2026, 8, 1, 10, 0, 0)
        });

        await contexto.SaveChangesAsync(CancellationToken.None);

        var dia = await contexto.TareosDiarios.AsNoTracking().FirstAsync(CancellationToken.None);

        dia.Estado.ShouldBe(EstadoTareo.PendienteValidacion);
        dia.TipoIncidencia.ShouldBe(TipoIncidencia.SinCoberturaDeCarga);
        dia.RequiereValidacion.ShouldBeTrue();
        dia.MarcacionesFueraVentana.ShouldBe(2);
    }

    /// <summary>
    /// Un día corriente no tiene nada pendiente: los campos nuevos nacen apagados, así que
    /// todo lo que ya se calculaba sigue igual mientras la fase siguiente no los llene.
    /// </summary>
    [Fact]
    public async Task Un_dia_corriente_nace_sin_incidencia()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444428");

        contexto.TareosDiarios.Add(new TareoDiario
        {
            PeriodoId = PeriodoJulio2026,
            TrabajadorId = trabajadorId,
            Fecha = Miercoles,
            Estado = EstadoTareo.Puntual,
            Observacion = "Jornada normal.",
            CalculadoEn = new DateTime(2026, 8, 1, 10, 0, 0)
        });

        await contexto.SaveChangesAsync(CancellationToken.None);

        var dia = await contexto.TareosDiarios.AsNoTracking().FirstAsync(CancellationToken.None);

        dia.TipoIncidencia.ShouldBeNull();
        dia.RequiereValidacion.ShouldBeFalse();
        dia.MarcacionesFueraVentana.ShouldBe(0);
    }

    // ------------------------------------------------------------------

    private static void Resolver(
        Justificacion justificacion,
        EstadoJustificacion estado,
        string usuario,
        string motivo)
    {
        justificacion.Estado = estado;
        justificacion.ResueltaPorUsuario = usuario;
        justificacion.ResueltaEn = new DateTime(2026, 8, 2, 9, 0, 0);
        justificacion.MotivoResolucion = motivo;
        justificacion.Version++;
    }

    private MarcacionDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);

    private static Justificacion Fila(long trabajadorId) =>
        new()
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = Miercoles,
            Origen = OrigenJustificacion.Manual,
            Tipo = TipoJustificacion.MarcacionIncompleta,
            Motivo = "Olvidó marcar la salida",
            RegistradoPorUsuario = "admin",
            RegistradoEn = new DateTime(2026, 8, 1, 10, 0, 0)
        };

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "MODELO",
            Nombres = "REGULARIZACION",
            Sexo = Sexo.Masculino,
            EmpresaId = EmpresaOutsourcing,
            TipoPlanilla = TipoPlanilla.Obrero,
            PlantaId = PlantaP01,
            PuestoId = PuestoOperario,
            FechaIngreso = new DateOnly(2020, 1, 1)
        };

        contexto.Trabajadores.Add(trabajador);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return trabajador.Id;
    }
}
