using System.ComponentModel.DataAnnotations;
using ControlMarcacion.Domain.Catalogos;

namespace ControlMarcacion.Application.Catalogos.Dto;

/// <summary>
/// Fila del listado de tipos de ausencia.
/// </summary>
public sealed record TipoAusenciaListaDto(
    long Id,
    string Codigo,
    string Nombre,
    ComportamientoAusencia Comportamiento,
    bool ComputaComoAsistencia,
    bool RequiereSustento,
    bool EsPorHoras,
    bool Activo,
    int Usos)
{
    /// <summary>Etiqueta legible del comportamiento para el listado.</summary>
    public string ComportamientoEtiqueta => Comportamiento switch
    {
        ComportamientoAusencia.JustificadaRemunerada => "Justificada y remunerada",
        ComportamientoAusencia.JustificadaNoRemunerada => "Justificada sin goce",
        ComportamientoAusencia.Subsidiada => "Subsidiada (EsSalud)",
        ComportamientoAusencia.PermisoParcial => "Permiso parcial",
        _ => "Desconocido"
    };
}

/// <summary>
/// Alta y edición de un tipo de ausencia.
/// </summary>
public sealed record GuardarTipoAusenciaRequest
{
    public long? Id { get; init; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>
    /// Determina cómo trata el motor de cálculo este tipo de ausencia. Es el campo que
    /// separa a este catálogo del genérico: declararlo obligatorio impide crear un tipo
    /// que luego "no sepa" el motor cómo procesar.
    /// </summary>
    /// <remarks>
    /// Es anulable a propósito. Si fuera un enum no anulable, al enviar el formulario sin
    /// elegir nada el enlazador de MVC fallaría antes de llegar a esta validación y
    /// mostraría su mensaje genérico en inglés ("The value '' is invalid"), que en una
    /// interfaz en español no le dice nada a quien la usa.
    /// </remarks>
    [Required(ErrorMessage = "Debe elegir el comportamiento del tipo de ausencia.")]
    public ComportamientoAusencia? Comportamiento { get; init; }

    /// <summary>El día cuenta como asistido para efectos del tareo (no genera falta).</summary>
    public bool ComputaComoAsistencia { get; init; } = true;

    /// <summary>Exige documento de sustento (certificado médico, resolución, etc.).</summary>
    public bool RequiereSustento { get; init; }

    /// <summary>Se registra por horas dentro de un día en vez de por días completos.</summary>
    public bool EsPorHoras { get; init; }

    public bool Activo { get; init; } = true;
}

