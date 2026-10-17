using System.Text.Json.Serialization;

namespace MS_EL_CAMINO.DTOs;

/// <summary>
/// Data Transfer Object para la respuesta de autenticación de la API.
/// </summary>
public class LoginResponseDTO
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("usuarioId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UsuarioId { get; set; }

    [JsonPropertyName("nombre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nombre { get; set; }
}
