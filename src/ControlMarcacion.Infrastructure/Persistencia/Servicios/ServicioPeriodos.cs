using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Mantenimiento de periodos de tareo.
/// </summary>
/// <remarks>
/// El periodo es la frontera que protege la integridad del tareo. Estas son las reglas:
/// <list type="bullet">
///   <item>El código es único y el rango de fechas no puede solaparse con otro periodo.</item>
///   <item>Un periodo <b>cerrado</b> no admite modificar su código, nombre ni fechas:
///   el rango define qué tareos cuelgan de él, y moverlo reasignaría datos ya cerrados.</item>
///   <item>Solo se puede cerrar un periodo <b>abierto</b>.</item>
///   <item>Solo se puede reabrir un periodo <b>cerrado</b>, y el motivo es obligatorio:
///   es información de auditoría de por qué se descongeló algo ya pagado.</item>
/// </list>
/// </remarks>
internal sealed class ServicioPeriodos(MarcacionDbContext contexto) : IServicioPeriodos
{
    public async Task<IReadOnlyList<PeriodoListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default) =>
        await Proyectar(
                contexto.Periodos.AsNoTracking()
                    .Where(p => incluirInactivos || p.Activo)
                    .OrderByDescending(p => p.FechaInicio))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<PeriodoListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.Periodos.AsNoTracking().Where(p => p.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarPeriodoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();
        var nombre = solicitud.Nombre.Trim();

        Validar(solicitud);

        Periodo periodo;

        if (solicitud.Id is { } id)
        {
            periodo = await contexto.Periodos
                .FirstOrDefaultAsync(p => p.Id == id, cancelacion)
                .ConfigureAwait(false)
                ?? throw new RecursoNoEncontradoException("el periodo", id);

            // Se comprueba ANTES que la unicidad y el solapamiento: si el periodo está
            // cerrado, da igual lo demás, y el usuario merece el motivo real del rechazo
            // en vez de un error de código duplicado que no explica nada.
            if (!periodo.EstaAbierto)
            {
                throw new ReglaNegocioException(
                    $"El periodo '{periodo.Codigo}' está cerrado y no admite modificaciones. " +
                    "Para corregirlo debe reabrirse explícitamente, indicando el motivo.");
            }

            periodo.ModificadoPor = usuario;
        }
        else
        {
            periodo = new Periodo { CreadoPor = usuario };
        }

        var repetido = await contexto.Periodos
            .AnyAsync(p => p.Codigo == codigo && p.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException($"Ya existe un periodo con el código '{codigo}'.");
        }

        // Cada fecha pertenece a un solo periodo: dos rangos no pueden solaparse, o el
        // sistema no sabría de qué periodo es un tareo que cae en la intersección.
        //
        // Los periodos dados de baja NO bloquean: si se creó uno con las fechas mal y se
        // desactivó, hay que poder crear el correcto sobre ese mismo rango. Antes quedaba
        // bloqueado para siempre y no había forma de salir sin tocar la base a mano.
        var solapado = await contexto.Periodos
            .AnyAsync(
                p => p.Id != solicitud.Id
                    && p.Activo
                    && p.FechaInicio <= solicitud.FechaFin
                    && p.FechaFin >= solicitud.FechaInicio,
                cancelacion)
            .ConfigureAwait(false);

        if (solapado)
        {
            throw new ReglaNegocioException(
                $"El rango {solicitud.FechaInicio:dd/MM/yyyy} – {solicitud.FechaFin:dd/MM/yyyy} " +
                "se solapa con otro periodo activo. Cada fecha debe pertenecer a un solo periodo.");
        }

        if (solicitud.Id is null)
        {
            await contexto.Periodos.AddAsync(periodo, cancelacion).ConfigureAwait(false);
        }

        periodo.Codigo = codigo;
        periodo.Nombre = nombre;
        periodo.FechaInicio = solicitud.FechaInicio;
        periodo.FechaFin = solicitud.FechaFin;
        periodo.Activo = solicitud.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return periodo.Id;
    }

    public async Task CerrarAsync(long id, string usuario, CancellationToken cancelacion = default)
    {
        var periodo = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (!periodo.EstaAbierto)
        {
            throw new ReglaNegocioException(
                $"El periodo '{periodo.Codigo}' ya está cerrado.");
        }

        // Cerrar un periodo dado de baja no significa nada: no va a recibir tareos, y
        // dejaría un registro de cierre con fecha y usuario sobre algo que no se usa.
        if (!periodo.Activo)
        {
            throw new ReglaNegocioException(
                $"El periodo '{periodo.Codigo}' está dado de baja y no puede cerrarse. " +
                "Actívelo primero si realmente corresponde cerrarlo.");
        }

        periodo.Estado = EstadoPeriodo.Cerrado;
        periodo.CerradoEn = DateTime.Now;
        periodo.CerradoPor = usuario;
        periodo.ModificadoPor = usuario;

        // Cerrar es de las dos únicas operaciones que INCREMENTAN la versión (la otra es
        // reabrir): invalida toda escritura que estuviera en vuelo dentro del periodo, que
        // es justamente lo que se quiere al cerrar el mes.
        periodo.Version++;

        await GuardarConcurrenteAsync(periodo, "cerraba el periodo", cancelacion).ConfigureAwait(false);
    }

    public async Task ReabrirAsync(
        long id,
        string motivo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var periodo = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (periodo.EstaAbierto)
        {
            throw new ReglaNegocioException(
                $"El periodo '{periodo.Codigo}' ya está abierto.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ReglaNegocioException(
                "Debe indicar el motivo de la reapertura: es información de auditoría.");
        }

        periodo.Estado = EstadoPeriodo.Abierto;
        periodo.ReabiertoEn = DateTime.Now;
        periodo.ReabiertoPor = usuario;
        periodo.MotivoReapertura = motivo.Trim();
        periodo.ModificadoPor = usuario;
        periodo.Version++;

        await GuardarConcurrenteAsync(periodo, "reabría el periodo", cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Guarda el cambio de estado del periodo traduciendo el choque de concurrencia.
    /// </summary>
    /// <remarks>
    /// Dos cierres simultáneos pasaban los dos la comprobación y escribían los dos: el
    /// último ganaba, y <c>CerradoPor</c> quedaba a nombre de quien llegó después mientras
    /// la auditoría mostraba dos cierres del mismo periodo. Ahora el segundo se entera.
    /// </remarks>
    private async Task GuardarConcurrenteAsync(
        Periodo periodo,
        string queSeEstabaHaciendo,
        CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw await contexto
                .ExplicarChoqueAsync(periodo.Id, queSeEstabaHaciendo, cancelacion)
                .ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DiaSinPeriodo>> DetectarDiasSinPeriodoAsync(
        CancellationToken cancelacion = default)
    {
        // Se agrupa en la base y no en memoria: son miles de marcaciones por carga y traerlas
        // todas para contarlas aquí sería un desperdicio.
        var diasConMarcaciones = await contexto.MarcacionesCrudas
            .AsNoTracking()
            .GroupBy(m => m.FechaHora.Date)
            .Select(g => new { Fecha = g.Key, Total = g.Count() })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (diasConMarcaciones.Count == 0)
        {
            return [];
        }

        var rangos = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.FechaInicio, p.FechaFin })
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        return
        [
            .. diasConMarcaciones
                .Select(d => new { Fecha = DateOnly.FromDateTime(d.Fecha), d.Total })
                .Where(d => !rangos.Any(r => r.FechaInicio <= d.Fecha && r.FechaFin >= d.Fecha))
                .OrderBy(d => d.Fecha)
                .Select(d => new DiaSinPeriodo(d.Fecha, d.Total))
        ];
    }

    // ------------------------------------------------------------------

    private async Task<Periodo> BuscarAsync(long id, CancellationToken cancelacion) =>
        await contexto.Periodos
            .FirstOrDefaultAsync(p => p.Id == id, cancelacion)
            .ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException("el periodo", id);

    private IQueryable<PeriodoListaDto> Proyectar(IQueryable<Periodo> consulta) =>
        consulta.Select(p => new PeriodoListaDto(
            p.Id,
            p.Codigo,
            p.Nombre,
            p.FechaInicio,
            p.FechaFin,
            p.Estado,
            p.CerradoEn,
            p.CerradoPor,
            p.ReabiertoEn,
            p.ReabiertoPor,
            p.MotivoReapertura,
            p.Activo,
            contexto.TareosDiarios.Count(t => t.PeriodoId == p.Id)));

    private static void Validar(GuardarPeriodoRequest solicitud)
    {
        if (solicitud.FechaFin < solicitud.FechaInicio)
        {
            throw new ReglaNegocioException(
                "La fecha de fin no puede ser anterior a la fecha de inicio.");
        }
    }
}
