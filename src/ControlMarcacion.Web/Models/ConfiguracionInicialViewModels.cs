using System.ComponentModel.DataAnnotations;

namespace ControlMarcacion.Web.Models;

/// <summary>Datos del coordinador que habilita una instalación nueva.</summary>
public sealed class ConfiguracionInicialVm
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    public string Correo { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    public string Clave { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Clave), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmacionClave { get; init; } = string.Empty;
}
