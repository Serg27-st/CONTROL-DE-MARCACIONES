namespace ControlMarcacion.Domain.Auditoria;

/// <summary>
/// Operación que quedó registrada en la bitácora de auditoría.
/// </summary>
public enum OperacionAuditada
{
    Insercion = 1,

    Modificacion = 2,

    /// <summary>
    /// Baja lógica: <c>Activo</c> pasó de verdadero a falso. Es la forma correcta de
    /// retirar algo en este sistema.
    /// </summary>
    BajaLogica = 3,

    /// <summary>
    /// Borrado físico (DELETE).
    /// </summary>
    /// <remarks>
    /// <b>No debería aparecer nunca:</b> la invariante 7 del sistema dice que nada se
    /// borra físicamente. Existe justamente para que, si algún día pasa, quede escrito
    /// quién lo hizo en vez de desaparecer sin rastro. Una fila con esta operación es
    /// motivo de revisión, no un registro más.
    /// </remarks>
    Eliminacion = 4
}

/// <summary>
/// Una fila de la bitácora: qué tabla cambió, qué registro, quién y cuándo.
/// </summary>
/// <remarks>
/// <b>No hereda de <c>EntidadBase</c> a propósito.</b> La bitácora es un libro de solo
/// escritura: no se edita, no se da de baja y no se audita a sí misma. Heredar los campos
/// <c>ModificadoEn</c>/<c>Activo</c> sugeriría lo contrario, y un <c>Activo = false</c>
/// sobre una fila de auditoría sería exactamente la manipulación que esta tabla existe
/// para hacer imposible.
///
/// <b>Ley 29733.</b> El detalle no guarda el valor de los campos marcados como dato
/// personal (documento, dirección, teléfono, correo): registra que cambiaron y quién los
/// cambió, no su contenido. Ver <c>InterceptorAuditoria</c>.
/// </remarks>
public class RegistroAuditoria
{
    public long Id { get; set; }

    /// <summary>Nombre de la tabla en la base, en snake_case (por ejemplo <c>trabajador</c>).</summary>
    public string Tabla { get; set; } = null!;

    /// <summary>
    /// Clave primaria del registro afectado, como texto: las tablas de Identity usan
    /// <c>string</c> y las del negocio <c>long</c>, y esta bitácora las cubre a todas.
    /// </summary>
    public string ClaveRegistro { get; set; } = null!;

    public OperacionAuditada Operacion { get; set; }

    /// <summary>
    /// Usuario de la aplicación que originó el cambio. <c>sistema</c> cuando el cambio
    /// viene de un proceso sin sesión (siembra, arranque, tarea programada).
    /// </summary>
    public string Usuario { get; set; } = null!;

    public DateTime FechaHora { get; set; }

    /// <summary>
    /// Campos que cambiaron, en JSON: <c>[{"campo":"...","anterior":"...","nuevo":"..."}]</c>.
    /// En una inserción solo lleva el valor nuevo; en una modificación, ambos.
    /// </summary>
    public string Detalle { get; set; } = null!;
}
