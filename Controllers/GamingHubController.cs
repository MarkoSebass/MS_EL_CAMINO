using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.ViewModels;

namespace MS_EL_CAMINO.Controllers;

[Authorize]
public class GamingHubController : Controller
{
    private readonly ApplicationDbContext _context;

    public GamingHubController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Forbid();
        }

        var juegos = await _context.Juegos
            .AsNoTracking()
            .Where(juego => juego.IdUsuario == userId)
            .OrderByDescending(juego => juego.FechaCompra.HasValue)
            .ThenByDescending(juego => juego.FechaCompra)
            .ThenBy(juego => juego.Titulo)
            .Select(juego => new GamingHubGameViewModel
            {
                Titulo = juego.Titulo,
                Plataforma = juego.Plataforma,
                Genero = juego.Genero,
                Estado = juego.Estado ?? string.Empty,
                ImagenUrl = juego.ImagenUrl,
                FechaRegistro = juego.FechaCompra
            })
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var juego in juegos)
        {
            (juego.EstadoKey, juego.Estado) = NormalizeStatus(juego.Estado);
            juego.ImagenUrl = SafeImageUrl(juego.ImagenUrl);
        }

        var timeline = juegos
            .GroupBy(juego => juego.FechaRegistro?.Year)
            .OrderByDescending(group => group.Key.HasValue)
            .ThenByDescending(group => group.Key)
            .Select(group => new GamingHubYearViewModel
            {
                Anio = group.Key,
                Juegos = group.ToList()
            })
            .ToList();

        return View(new GamingHubViewModel
        {
            Juegos = juegos,
            Timeline = timeline
        });
    }

    private static (string Key, string Label) NormalizeStatus(string status)
    {
        var normalized = status.Trim().ToUpperInvariant();
        return normalized switch
        {
            "JUGANDO" or "EN CURSO" or "ACTUALMENTE JUGANDO" => ("JUGANDO", "Jugando"),
            "COMPLETADO" or "COMPLETADA" or "TERMINADO" or "TERMINADA" or "FINALIZADO" or "FINALIZADA"
                => ("COMPLETADO", "Completado"),
            "ABANDONADO" or "ABANDONADA" or "PAUSADO" or "PAUSADA"
                => ("ABANDONADO", "Abandonado"),
            "PENDIENTE" or "POR JUGAR" or "EN ESPERA"
                => ("PENDIENTE", "Pendiente"),
            _ => ("PENDIENTE", "Pendiente")
        };
    }

    private static string? SafeImageUrl(string? imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString();
    }
}
