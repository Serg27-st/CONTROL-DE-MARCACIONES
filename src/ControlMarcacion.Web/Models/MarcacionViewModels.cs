using ControlMarcacion.Application.Catalogos.Dto;
using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de carga de marcaciones.</summary>
public sealed class CargaMarcacionesVm
{
    public IReadOnlyList<ImportacionHistorialDto> Historial { get; init; } = [];

    /// <summary>Equipos activos, para exigir de cuál salió el archivo.</summary>
    /// <remarks>
    /// Llevan su planta dentro, así el desplegable de marcador se filtra al elegir la
    /// planta sin volver al servidor. Si está vacía no hay ningún aparato registrado y la
    /// pantalla lo dice en vez de ofrecer un formulario que no puede completarse.
    /// </remarks>
    public IReadOnlyList<EquipoOpcionDto> Equipos { get; init; } = [];

    /// <summary>Qué días hay cargados en el año y el mes que se está viendo.</summary>
    public CoberturaMarcaciones Cobertura { get; init; } = null!;

    /// <summary>
    /// Números de aparato que marcaron en el mes y nadie tiene asignados en esas fechas.
    /// </summary>
    /// <remarks>
    /// Es la lista de trabajo para completar el padrón: sus marcaciones están guardadas,
    /// pero el tareo de esas personas sale en falta hasta que alguien tenga el número. Se
    /// calcula en vivo, así que se vacía sola a medida que se asignan.
    /// </remarks>
    public IReadOnlyList<NumeroSinAsignarDto> NumerosSinAsignar { get; init; } = [];

    /// <summary>
    /// Hoy, para distinguir un día sin marcaciones de un día que todavía no ocurrió.
    /// </summary>
    /// <remarks>
    /// Llega desde el controlador y no se calcula en la vista: pintar el calendario es
    /// presentación, pero decidir qué cuenta como hueco es una regla, y la vista no es
    /// lugar para reglas.
    /// </remarks>
    public DateOnly Hoy { get; init; }

    /// <summary>Cargas vigentes y anuladas del historial, para la cabecera de resumen.</summary>
    public int CargasVigentes => Historial.Count(i => !i.Anulada);

    public int CargasAnuladas => Historial.Count(i => i.Anulada);

    /// <summary>
    /// Aviso de que el archivo que se acaba de subir ya se cargó antes.
    /// </summary>
    /// <remarks>
    /// Cuando esto viene lleno, la pantalla pide confirmación explícita en vez de
    /// procesar: recargar el mismo archivo no rompe nada gracias al índice único, pero
    /// el usuario merece saber que probablemente se equivocó de archivo.
    /// </remarks>
    public ImportacionPrevia? Previa { get; init; }

    /// <summary>Nombre del archivo pendiente de confirmar.</summary>
    public string? ArchivoPendiente { get; init; }
}

/// <summary>Modelo de la pantalla de resultado de una importación.</summary>
public sealed class ResultadoImportacionVm
{
    public ResultadoImportacion Resultado { get; init; } = null!;
}
