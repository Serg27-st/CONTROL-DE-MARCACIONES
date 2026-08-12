using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Mantenimiento de tipos de ausencia.
/// </summary>
/// <remarks>
/// Un tipo de ausencia no es código más nombre: el <see cref="ComportamientoAusencia"/>
/// decide cómo el motor de cálculo lo trata. Estas son las reglas de negocio:
/// <list type="bullet">
///   <item>El código es único y se guarda en mayúsculas, como en el resto de catálogos.</item>
///   <item>El comportamiento es obligatorio y debe ser un valor definido del enum: un tipo
///   sin comportamiento no se puede calcular y caería en silencio.</item>
///   <item>Un tipo <b>ya utilizado</b> no puede cambiar su comportamiento: el comportamiento
///   con el que se calculó el tareo histórico es parte de su definición y cambiarlo
///   reasignaría lo ya cerrado sin dejar rastro.</item>
///   <item>No se puede desactivar un tipo que esté en uso por solicitudes o justificaciones,
///   porque dejaría registros huérfanos.</item>
/// </list>
/// </remarks>
internal sealed class ServicioTiposAusencia(MarcacionDbContext contexto) : IServicioTiposAusencia
{
    public async Task<IReadOnlyList<TipoAusenciaListaDto>> ListarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default) =>
        await Proyectar(
                contexto.TiposAusencia.AsNoTracking()
                    .Where(t => incluirInactivos || t.Activo)
                    .OrderBy(t => t.Codigo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<TipoAusenciaListaDto?> ObtenerAsync(long id, CancellationToken cancelacion = default) =>
        await Proyectar(contexto.TiposAusencia.AsNoTracking().Where(t => t.Id == id))
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

    public async Task<long> GuardarAsync(
        GuardarTipoAusenciaRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();
        var nombre = solicitud.Nombre.Trim();

        Validar(solicitud);

        var repetido = await contexto.TiposAusencia
            .AnyAsync(t => t.Codigo == codigo && t.Id != solicitud.Id, cancelacion)
            .ConfigureAwait(false);

        if (repetido)
        {
            throw new ReglaNegocioException($"Ya existe un tipo de ausencia con el código '{codigo}'.");
        }

        TipoAusencia tipo;

        if (solicitud.Id is { } id)
        {
            tipo = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

            tipo.ModificadoPor = usuario;
        }
        else
        {
            tipo = new TipoAusencia { CreadoPor = usuario };
            await contexto.TiposAusencia.AddAsync(tipo, cancelacion).ConfigureAwait(false);
        }

        // Un tipo que ya tiene solicitudes o justificaciones no puede cambiar su
        // comportamiento: el cálculo de esos registros se hizo (o se hará) bajo la regla
        // de ese comportamiento, y cambiarlo reasignaría información sin rastro.
        if (solicitud.Id is { } yEnUso && await EnUsoAsync(yEnUso, cancelacion).ConfigureAwait(false))
        {
            if (tipo.Comportamiento != solicitud.Comportamiento)
            {
                throw new ReglaNegocioException(
                    $"El tipo '{tipo.Codigo}' ya está en uso por solicitudes o justificaciones y " +
                    "no puede cambiar su comportamiento: el cálculo de esos registros depende de él. " +
                    "Si es necesario, cree un tipo nuevo con el comportamiento correcto.");
            }
        }

        tipo.Codigo = codigo;
        tipo.Nombre = nombre;
        // Validar() ya garantizó que trae valor.
        tipo.Comportamiento = solicitud.Comportamiento!.Value;
        tipo.ComputaComoAsistencia = solicitud.ComputaComoAsistencia;
        tipo.RequiereSustento = solicitud.RequiereSustento;
        tipo.EsPorHoras = solicitud.EsPorHoras;
        tipo.Activo = solicitud.Activo;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return tipo.Id;
    }

    public async Task CambiarEstadoAsync(
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var tipo = await BuscarAsync(id, cancelacion).ConfigureAwait(false);

        if (!activo)
        {
            // Desactivar un tipo en uso dejaría solicitudes y justificaciones apuntando a un
            // catálogo que ya no aparece en ningún desplegable. Se bloquea indicando cuántos
            // registros lo usan, para que el usuario sepa qué mover primero.
            var usos = await ContarUsosAsync(id, cancelacion).ConfigureAwait(false);

            if (usos > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede desactivar el tipo de ausencia '{tipo.Codigo}': hay {usos} " +
                    "registro(s) (solicitudes o justificaciones) que lo utilizan. Reasígnelos primero.");
            }
        }

        tipo.Activo = activo;
        tipo.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------

    private async Task<TipoAusencia> BuscarAsync(long id, CancellationToken cancelacion) =>
        await contexto.TiposAusencia
            .FirstOrDefaultAsync(t => t.Id == id, cancelacion)
            .ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException("el tipo de ausencia", id);

    private IQueryable<TipoAusenciaListaDto> Proyectar(IQueryable<TipoAusencia> consulta) =>
        consulta.Select(t => new TipoAusenciaListaDto(
            t.Id,
            t.Codigo,
            t.Nombre,
            t.Comportamiento,
            t.ComputaComoAsistencia,
            t.RequiereSustento,
            t.EsPorHoras,
            t.Activo,
            contexto.SolicitudesAusencia.Count(s => s.TipoAusenciaId == t.Id)
            + contexto.Justificaciones.Count(j => j.TipoAusenciaId == t.Id)));

    private async Task<bool> EnUsoAsync(long id, CancellationToken cancelacion) =>
        await contexto.SolicitudesAusencia
            .AnyAsync(s => s.TipoAusenciaId == id, cancelacion)
            .ConfigureAwait(false)
        || await contexto.Justificaciones
            .AnyAsync(j => j.TipoAusenciaId == id, cancelacion)
            .ConfigureAwait(false);

    private async Task<int> ContarUsosAsync(long id, CancellationToken cancelacion)
    {
        var solicitudes = await contexto.SolicitudesAusencia
            .CountAsync(s => s.TipoAusenciaId == id, cancelacion)
            .ConfigureAwait(false);

        var justificaciones = await contexto.Justificaciones
            .CountAsync(j => j.TipoAusenciaId == id, cancelacion)
            .ConfigureAwait(false);

        return solicitudes + justificaciones;
    }

    private static void Validar(GuardarTipoAusenciaRequest solicitud)
    {
        // El comportamiento es el corazón del catálogo: si llega 0 (sin elegir) o un valor
        // fuera del enum, el motor no sabría cómo tratar el tipo y produciría tareos
        // incorrectos en silencio. Error explícito en vez de un valor por omisión.
        if (solicitud.Comportamiento is not { } comportamiento
            || !Enum.IsDefined(comportamiento)
            || comportamiento == 0)
        {
            throw new ReglaNegocioException(
                "Debe elegir cómo se comporta este tipo de ausencia en el cálculo. " +
                "Es el campo que decide cómo lo trata el motor (pago, subsidio, parcial, etc.).");
        }
    }
}
