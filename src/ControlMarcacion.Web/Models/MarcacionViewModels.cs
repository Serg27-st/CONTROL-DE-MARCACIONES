using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de carga de marcaciones.</summary>
public sealed class CargaMarcacionesVm
{
    public IReadOnlyList<ImportacionHistorialDto> Historial { get; init; } = [];

    /// <summary>Qué días hay cargados en el año y el mes que se está viendo.</summary>
    public CoberturaMarcaciones Cobertura { get; init; } = null!;

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
