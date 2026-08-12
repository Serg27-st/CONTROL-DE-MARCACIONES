using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Web.Models;

/// <summary>Modelo del formulario de inicio de sesión.</summary>
public sealed class IngresarVm
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    public string Correo { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    public string Clave { get; init; } = string.Empty;

    public bool Recordarme { get; init; }

    /// <summary>
    /// A dónde volver tras iniciar sesión: la página que el usuario pedía cuando
    /// [Authorize] lo interceptó y lo trajo aquí.
    /// </summary>
    public string? ReturnUrl { get; init; }
}
