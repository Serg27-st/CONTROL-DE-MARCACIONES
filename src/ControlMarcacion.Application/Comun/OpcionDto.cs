namespace ControlMarcacion.Application.Comun;

/// <summary>
/// Elemento de una lista desplegable.
/// </summary>
public sealed record OpcionDto(long Id, string Nombre);

/// <summary>
/// Elemento de una lista desplegable cuyo valor es texto y no un identificador numérico.
/// </summary>
/// <remarks>
/// Lo usa el desplegable de ID de marcador. Lo que se guarda en
/// <c>AsignacionMarcador.IdMarcador</c> es el código tal como lo escribe el equipo
/// ("00123"), no la clave de la fila del catálogo: la ingesta cruza el TXT por ese texto.
/// Mandar el Id numérico obligaría a resolverlo de vuelta a código en cada guardado,
/// y a que un catálogo reordenado cambiara el significado de lo ya guardado.
/// </remarks>
public sealed record OpcionTextoDto(string Valor, string Nombre);
