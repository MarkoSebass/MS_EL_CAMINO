using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.DTOs;
using MS_EL_CAMINO.Models;
using MS_EL_CAMINO.Services;

namespace MS_EL_CAMINO.Controllers.Api;

/// <summary>
/// API de consulta y administración del catálogo tecnológico personal.
/// </summary>
/// <remarks>
/// Todos los endpoints requieren una sesión iniciada. Las operaciones de escritura de administración requieren rol ADMIN
/// y el encabezado antifalsificación X-CSRF-TOKEN. Obtén el token mediante GET /api/catalogo/antiforgery.
/// Los resultados quedan limitados al usuario autenticado.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/catalogo")]
[Produces("application/json")]
public sealed class CatalogoApiController : ControllerBase
{
    private const string CollectionState = "COLECCION";
    private const string DesiredState = "DESEADO";
    private readonly ApplicationDbContext _context;
    private readonly IAntiforgery _antiforgery;
    private readonly IStorageService _storageService;
    private readonly ILogger<CatalogoApiController> _logger;

    public CatalogoApiController(
        ApplicationDbContext context,
        IAntiforgery antiforgery,
        IStorageService storageService,
        ILogger<CatalogoApiController> logger)
    {
        _context = context;
        _antiforgery = antiforgery;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Genera el token antifalsificación para las operaciones de esta sesión.
    /// </summary>
    /// <response code="200">Token listo para enviar en X-CSRF-TOKEN.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    [HttpGet("antiforgery")]
    [ProducesResponseType(typeof(CatalogoAntiforgeryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CatalogoAntiforgeryDto
        {
            RequestToken = tokens.RequestToken ?? string.Empty,
            HeaderName = "X-CSRF-TOKEN"
        });
    }

    /// <summary>
    /// Devuelve las categorías disponibles para clasificar y editar dispositivos y juegos.
    /// </summary>
    /// <response code="200">Catálogo de categorías ordenado por nombre.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    [HttpGet("categorias")]
    [ProducesResponseType(typeof(IReadOnlyList<Categoria>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<Categoria>>> GetCategories()
    {
        var categories = await _context.Categorias
            .AsNoTracking()
            .OrderBy(category => category.Nombre)
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(categories);
    }

    /// <summary>
    /// Lista los dispositivos del usuario autenticado, opcionalmente filtrados por COLECCION o DESEADO.
    /// </summary>
    /// <param name="estadoColeccion">Filtro opcional: COLECCION o DESEADO.</param>
    /// <response code="200">Dispositivos personales con categoría y estado de colección.</response>
    /// <response code="400">El filtro de colección no es válido.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    [HttpGet("dispositivos")]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogoDispositivoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CatalogoDispositivoDto>>> GetDevices([FromQuery] string? estadoColeccion)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var normalizedState = estadoColeccion?.Trim().ToUpperInvariant();
        if (normalizedState is not null && normalizedState is not (CollectionState or DesiredState))
        {
            return BadRequest(new { message = "estadoColeccion debe ser COLECCION o DESEADO." });
        }

        var query = _context.Dispositivos
            .AsNoTracking()
            .Where(device => device.IdUsuario == userId);
        if (normalizedState is not null)
        {
            query = query.Where(device => device.EstadoColeccion == normalizedState);
        }

        var devices = await query
            .OrderBy(device => device.Marca)
            .ThenBy(device => device.Modelo)
            .Select(device => new CatalogoDispositivoDto
            {
                IdDispositivo = device.IdDispositivo,
                IdCategoria = device.IdCategoria,
                Categoria = device.Categoria.Nombre,
                Marca = device.Marca,
                Modelo = device.Modelo,
                Estado = device.Estado,
                ImagenUrl = device.ImagenUrl,
                EstadoColeccion = device.EstadoColeccion
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(devices);
    }

    /// <summary>
    /// Obtiene un dispositivo propio por identificador.
    /// </summary>
    /// <param name="id">Identificador del dispositivo.</param>
    /// <response code="200">Dispositivo encontrado.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="404">El dispositivo no existe o no pertenece al usuario.</response>
    [HttpGet("dispositivos/{id:int}")]
    [ProducesResponseType(typeof(CatalogoDispositivoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoDispositivoDto>> GetDevice(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var device = await _context.Dispositivos
            .AsNoTracking()
            .Where(item => item.IdDispositivo == id && item.IdUsuario == userId)
            .Select(item => new CatalogoDispositivoDto
            {
                IdDispositivo = item.IdDispositivo,
                IdCategoria = item.IdCategoria,
                Categoria = item.Categoria.Nombre,
                Marca = item.Marca,
                Modelo = item.Modelo,
                Estado = item.Estado,
                ImagenUrl = item.ImagenUrl,
                EstadoColeccion = item.EstadoColeccion
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>
    /// Registra un nuevo dispositivo en Futuras Colecciones (ESTADO_COLECCION = DESEADO) subiendo su imagen a Supabase Storage.
    /// </summary>
    /// <response code="201">Producto creado exitosamente en PostgreSQL y almacenado en Supabase.</response>
    /// <response code="400">Datos inválidos o categoría inexistente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="500">No se pudo subir la imagen o guardar el producto.</response>
    [HttpPost("dispositivos/futura-coleccion")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> CreateFutureCollection([FromForm] CatalogoDispositivoCrearDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var categoria = await _context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCategoria == request.IdCategoria, HttpContext.RequestAborted);

        if (categoria == null)
        {
            return BadRequest(new { message = "La categoría seleccionada no existe." });
        }

        if (request.Imagen is { Length: > 0 } && !IsValidImage(request.Imagen))
        {
            return BadRequest(new { message = "La imagen debe ser JPG, PNG, WEBP o GIF y pesar como máximo 10 MB." });
        }

        string? imagenUrl = request.ImagenUrl;
        if (request.Imagen != null && request.Imagen.Length > 0)
        {
            try
            {
                imagenUrl = await _storageService.UploadImageAsync(request.Imagen, "dispositivos", HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir imagen a Supabase Storage.");
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No se pudo subir la imagen",
                    detail: "Inténtalo nuevamente.");
            }
        }

        var nuevoDispositivo = new Dispositivo
        {
            IdUsuario = userId,
            IdCategoria = request.IdCategoria,
            Marca = request.Marca.Trim(),
            Modelo = request.Modelo.Trim(),
            Precio = 0m,
            Estado = "Deseado",
            FechaCompra = null,
            ImagenUrl = imagenUrl,
            EstadoColeccion = DesiredState
        };

        _context.Dispositivos.Add(nuevoDispositivo);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction(nameof(GetDevice), new { id = nuevoDispositivo.IdDispositivo }, new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Dispositivo registrado en Futuras Colecciones correctamente!",
            Seccion = "future-collections",
            Data = new
            {
                idDispositivo = nuevoDispositivo.IdDispositivo,
                marca = nuevoDispositivo.Marca,
                modelo = nuevoDispositivo.Modelo,
                categoria = categoria.Nombre,
                estadoColeccion = DesiredState,
                imagenUrl = nuevoDispositivo.ImagenUrl
            }
        });
    }

    /// <summary>
    /// Marca un dispositivo de Futuras Colecciones como adquirido (cambia a COLECCION sin duplicar registros).
    /// </summary>
    /// <param name="id">Identificador del dispositivo.</param>
    /// <response code="200">Dispositivo marcado como adquirido y transferido a Mi Colección.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="404">El dispositivo no existe o no pertenece al usuario.</response>
    [HttpPost("dispositivos/{id:int}/adquirido")]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> MarkAsAcquired(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var dispositivo = await _context.Dispositivos
            .Include(d => d.Categoria)
            .FirstOrDefaultAsync(d => d.IdDispositivo == id && d.IdUsuario == userId, HttpContext.RequestAborted);

        if (dispositivo == null)
        {
            return NotFound(new { message = "No se encontró el dispositivo en tu catálogo." });
        }

        dispositivo.EstadoColeccion = CollectionState;
        if (string.Equals(dispositivo.Estado, "Deseado", StringComparison.OrdinalIgnoreCase))
        {
            dispositivo.Estado = "Adquirido";
        }
        if (!dispositivo.FechaCompra.HasValue)
        {
            dispositivo.FechaCompra = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Dispositivo marcado como adquirido y transferido a Mi Colección correctamente!",
            Seccion = "my-collection",
            Data = new
            {
                idDispositivo = dispositivo.IdDispositivo,
                marca = dispositivo.Marca,
                modelo = dispositivo.Modelo,
                categoria = dispositivo.Categoria.Nombre,
                estadoColeccion = CollectionState,
                imagenUrl = dispositivo.ImagenUrl
            }
        });
    }

    /// <summary>
    /// Actualiza los datos de un dispositivo propio en PostgreSQL. Solo ADMIN.
    /// </summary>
    /// <param name="id">Identificador del dispositivo.</param>
    /// <param name="request">Datos validados del dispositivo.</param>
    /// <param name="csrfToken">Token solicitado mediante GET /api/catalogo/antiforgery.</param>
    /// <response code="200">Dispositivo actualizado en PostgreSQL.</response>
    /// <response code="400">Datos inválidos o categoría inexistente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="403">El usuario no tiene rol ADMIN.</response>
    /// <response code="404">El dispositivo no existe o no pertenece al usuario.</response>
    [HttpPut("dispositivos/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> UpdateDevice(
        int id,
        [FromBody] CatalogoDispositivoUpdateDto request,
        [FromHeader(Name = "X-CSRF-TOKEN")] string csrfToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (!await _context.Categorias.AnyAsync(category => category.IdCategoria == request.IdCategoria, HttpContext.RequestAborted))
        {
            return BadRequest(new { message = "La categoría seleccionada no existe." });
        }

        var marca = request.Marca.Trim();
        var modelo = request.Modelo.Trim();
        var estado = request.Estado.Trim();
        var imagenUrl = NormalizeImageUrl(request.ImagenUrl);
        try
        {
            var affectedRows = await _context.Dispositivos
                .Where(item => item.IdDispositivo == id && item.IdUsuario == userId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(item => item.IdCategoria, request.IdCategoria)
                    .SetProperty(item => item.Marca, marca)
                    .SetProperty(item => item.Modelo, modelo)
                    .SetProperty(item => item.Estado, estado)
                    .SetProperty(item => item.ImagenUrl, imagenUrl)
                    .SetProperty(item => item.EstadoColeccion, request.EstadoColeccion),
                    HttpContext.RequestAborted);

            if (affectedRows == 0)
            {
                return NotFound(new { message = "No se encontró el dispositivo de tu catálogo." });
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(exception, "Error actualizando dispositivo {DeviceId} del usuario {UserId}.", id, userId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "No se pudo actualizar el dispositivo",
                detail: "La base de datos no pudo guardar los cambios.");
        }

        return Ok(new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Dispositivo actualizado correctamente en la base de datos!",
            Seccion = request.EstadoColeccion == DesiredState ? "future-collections" : "my-collection"
        });
    }

    /// <summary>
    /// Elimina un dispositivo propio de PostgreSQL. Solo ADMIN.
    /// </summary>
    /// <param name="id">Identificador del dispositivo.</param>
    /// <param name="csrfToken">Token solicitado mediante GET /api/catalogo/antiforgery.</param>
    /// <response code="200">Dispositivo eliminado definitivamente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="403">El usuario no tiene rol ADMIN.</response>
    /// <response code="404">El dispositivo no existe o no pertenece al usuario.</response>
    [HttpDelete("dispositivos/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> DeleteDevice(
        int id,
        [FromHeader(Name = "X-CSRF-TOKEN")] string csrfToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var deviceState = await _context.Dispositivos
            .AsNoTracking()
            .Where(item => item.IdDispositivo == id && item.IdUsuario == userId)
            .Select(item => item.EstadoColeccion)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (deviceState is null)
        {
            return NotFound(new { message = "No se encontró el dispositivo de tu catálogo." });
        }

        var section = deviceState == DesiredState ? "future-collections" : "my-collection";
        try
        {
            var affectedRows = await _context.Dispositivos
                .Where(item => item.IdDispositivo == id && item.IdUsuario == userId)
                .ExecuteDeleteAsync(HttpContext.RequestAborted);

            if (affectedRows == 0)
            {
                return NotFound(new { message = "El dispositivo ya no existe en tu catálogo." });
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(exception, "Error eliminando dispositivo {DeviceId} del usuario {UserId}.", id, userId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "No se pudo eliminar el dispositivo",
                detail: "La base de datos no pudo eliminar el registro.");
        }

        return Ok(new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Dispositivo eliminado de la base de datos correctamente!",
            Seccion = section
        });
    }

    /// <summary>
    /// Lista los juegos del usuario autenticado.
    /// </summary>
    /// <response code="200">Juegos con plataforma, género, estado y categoría.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    [HttpGet("juegos")]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogoJuegoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CatalogoJuegoDto>>> GetGames()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var games = await _context.Juegos
            .AsNoTracking()
            .Where(game => game.IdUsuario == userId)
            .OrderBy(game => game.Titulo)
            .Select(game => new CatalogoJuegoDto
            {
                IdJuego = game.IdJuego,
                IdCategoria = game.IdCategoria,
                Categoria = game.Categoria.Nombre,
                Titulo = game.Titulo,
                Plataforma = game.Plataforma,
                Genero = game.Genero,
                Estado = game.Estado,
                ImagenUrl = game.ImagenUrl
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(games);
    }

    /// <summary>
    /// Obtiene un juego propio por identificador.
    /// </summary>
    /// <param name="id">Identificador del juego.</param>
    /// <response code="200">Juego encontrado.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="404">El juego no existe o no pertenece al usuario.</response>
    [HttpGet("juegos/{id:int}")]
    [ProducesResponseType(typeof(CatalogoJuegoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoJuegoDto>> GetGame(int id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var game = await _context.Juegos
            .AsNoTracking()
            .Where(item => item.IdJuego == id && item.IdUsuario == userId)
            .Select(item => new CatalogoJuegoDto
            {
                IdJuego = item.IdJuego,
                IdCategoria = item.IdCategoria,
                Categoria = item.Categoria.Nombre,
                Titulo = item.Titulo,
                Plataforma = item.Plataforma,
                Genero = item.Genero,
                Estado = item.Estado,
                ImagenUrl = item.ImagenUrl
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return game is null ? NotFound() : Ok(game);
    }

    /// <summary>
    /// Actualiza la ficha de un juego propio. Solo ADMIN.
    /// </summary>
    /// <param name="id">Identificador del juego.</param>
    /// <param name="request">Título, plataforma, categoría y datos descriptivos del juego.</param>
    /// <param name="csrfToken">Token solicitado mediante GET /api/catalogo/antiforgery.</param>
    /// <response code="200">Juego actualizado en PostgreSQL.</response>
    /// <response code="400">Datos inválidos o categoría inexistente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="403">El usuario no tiene rol ADMIN.</response>
    /// <response code="404">El juego no existe o no pertenece al usuario.</response>
    [HttpPut("juegos/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> UpdateGame(
        int id,
        [FromForm] CatalogoJuegoUpdateDto request,
        [FromHeader(Name = "X-CSRF-TOKEN")] string csrfToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var category = await _context.Categorias
            .AsNoTracking()
            .Where(item => item.IdCategoria == request.IdCategoria)
            .Select(item => item.Nombre)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (category is null)
        {
            return BadRequest(new { message = "La categoría seleccionada no existe." });
        }

        if (request.Imagen is not null && !IsValidImage(request.Imagen))
        {
            return BadRequest(new { message = "La imagen debe ser JPG, PNG, WEBP o GIF y pesar como máximo 10 MB." });
        }

        var titulo = request.Titulo.Trim();
        var plataforma = request.Plataforma.Trim();
        var genero = NormalizeOptionalText(request.Genero);
        var estado = NormalizeOptionalText(request.Estado);
        var imagenUrl = NormalizeImageUrl(request.ImagenUrl);
        if (!IsSafeImageUrl(imagenUrl))
        {
            return BadRequest(new { message = "La imagen actual no tiene una dirección válida." });
        }

        if (request.Imagen is { Length: > 0 })
        {
            try
            {
                imagenUrl = await _storageService.UploadImageAsync(request.Imagen, "juegos", HttpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error subiendo imagen al actualizar juego {GameId}.", id);
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "No se pudo subir la imagen",
                    detail: "Inténtalo nuevamente.");
            }
        }
        try
        {
            var affectedRows = await _context.Juegos
                .Where(item => item.IdJuego == id && item.IdUsuario == userId)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(item => item.IdCategoria, request.IdCategoria)
                    .SetProperty(item => item.Titulo, titulo)
                    .SetProperty(item => item.Plataforma, plataforma)
                    .SetProperty(item => item.Genero, genero)
                    .SetProperty(item => item.Estado, estado)
                    .SetProperty(item => item.ImagenUrl, imagenUrl),
                    HttpContext.RequestAborted);

            if (affectedRows == 0)
            {
                return NotFound(new { message = "No se encontró el juego de tu catálogo." });
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(exception, "Error actualizando juego {GameId} del usuario {UserId}.", id, userId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "No se pudo actualizar el juego",
                detail: "La base de datos no pudo guardar los cambios.");
        }

        return Ok(new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Juego actualizado correctamente en la base de datos!",
            Data = new
            {
                idJuego = id,
                idCategoria = request.IdCategoria,
                titulo,
                nombre = titulo,
                categoria = category,
                plataforma,
                genero,
                estado,
                imagenUrl
            }
        });
    }

    /// <summary>
    /// Elimina un juego propio. Solo ADMIN.
    /// </summary>
    /// <param name="id">Identificador del juego.</param>
    /// <param name="csrfToken">Token solicitado mediante GET /api/catalogo/antiforgery.</param>
    /// <response code="200">Juego eliminado definitivamente.</response>
    /// <response code="401">No hay una sesión iniciada.</response>
    /// <response code="403">El usuario no tiene rol ADMIN.</response>
    /// <response code="404">El juego no existe o no pertenece al usuario.</response>
    [HttpDelete("juegos/{id:int}")]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(CatalogoMutationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogoMutationResponseDto>> DeleteGame(
        int id,
        [FromHeader(Name = "X-CSRF-TOKEN")] string csrfToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var affectedRows = await _context.Juegos
                .Where(item => item.IdJuego == id && item.IdUsuario == userId)
                .ExecuteDeleteAsync(HttpContext.RequestAborted);

            if (affectedRows == 0)
            {
                return NotFound(new { message = "No se encontró el juego de tu catálogo." });
            }
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogError(exception, "Error eliminando juego {GameId} del usuario {UserId}.", id, userId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "No se pudo eliminar el juego",
                detail: "La base de datos no pudo eliminar el registro.");
        }

        return Ok(new CatalogoMutationResponseDto
        {
            Success = true,
            Message = "¡Juego eliminado de la base de datos correctamente!"
        });
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

    private static bool IsSafeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl)
        || Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        || imageUrl.StartsWith('/') && !imageUrl.StartsWith("//", StringComparison.Ordinal);

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidImage(IFormFile image)
    {
        const long maxImageSizeBytes = 10 * 1024 * 1024;
        var extension = Path.GetExtension(image.FileName);
        return image.Length is > 0 and <= maxImageSizeBytes
            && extension is not null
            && new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
