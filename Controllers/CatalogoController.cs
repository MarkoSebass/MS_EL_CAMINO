using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.Models;
using MS_EL_CAMINO.ViewModels;

namespace MS_EL_CAMINO.Controllers;

[Authorize]
public class CatalogoController : Controller
{
    private const string CollectionState = "COLECCION";
    private const string DesiredState = "DESEADO";
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CatalogoController(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? seccion)
    {
        if (!TryGetUserId(out var userId))
        {
            return Forbid();
        }

        var requestAborted = HttpContext.RequestAborted;
        var devicesTask = LoadDevicesAsync(userId, requestAborted);
        var componentsTask = LoadComponentsAsync(userId, requestAborted);
        var gamesTask = LoadGamesAsync(userId, requestAborted);
        var categoriesTask = LoadCategoriesAsync(requestAborted);
        await Task.WhenAll(new Task[] { devicesTask, componentsTask, gamesTask, categoriesTask });

        var dispositivosCatalogo = await devicesTask;
        var productos = dispositivosCatalogo
            .Where(dispositivo => dispositivo.EstadoColeccion == CollectionState)
            .ToList();
        productos.AddRange(await componentsTask);
        productos.AddRange(await gamesTask);
        var futurasColecciones = dispositivosCatalogo
            .Where(dispositivo => dispositivo.EstadoColeccion == DesiredState)
            .ToList();

        NormalizeProducts(productos);
        NormalizeProducts(futurasColecciones);

        productos = SortProducts(productos);
        futurasColecciones = SortProducts(futurasColecciones);

        var viewModel = new CatalogoViewModel
        {
            Productos = productos,
            FuturasColecciones = futurasColecciones,
            Categorias = await categoriesTask,
            TotalDispositivos = productos.Count(producto => producto.Tipo == "DISPOSITIVO"),
            TotalComponentes = productos.Count(producto => producto.Tipo == "COMPONENTE"),
            TotalJuegos = productos.Count(producto => producto.Tipo == "JUEGO"),
            EsAdministrador = User.IsInRole("ADMIN"),
            SeccionInicial = string.Equals(seccion, "future-collections", StringComparison.OrdinalIgnoreCase)
                ? "future-collections"
                : "my-collection"
        };

        return View(viewModel);
    }

    private async Task<List<CatalogoProductoViewModel>> LoadDevicesAsync(int userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Dispositivos
            .AsNoTracking()
            .Where(dispositivo => dispositivo.IdUsuario == userId
                && (dispositivo.EstadoColeccion == CollectionState || dispositivo.EstadoColeccion == DesiredState))
            .Select(dispositivo => new CatalogoProductoViewModel
            {
                Tipo = "DISPOSITIVO",
                Categoria = dispositivo.Categoria.Nombre,
                CategoriaId = dispositivo.IdCategoria,
                Nombre = (dispositivo.Marca ?? string.Empty) + " " + (dispositivo.Modelo ?? string.Empty),
                DispositivoId = dispositivo.IdDispositivo,
                EstadoColeccion = dispositivo.EstadoColeccion,
                Marca = dispositivo.Marca,
                Modelo = dispositivo.Modelo,
                JuegoId = null,
                Plataforma = null,
                Genero = null,
                Detalle = null,
                Estado = dispositivo.Estado,
                ImagenUrl = dispositivo.ImagenUrl,
                FechaCompra = dispositivo.FechaCompra,
                Icono = string.Empty
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<CatalogoProductoViewModel>> LoadComponentsAsync(int userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Componentes
            .AsNoTracking()
            .Where(componente => componente.IdUsuario == userId)
            .Select(componente => new CatalogoProductoViewModel
            {
                Tipo = "COMPONENTE",
                Categoria = componente.TipoComponente,
                CategoriaId = null,
                Nombre = (componente.Marca ?? string.Empty) + " " + (componente.Modelo ?? string.Empty),
                DispositivoId = null,
                JuegoId = null,
                EstadoColeccion = CollectionState,
                Marca = componente.Marca,
                Modelo = componente.Modelo,
                Plataforma = null,
                Genero = null,
                Detalle = componente.Estado,
                Estado = componente.Estado,
                ImagenUrl = componente.ImagenUrl,
                FechaCompra = componente.FechaCompra,
                Icono = "bi-cpu"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<CatalogoProductoViewModel>> LoadGamesAsync(int userId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Juegos
            .AsNoTracking()
            .Where(juego => juego.IdUsuario == userId)
            .Select(juego => new CatalogoProductoViewModel
            {
                Tipo = "JUEGO",
                Categoria = juego.Categoria.Nombre,
                Nombre = juego.Titulo,
                DispositivoId = null,
                JuegoId = juego.IdJuego,
                CategoriaId = juego.IdCategoria,
                EstadoColeccion = CollectionState,
                Plataforma = juego.Plataforma,
                Marca = null,
                Modelo = null,
                Genero = juego.Genero,
                Detalle = juego.Plataforma + " " + (juego.Genero ?? string.Empty),
                Estado = juego.Estado,
                ImagenUrl = juego.ImagenUrl,
                FechaCompra = juego.FechaCompra,
                Icono = "bi-controller"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Categoria>> LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Categorias
            .AsNoTracking()
            .OrderBy(categoria => categoria.Nombre)
            .ToListAsync(cancellationToken);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static void NormalizeProducts(IEnumerable<CatalogoProductoViewModel> products)
    {
        foreach (var product in products)
        {
            product.Nombre = string.IsNullOrWhiteSpace(product.Nombre)
                ? product.Categoria
                : product.Nombre.Trim();
            product.Detalle = string.IsNullOrWhiteSpace(product.Detalle)
                ? null
                : product.Detalle.Trim();
            product.ImagenUrl = IsSafeImageUrl(product.ImagenUrl) ? product.ImagenUrl : null;
            if (product.Tipo == "DISPOSITIVO")
            {
                product.Icono = GetDeviceIcon(product.Categoria);
            }
        }
    }

    private static List<CatalogoProductoViewModel> SortProducts(List<CatalogoProductoViewModel> products) =>
        products
            .OrderByDescending(product => product.FechaCompra.HasValue)
            .ThenByDescending(product => product.FechaCompra)
            .ThenBy(product => product.Nombre)
            .ToList();

    private static bool IsSafeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl)
        || Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

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
