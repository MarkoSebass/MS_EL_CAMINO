using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.Models;
using MS_EL_CAMINO.Services;
using MS_EL_CAMINO.ViewModels;

namespace MS_EL_CAMINO.Controllers;

/// <summary>
/// Controlador MVC para la autenticación de usuarios mediante Cookies en 'El Camino'.
/// </summary>
[AllowAnonymous]
public class LoginController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        ApplicationDbContext context,
        IPasswordHasherService passwordHasher,
        ILogger<LoginController> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Muestra la vista de login con el navbar del sistema.
    /// Si el usuario ya está autenticado, lo redirige al Dashboard.
    /// </summary>
    [HttpGet]
    public IActionResult Index(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };

        return View(model);
    }

    /// <summary>
    /// Procesa el inicio de sesión con validación robusta y manejo de excepciones de base de datos.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Correo.Trim().ToLower();
        Usuario? usuario = null;

        // 1. Consulta segura a PostgreSQL con captura de excepciones
        try
        {
            usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo.ToLower() == normalizedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al conectar o consultar PostgreSQL durante el inicio de sesión.");
            ModelState.AddModelError(string.Empty, 
                $"Error al conectar con PostgreSQL: {ex.Message}. Verifica tu cadena de conexión en appsettings.json y que la tabla 'usuario' exista.");
            return View(model);
        }

        // 2. Validar si el correo existe
        if (usuario == null)
        {
            ModelState.AddModelError(string.Empty, "El correo electrónico no se encuentra registrado en el sistema.");
            return View(model);
        }

        // 3. Validar si la cuenta está activa
        if (!usuario.Activo)
        {
            ModelState.AddModelError(string.Empty, "Tu cuenta se encuentra inactiva. Comunícate con el administrador.");
            return View(model);
        }

        // 4. Validar contraseña contra el hash almacenado
        bool esPasswordValido = _passwordHasher.VerifyPassword(model.Password, usuario.PasswordHash);
        if (!esPasswordValido)
        {
            ModelState.AddModelError(string.Empty, "La contraseña ingresada es incorrecta.");
            return View(model);
        }

        // 5. Configurar Claims e iniciar sesión con Cookie
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, $"{usuario.Nombres} {usuario.Apellidos}".Trim()),
            new(ClaimTypes.Email, usuario.Correo),
            new("Nombres", usuario.Nombres),
            new("Apellidos", usuario.Apellidos),
            new("UsuarioId", usuario.IdUsuario.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.Recordarme,
            ExpiresUtc = model.Recordarme
                ? DateTimeOffset.UtcNow.AddDays(15)
                : DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        _logger.LogInformation("Usuario ID {UsuarioId} ({Correo}) autenticado exitosamente.", usuario.IdUsuario, usuario.Correo);

        // 6. Redireccionamiento seguro
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    /// <summary>
    /// Cierra la sesión activa del usuario y limpia la Cookie.
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("Sesión cerrada por el usuario.");
        return RedirectToAction("Index", "Login");
    }
}
