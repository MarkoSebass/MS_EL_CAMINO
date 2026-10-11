using System.ComponentModel.DataAnnotations;

namespace MS_EL_CAMINO.DTOs;

/// <summary>
/// Data Transfer Object para la solicitud de inicio de sesión vía API / Swagger.
/// </summary>
public class LoginRequestDTO
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo no es válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}
