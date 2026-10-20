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
    private readonly Dictionary<string, long> _turnos;

    /// <summary>
    /// Puestos indexados por empresa (nula para los universales).
    /// </summary>
    /// <remarks>
    /// "OPERARIO" y "SUPERVISOR" existen en las dos razones sociales: resolver solo por
    /// nombre le daría a un trabajador de CA Outsourcing el puesto de Consulting Advisers.
    /// </remarks>
    private readonly Dictionary<(long? Empresa, string Nombre), long> _puestos;

    /// <summary>
    /// Centros de costo por código, con la empresa y la planta de su centro SAP.
    /// </summary>
    /// <remarks>
    /// Una lista y no un único id: el código de centro de costo no es único ni dentro de
    /// una misma empresa (<c>PE21430001</c> está bajo tres centros de CA Outsourcing).
    /// </remarks>
    private readonly Dictionary<string, List<CandidatoCentroCosto>> _centrosCosto;

    private sealed record CandidatoCentroCosto(long Id, long EmpresaId, long? PlantaId, string Centro);

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

    /// <summary>
    /// Equipos marcadores indexados por planta, por código y por nombre.
    /// </summary>
    /// <remarks>
    /// Indexado por planta como las áreas: dos plantas pueden llamar "Principal" a su
    /// aparato, y resolver solo por nombre le daría al trabajador el equipo de otra —lo que
    /// mandaría sus marcaciones a otra persona.
    /// </remarks>
    private readonly Dictionary<(long Planta, string Nombre), long> _equipos;

    private CatalogosResueltos(
        Dictionary<string, long> tiposDocumento,
        Dictionary<string, long> empresas,
        Dictionary<(long, string), long> plantas,
        Dictionary<(long, string), long> areas,
        Dictionary<(long, string), long> equipos,
        Dictionary<(long?, string), long> puestos,
        Dictionary<string, List<CandidatoCentroCosto>> centrosCosto,
        Dictionary<string, long> turnos)
    {
        _tiposDocumento = tiposDocumento;
        _empresas = empresas;
        _plantas = plantas;
        _areas = areas;
        _equipos = equipos;
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

        var equipos = new Dictionary<(long, string), long>();

        foreach (var equipo in await contexto.EquiposMarcador.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.Nombre, e.Codigo, e.PlantaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            equipos.TryAdd((equipo.PlantaId, CargaMasivaTrabajadores.Normalizar(equipo.Nombre)), equipo.Id);
            equipos.TryAdd((equipo.PlantaId, CargaMasivaTrabajadores.Normalizar(equipo.Codigo)), equipo.Id);
        }

        var puestos = new Dictionary<(long?, string), long>();

        foreach (var puesto in await contexto.Puestos.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.EmpresaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            puestos.TryAdd((puesto.EmpresaId, CargaMasivaTrabajadores.Normalizar(puesto.Codigo)), puesto.Id);
            puestos.TryAdd((puesto.EmpresaId, CargaMasivaTrabajadores.Normalizar(puesto.Nombre)), puesto.Id);
        }

        var centrosCosto = new Dictionary<string, List<CandidatoCentroCosto>>(StringComparer.Ordinal);

        foreach (var centro in await contexto.CentrosCosto.AsNoTracking()
            .Where(c => c.Activo && c.Centro!.Activo)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Codigo, c.Centro!.EmpresaId, c.Centro.PlantaId, Centro = c.Centro.Codigo })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            var clave = CargaMasivaTrabajadores.Normalizar(centro.Codigo);

            if (!centrosCosto.TryGetValue(clave, out var candidatos))
            {
                candidatos = [];
                centrosCosto[clave] = candidatos;
            }

            candidatos.Add(new CandidatoCentroCosto(centro.Id, centro.EmpresaId, centro.PlantaId, centro.Centro));
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
            tiposDocumento, empresas, plantas, areas, equipos, puestos, centrosCosto, turnos);
    }

    public long TipoDocumento(string valor) =>
        Resolver(_tiposDocumento, valor, "tipo de documento");

    public long Empresa(string valor) =>
        Resolver(_empresas, valor, "empresa");

    /// <summary>
    /// Puesto por código o nombre: primero entre los de la empresa, luego los universales.
    /// </summary>
    public long Puesto(string valor, long empresaId)
    {
        var clave = CargaMasivaTrabajadores.Normalizar(valor);

        if (_puestos.TryGetValue((empresaId, clave), out var id)
            || _puestos.TryGetValue((null, clave), out id))
        {
            return id;
        }

        throw new ReglaNegocioException(
            $"El puesto '{valor}' no existe en el catálogo de la empresa indicada o está inactivo.");
    }

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

    /// <summary>
    /// Equipo marcador del que sale el ID de la fila. Nulo si el archivo no lo trae.
    /// </summary>
    /// <remarks>
    /// A diferencia del área, un valor que no existe <b>sí rechaza la fila</b>. El área es
    /// clasificación y dejarla vacía no rompe nada; el equipo decide a quién se le
    /// atribuyen las marcaciones, y tragarse un nombre mal escrito dejaría al trabajador
    /// colgado del aparato equivocado —o, cuando la planta tiene uno solo, del correcto por
    /// casualidad, que es peor porque el error reaparece al instalar el segundo—.
    ///
    /// Omitirlo sigue siendo válido: con un único aparato en la planta no hay nada que
    /// preguntar, y <c>ServicioTrabajadores</c> lo resuelve solo. Con dos, es ese servicio
    /// el que rechaza la fila nombrando los candidatos.
    /// </remarks>
    public long? EquipoOpcional(string? valor, long plantaId)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return _equipos.TryGetValue((plantaId, CargaMasivaTrabajadores.Normalizar(valor)), out var id)
            ? id
            : throw new ReglaNegocioException(
                $"El marcador '{valor}' no existe en la unidad indicada o está de baja. " +
                "Regístrelo en Equipos, o deje la columna vacía si la planta tiene un solo aparato.");
    }

    /// <summary>
    /// Centro de costo por código, dentro de la empresa de la fila. Nulo si el archivo no
    /// lo trae.
    /// </summary>
    /// <remarks>
    /// Antes se resolvía solo por código y ganaba el primero: <c>PE21430001</c> le daba a
    /// cualquier trabajador el centro de Planta Intracloro. Ahora, entre los centros de su
    /// empresa, gana el que atiende a su planta (<c>Centro.PlantaId</c>); si ninguno la
    /// atiende y hay más de un candidato, la fila se rechaza nombrándolos en vez de
    /// adivinar, porque el centro de costo es lo que el tareo final manda a SAP.
    /// </remarks>
    public long? CentroCostoOpcional(string? valor, long empresaId, long plantaId)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var candidatos = _centrosCosto.TryGetValue(CargaMasivaTrabajadores.Normalizar(valor), out var todos)
            ? todos.Where(c => c.EmpresaId == empresaId).ToList()
            : [];

        if (candidatos.Count == 0)
        {
            throw new ReglaNegocioException(
                $"El centro de costo '{valor}' no existe en el catálogo de la empresa indicada o está inactivo.");
        }

        var dePlanta = candidatos.Where(c => c.PlantaId == plantaId).ToList();

        if (dePlanta.Count == 1)
        {
            return dePlanta[0].Id;
        }

        if (candidatos.Count == 1)
        {
            return candidatos[0].Id;
        }

        throw new ReglaNegocioException(
            $"El centro de costo '{valor}' existe en varios centros SAP de la empresa (" +
            string.Join(", ", candidatos.Select(c => c.Centro)) +
            ") y ninguno está vinculado a la unidad de la fila. Asígnelo desde la ficha del trabajador.");
    }

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
