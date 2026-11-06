namespace ControlMarcacion.Application.Personal.Dto;

/// <summary>
/// Una fila que no se pudo registrar, con el motivo redactado para el usuario.
/// </summary>
/// <param name="Fila">
/// Número de línea en el archivo tal como lo ve el usuario en Excel: la 1 es el
/// encabezado, así que el primer trabajador es la 2. Sin esto el reporte diría
/// "fila 37" y el usuario tendría que contar a mano.
/// </param>
public sealed record ErrorCargaMasiva(int Fila, string Referencia, string Motivo);

/// <summary>
/// ID de marcador que la carga dio de alta en el catálogo de su aparato porque faltaba.
/// </summary>
/// <remarks>
/// Se lista en el resultado, agrupado por aparato, para que un error de tipeo en el padrón
/// salte a la vista en vez de quedar registrado como un número más.
/// </remarks>
public sealed record MarcadorRegistradoEnCarga(int Fila, string Codigo, string Equipo);

/// <summary>Un puesto reconocido o propuesto por el análisis de la plantilla.</summary>
public sealed record PuestoCargaMasiva(
    long EmpresaId,
    string Empresa,
    string Nombre,
    int TrabajadoresAfectados,
    long? PuestoId = null);

/// <summary>
/// Valor que coincide con más de una fila activa del mismo alcance y, por tanto, no se
/// puede asignar sin una corrección explícita del catálogo o de la plantilla.
/// </summary>
public sealed record PuestoAmbiguoCargaMasiva(
    long EmpresaId,
    string Empresa,
    string Valor,
    int TrabajadoresAfectados,
    IReadOnlyList<string> Candidatos);

/// <summary>Conciliación de puestos calculada sin alterar el catálogo.</summary>
public sealed record ResumenPuestosCargaMasiva(
    IReadOnlyList<PuestoCargaMasiva> Existentes,
    IReadOnlyList<PuestoCargaMasiva> NuevosPropuestos,
    IReadOnlyList<PuestoAmbiguoCargaMasiva> Ambiguos,
    string Huella)
{
    public int Diferentes => Existentes.Count + NuevosPropuestos.Count + Ambiguos.Count;

    public int TrabajadoresAfectados => Existentes.Sum(p => p.TrabajadoresAfectados)
        + NuevosPropuestos.Sum(p => p.TrabajadoresAfectados)
        + Ambiguos.Sum(p => p.TrabajadoresAfectados);
}

/// <summary>Resultado de conciliar una dependencia declarada por la plantilla.</summary>
public enum EstadoConciliacionCargaMasiva
{
    Existente,
    NuevoPropuesto,
    Ambiguo,
    Invalido,
    NoAplica,
    PendienteDatos
}

/// <summary>Unidad, equipo o marcador reconocido durante la previsualización.</summary>
public sealed record EntidadConciliadaCargaMasiva(
    string Clave,
    string Nombre,
    string? Contexto,
    EstadoConciliacionCargaMasiva Estado,
    int FilasAfectadas,
    long? Id = null,
    string? Motivo = null);

/// <summary>Acción propuesta para una fila, sin exponer datos sensibles innecesarios.</summary>
public sealed record DetalleFilaCargaMasiva(
    int Fila,
    string Referencia,
    string Empresa,
    string Unidad,
    string? Puesto,
    string? Equipo,
    string? CodigoMarcador,
    DateOnly? VigenteDesde,
    EstadoConciliacionCargaMasiva Estado,
    string Accion,
    string? Motivo = null);

/// <summary>Previsualización de las dependencias organizativas y biométricas del lote.</summary>
public sealed record ResumenConciliacionCargaMasiva(
    IReadOnlyList<EntidadConciliadaCargaMasiva> Unidades,
    IReadOnlyList<EntidadConciliadaCargaMasiva> Equipos,
    IReadOnlyList<EntidadConciliadaCargaMasiva> Marcadores,
    IReadOnlyList<DetalleFilaCargaMasiva> Filas,
    string Huella)
{
    public bool TieneBloqueos => Unidades.Concat(Equipos).Concat(Marcadores)
        .Any(e => e.Estado is EstadoConciliacionCargaMasiva.Ambiguo
            or EstadoConciliacionCargaMasiva.Invalido
            or EstadoConciliacionCargaMasiva.PendienteDatos);

    public int UnidadesNuevas => Unidades.Count(e => e.Estado == EstadoConciliacionCargaMasiva.NuevoPropuesto);

    public int EquiposNuevos => Equipos.Count(e => e.Estado == EstadoConciliacionCargaMasiva.NuevoPropuesto);

    public int MarcadoresNuevos => Marcadores.Count(e => e.Estado == EstadoConciliacionCargaMasiva.NuevoPropuesto);
}

/// <summary>Catálogo creado efectivamente por una fila confirmada.</summary>
public sealed record CatalogoCreadoEnCarga(
    int Fila,
    long Id,
    string Codigo,
    string Nombre);

/// <summary>
/// Resultado de procesar el archivo.
/// </summary>
/// <remarks>
/// Las filas válidas se registran aunque otras fallen. Es deliberado: en un padrón de
/// 300 personas, rechazar el archivo entero por dos DNI repetidos obliga a rehacer la
/// carga completa, y en la práctica termina en que alguien parte el archivo en pedazos
/// hasta que pasa. Se registra lo que se puede y se informa con precisión lo que no.
/// </remarks>
public sealed record ResultadoCargaMasiva(
    string NombreArchivo,
    int FilasLeidas,
    int Creados,
    IReadOnlyList<ErrorCargaMasiva> Errores,
    bool SoloValidacion,
    /// <summary>ID de marcador que la carga registró en el catálogo porque faltaban.</summary>
    IReadOnlyList<MarcadorRegistradoEnCarga> MarcadoresRegistrados,
    /// <summary>
    /// Filas rechazadas porque su número de marcador no está en el catálogo de su aparato y
    /// la carga se hizo sin registrar los que faltan. La pantalla lo usa para decir que basta
    /// con volver a subir el archivo con esa casilla marcada.
    /// </summary>
    int FilasSinMarcadorEnCatalogo,
    ResumenPuestosCargaMasiva Puestos,
    ResumenConciliacionCargaMasiva Conciliacion,
    IReadOnlyList<CatalogoCreadoEnCarga> UnidadesCreadas,
    IReadOnlyList<CatalogoCreadoEnCarga> EquiposCreados)
{
    public bool TieneErrores => Errores.Count > 0;

    public int Rechazados => Errores.Count;

    public string HuellaConfirmacion => Conciliacion.Huella;
}
