using System.Globalization;
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

    private readonly List<PuestoCatalogo> _puestos;
    private readonly IReadOnlyDictionary<long, string> _nombresEmpresa;

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
    private readonly List<PlantaCatalogo> _plantas;

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
    private readonly List<EquipoCatalogo> _equipos;
    private readonly List<MarcadorCatalogo> _marcadores;

    private CatalogosResueltos(
        Dictionary<string, long> tiposDocumento,
        Dictionary<string, long> empresas,
        List<PlantaCatalogo> plantas,
        Dictionary<(long, string), long> areas,
        List<EquipoCatalogo> equipos,
        List<MarcadorCatalogo> marcadores,
        List<PuestoCatalogo> puestos,
        IReadOnlyDictionary<long, string> nombresEmpresa,
        Dictionary<string, List<CandidatoCentroCosto>> centrosCosto,
        Dictionary<string, long> turnos)
    {
        _tiposDocumento = tiposDocumento;
        _empresas = empresas;
        _plantas = plantas;
        _areas = areas;
        _equipos = equipos;
        _marcadores = marcadores;
        _puestos = puestos;
        _nombresEmpresa = nombresEmpresa;
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
        var nombresEmpresa = new Dictionary<long, string>();

        foreach (var empresa in await contexto.Empresas.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.RazonSocial, e.Ruc })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            Indexar(empresas, empresa.RazonSocial, empresa.Id);
            Indexar(empresas, empresa.Ruc, empresa.Id);
            nombresEmpresa[empresa.Id] = empresa.RazonSocial;
        }

        var plantas = new List<PlantaCatalogo>();

        foreach (var planta in await contexto.Plantas.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.Id, p.Nombre, p.Codigo, p.EmpresaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            plantas.Add(new PlantaCatalogo(
                planta.Id, planta.EmpresaId, planta.Codigo, planta.Nombre, CreadaEnLote: false));
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

        var equipos = new List<EquipoCatalogo>();

        foreach (var equipo in await contexto.EquiposMarcador.AsNoTracking()
            .Where(e => e.Activo)
            .Select(e => new { e.Id, e.Nombre, e.Codigo, e.Serie, e.PlantaId })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            equipos.Add(new EquipoCatalogo(
                equipo.Id, equipo.PlantaId, equipo.Codigo, equipo.Nombre, equipo.Serie,
                CreadoEnLote: false));
        }

        var marcadores = await contexto.Marcadores.AsNoTracking()
            .Where(m => m.Activo)
            .Select(m => new MarcadorCatalogo(m.Id, m.EquipoId, m.Codigo))
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        var puestos = new List<PuestoCatalogo>();

        foreach (var puesto in await contexto.Puestos.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Codigo, p.Nombre, p.EmpresaId, p.Activo })
            .ToListAsync(cancelacion).ConfigureAwait(false))
        {
            puestos.Add(new PuestoCatalogo(
                puesto.Id,
                puesto.EmpresaId,
                puesto.Codigo,
                puesto.Nombre,
                puesto.Activo));
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
            tiposDocumento, empresas, plantas, areas, equipos, marcadores, puestos, nombresEmpresa,
            centrosCosto, turnos);
    }

    public long TipoDocumento(string valor) =>
        Resolver(_tiposDocumento, valor, "tipo de documento");

    public long Empresa(string valor) =>
        Resolver(_empresas, valor, "empresa");

    public string NombreEmpresa(long empresaId) =>
        _nombresEmpresa.TryGetValue(empresaId, out var nombre)
            ? nombre
            : empresaId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Concilia un puesto por código o nombre. Primero considera el alcance de la empresa;
    /// solo cuando allí no hay coincidencias consulta los puestos universales.
    /// </summary>
    public ResolucionPuesto ResolverPuesto(string valor, long empresaId)
    {
        var visible = CargaMasivaTrabajadores.LimpiarPuestoVisible(valor);
        var clave = CargaMasivaTrabajadores.NormalizarPuesto(visible);

        if (CargaMasivaTrabajadores.EsPuestoSentinela(clave))
        {
            throw new ReglaNegocioException(
                $"El valor de puesto '{visible}' no identifica un puesto válido. " +
                "Complete la columna con el nombre o código real del puesto.");
        }

        var deEmpresa = CoincidenciasActivas(empresaId, clave);
        var candidatos = deEmpresa.Count > 0 ? deEmpresa : CoincidenciasActivas(null, clave);

        if (candidatos.Count == 1)
        {
            var encontrado = candidatos[0];
            return ResolucionPuesto.Existente(
                empresaId, clave, encontrado.Id, encontrado.Nombre, encontrado.Codigo);
        }

        if (candidatos.Count > 1)
        {
            return ResolucionPuesto.Ambigua(
                empresaId,
                clave,
                visible,
                candidatos.Select(p => $"{p.Codigo} — {p.Nombre}").ToArray());
        }

        var codigo = CargaMasivaTrabajadores.CodigoPuestoImportado(empresaId, clave);
        var colision = _puestos.FirstOrDefault(p =>
            string.Equals(p.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

        if (colision is not null)
        {
            return ResolucionPuesto.Ambigua(
                empresaId,
                clave,
                visible,
                [$"{colision.Codigo} — {colision.Nombre}" + (colision.Activo ? string.Empty : " (inactivo)")]);
        }

        return ResolucionPuesto.Nueva(empresaId, clave, visible, codigo);
    }

    public void RegistrarPuesto(long id, long empresaId, string codigo, string nombre) =>
        _puestos.Add(new PuestoCatalogo(id, empresaId, codigo, nombre, true));

    private List<PuestoCatalogo> CoincidenciasActivas(long? empresaId, string clave) =>
        _puestos
            .Where(p => p.Activo && p.EmpresaId == empresaId)
            .Where(p =>
                CargaMasivaTrabajadores.NormalizarPuesto(p.Codigo) == clave
                || CargaMasivaTrabajadores.NormalizarPuesto(p.Nombre) == clave)
            .DistinctBy(p => p.Id)
            .OrderBy(p => p.Id)
            .ToList();

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
    public ResolucionCatalogo ResolverPlanta(string valor, long empresaId, string? codigoPropuesto)
    {
        var visible = CargaMasivaTrabajadores.LimpiarCatalogoVisible(valor);
        var clave = CargaMasivaTrabajadores.NormalizarCatalogo(visible);
        var candidatos = _plantas
            .Where(p => p.EmpresaId == empresaId)
            .Where(p => CargaMasivaTrabajadores.NormalizarCatalogo(p.Codigo) == clave
                || CargaMasivaTrabajadores.NormalizarCatalogo(p.Nombre) == clave)
            .DistinctBy(p => p.Id)
            .ToList();

        if (candidatos.Count == 0)
        {
            var guion = visible.LastIndexOf('-');
            if (guion >= 0 && guion < visible.Length - 1)
            {
                var tramo = CargaMasivaTrabajadores.NormalizarCatalogo(visible[(guion + 1)..]);
                candidatos = _plantas
                    .Where(p => p.EmpresaId == empresaId)
                    .Where(p => CargaMasivaTrabajadores.NormalizarCatalogo(p.Codigo) == tramo
                        || CargaMasivaTrabajadores.NormalizarCatalogo(p.Nombre) == tramo)
                    .DistinctBy(p => p.Id)
                    .ToList();
            }
        }

        if (candidatos.Count == 1)
        {
            var existente = candidatos[0];
            if (!string.IsNullOrWhiteSpace(codigoPropuesto)
                && CargaMasivaTrabajadores.NormalizarCatalogo(codigoPropuesto)
                    != CargaMasivaTrabajadores.NormalizarCatalogo(existente.Codigo))
            {
                return ResolucionCatalogo.Ambigua(
                    visible,
                    [$"La unidad existe con código '{existente.Codigo}', no '{codigoPropuesto}'."]);
            }

            return ResolucionCatalogo.Existente(existente.Id, existente.Codigo, existente.Nombre);
        }

        if (candidatos.Count > 1)
        {
            return ResolucionCatalogo.Ambigua(
                visible, candidatos.Select(p => $"{p.Codigo} — {p.Nombre}").ToArray());
        }

        if (string.IsNullOrWhiteSpace(codigoPropuesto))
        {
            return ResolucionCatalogo.Pendiente(
                visible, "La unidad no existe. Complete la columna 'codigo_unidad' para proponer su alta.");
        }

        var codigo = CargaMasivaTrabajadores.LimpiarCatalogoVisible(codigoPropuesto);
        var colision = _plantas.FirstOrDefault(p => p.EmpresaId == empresaId &&
            CargaMasivaTrabajadores.NormalizarCatalogo(p.Codigo)
                == CargaMasivaTrabajadores.NormalizarCatalogo(codigo));
        if (colision is not null)
        {
            return ResolucionCatalogo.Ambigua(
                visible, [$"El código '{codigo}' ya pertenece a {colision.Nombre}."]);
        }

        return ResolucionCatalogo.Nueva(codigo, visible);
    }

    public long Planta(string valor, long empresaId)
    {
        var resolucion = ResolverPlanta(valor, empresaId, null);
        return resolucion.Estado == EstadoResolucionCatalogo.Existente
            ? resolucion.Id!.Value
            : throw new ReglaNegocioException(resolucion.Motivo
                ?? $"La unidad '{valor}' no existe en la empresa indicada.");
    }

    public void RegistrarPlanta(long id, long empresaId, string codigo, string nombre) =>
        _plantas.Add(new PlantaCatalogo(id, empresaId, codigo, nombre, CreadaEnLote: true));

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
    public ResolucionCatalogo ResolverEquipo(
        string? selector,
        string? codigoPropuesto,
        string? nombrePropuesto,
        string? seriePropuesta,
        long plantaId,
        bool requerido)
    {
        var valores = new[] { selector, codigoPropuesto, nombrePropuesto, seriePropuesta }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(CargaMasivaTrabajadores.LimpiarCatalogoVisible)
            .ToArray();

        var candidatos = _equipos
            .Where(e => e.PlantaId == plantaId)
            .Where(e => valores.Any(v =>
                CargaMasivaTrabajadores.NormalizarCatalogo(e.Codigo) == CargaMasivaTrabajadores.NormalizarCatalogo(v)
                || CargaMasivaTrabajadores.NormalizarCatalogo(e.Nombre) == CargaMasivaTrabajadores.NormalizarCatalogo(v)
                || (e.Serie is not null && CargaMasivaTrabajadores.NormalizarCatalogo(e.Serie)
                    == CargaMasivaTrabajadores.NormalizarCatalogo(v))))
            .DistinctBy(e => e.Id)
            .ToList();

        if (candidatos.Count == 0 && valores.Length == 0)
        {
            var deUnidad = _equipos.Where(e => e.PlantaId == plantaId).ToList();
            if (deUnidad.Count == 1)
            {
                var unico = deUnidad[0];
                return ResolucionCatalogo.Existente(unico.Id, unico.Codigo, unico.Nombre);
            }

            return requerido
                ? ResolucionCatalogo.Pendiente(
                    string.Empty,
                    deUnidad.Count == 0
                        ? "La unidad no tiene equipo. Complete codigo_equipo y nombre_equipo."
                        : "La unidad tiene varios equipos. Identifique uno con la columna 'equipo'.")
                : ResolucionCatalogo.NoAplica();
        }

        if (candidatos.Count == 1)
        {
            var existente = candidatos[0];
            var codigoContradice = !string.IsNullOrWhiteSpace(codigoPropuesto)
                && CargaMasivaTrabajadores.NormalizarCatalogo(codigoPropuesto)
                    != CargaMasivaTrabajadores.NormalizarCatalogo(existente.Codigo);
            var nombreContradice = !string.IsNullOrWhiteSpace(nombrePropuesto)
                && CargaMasivaTrabajadores.NormalizarCatalogo(nombrePropuesto)
                    != CargaMasivaTrabajadores.NormalizarCatalogo(existente.Nombre);
            var serieContradice = !string.IsNullOrWhiteSpace(seriePropuesta)
                && existente.Serie is not null
                && CargaMasivaTrabajadores.NormalizarCatalogo(seriePropuesta)
                    != CargaMasivaTrabajadores.NormalizarCatalogo(existente.Serie);
            if (codigoContradice || nombreContradice || serieContradice)
            {
                return ResolucionCatalogo.Ambigua(
                    selector ?? codigoPropuesto ?? nombrePropuesto ?? string.Empty,
                    [$"Los datos declarados no coinciden con {existente.Codigo} — {existente.Nombre}."]);
            }

            return ResolucionCatalogo.Existente(existente.Id, existente.Codigo, existente.Nombre);
        }

        if (candidatos.Count > 1)
        {
            return ResolucionCatalogo.Ambigua(
                selector ?? codigoPropuesto ?? nombrePropuesto ?? seriePropuesta ?? string.Empty,
                candidatos.Select(e => $"{e.Codigo} — {e.Nombre}").ToArray());
        }

        if (string.IsNullOrWhiteSpace(codigoPropuesto) || string.IsNullOrWhiteSpace(nombrePropuesto))
        {
            return ResolucionCatalogo.Pendiente(
                selector ?? codigoPropuesto ?? nombrePropuesto ?? string.Empty,
                "El equipo no existe. Complete 'codigo_equipo' y 'nombre_equipo' para proponer su alta.");
        }

        var codigo = CargaMasivaTrabajadores.LimpiarCatalogoVisible(codigoPropuesto);
        var nombre = CargaMasivaTrabajadores.LimpiarCatalogoVisible(nombrePropuesto);
        var colision = _equipos.FirstOrDefault(e =>
            CargaMasivaTrabajadores.NormalizarCatalogo(e.Codigo)
                == CargaMasivaTrabajadores.NormalizarCatalogo(codigo)
            || (!string.IsNullOrWhiteSpace(seriePropuesta) && e.Serie is not null
                && CargaMasivaTrabajadores.NormalizarCatalogo(e.Serie)
                    == CargaMasivaTrabajadores.NormalizarCatalogo(seriePropuesta)));
        if (colision is not null)
        {
            return ResolucionCatalogo.Ambigua(
                nombre, [$"El código o serie ya pertenece a {colision.Codigo} — {colision.Nombre}."]);
        }

        return ResolucionCatalogo.Nueva(codigo, nombre, seriePropuesta);
    }

    public long? EquipoOpcional(string? valor, long plantaId)
    {
        var resolucion = ResolverEquipo(valor, null, null, null, plantaId, requerido: false);
        return resolucion.Estado switch
        {
            EstadoResolucionCatalogo.Existente => resolucion.Id,
            EstadoResolucionCatalogo.NoAplica => null,
            _ => throw new ReglaNegocioException(resolucion.Motivo
                ?? $"El equipo '{valor}' no existe en la unidad indicada o está de baja.")
        };
    }

    public void RegistrarEquipo(long id, long plantaId, string codigo, string nombre, string? serie) =>
        _equipos.Add(new EquipoCatalogo(id, plantaId, codigo, nombre, serie, CreadoEnLote: true));

    public bool ExisteMarcador(long equipoId, string codigo) =>
        _marcadores.Any(m => m.EquipoId == equipoId
            && string.Equals(m.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    public void RegistrarMarcador(long id, long equipoId, string codigo) =>
        _marcadores.Add(new MarcadorCatalogo(id, equipoId, codigo));

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

    internal sealed record PuestoCatalogo(
        long Id,
        long? EmpresaId,
        string Codigo,
        string Nombre,
        bool Activo);

    private sealed record PlantaCatalogo(
        long Id, long EmpresaId, string Codigo, string Nombre, bool CreadaEnLote);

    private sealed record EquipoCatalogo(
        long Id, long PlantaId, string Codigo, string Nombre, string? Serie, bool CreadoEnLote);

    private sealed record MarcadorCatalogo(long Id, long EquipoId, string Codigo);
}

internal enum EstadoResolucionCatalogo
{
    Existente,
    Nuevo,
    Ambiguo,
    Invalido,
    NoAplica,
    PendienteDatos
}

internal sealed record ResolucionCatalogo(
    EstadoResolucionCatalogo Estado,
    long? Id,
    string Codigo,
    string Nombre,
    string? Serie,
    IReadOnlyList<string> Candidatos,
    string? Motivo)
{
    public static ResolucionCatalogo Existente(long id, string codigo, string nombre) =>
        new(EstadoResolucionCatalogo.Existente, id, codigo, nombre, null, [], null);

    public static ResolucionCatalogo Nueva(string codigo, string nombre, string? serie = null) =>
        new(EstadoResolucionCatalogo.Nuevo, null, codigo, nombre, serie, [], null);

    public static ResolucionCatalogo Ambigua(string nombre, IReadOnlyList<string> candidatos) =>
        new(EstadoResolucionCatalogo.Ambiguo, null, string.Empty, nombre, null, candidatos,
            "La referencia coincide con más de un elemento o colisiona con otro catálogo.");

    public static ResolucionCatalogo Pendiente(string nombre, string motivo) =>
        new(EstadoResolucionCatalogo.PendienteDatos, null, string.Empty, nombre, null, [], motivo);

    public static ResolucionCatalogo NoAplica() =>
        new(EstadoResolucionCatalogo.NoAplica, null, string.Empty, string.Empty, null, [], null);
}

internal enum EstadoResolucionPuesto
{
    Existente,
    Nuevo,
    Ambiguo
}

internal sealed record ResolucionPuesto(
    EstadoResolucionPuesto Estado,
    long EmpresaId,
    string Clave,
    string Nombre,
    string? Codigo,
    long? PuestoId,
    IReadOnlyList<string> Candidatos)
{
    public static ResolucionPuesto Existente(
        long empresaId, string clave, long puestoId, string nombre, string codigo) =>
        new(EstadoResolucionPuesto.Existente, empresaId, clave, nombre, codigo, puestoId, []);

    public static ResolucionPuesto Nueva(long empresaId, string clave, string nombre, string codigo) =>
        new(EstadoResolucionPuesto.Nuevo, empresaId, clave, nombre, codigo, null, []);

    public static ResolucionPuesto Ambigua(
        long empresaId, string clave, string nombre, IReadOnlyList<string> candidatos) =>
        new(EstadoResolucionPuesto.Ambiguo, empresaId, clave, nombre, null, null, candidatos);
}
