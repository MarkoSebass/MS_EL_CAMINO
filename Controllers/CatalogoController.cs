using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.ViewModels;

namespace MS_EL_CAMINO.Controllers;

[Authorize]
public class CatalogoController : Controller
{
    private readonly ApplicationDbContext _context;

    public CatalogoController(ApplicationDbContext context)
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

        var dispositivos = _context.Dispositivos
            .AsNoTracking()
            .Where(dispositivo => dispositivo.IdUsuario == userId)
            .Select(dispositivo => new CatalogoProductoViewModel
            {
                Tipo = "DISPOSITIVO",
                Categoria = dispositivo.Categoria.Nombre,
                Nombre = (dispositivo.Marca ?? string.Empty) + " " + (dispositivo.Modelo ?? string.Empty),
                Detalle = dispositivo.Estado,
                Estado = dispositivo.Estado,
                ImagenUrl = dispositivo.ImagenUrl,
                FechaCompra = dispositivo.FechaCompra,
                Precio = dispositivo.Precio,
                Icono = string.Empty
            });

        var componentes = _context.Componentes
            .AsNoTracking()
            .Where(componente => componente.IdUsuario == userId)
            .Select(componente => new CatalogoProductoViewModel
            {
                Tipo = "COMPONENTE",
                Categoria = componente.TipoComponente,
                Nombre = (componente.Marca ?? string.Empty) + " " + (componente.Modelo ?? string.Empty),
                Detalle = componente.Estado,
                Estado = componente.Estado,
                ImagenUrl = componente.ImagenUrl,
                FechaCompra = componente.FechaCompra,
                Precio = componente.Precio,
                Icono = "bi-cpu"
            });

        var juegos = _context.Juegos
            .AsNoTracking()
            .Where(juego => juego.IdUsuario == userId)
            .Select(juego => new CatalogoProductoViewModel
            {
                Tipo = "JUEGO",
                Categoria = juego.Categoria.Nombre,
                Nombre = juego.Titulo,
                Detalle = juego.Plataforma + " " + (juego.Genero ?? string.Empty),
                Estado = juego.Estado,
                ImagenUrl = juego.ImagenUrl,
                FechaCompra = juego.FechaCompra,
                Precio = juego.Precio,
                Icono = "bi-controller"
            });

        var productos = await dispositivos
            .Concat(componentes)
            .Concat(juegos)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var producto in productos)
        {
            producto.Nombre = string.IsNullOrWhiteSpace(producto.Nombre)
                ? producto.Categoria
                : producto.Nombre.Trim();
            producto.Detalle = string.IsNullOrWhiteSpace(producto.Detalle)
                ? null
                : producto.Detalle.Trim();
            producto.ImagenUrl = SafeImageUrl(producto.ImagenUrl);
            if (producto.Tipo == "DISPOSITIVO")
            {
                producto.Icono = GetDeviceIcon(producto.Categoria);
            }
        }

        productos = productos
            .OrderByDescending(producto => producto.FechaCompra.HasValue)
            .ThenByDescending(producto => producto.FechaCompra)
            .ThenBy(producto => producto.Nombre)
            .ToList();

        var viewModel = new CatalogoViewModel
        {
            Productos = productos,
            TotalDispositivos = productos.Count(producto => producto.Tipo == "DISPOSITIVO"),
            TotalComponentes = productos.Count(producto => producto.Tipo == "COMPONENTE"),
            TotalJuegos = productos.Count(producto => producto.Tipo == "JUEGO"),
            InversionTotal = productos.Sum(producto => producto.Precio ?? 0)
        };

        return View(viewModel);
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

    private static string GetDeviceIcon(string category) =>
        category.Trim().ToUpperInvariant() switch
        {
            "LAPTOP" => "bi-laptop",
            "PC" => "bi-pc-display",
            "SMARTPHONE" => "bi-phone",
            "SMARTWATCH" => "bi-clock",
            "CONSOLA" => "bi-controller",
            "PERIFERICO" => "bi-keyboard",
            _ => "bi-box-seam"
        };
}
