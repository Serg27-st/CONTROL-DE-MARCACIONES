using ControlMarcacion.Application.Marcaciones.Dto;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo de la pantalla de carga de marcaciones.</summary>
public sealed class CargaMarcacionesVm
{
    public IReadOnlyList<ImportacionHistorialDto> Historial { get; init; } = [];

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
