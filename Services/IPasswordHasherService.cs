namespace MS_EL_CAMINO.Services;

/// <summary>
/// Contrato para el servicio de hashing y verificación de contraseñas.
/// Permite escalabilidad y desacoplamiento para futuras mejoras de seguridad.
/// </summary>
public interface IPasswordHasherService
{
    /// <summary>
    /// Genera un hash seguro para la contraseña proporcionada usando BCrypt.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifica si la contraseña en texto plano coincide con el hash almacenado.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
