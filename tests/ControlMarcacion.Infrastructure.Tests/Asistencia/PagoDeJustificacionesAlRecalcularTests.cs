using Microsoft.Extensions.Logging.Abstractions;
using ControlMarcacion.Domain.Asistencia;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Justificaciones;
using ControlMarcacion.Domain.Marcaciones;
using ControlMarcacion.Domain.Personal;
using ControlMarcacion.Domain.Solicitudes;
using ControlMarcacion.Infrastructure.Persistencia;
using ControlMarcacion.Infrastructure.Persistencia.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ControlMarcacion.Infrastructure.Tests.Asistencia;

/// <summary>
/// Qué paga el recálculo cuando hay una justificación de por medio, sobre <b>SQLite real</b>.
/// </summary>
/// <remarks>
/// El motor puro ya fija estas reglas; lo que se prueba aquí es el otro lado: que el
/// orquestador le entregue los datos correctos. Era justamente ahí donde se perdían — la
/// consulta resolvía el tipo de ausencia con una subconsulta que traía un solo campo y leía
/// «sin tipo de ausencia» como «sí computa como asistencia».
///
/// Va sobre SQLite y no sobre InMemory porque la proyección lleva subconsultas con
/// <c>FirstOrDefault</c> —la ventana del permiso sale de la solicitud que lo generó— y ese
/// es exactamente el patrón que InMemory ejecuta en memoria sin traducir nada.
/// </remarks>
public sealed class PagoDeJustificacionesAlRecalcularTests : IDisposable
{
    private const long PlantaP01 = 3;
    private const long EmpresaOutsourcing = 2;
    private const long PuestoOperario = 1;
    private const long TipoDocumentoDni = 1;
    private const long PeriodoJulio2026 = 1;
    private const long TurnoDiurno = 1;

    private const long TipoLicenciaSinGoce = 4;
    private const long TipoLicenciaConGoce = 3;
    private const long TipoPermisoPorHoras = 5;
    private const long TipoVacaciones = 1;

    /// <summary>Miércoles 1 de julio de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 7, 1);

    private readonly SqliteConnection conexion;
    private readonly MarcacionDbContext contexto;
    private readonly ServicioCalculoTareo servicio;
    private readonly long equipoId;

    public PagoDeJustificacionesAlRecalcularTests()
    {
        conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();

        contexto = new MarcacionDbContext(
            new DbContextOptionsBuilder<MarcacionDbContext>().UseSqlite(conexion).Options);
        contexto.Database.EnsureCreated();

        var equipo = new EquipoMarcador
        {
            Codigo = "P01-A",
            Nombre = "Planta 1",
            PlantaId = PlantaP01,
            Activo = true
        };

        contexto.EquiposMarcador.Add(equipo);
        contexto.SaveChanges();
        equipoId = equipo.Id;

        servicio = new ServicioCalculoTareo(contexto, new AlcanceSinRestriccion(), CoberturaDePrueba.TodoCargado(), NullLogger<ServicioCalculoTareo>.Instance);
    }

    public void Dispose()
    {
        contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>
    /// Una licencia sin goce justifica la falta y deja el día en cero. Antes pagaba la
    /// jornada ordinaria completa porque el catálogo la sembró computando como asistencia.
    /// </summary>
    [Fact]
    public async Task Una_licencia_sin_goce_no_paga_la_jornada()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444401");
        await JustificarAsync(trabajadorId, TipoJustificacion.Ausencia, TipoLicenciaSinGoce);

        var dia = await RecalcularYLeerAsync(trabajadorId);

        dia.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        dia.MinutosTrabajados.ShouldBe(0);
        dia.MinutosOrdinarios.ShouldBe(0);
    }

    [Fact]
    public async Task Unas_vacaciones_siguen_pagando_la_jornada_ordinaria()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444402");
        await JustificarAsync(trabajadorId, TipoJustificacion.Ausencia, TipoVacaciones);

        var dia = await RecalcularYLeerAsync(trabajadorId);

        dia.MinutosOrdinarios.ShouldBe(480);
    }

    /// <summary>
    /// AUS-PAG-01: prueba el catálogo real y el recálculo persistido, no solo el motor
    /// puro. LCG y VAC son categorías distintas, aunque comparten comportamiento pagado.
    /// </summary>
    [Fact]
    public async Task Una_licencia_con_goce_completa_persiste_480_minutos()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444405");
        await JustificarAsync(trabajadorId, TipoJustificacion.Ausencia, TipoLicenciaConGoce);

        var dia = await RecalcularYLeerAsync(trabajadorId);

        dia.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        dia.MinutosTrabajados.ShouldBe(480);
        dia.MinutosOrdinarios.ShouldBe(480);
        dia.MinutosExtra25.ShouldBe(0);
        dia.MinutosExtra35.ShouldBe(0);
        dia.MinutosNocturnos.ShouldBe(0);
    }

    /// <summary>
    /// Completar una marca impar no inventa una jornada de ocho horas.
    /// </summary>
    /// <remarks>
    /// El turno diurno de GCA son doce horas efectivas (480 ordinarias + 240 de
    /// sobretiempo). Pagar 480 planos por una marca impar justificada le borraba a la
    /// persona cuatro horas de jornada y todo su sobretiempo del día, y encima lo etiquetaba
    /// como ausencia habiendo trabajado.
    ///
    /// Cero tampoco es la respuesta final —lo correcto es registrar la hora que faltó, y eso
    /// exige el campo de hora regularizada que todavía no existe—, pero es la respuesta
    /// conservadora: el sistema no sabe hasta qué hora estuvo, así que no se inventa nada.
    /// </remarks>
    [Fact]
    public async Task Una_marca_impar_justificada_no_paga_una_jornada_ordinaria_inventada()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444403");

        await CrearMarcacionesAsync(trabajadorId, new DateTime(2026, 7, 1, 7, 0, 0));
        await JustificarAsync(trabajadorId, TipoJustificacion.MarcacionIncompleta, tipoAusenciaId: null);

        var dia = await RecalcularYLeerAsync(trabajadorId);

        dia.Estado.ShouldBe(EstadoTareo.AusenciaJustificada);
        dia.MinutosTrabajados.ShouldBe(0);
        dia.MinutosOrdinarios.ShouldBe(0);
    }

    /// <summary>
    /// La ventana del permiso llega al motor desde la solicitud que lo generó, así que un
    /// permiso al final de la jornada descuenta salida anticipada y no tardanza.
    /// </summary>
    [Fact]
    public async Task Un_permiso_al_final_de_la_jornada_borra_la_salida_anticipada()
    {
        var trabajadorId = await CrearTrabajadorAsync("44444404");

        // Turno diurno 07:00–19:00: entra puntual y se va a las 17:00, dos horas antes.
        await CrearMarcacionesAsync(trabajadorId,
            new DateTime(2026, 7, 1, 7, 0, 0),
            new DateTime(2026, 7, 1, 17, 0, 0));

        var sinPermiso = await RecalcularYLeerAsync(trabajadorId);
        sinPermiso.MinutosSalidaAnticipada.ShouldBe(120);

        var solicitudId = await CrearSolicitudPorHorasAsync(
            trabajadorId, new TimeOnly(17, 0), new TimeOnly(19, 0));

        await JustificarAsync(
            trabajadorId, TipoJustificacion.PermisoParcial, TipoPermisoPorHoras, solicitudId);

        var conPermiso = await RecalcularYLeerAsync(trabajadorId);

        conPermiso.MinutosSalidaAnticipada.ShouldBe(0);
        conPermiso.MinutosTardanza.ShouldBe(0);
        conPermiso.MinutosOrdinarios.ShouldBe(600);
        conPermiso.MinutosOrdinarios.ShouldNotBe(480);
        conPermiso.Observacion.ShouldNotBeNull();
        conPermiso.Observacion.ShouldContain("PERMISO PARCIAL de 17:00 a 19:00");
    }

    // ------------------------------------------------------------------

    private async Task<TareoDiario> RecalcularYLeerAsync(long trabajadorId)
    {
        await servicio.RecalcularPeriodoAsync(PeriodoJulio2026, "test", CancellationToken.None);

        return await contexto.TareosDiarios
            .AsNoTracking()
            .FirstAsync(t => t.TrabajadorId == trabajadorId && t.Fecha == Miercoles);
    }

    private async Task<long> CrearTrabajadorAsync(string documento)
    {
        var trabajador = new Trabajador
        {
            TipoDocumentoId = TipoDocumentoDni,
            NumeroDocumento = documento,
            ApellidoPaterno = "PRUEBA",
            ApellidoMaterno = "PAGO",
            Nombres = "JUSTIFICACION",
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

    private async Task CrearMarcacionesAsync(long trabajadorId, params DateTime[] momentos)
    {
        var carga = new ImportacionMarcador
        {
            NombreArchivo = "prueba.txt",
            EquipoId = equipoId,
            HashContenido = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            FechaCarga = new DateTime(2026, 8, 1, 9, 0, 0),
            CargadoPorUsuario = "test",
            FilasLeidas = momentos.Length,
            FilasInsertadas = momentos.Length
        };

        contexto.Importaciones.Add(carga);
        await contexto.SaveChangesAsync(CancellationToken.None);

        var numero = await contexto.AsignacionesMarcador
            .Where(a => a.TrabajadorId == trabajadorId)
            .Select(a => a.IdMarcador)
            .FirstAsync(CancellationToken.None);

        foreach (var momento in momentos)
        {
            contexto.MarcacionesCrudas.Add(new MarcacionCruda
            {
                EquipoId = equipoId,
                IdMarcador = numero,
                FechaHora = momento,
                ImportacionId = carga.Id
            });
        }

        await contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<long> CrearSolicitudPorHorasAsync(long trabajadorId, TimeOnly desde, TimeOnly hasta)
    {
        var solicitud = new SolicitudAusencia
        {
            TrabajadorId = trabajadorId,
            TipoAusenciaId = TipoPermisoPorHoras,
            FechaInicio = Miercoles,
            FechaFin = Miercoles,
            HoraInicio = desde,
            HoraFin = hasta,
            Estado = EstadoSolicitud.Aprobada,
            Observacion = "Trámite personal"
        };

        contexto.SolicitudesAusencia.Add(solicitud);
        await contexto.SaveChangesAsync(CancellationToken.None);

        return solicitud.Id;
    }

    private async Task JustificarAsync(
        long trabajadorId,
        TipoJustificacion tipo,
        long? tipoAusenciaId,
        long? solicitudId = null)
    {
        contexto.Justificaciones.Add(new Justificacion
        {
            TrabajadorId = trabajadorId,
            PeriodoId = PeriodoJulio2026,
            Fecha = Miercoles,
            Origen = solicitudId is null ? OrigenJustificacion.Manual : OrigenJustificacion.Solicitud,
            SolicitudAusenciaId = solicitudId,
            TipoAusenciaId = tipoAusenciaId,
            Tipo = tipo,
            Motivo = "Justificación de prueba",
            RegistradoPorUsuario = "test",
            RegistradoEn = new DateTime(2026, 8, 1, 10, 0, 0)
        });

        await contexto.SaveChangesAsync(CancellationToken.None);
    }
}
