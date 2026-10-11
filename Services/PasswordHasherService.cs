namespace MS_EL_CAMINO.Services;

/// <summary>
/// Implementación profesional de IPasswordHasherService utilizando BCrypt.Net.
/// Diseñado con soporte de verificación seguro y tolerante para el entorno 'El Camino'.
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly ILogger<PasswordHasherService> _logger;

    public PasswordHasherService(ILogger<PasswordHasherService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Genera un hash BCrypt con factor de costo 11 (estándar recomendado en la industria).
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));
        }

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    /// <summary>
    /// Valida la contraseña comparándola contra el hash BCrypt.
    /// También provee tolerancia en caso de pruebas iniciales directas en base de datos.
    /// </summary>
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            // Verificación estándar para hashes BCrypt ($2a$, $2b$, $2y$)
            if (passwordHash.StartsWith("$2a$") || passwordHash.StartsWith("$2b$") || passwordHash.StartsWith("$2y$"))
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }

            // Fallback de contingencia: si en pruebas iniciales en DBeaver se insertó en texto plano
            _logger.LogWarning("Se detectó una contraseña sin formato BCrypt en la base de datos.");
            return string.Equals(password, passwordHash, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la verificación del hash de contraseña.");
            return false;
        }
    }
}
