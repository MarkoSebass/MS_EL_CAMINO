using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MS_EL_CAMINO.DTOs;

/// <summary>
/// Data Transfer Object para la solicitud de inicio de sesión vía API / Swagger.
/// </summary>
public class LoginRequestDTO
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [JsonPropertyName("usu_lg")]
    public string UsuLg { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}
