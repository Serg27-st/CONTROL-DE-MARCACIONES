namespace ControlMarcacion.Infrastructure.Persistencia;

/// <summary>
/// Prepara el término de una búsqueda por texto para compararlo sin distinguir
/// mayúsculas de minúsculas.
/// </summary>
/// <remarks>
/// <b>Por qué existe.</b> Los nombres se guardan tal como llegan —<c>ServicioTrabajadores</c>
/// solo hace <c>Trim()</c>—, así que en la base conviven "QUISPE" del padrón de planilla y
/// "Quispe" tecleado en el alta manual. Y quien busca escribe en minúscula.
///
/// Con SQL Server esto funcionaba solo, porque su colación por omisión no distingue caja.
/// <b>SQLite sí distingue</b>: EF traduce <c>string.Contains</c> a <c>instr()</c>, que
/// compara byte a byte, de modo que "quispe" no encontraba a QUISPE MAMANI. El cambio de
/// motor lo rompió en silencio: ningún test lo notó porque el proveedor InMemory compara
/// con la semántica de .NET, no con la de la base.
///
/// El patrón es <c>columna.ToUpper().Contains(termino)</c> con el término ya en mayúsculas:
/// EF lo traduce a <c>instr(upper(columna), 'TERMINO')</c> y los dos lados quedan en la
/// misma caja.
///
/// ⚠️ <b>Límite conocido: solo pliega la A a la Z.</b> La función <c>upper()</c> de SQLite
/// no toca los caracteres no ASCII, así que un apellido con eñe o tilde solo se encuentra
/// escribiéndolo con la misma caja con que está guardado: "MUÑOZ" encuentra a MUÑOZ, pero
/// "muñoz" no. Hoy no afecta a nadie —en el padrón cargado no hay ni un apellido con tilde
/// o eñe—, y SQLite no puede plegar Unicode sin la extensión ICU. Si algún día hace falta,
/// la salida es una columna normalizada de búsqueda, no más funciones en el WHERE.
/// </remarks>
internal static class BusquedaTexto
{
    /// <summary>
    /// Recorta el término y lo pasa a mayúsculas, para compararlo contra
    /// <c>columna.ToUpper()</c>.
    /// </summary>
    /// <remarks>
    /// <c>ToUpperInvariant</c> y no <c>ToUpper</c>: la cultura del hilo es <c>es-PE</c>,
    /// pero una comparación de datos no debe depender de la configuración regional de la
    /// máquina donde se despliegue.
    /// </remarks>
    public static string Normalizar(string? termino) =>
        (termino ?? string.Empty).Trim().ToUpperInvariant();
}

/*
  ⚠️ NOTA SOBRE LOS ANALIZADORES, que aplica a los cinco sitios que buscan por texto.

  Al escribir `columna.ToUpper().Contains(termino)` dentro de un Where saltan tres reglas:

    CA1304 / CA1311  «ToUpper() depende de la cultura actual; use ToUpper(CultureInfo)»
    CA1862           «use la sobrecarga de Contains con StringComparison»

  Las tres se suprimen a propósito, y no por comodidad: esa lambda es un ÁRBOL DE
  EXPRESIÓN que EF Core traduce a SQL —`instr(upper(columna), @p)`—. No llega a ejecutarse
  en .NET, así que no hay ninguna cultura actual de la que depender.

  Y no se puede hacer de otro modo: EF solo sabe traducir `ToUpper()` sin argumentos.
  `ToUpper(CultureInfo)` y `Contains(..., StringComparison)` no tienen traducción, y usar
  cualquiera de las dos convierte la consulta en un error de traducción en tiempo de
  ejecución. Tampoco hay riesgo de que se evalúe en el cliente sin avisar: desde EF Core 3
  una evaluación de cliente en un Where lanza excepción en vez de degradarse en silencio.
*/
