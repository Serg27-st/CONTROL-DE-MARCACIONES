using ControlMarcacion.Domain.Comun;

namespace ControlMarcacion.Domain.Catalogos;

/// <summary>
/// Periodo de tareo. Gobierna qué se puede modificar y qué está congelado.
/// </summary>
/// <remarks>
/// ⚠️ La duración y el corte del periodo (¿mes calendario? ¿del 26 al 25?) todavía no
/// están definidos con GCA. Por eso el rango se guarda como dos fechas explícitas y no
/// se deriva de un mes: así el modelo soporta cualquier corte sin cambios.
/// </remarks>
public class Periodo : EntidadBase
{
    /// <summary>Identificador legible, por ejemplo "2026-07".</summary>
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public EstadoPeriodo Estado { get; set; } = EstadoPeriodo.Abierto;

    public DateTime? CerradoEn { get; set; }

    public string? CerradoPor { get; set; }

    public DateTime? ReabiertoEn { get; set; }

    public string? ReabiertoPor { get; set; }

    /// <summary>Motivo de la reapertura. Obligatorio: es información de auditoría.</summary>
    public string? MotivoReapertura { get; set; }

    /// <summary>
    /// Token de concurrencia. Lo incrementa <b>solo</b> quien cierra o reabre el periodo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es lo que hace atómica la regla «periodo cerrado, nada se escribe». Sin él,
    /// comprobar que está abierto y escribir eran dos operaciones separadas, y entre las dos
    /// cabía un cierre: el recálculo lee el estado al empezar y guarda decenas de miles de
    /// filas <b>minutos después</b>, así que la ventana no era teórica.
    /// </para>
    /// <para>
    /// ⚠️ Quien escribe <i>dentro</i> del periodo no lo incrementa: <b>lo afirma</b>. Marca
    /// esta propiedad como modificada sin cambiarle el valor, y EF la mete en el <c>WHERE</c>
    /// del <c>UPDATE</c> — de modo que la escritura se pierde si alguien cerró mientras
    /// tanto, y no choca con otra escritura que solo esté afirmando lo mismo. Por eso dos
    /// recálculos simultáneos siguen conviviendo y un recálculo contra un cierre no.
    /// </para>
    /// </remarks>
    public int Version { get; set; }

    public bool EstaAbierto => Estado == EstadoPeriodo.Abierto;

    public bool Contiene(DateOnly fecha) => fecha >= FechaInicio && fecha <= FechaFin;
}

public enum EstadoPeriodo
{
    /// <summary>Admite carga de marcaciones, solicitudes, justificaciones y recálculo.</summary>
    Abierto = 1,

    /// <summary>Solo consulta y reportes. Ninguna escritura, sin excepciones.</summary>
    Cerrado = 2
}
