using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    /// <response code="200">Producto registrado y agregado a Futuras Colecciones.</response>
    /// <response code="400">Datos inválidos, imagen no válida o categoría inexistente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="500">No se pudo almacenar la imagen o guardar el producto.</response>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        if (!model.HasValidImage)
        {
            return BadRequest(new { success = false, message = "La imagen debe ser JPG, PNG, WEBP o GIF y pesar como máximo 10 MB." });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var categoria = await context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategoria == model.IdCategoria, HttpContext.RequestAborted);

        if (categoria == null)
        {
            return BadRequest(new { success = false, message = "La categoría seleccionada no existe." });
        }

        var marca = model.Marca.Trim();
        var modelo = model.Modelo.Trim();
        var alreadyRegistered = await context.Dispositivos.FirstOrDefaultAsync(dispositivo =>
            dispositivo.IdUsuario == userId
            && dispositivo.IdCategoria == model.IdCategoria
            && dispositivo.Marca != null
            && dispositivo.Modelo != null
            && dispositivo.Marca.Trim().ToLower() == marca.ToLower()
            && dispositivo.Modelo.Trim().ToLower() == modelo.ToLower(),
            HttpContext.RequestAborted);
        if (alreadyRegistered != null)
        {
            return Conflict(new { success = false, message = "Este producto ya se encuentra registrado en tu catálogo." });
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
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No se pudo subir la imagen",
                    detail: "Inténtalo nuevamente.");
            }
        }

        var nuevoDispositivo = new Dispositivo
        {
            IdUsuario = userId,
            IdCategoria = model.IdCategoria,
            Marca = marca,
            Modelo = modelo,
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_dispositivo_usuario_producto" })
        {
            _logger.LogInformation("La BD ya contiene el producto del usuario {UserId}.", userId);
            return Conflict(new { success = false, message = "El producto ya está registrado en tu catálogo." });
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
            message = "¡Registro completado correctamente!",
            idDispositivo = nuevoDispositivo.IdDispositivo,
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

        var dispositivosUsuario = context.Dispositivos
            .AsNoTracking()
            .Where(d => d.IdUsuario == userId);
        var dispositivo = await dispositivosUsuario
            .Include(d => d.Categoria)
            .FirstOrDefaultAsync(d => d.IdDispositivo == model.IdDispositivo, HttpContext.RequestAborted);

        if (dispositivo == null
            && model.IdCategoria is > 0
            && !string.IsNullOrWhiteSpace(model.Marca)
            && !string.IsNullOrWhiteSpace(model.Modelo))
        {
            var marca = model.Marca.Trim().ToLower();
            var modelo = model.Modelo.Trim().ToLower();
            dispositivo = await dispositivosUsuario
                .Where(d => d.IdCategoria == model.IdCategoria
                    && d.Marca != null
                    && d.Modelo != null
                    && d.Marca.Trim().ToLower() == marca
                    && d.Modelo.Trim().ToLower() == modelo)
                .OrderBy(d => d.IdDispositivo)
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
        }

        if (dispositivo == null)
        {
            return NotFound(new { success = false, message = "El producto no fue encontrado en tu catálogo." });
        }

        const string estado = "Coleccion";
        var fechaCompra = dispositivo.FechaCompra ?? DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            var affectedRows = await context.Dispositivos
                .Where(d => d.IdDispositivo == dispositivo.IdDispositivo
                    && d.IdUsuario == userId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(d => d.EstadoColeccion, CollectionState)
                    .SetProperty(d => d.Estado, estado)
                    .SetProperty(d => d.FechaCompra, fechaCompra),
                    HttpContext.RequestAborted);

            if (affectedRows == 0)
            {
                return NotFound(new { success = false, message = "El producto no fue encontrado en tu catálogo." });
            }
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
                Estado = estado,
                ImagenUrl = dispositivo.ImagenUrl,
                FechaCompra = fechaCompra,
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
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Editar([FromForm] DispositivoEditarViewModel model)
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

        if (!model.HasValidImage)
        {
            return BadRequest(new { success = false, message = "La imagen debe ser JPG, PNG, WEBP o GIF y pesar como máximo 10 MB." });
        }

        await using var context = await _contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var categoria = await context.Categorias
            .AsNoTracking()
            .Where(c => c.IdCategoria == model.IdCategoria)
            .Select(c => c.Nombre)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (categoria == null)
        {
            return BadRequest(new { success = false, message = "La categoría seleccionada no existe." });
        }

        var marca = model.Marca.Trim();
        var modelo = model.Modelo.Trim();
        var estado = model.Estado.Trim();
        var estadoColeccion = model.EstadoColeccion.Trim().ToUpperInvariant();
        var imagenUrl = string.IsNullOrWhiteSpace(model.ImagenUrl) ? null : model.ImagenUrl.Trim();
        if (!IsSafeImageUrl(imagenUrl))
        {
            return BadRequest(new { success = false, message = "La imagen actual no tiene una dirección válida." });
        }

        var idDispositivo = model.IdDispositivo;
        var productoExiste = await context.Dispositivos
            .AnyAsync(d => d.IdDispositivo == idDispositivo && d.IdUsuario == userId, HttpContext.RequestAborted);
        if (!productoExiste
            && model.OriginalIdCategoria is > 0
            && !string.IsNullOrWhiteSpace(model.OriginalMarca)
            && !string.IsNullOrWhiteSpace(model.OriginalModelo))
        {
            var marcaOriginal = model.OriginalMarca.Trim().ToLower();
            var modeloOriginal = model.OriginalModelo.Trim().ToLower();
            idDispositivo = await context.Dispositivos
                .Where(d => d.IdUsuario == userId
                    && d.IdCategoria == model.OriginalIdCategoria
                    && d.Marca != null
                    && d.Modelo != null
                    && d.Marca.Trim().ToLower() == marcaOriginal
                    && d.Modelo.Trim().ToLower() == modeloOriginal)
                .OrderBy(d => d.IdDispositivo)
                .Select(d => d.IdDispositivo)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
        }

        if (idDispositivo <= 0)
        {
            return NotFound(new { success = false, message = "El dispositivo no fue encontrado en tu catálogo." });
        }

        if (model.Imagen is { Length: > 0 })
        {
            try
            {
                imagenUrl = await _storageService.UploadImageAsync(model.Imagen, "dispositivos", HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen al editar el dispositivo {Id}.", model.IdDispositivo);
                return StatusCode(500, new { success = false, message = "No se pudo subir la imagen. Inténtalo nuevamente." });
            }
        }

        try
        {
            var affectedRows = await context.Dispositivos
                .Where(d => d.IdDispositivo == idDispositivo && d.IdUsuario == userId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(d => d.IdCategoria, model.IdCategoria)
                    .SetProperty(d => d.Marca, marca)
                    .SetProperty(d => d.Modelo, modelo)
                    .SetProperty(d => d.Estado, estado)
                    .SetProperty(d => d.EstadoColeccion, estadoColeccion)
                    .SetProperty(d => d.ImagenUrl, imagenUrl),
                    HttpContext.RequestAborted);
            if (affectedRows == 0)
            {
                return NotFound(new { success = false, message = "El dispositivo no fue encontrado en tu catálogo." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar dispositivo {Id} del usuario {UserId}", model.IdDispositivo, userId);
            return StatusCode(500, new { success = false, message = "Error al guardar los cambios en la base de datos." });
        }

        return Ok(new
        {
            success = true,
            message = "¡Producto actualizado correctamente!",
            data = new
            {
                dispositivoId = idDispositivo,
                idDispositivo,
                idCategoria = model.IdCategoria,
                categoria,
                marca,
                modelo,
                nombre = $"{marca} {modelo}".Trim(),
                estado,
                estadoColeccion,
                imagenUrl
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

        try
        {
            var affectedRows = await context.Dispositivos
                .Where(d => d.IdDispositivo == model.IdDispositivo && d.IdUsuario == userId)
                .ExecuteDeleteAsync(HttpContext.RequestAborted);
            if (affectedRows == 0)
            {
                return NotFound(new { success = false, message = "El dispositivo no fue encontrado o ya fue eliminado." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar dispositivo {Id} del usuario {UserId}", model.IdDispositivo, userId);
            return StatusCode(500, new { success = false, message = "No se pudo eliminar el producto de la base de datos." });
        }

        return Ok(new
        {
            success = true,
            message = "¡Producto eliminado correctamente!",
            idDispositivo = model.IdDispositivo
        });
    }

    private async Task<IActionResult> CompleteExistingDeviceRegistrationAsync(
        ApplicationDbContext context,
        Dispositivo dispositivo,
        int userId,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var wasDesired = dispositivo.EstadoColeccion == DesiredState;
        var estadoColeccion = wasDesired ? CollectionState : dispositivo.EstadoColeccion;
        var estado = wasDesired ? "Coleccion" : dispositivo.Estado;
        var fechaCompra = wasDesired
            ? dispositivo.FechaCompra ?? DateOnly.FromDateTime(DateTime.UtcNow)
            : dispositivo.FechaCompra;

        var affectedRows = await context.Dispositivos
            .Where(item => item.IdDispositivo == dispositivo.IdDispositivo && item.IdUsuario == userId)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(item => item.EstadoColeccion, estadoColeccion)
                .SetProperty(item => item.Estado, estado)
                .SetProperty(item => item.FechaCompra, fechaCompra),
                cancellationToken);

        if (affectedRows == 0)
        {
            return NotFound(new { success = false, message = "El producto ya no existe en tu catálogo." });
        }

        return Ok(new
        {
            success = true,
            message = wasDesired
                ? "¡Producto registrado y movido a Mi Colección correctamente!"
                : "El producto ya estaba registrado en tu colección.",
            idDispositivo = dispositivo.IdDispositivo,
            data = new CatalogoProductoViewModel
            {
                Tipo = "DISPOSITIVO",
                Categoria = categoryName,
                CategoriaId = dispositivo.IdCategoria,
                Nombre = $"{dispositivo.Marca} {dispositivo.Modelo}".Trim(),
                DispositivoId = dispositivo.IdDispositivo,
                EstadoColeccion = estadoColeccion,
                Marca = dispositivo.Marca,
                Modelo = dispositivo.Modelo,
                Estado = estado,
                ImagenUrl = dispositivo.ImagenUrl,
                FechaCompra = fechaCompra,
                Icono = GetDeviceIcon(categoryName)
            }
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
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        || imageUrl.StartsWith('/') && !imageUrl.StartsWith("//", StringComparison.Ordinal);

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
