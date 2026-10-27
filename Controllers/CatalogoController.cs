using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.Models;
using MS_EL_CAMINO.Services;
using MS_EL_CAMINO.ViewModels;

namespace MS_EL_CAMINO.Controllers;

[Authorize]
public class CatalogoController : Controller
{
    private const string CollectionState = "COLECCION";
    private const string DesiredState = "DESEADO";
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IStorageService _storageService;
    private readonly ILogger<CatalogoController> _logger;

    public CatalogoController(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IStorageService storageService,
        ILogger<CatalogoController> logger)
    {
        _contextFactory = contextFactory;
        _storageService = storageService;
        _logger = logger;
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

    /// <summary>
    /// Registro de un nuevo producto en 'Futuras Colecciones' (ESTADO_COLECCION = 'DESEADO').
    /// Sube la imagen a Supabase Storage y obtiene su URL pública para guardarla en PostgreSQL.
    /// Respuesta instantánea vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearFuturaColeccion([FromForm] DispositivoCrearFuturaColeccionViewModel model)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { success = false, message = "Datos incompletos o inválidos.", errors });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var categoria = await context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategoria == model.IdCategoria, HttpContext.RequestAborted);

        if (categoria == null)
        {
            return BadRequest(new { success = false, message = "La categoría seleccionada no existe." });
        }

        string? imagenUrl = null;
        if (model.Imagen != null && model.Imagen.Length > 0)
        {
            try
            {
                imagenUrl = await _storageService.UploadImageAsync(model.Imagen, "dispositivos", HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen a Supabase Storage para nueva futura colección.");
            }
        }

        var nuevoDispositivo = new Dispositivo
        {
            IdUsuario = userId,
            IdCategoria = model.IdCategoria,
            Marca = model.Marca.Trim(),
            Modelo = model.Modelo.Trim(),
            Precio = 0m,
            Estado = "Deseado",
            FechaCompra = null,
            ImagenUrl = imagenUrl,
            EstadoColeccion = DesiredState
        };

        try
        {
            context.Dispositivos.Add(nuevoDispositivo);
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar futura colección en PostgreSQL para usuario {UserId}", userId);
            return StatusCode(500, new { success = false, message = "Error al guardar el nuevo producto en la base de datos." });
        }

        var nombreCompleto = $"{nuevoDispositivo.Marca} {nuevoDispositivo.Modelo}".Trim();
        var icono = GetDeviceIcon(categoria.Nombre);

        return Ok(new
        {
            success = true,
            message = "Producto registrado en Futuras Colecciones con éxito.",
            data = new CatalogoProductoViewModel
            {
                Tipo = "DISPOSITIVO",
                Categoria = categoria.Nombre,
                CategoriaId = categoria.IdCategoria,
                Nombre = nombreCompleto,
                DispositivoId = nuevoDispositivo.IdDispositivo,
                EstadoColeccion = DesiredState,
                Marca = nuevoDispositivo.Marca,
                Modelo = nuevoDispositivo.Modelo,
                Estado = nuevoDispositivo.Estado,
                ImagenUrl = nuevoDispositivo.ImagenUrl,
                Icono = icono
            }
        });
    }

    /// <summary>
    /// Marca un dispositivo de 'Futuras Colecciones' como adquirido (ESTADO_COLECCION = 'COLECCION').
    /// No duplica datos ni mueve entre tablas, solo actualiza la columna estado_coleccion en PostgreSQL.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarComoAdquirido([FromBody] DispositivoMarcarAdquiridoViewModel model)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (model == null || model.IdDispositivo <= 0)
        {
            return BadRequest(new { success = false, message = "Identificador de producto inválido." });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var dispositivo = await context.Dispositivos
            .Include(d => d.Categoria)
            .FirstOrDefaultAsync(d => d.IdDispositivo == model.IdDispositivo && d.IdUsuario == userId, HttpContext.RequestAborted);

        if (dispositivo == null)
        {
            return NotFound(new { success = false, message = "El producto no fue encontrado en tu catálogo." });
        }

        // Actualización directa sin crear registros nuevos ni duplicar datos
        dispositivo.EstadoColeccion = CollectionState;
        if (string.Equals(dispositivo.Estado, "Deseado", StringComparison.OrdinalIgnoreCase))
        {
            dispositivo.Estado = "Adquirido";
        }
        if (!dispositivo.FechaCompra.HasValue)
        {
            dispositivo.FechaCompra = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al marcar como adquirido el dispositivo {Id} del usuario {UserId}", model.IdDispositivo, userId);
            return StatusCode(500, new { success = false, message = "Error al actualizar en la base de datos." });
        }

        var nombreCompleto = $"{dispositivo.Marca} {dispositivo.Modelo}".Trim();
        var icono = GetDeviceIcon(dispositivo.Categoria.Nombre);

        return Ok(new
        {
            success = true,
            message = "¡Producto marcado como adquirido y movido a Mi Colección!",
            data = new CatalogoProductoViewModel
            {
                Tipo = "DISPOSITIVO",
                Categoria = dispositivo.Categoria.Nombre,
                CategoriaId = dispositivo.IdCategoria,
                Nombre = nombreCompleto,
                DispositivoId = dispositivo.IdDispositivo,
                EstadoColeccion = CollectionState,
                Marca = dispositivo.Marca,
                Modelo = dispositivo.Modelo,
                Estado = dispositivo.Estado,
                ImagenUrl = dispositivo.ImagenUrl,
                FechaCompra = dispositivo.FechaCompra,
                Icono = icono
            }
        });
    }

    /// <summary>
    /// Acción MVC para editar un dispositivo existente. Exclusivo para administradores.
    /// Soporta tanto peticiones AJAX (JSON) como envíos de formulario estándar.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar([FromBody] DispositivoEditarViewModel model)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { success = false, message = "Datos inválidos.", errors });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var categoriaExiste = await context.Categorias
            .AnyAsync(c => c.IdCategoria == model.IdCategoria, HttpContext.RequestAborted);
        if (!categoriaExiste)
        {
            return BadRequest(new { success = false, message = "La categoría seleccionada no existe." });
        }

        var dispositivo = await context.Dispositivos
            .FirstOrDefaultAsync(d => d.IdDispositivo == model.IdDispositivo && d.IdUsuario == userId, HttpContext.RequestAborted);

        if (dispositivo == null)
        {
            return NotFound(new { success = false, message = "El dispositivo no fue encontrado en tu catálogo." });
        }

        var categoria = await context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategoria == model.IdCategoria, HttpContext.RequestAborted);

        dispositivo.IdCategoria = model.IdCategoria;
        dispositivo.Marca = model.Marca.Trim();
        dispositivo.Modelo = model.Modelo.Trim();
        dispositivo.Estado = model.Estado.Trim();
        dispositivo.EstadoColeccion = model.EstadoColeccion.Trim().ToUpperInvariant();
        dispositivo.ImagenUrl = string.IsNullOrWhiteSpace(model.ImagenUrl) ? null : model.ImagenUrl.Trim();

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar dispositivo {Id} del usuario {UserId}", model.IdDispositivo, userId);
            return StatusCode(500, new { success = false, message = "Error al guardar los cambios en la base de datos." });
        }

        return Ok(new
        {
            success = true,
            message = "Dispositivo actualizado correctamente.",
            data = new
            {
                idDispositivo = dispositivo.IdDispositivo,
                idCategoria = dispositivo.IdCategoria,
                categoria = categoria?.Nombre ?? string.Empty,
                marca = dispositivo.Marca,
                modelo = dispositivo.Modelo,
                nombre = $"{dispositivo.Marca} {dispositivo.Modelo}".Trim(),
                estado = dispositivo.Estado,
                estadoColeccion = dispositivo.EstadoColeccion,
                imagenUrl = dispositivo.ImagenUrl
            }
        });
    }

    /// <summary>
    /// Acción MVC para eliminar un dispositivo existente. Exclusivo para administradores.
    /// Soporta peticiones asíncronas para una respuesta instantánea sin recarga pesada.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar([FromBody] DispositivoEliminarViewModel model)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (model == null || model.IdDispositivo <= 0)
        {
            return BadRequest(new { success = false, message = "Identificador de producto inválido." });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var dispositivo = await context.Dispositivos
            .FirstOrDefaultAsync(d => d.IdDispositivo == model.IdDispositivo && d.IdUsuario == userId, HttpContext.RequestAborted);

        if (dispositivo == null)
        {
            return NotFound(new { success = false, message = "El dispositivo no fue encontrado o ya fue eliminado." });
        }

        var estadoColeccion = dispositivo.EstadoColeccion;

        try
        {
            context.Dispositivos.Remove(dispositivo);
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar dispositivo {Id} del usuario {UserId}", model.IdDispositivo, userId);
            return StatusCode(500, new { success = false, message = "No se pudo eliminar el producto de la base de datos." });
        }

        return Ok(new
        {
            success = true,
            message = "Dispositivo eliminado correctamente.",
            idDispositivo = model.IdDispositivo,
            estadoColeccion
        });
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
