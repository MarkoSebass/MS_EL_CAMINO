using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.DTOs;
using MS_EL_CAMINO.Models;
using MS_EL_CAMINO.Services;

namespace MS_EL_CAMINO.Controllers.Api;

/// <summary>
/// Controlador API para la autenticación en Swagger y clientes REST.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        IPasswordHasherService passwordHasher,
        ILogger<AuthController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de inicio de sesión para Swagger y servicios externos.
    /// </summary>
    /// <param name="request">Credenciales del usuario</param>
    /// <returns>Resultado de autenticación con identificador y nombre</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginResponseDTO), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new LoginResponseDTO
            {
                Success = false,
                Message = "Datos de entrada incompletos o inválidos"
            });
        }

        var normalizedEmail = request.Correo.Trim().ToLower();
        Usuario? usuario = null;

        try
        {
            usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo.ToLower() == normalizedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Auth: Error al conectar con PostgreSQL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new LoginResponseDTO
            {
                Success = false,
                Message = $"Error de base de datos: {ex.Message}. Verifica tu conexión a PostgreSQL en appsettings.json."
            });
        }

        if (usuario == null)
        {
            return Unauthorized(new LoginResponseDTO
            {
                Success = false,
                Message = "Correo o contraseña incorrectos"
            });
        }

        if (!usuario.Activo)
        {
            return Unauthorized(new LoginResponseDTO
            {
                Success = false,
                Message = "El usuario se encuentra inactivo"
            });
        }

        bool esPasswordValido = _passwordHasher.VerifyPassword(request.Password, usuario.PasswordHash);
        if (!esPasswordValido)
        {
            return Unauthorized(new LoginResponseDTO
            {
                Success = false,
                Message = "Correo o contraseña incorrectos"
            });
        }

        _logger.LogInformation("API: Usuario {Correo} autenticado con éxito.", usuario.Correo);

        return Ok(new LoginResponseDTO
        {
            Success = true,
            Message = "Login correcto",
            UsuarioId = usuario.IdUsuario,
            Nombre = usuario.Nombres
        });
    }
}
