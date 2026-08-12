using ControlMarcacion.Application.Catalogos;
using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Comun;
using ControlMarcacion.Domain.Catalogos;
using ControlMarcacion.Domain.Comun;
using ControlMarcacion.Domain.Organizacion;
using Microsoft.EntityFrameworkCore;

namespace ControlMarcacion.Infrastructure.Persistencia.Servicios;

/// <summary>
/// Mantenimiento de los catálogos simples.
/// </summary>
/// <remarks>
/// El despacho por clave se escribe de forma explícita, catálogo por catálogo. Se podría
/// resolver con reflexión sobre <c>DbSet</c>, pero eso convertiría un error de tipeo en un
/// fallo en tiempo de ejecución en vez de uno de compilación, y esto administra la
/// estructura organizativa de la que cuelga toda la planilla.
/// </remarks>
internal sealed class ServicioCatalogos(MarcacionDbContext contexto) : IServicioCatalogos
{
    public async Task<IReadOnlyList<CatalogoItemDto>> ListarAsync(
        string clave,
        long? padreId,
        bool incluirInactivos,
        CancellationToken cancelacion = default) =>
        Normalizar(clave) switch
        {
            "puestos" => await ListarPuestosAsync(incluirInactivos, cancelacion).ConfigureAwait(false),
            "plantas" => await ListarPlantasAsync(padreId, incluirInactivos, cancelacion).ConfigureAwait(false),
            "areas" => await ListarAreasAsync(padreId, incluirInactivos, cancelacion).ConfigureAwait(false),
            "secciones" => await ListarSeccionesAsync(padreId, incluirInactivos, cancelacion).ConfigureAwait(false),
            "tipos-documento" => await ListarTiposDocumentoAsync(incluirInactivos, cancelacion).ConfigureAwait(false),
            _ => throw new RecursoNoEncontradoException("el catálogo", clave)
        };

    public async Task<CatalogoItemDto?> ObtenerAsync(
        string clave,
        long id,
        CancellationToken cancelacion = default)
    {
        var elementos = await ListarAsync(clave, null, incluirInactivos: true, cancelacion).ConfigureAwait(false);

        return elementos.FirstOrDefault(e => e.Id == id);
    }

    public async Task<long> GuardarAsync(
        string clave,
        GuardarCatalogoRequest solicitud,
        string usuario,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var definicion = CatalogosDisponibles.Buscar(clave)
            ?? throw new RecursoNoEncontradoException("el catálogo", clave);

        var codigo = solicitud.Codigo.Trim().ToUpperInvariant();
        var nombre = solicitud.Nombre.Trim();

        if (definicion.EsJerarquico && solicitud.PadreId is null or 0)
        {
            throw new ReglaNegocioException(
                $"Debe indicar a qué {definicion.EtiquetaPadre?.ToLowerInvariant()} pertenece.");
        }

        await ValidarCodigoUnicoAsync(definicion, codigo, solicitud, cancelacion).ConfigureAwait(false);

        var id = Normalizar(clave) switch
        {
            "puestos" => await GuardarPuestoAsync(solicitud, codigo, nombre, usuario, cancelacion).ConfigureAwait(false),
            "plantas" => await GuardarPlantaAsync(solicitud, codigo, nombre, usuario, cancelacion).ConfigureAwait(false),
            "areas" => await GuardarAreaAsync(solicitud, codigo, nombre, usuario, cancelacion).ConfigureAwait(false),
            "secciones" => await GuardarSeccionAsync(solicitud, codigo, nombre, usuario, cancelacion).ConfigureAwait(false),
            "tipos-documento" => await GuardarTipoDocumentoAsync(solicitud, codigo, nombre, usuario, cancelacion).ConfigureAwait(false),
            _ => throw new RecursoNoEncontradoException("el catálogo", clave)
        };

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);

        return id;
    }

    public async Task CambiarEstadoAsync(
        string clave,
        long id,
        bool activo,
        string usuario,
        CancellationToken cancelacion = default)
    {
        var definicion = CatalogosDisponibles.Buscar(clave)
            ?? throw new RecursoNoEncontradoException("el catálogo", clave);

        EntidadBase entidad = Normalizar(clave) switch
        {
            "puestos" => await BuscarAsync(contexto.Puestos, id, clave, cancelacion).ConfigureAwait(false),
            "plantas" => await BuscarAsync(contexto.Plantas, id, clave, cancelacion).ConfigureAwait(false),
            "areas" => await BuscarAsync(contexto.Areas, id, clave, cancelacion).ConfigureAwait(false),
            "secciones" => await BuscarAsync(contexto.Secciones, id, clave, cancelacion).ConfigureAwait(false),
            "tipos-documento" => await BuscarAsync(contexto.TiposDocumento, id, clave, cancelacion).ConfigureAwait(false),
            _ => throw new RecursoNoEncontradoException("el catálogo", clave)
        };

        // Desactivar algo que está en uso dejaría fichas apuntando a un catálogo que ya
        // no aparece en ningún desplegable. Se bloquea con un mensaje que dice cuántos
        // registros lo usan, para que el usuario sepa qué mover primero.
        if (!activo)
        {
            var dependencias = await ContarDependenciasAsync(clave, id, cancelacion).ConfigureAwait(false);

            if (dependencias > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede desactivar {definicion.ConArticulo}: hay {dependencias} " +
                    "registro(s) que lo utilizan. Reasígnelos primero.");
            }
        }

        entidad.Activo = activo;
        entidad.ModificadoPor = usuario;

        await contexto.SaveChangesAsync(cancelacion).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcionDto>> OpcionesPadreAsync(
        string clave,
        CancellationToken cancelacion = default) =>
        Normalizar(clave) switch
        {
            "plantas" => await contexto.Empresas.AsNoTracking()
                .Where(e => e.Activo)
                .OrderBy(e => e.RazonSocial)
                .Select(e => new OpcionDto(e.Id, e.RazonSocial))
                .ToListAsync(cancelacion).ConfigureAwait(false),

            "areas" => await contexto.Plantas.AsNoTracking()
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .Select(p => new OpcionDto(p.Id, p.Nombre))
                .ToListAsync(cancelacion).ConfigureAwait(false),

            "secciones" => await contexto.Areas.AsNoTracking()
                .Where(a => a.Activo)
                .OrderBy(a => a.Nombre)
                // El área se muestra con su planta: hay una "Producción" por planta y sin
                // el prefijo el desplegable ofrece cuatro opciones idénticas.
                .Select(a => new OpcionDto(a.Id, a.Planta!.Nombre + " · " + a.Nombre))
                .ToListAsync(cancelacion).ConfigureAwait(false),

            _ => []
        };

    // ------------------------------------------------------------------
    // Listados
    // ------------------------------------------------------------------

    private async Task<IReadOnlyList<CatalogoItemDto>> ListarPuestosAsync(
        bool incluirInactivos, CancellationToken cancelacion) =>
        await contexto.Puestos.AsNoTracking()
            .Where(p => incluirInactivos || p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new CatalogoItemDto(
                p.Id, p.Codigo, p.Nombre, null, null, p.Activo,
                contexto.Trabajadores.Count(t => t.PuestoId == p.Id)))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    private async Task<IReadOnlyList<CatalogoItemDto>> ListarPlantasAsync(
        long? empresaId, bool incluirInactivos, CancellationToken cancelacion) =>
        await contexto.Plantas.AsNoTracking()
            .Where(p => (incluirInactivos || p.Activo) && (empresaId == null || p.EmpresaId == empresaId))
            .OrderBy(p => p.Empresa!.RazonSocial).ThenBy(p => p.Nombre)
            .Select(p => new CatalogoItemDto(
                p.Id, p.Codigo, p.Nombre, p.EmpresaId, p.Empresa!.RazonSocial, p.Activo,
                contexto.Areas.Count(a => a.PlantaId == p.Id)
                + contexto.Trabajadores.Count(t => t.PlantaId == p.Id)))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    private async Task<IReadOnlyList<CatalogoItemDto>> ListarAreasAsync(
        long? plantaId, bool incluirInactivos, CancellationToken cancelacion) =>
        await contexto.Areas.AsNoTracking()
            .Where(a => (incluirInactivos || a.Activo) && (plantaId == null || a.PlantaId == plantaId))
            .OrderBy(a => a.Planta!.Nombre).ThenBy(a => a.Nombre)
            .Select(a => new CatalogoItemDto(
                a.Id, a.Codigo, a.Nombre, a.PlantaId, a.Planta!.Nombre, a.Activo,
                contexto.Secciones.Count(s => s.AreaId == a.Id)
                + contexto.Trabajadores.Count(t => t.AreaId == a.Id)))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    private async Task<IReadOnlyList<CatalogoItemDto>> ListarSeccionesAsync(
        long? areaId, bool incluirInactivos, CancellationToken cancelacion) =>
        await contexto.Secciones.AsNoTracking()
            .Where(s => (incluirInactivos || s.Activo) && (areaId == null || s.AreaId == areaId))
            .OrderBy(s => s.Area!.Planta!.Nombre).ThenBy(s => s.Area!.Nombre).ThenBy(s => s.Nombre)
            .Select(s => new CatalogoItemDto(
                s.Id, s.Codigo, s.Nombre, s.AreaId,
                s.Area!.Planta!.Nombre + " · " + s.Area!.Nombre, s.Activo,
                contexto.Trabajadores.Count(t => t.SeccionId == s.Id)))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    private async Task<IReadOnlyList<CatalogoItemDto>> ListarTiposDocumentoAsync(
        bool incluirInactivos, CancellationToken cancelacion) =>
        await contexto.TiposDocumento.AsNoTracking()
            .Where(t => incluirInactivos || t.Activo)
            .OrderBy(t => t.Codigo)
            .Select(t => new CatalogoItemDto(
                t.Id, t.Codigo, t.Nombre, null, null, t.Activo,
                contexto.Trabajadores.Count(x => x.TipoDocumentoId == t.Id)))
            .ToListAsync(cancelacion).ConfigureAwait(false);

    // ------------------------------------------------------------------
    // Altas y ediciones
    // ------------------------------------------------------------------

    private async Task<long> GuardarPuestoAsync(
        GuardarCatalogoRequest s, string codigo, string nombre, string usuario, CancellationToken ct)
    {
        if (s.Id is not { } id)
        {
            var nuevo = new Puesto { Codigo = codigo, Nombre = nombre, Activo = s.Activo, CreadoPor = usuario };
            await contexto.Puestos.AddAsync(nuevo, ct).ConfigureAwait(false);
            return nuevo.Id;
        }

        var actual = await BuscarAsync(contexto.Puestos, id, "puestos", ct).ConfigureAwait(false);
        actual.Codigo = codigo;
        actual.Nombre = nombre;
        actual.Activo = s.Activo;
        actual.ModificadoPor = usuario;
        return actual.Id;
    }

    private async Task<long> GuardarPlantaAsync(
        GuardarCatalogoRequest s, string codigo, string nombre, string usuario, CancellationToken ct)
    {
        if (s.Id is not { } id)
        {
            var nueva = new Planta
            {
                Codigo = codigo, Nombre = nombre, EmpresaId = s.PadreId!.Value,
                Activo = s.Activo, CreadoPor = usuario
            };
            await contexto.Plantas.AddAsync(nueva, ct).ConfigureAwait(false);
            return nueva.Id;
        }

        var actual = await BuscarAsync(contexto.Plantas, id, "plantas", ct).ConfigureAwait(false);
        actual.Codigo = codigo;
        actual.Nombre = nombre;
        actual.EmpresaId = s.PadreId!.Value;
        actual.Activo = s.Activo;
        actual.ModificadoPor = usuario;
        return actual.Id;
    }

    private async Task<long> GuardarAreaAsync(
        GuardarCatalogoRequest s, string codigo, string nombre, string usuario, CancellationToken ct)
    {
        if (s.Id is not { } id)
        {
            var nueva = new Area
            {
                Codigo = codigo, Nombre = nombre, PlantaId = s.PadreId!.Value,
                Activo = s.Activo, CreadoPor = usuario
            };
            await contexto.Areas.AddAsync(nueva, ct).ConfigureAwait(false);
            return nueva.Id;
        }

        var actual = await BuscarAsync(contexto.Areas, id, "areas", ct).ConfigureAwait(false);
        actual.Codigo = codigo;
        actual.Nombre = nombre;
        actual.PlantaId = s.PadreId!.Value;
        actual.Activo = s.Activo;
        actual.ModificadoPor = usuario;
        return actual.Id;
    }

    private async Task<long> GuardarSeccionAsync(
        GuardarCatalogoRequest s, string codigo, string nombre, string usuario, CancellationToken ct)
    {
        if (s.Id is not { } id)
        {
            var nueva = new Seccion
            {
                Codigo = codigo, Nombre = nombre, AreaId = s.PadreId!.Value,
                Activo = s.Activo, CreadoPor = usuario
            };
            await contexto.Secciones.AddAsync(nueva, ct).ConfigureAwait(false);
            return nueva.Id;
        }

        var actual = await BuscarAsync(contexto.Secciones, id, "secciones", ct).ConfigureAwait(false);
        actual.Codigo = codigo;
        actual.Nombre = nombre;
        actual.AreaId = s.PadreId!.Value;
        actual.Activo = s.Activo;
        actual.ModificadoPor = usuario;
        return actual.Id;
    }

    private async Task<long> GuardarTipoDocumentoAsync(
        GuardarCatalogoRequest s, string codigo, string nombre, string usuario, CancellationToken ct)
    {
        if (s.Id is not { } id)
        {
            var nuevo = new TipoDocumento { Codigo = codigo, Nombre = nombre, Activo = s.Activo, CreadoPor = usuario };
            await contexto.TiposDocumento.AddAsync(nuevo, ct).ConfigureAwait(false);
            return nuevo.Id;
        }

        var actual = await BuscarAsync(contexto.TiposDocumento, id, "tipos-documento", ct).ConfigureAwait(false);
        actual.Codigo = codigo;
        actual.Nombre = nombre;
        actual.Activo = s.Activo;
        actual.ModificadoPor = usuario;
        return actual.Id;
    }

    // ------------------------------------------------------------------
    // Apoyo
    // ------------------------------------------------------------------

    private async Task ValidarCodigoUnicoAsync(
        DefinicionCatalogo definicion,
        string codigo,
        GuardarCatalogoRequest solicitud,
        CancellationToken cancelacion)
    {
        // En los catálogos jerárquicos la unicidad es POR PADRE: dos plantas distintas
        // pueden tener cada una un área "PROD", y de hecho así está sembrado.
        var repetido = Normalizar(definicion.Clave) switch
        {
            "puestos" => await contexto.Puestos.AnyAsync(
                x => x.Codigo == codigo && x.Id != solicitud.Id, cancelacion).ConfigureAwait(false),

            "tipos-documento" => await contexto.TiposDocumento.AnyAsync(
                x => x.Codigo == codigo && x.Id != solicitud.Id, cancelacion).ConfigureAwait(false),

            "plantas" => await contexto.Plantas.AnyAsync(
                x => x.Codigo == codigo && x.EmpresaId == solicitud.PadreId && x.Id != solicitud.Id,
                cancelacion).ConfigureAwait(false),

            "areas" => await contexto.Areas.AnyAsync(
                x => x.Codigo == codigo && x.PlantaId == solicitud.PadreId && x.Id != solicitud.Id,
                cancelacion).ConfigureAwait(false),

            "secciones" => await contexto.Secciones.AnyAsync(
                x => x.Codigo == codigo && x.AreaId == solicitud.PadreId && x.Id != solicitud.Id,
                cancelacion).ConfigureAwait(false),

            _ => false
        };

        if (repetido)
        {
            var ambito = definicion.EsJerarquico
                ? $" dentro de esa {definicion.EtiquetaPadre?.ToLowerInvariant()}"
                : string.Empty;

            throw new ReglaNegocioException($"Ya existe un registro con el código '{codigo}'{ambito}.");
        }
    }

    private Task<int> ContarDependenciasAsync(string clave, long id, CancellationToken cancelacion) =>
        Normalizar(clave) switch
        {
            "puestos" => contexto.Trabajadores.CountAsync(t => t.PuestoId == id, cancelacion),
            "tipos-documento" => contexto.Trabajadores.CountAsync(t => t.TipoDocumentoId == id, cancelacion),
            "plantas" => contexto.Trabajadores.CountAsync(t => t.PlantaId == id, cancelacion),
            "areas" => contexto.Trabajadores.CountAsync(t => t.AreaId == id, cancelacion),
            "secciones" => contexto.Trabajadores.CountAsync(t => t.SeccionId == id, cancelacion),
            _ => Task.FromResult(0)
        };

    private static async Task<T> BuscarAsync<T>(
        DbSet<T> conjunto, long id, string clave, CancellationToken cancelacion)
        where T : EntidadBase =>
        await conjunto.FirstOrDefaultAsync(e => e.Id == id, cancelacion).ConfigureAwait(false)
        ?? throw new RecursoNoEncontradoException($"el elemento de {clave}", id);

    private static string Normalizar(string clave) => clave.Trim().ToLowerInvariant();
}
