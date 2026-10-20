using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Hace atómica la regla «periodo cerrado, nada se escribe».
/// </summary>
/// <remarks>
/// <para>
/// Comprobar <c>EstaAbierto</c> y guardar eran dos operaciones separadas, y entre las dos
/// cabía un cierre. En el recálculo la ventana es de <b>minutos</b> —lee el estado al
/// empezar y escribe decenas de miles de filas al final—, así que bastaba con que alguien
/// cerrara el mes mientras otro recalculaba para meter tareo en un periodo cerrado, en
/// silencio y contra el invariante.
/// </para>
/// <para>
/// La solución es que la comprobación viaje <b>dentro</b> de la escritura: el
/// <c>UPDATE</c> del periodo lleva su versión en el <c>WHERE</c>, y EF cuenta las filas
/// afectadas. Como todo lo que guarda un <c>SaveChanges</c> va en una sola transacción, si
/// la versión ya no coincide no se escribe <b>nada</b> — ni el periodo ni los miles de
/// tareos que iban con él.
/// </para>
/// <para>
/// ⚠️ <b>Afirmar no es incrementar.</b> Quien escribe dentro del periodo deja la versión
/// como está; solo cerrar y reabrir la incrementan. Así dos recálculos simultáneos no
/// chocan entre sí —los dos afirman la misma versión y los dos la dejan igual— y sí choca
/// cualquiera de ellos contra un cierre. Si esto incrementara, el segundo recálculo fallaría
/// sin que nada malo hubiera pasado, y quien lo viera aprendería a ignorar el aviso.
/// </para>
/// </remarks>
internal static class GuardiaDePeriodo
{
    /// <summary>
    /// Ata la escritura pendiente al estado actual del periodo.
    /// </summary>
    /// <remarks>
    /// El periodo tiene que llegar <b>seguido por el contexto</b> (sin <c>AsNoTracking</c>):
    /// lo que se marca es su propiedad de versión, y una entidad no seguida no produce
    /// ningún <c>UPDATE</c> que llevar el <c>WHERE</c>.
    /// </remarks>
    public static void AfirmarAbierto(this MarcacionDbContext contexto, Periodo periodo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(periodo);

        if (!periodo.EstaAbierto)
        {
            throw new PeriodoCerradoException(periodo.Codigo);
        }

        // Marcada como modificada SIN cambiar el valor: EF emite
        // `SET version = @v WHERE id = @id AND version = @v`, que es exactamente una
        // afirmación. El interceptor de auditoría no registra nada porque compara el valor
        // anterior con el nuevo y son iguales.
        contexto.Entry(periodo).Property(p => p.Version).IsModified = true;
    }

    /// <summary>
    /// Convierte el choque de concurrencia en un mensaje que se entiende.
    /// </summary>
    /// <remarks>
    /// Se vuelve a mirar la base para distinguir los dos finales posibles, porque la acción
    /// a tomar es distinta: si el periodo quedó cerrado no hay nada que reintentar —hay que
    /// reabrirlo, con motivo—, y si sigue abierto fue otra escritura y basta con repetir.
    /// Un «error de concurrencia» a secas no le dice a nadie cuál de las dos es.
    /// </remarks>
    public static async Task<Exception> ExplicarChoqueAsync(
        this MarcacionDbContext contexto,
        long periodoId,
        string queSeEstabaHaciendo,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var actual = await contexto.Periodos
            .AsNoTracking()
            .Where(p => p.Id == periodoId)
            .Select(p => new { p.Codigo, p.Estado })
            .FirstOrDefaultAsync(cancelacion)
            .ConfigureAwait(false);

        if (actual is null)
        {
            return new RecursoNoEncontradoException("el periodo", periodoId);
        }

        return actual.Estado == EstadoPeriodo.Abierto
            ? new ReglaNegocioException(
                $"Otra operación tocó el periodo '{actual.Codigo}' mientras se " +
                $"{queSeEstabaHaciendo}, así que no se guardó nada. Vuelva a intentarlo.")
            : new ReglaNegocioException(
                $"El periodo '{actual.Codigo}' se cerró mientras se {queSeEstabaHaciendo}: " +
                "no se guardó nada. Si el cierre fue un error, reábralo indicando el motivo " +
                "y repita la operación.");
    }
}
