using ControlMarcacion.Application.Asistencia;
using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Marcaciones;
using ControlMarcacion.Application.Reportes;
using ControlMarcacion.Application.Reportes.Dto;
using ControlMarcacion.Application.Solicitudes;
using ControlMarcacion.Application.Solicitudes.Dto;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Reportes;
using ControlMarcacion.Domain.Solicitudes;

namespace ControlMarcacion.Application.Inicio;

/// <summary>
/// Reúne el estado del mes desde los servicios de cada módulo.
/// </summary>
/// <remarks>
/// Es un orquestador: no consulta la base directamente ni conoce ningún <c>DbSet</c>.
/// Cada cifra la produce el módulo que la posee —cobertura la produce Marcaciones, el
/// reparto por estado lo produce Asistencia—, de modo que la portada no puede acabar
/// diciendo algo distinto de la pantalla a la que enlaza.
///
/// Cada bloque se resuelve por separado y <b>tolera su propio fallo</b>: si falta un
/// parámetro legal y el estado del cálculo revienta, la portada sigue mostrando el periodo
/// y las solicitudes en vez de caerse entera. Una portada que no carga deja al usuario sin
/// punto de partida.
/// </remarks>
public sealed class ServicioPanelInicio(
    IServicioPeriodos periodos,
    IServicioImportacion importacion,
    IServicioCalculoTareo tareo,
    IServicioSolicitudes solicitudes,
    IServicioReportes reportes) : IServicioPanelInicio
{
    public async Task<PanelInicioDto> ObtenerAsync(
        string usuario,
        CancellationToken cancelacion = default)
    {
        var pendientes = await ContarSolicitudesPendientesAsync(cancelacion).ConfigureAwait(false);

        // La lista viene ordenada por fecha de inicio descendente: el primero es el periodo
        // sobre el que se está trabajando. Es el mismo criterio que usa la pantalla de
        // Tareo al entrar sin elegir nada, y tienen que coincidir.
        var lista = await periodos
            .ListarAsync(incluirInactivos: false, cancelacion)
            .ConfigureAwait(false);

        if (lista.Count == 0)
        {
            return PanelInicioDto.Vacio with { SolicitudesPendientes = pendientes };
        }

        var actual = lista[0];

        var periodo = new PeriodoActualDto(
            actual.Id, actual.Codigo, actual.Nombre,
            actual.FechaInicio, actual.FechaFin, actual.EstaAbierto);

        var cobertura = await importacion
            .CoberturaDeRangoAsync(actual.FechaInicio, actual.FechaFin, cancelacion)
            .ConfigureAwait(false);

        return new PanelInicioDto(
            periodo,
            cobertura,
            await ResumirTareoAsync(actual.Id, usuario, cancelacion).ConfigureAwait(false),
            pendientes);
    }

    /// <summary>
    /// Cuántas solicitudes esperan decisión. Se pide <b>una sola fila</b>: lo que interesa
    /// es el total que declara la paginación, no las solicitudes en sí.
    /// </summary>
    private async Task<int> ContarSolicitudesPendientesAsync(CancellationToken cancelacion)
    {
        var pagina = await solicitudes
            .ListarAsync(
                new FiltroSolicitudes { Estado = EstadoSolicitud.Pendiente, TamanioPagina = 1 },
                cancelacion)
            .ConfigureAwait(false);

        return pagina.TotalElementos;
    }

    /// <summary>
    /// Reparto por estado del periodo más el sello de vigencia del cálculo.
    /// </summary>
    /// <remarks>
    /// El estado del cálculo se pide a Reportes y no se deduce aquí: es la misma
    /// comprobación que avisa dentro del Excel, y tener dos versiones de "el tareo está
    /// desactualizado" es garantía de que una de ellas mienta.
    /// </remarks>
    private async Task<TareoDelPeriodoDto?> ResumirTareoAsync(
        long periodoId,
        string usuario,
        CancellationToken cancelacion)
    {
        var resumen = await tareo
            .ResumirAsync(new FiltroTareo { PeriodoId = periodoId }, cancelacion)
            .ConfigureAwait(false);

        EstadoCalculoTareo calculo;

        try
        {
            var metadatos = await reportes
                .ObtenerMetadatosAsync(
                    new FiltroReporte { Alcance = AlcanceReporte.Periodo, PeriodoId = periodoId },
                    usuario,
                    cancelacion)
                .ConfigureAwait(false);

            calculo = metadatos.Calculo;
        }
        catch (ReglaNegocioException)
        {
            // Alcance mal formado o parámetro legal ausente. No es motivo para dejar la
            // portada en blanco: se muestra el resumen y el estado queda como "sin
            // calcular", que es la lectura conservadora.
            calculo = new EstadoCalculoTareo(null, null, null);
        }
        catch (RecursoNoEncontradoException)
        {
            calculo = new EstadoCalculoTareo(null, null, null);
        }

        return new TareoDelPeriodoDto(
            resumen.Trabajadores,
            resumen.Dias,
            resumen.Conteo(Domain.Asistencia.EstadoTareo.Falta),
            resumen.Conteo(Domain.Asistencia.EstadoTareo.Tardanza),
            resumen.MinutosExtra,
            resumen.Conteo(Domain.Asistencia.EstadoTareo.PendienteValidacion),
            calculo);
    }
}
