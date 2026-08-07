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

    private CatalogosResueltos(
        Dictionary<string, long> tiposDocumento,
        Dictionary<string, long> empresas,
        Dictionary<(long, string), long> plantas,
        Dictionary<string, long> puestos,
        Dictionary<string, long> centrosCosto,
        Dictionary<string, long> turnos)
    {
        _tiposDocumento = tiposDocumento;
        _empresas = empresas;
        _plantas = plantas;
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

        return new CatalogosResueltos(tiposDocumento, empresas, plantas, puestos, centrosCosto, turnos);
    }

    public long TipoDocumento(string valor) =>
        Resolver(_tiposDocumento, valor, "tipo de documento");

    public long Empresa(string valor) =>
        Resolver(_empresas, valor, "empresa");

    public long Puesto(string valor) =>
        Resolver(_puestos, valor, "puesto");

    public long Planta(string valor, long empresaId)
    {
        if (_plantas.TryGetValue((empresaId, CargaMasivaTrabajadores.Normalizar(valor)), out var id))
        {
            return id;
        }

        throw new ReglaNegocioException(
            $"La unidad (planta) '{valor}' no existe en la empresa indicada, o pertenece a la " +
            "otra razón social. Revise que la empresa y la unidad de la fila correspondan.");
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
