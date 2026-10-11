using System.ComponentModel.DataAnnotations;

namespace MS_EL_CAMINO.ViewModels;

/// <summary>
/// Modelo de vista para el formulario de Login en Razor Views.
/// Contiene validaciones con mensajes descriptivos y amigables.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Por favor, ingresa tu correo electrónico.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [Display(Name = "Correo Electrónico")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Por favor, ingresa tu contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool Recordarme { get; set; } = false;

    public string? ReturnUrl { get; set; }
}
