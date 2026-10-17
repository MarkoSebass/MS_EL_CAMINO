using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MS_EL_CAMINO.Controllers;

/// <summary>
/// Dashboard principal del sistema 'El Camino'.
/// Protegido estrictamente con [Authorize].
/// Redirige al login si no se cuenta con una cookie de autenticación válida.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
