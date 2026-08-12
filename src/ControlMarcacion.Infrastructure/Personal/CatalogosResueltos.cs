using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Personal;

/// <summary>
/// Catálogos cargados una sola vez para toda la carga masiva.
/// </summary>
/// <remarks>
/// Se traen enteros al inicio y no fila por fila: resolver "Detergentes" contra la base
/// en cada una de trescientas filas son trescientas consultas para leer una tabla de
/// cinco registros. Todos los catálogos que intervienen aquí tienen decenas o cientos de
/// filas, no miles.
///
/// El usuario escribe códigos y nombres, no claves numéricas, así que cada catálogo se
/// indexa por las dos cosas.
/// </remarks>
internal sealed class CatalogosResueltos
{
    private readonly Dictionary<string, long> _tiposDocumento;
    private readonly Dictionary<string, long> _empresas;
    private readonly Dictionary<string, long> _puestos;
    private readonly Dictionary<string, long> _centrosCosto;
    private readonly Dictionary<string, long> _turnos;

    /// <summary>
    /// Plantas indexadas por empresa: dos razones sociales distintas pueden tener una
    /// planta con el mismo nombre, y resolver solo por nombre elegiría cualquiera de las dos.
    /// </summary>
    private readonly Dictionary<(long Empresa, string Nombre), long> _plantas;

    /// <summary>
    /// Áreas indexadas por planta: "Producción" existe en las cuatro plantas, y resolver
    /// solo por nombre le asignaría al trabajador el área de otra.
    /// </summary>
    private readonly Dictionary<(long Planta, string Nombre), long> _areas;

    private CatalogosResueltos(
        Dictionary<string, long> tiposDocumento,
        Dictionary<string, long> empresas,
        Dictionary<(long, string), long> plantas,
        Dictionary<(long, string), long> areas,
        Dictionary<string, long> puestos,
        Dictionary<string, long> centrosCosto,
        Dictionary<string, long> turnos)
    {
        _tiposDocumento = tiposDocumento;
        _empresas = empresas;
        _plantas = plantas;
        _areas = areas;
        _puestos = puestos;
        _centrosCosto = centrosCosto;
        _turnos = turnos;
    }

    public static async Task<CatalogosResueltos> CargarAsync(
        MarcacionDbContext contexto,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var tiposDocumento = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var tipo in await contexto.TiposDocumento.AsNoTracking()
            .Where(t => t.Activo)
            .Select(t => new { t.Id, t.Codigo, t.Nombre })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(tiposDocumento, tipo.Codigo, tipo.Id);
            Indexar(tiposDocumento, tipo.Nombre, tipo.Id);
        }

        var empresas = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var empresa in await contexto.Empresas.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.RazonSocial, e.Ruc })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(empresas, empresa.RazonSocial, empresa.Id);
            Indexar(empresas, empresa.Ruc, empresa.Id);
        }

        var plantas = new Dictionary<(long, string), long>();

        foreach (var planta in await contexto.Plantas.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.Id, p.Nombre, p.Codigo, p.EmpresaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            plantas.TryAdd((planta.EmpresaId, CargaMasivaTrabajadores.Normalizar(planta.Nombre)), planta.Id);
            plantas.TryAdd((planta.EmpresaId, CargaMasivaTrabajadores.Normalizar(planta.Codigo)), planta.Id);
        }

        var areas = new Dictionary<(long, string), long>();

        foreach (var area in await contexto.Areas.AsNoTracking()
            .Where(a => a.Activo)
            .Select(a => new { a.Id, a.Nombre, a.Codigo, a.PlantaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            areas.TryAdd((area.PlantaId, CargaMasivaTrabajadores.Normalizar(area.Nombre)), area.Id);
            areas.TryAdd((area.PlantaId, CargaMasivaTrabajadores.Normalizar(area.Codigo)), area.Id);
        }

        var puestos = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var puesto in await contexto.Puestos.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.Id, p.Codigo, p.Nombre })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(puestos, puesto.Codigo, puesto.Id);
            Indexar(puestos, puesto.Nombre, puesto.Id);
        }

        var centrosCosto = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var centro in await contexto.CentrosCosto.AsNoTracking()
            .Where(c => c.Activo)
            .Select(c => new { c.Id, c.Codigo })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(centrosCosto, centro.Codigo, centro.Id);
        }

        var turnos = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var turno in await contexto.Turnos.AsNoTracking()
            .Where(t => t.Activo)
            .Select(t => new { t.Id, t.Codigo, t.Nombre })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(turnos, turno.Codigo, turno.Id);
            Indexar(turnos, turno.Nombre, turno.Id);
        }

        return new CatalogosResueltos(
            tiposDocumento, empresas, plantas, areas, puestos, centrosCosto, turnos);
    }

    public long TipoDocumento(string valor) =>
        Resolver(_tiposDocumento, valor, "tipo de documento");

    public long Empresa(string valor) =>
        Resolver(_empresas, valor, "empresa");

    public long Puesto(string valor) =>
        Resolver(_puestos, valor, "puesto");

    /// <summary>
    /// Resuelve la unidad (planta) por nombre o código dentro de la empresa.
    /// </summary>
    /// <remarks>
    /// Acepta además la forma en que la exportación de planilla de GCA nombra la unidad:
    /// <c>INTRADEVCO-PLANTA DETERGENTES</c>, es decir cliente y planta concatenados con un
    /// guion. Se reintenta con el tramo posterior al último guion antes de rendirse.
    ///
    /// Se resuelve aquí y no renombrando la planta en el catálogo a propósito: "Planta
    /// Detergentes" es el nombre de la planta, e <c>INTRADEVCO</c> es el cliente. Meter el
    /// cliente dentro del nombre de la planta confundiría dos dimensiones que el modelo
    /// tiene separadas, y rompería el día que GCA atienda a un segundo cliente.
    /// </remarks>
    public long Planta(string valor, long empresaId)
    {
        if (_plantas.TryGetValue((empresaId, CargaMasivaTrabajadores.Normalizar(valor)), out var id))
        {
            return id;
        }

        var guion = valor.LastIndexOf('-');

        if (guion >= 0 && guion < valor.Length - 1)
        {
            var tramoFinal = CargaMasivaTrabajadores.Normalizar(valor[(guion + 1)..]);

            if (_plantas.TryGetValue((empresaId, tramoFinal), out var idPorTramo))
            {
                return idPorTramo;
            }
        }

        throw new ReglaNegocioException(
            $"La unidad (planta) '{valor}' no existe en la empresa indicada, o pertenece a la " +
            "otra razón social. Revise que la empresa y la unidad de la fila correspondan.");
    }

    /// <summary>
    /// Área interna dentro de la planta. Nulo si el archivo no la trae.
    /// </summary>
    /// <remarks>
    /// Un área que no existe en el catálogo <b>no rechaza la fila</b>: el área es un dato de
    /// clasificación, no un requisito para dar de alta a nadie, y bloquear a doscientos
    /// trabajadores porque falta un área en el catálogo sería desproporcionado. Se deja
    /// vacía y se corrige después desde la ficha o desde Catálogos.
    /// </remarks>
    public long? AreaOpcional(string? valor, long plantaId)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return _areas.TryGetValue((plantaId, CargaMasivaTrabajadores.Normalizar(valor)), out var id)
            ? id
            : null;
    }

    public long? CentroCostoOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : Resolver(_centrosCosto, valor, "centro de costo");

    public long? TurnoOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : Resolver(_turnos, valor, "turno");

    private static void Indexar(Dictionary<string, long> destino, string? clave, long id)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return;
        }

        // TryAdd y no el indexador: si dos filas del catálogo comparten nombre, gana la
        // primera en vez de reventar la carga entera por un dato duplicado que ya existía.
        destino.TryAdd(CargaMasivaTrabajadores.Normalizar(clave), id);
    }

    private static long Resolver(Dictionary<string, long> catalogo, string valor, string queEs) =>
        catalogo.TryGetValue(CargaMasivaTrabajadores.Normalizar(valor), out var id)
            ? id
            : throw new ReglaNegocioException(
                $"El {queEs} '{valor}' no existe en el catálogo o está inactivo.");
}
