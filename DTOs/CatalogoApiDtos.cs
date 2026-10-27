using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MS_EL_CAMINO.DTOs;

public sealed class CatalogoDispositivoDto
{
    public int IdDispositivo { get; init; }
    public int IdCategoria { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string? Marca { get; init; }
    public string? Modelo { get; init; }
    public string? Estado { get; init; }
    public string? ImagenUrl { get; init; }
    public string EstadoColeccion { get; init; } = string.Empty;
}

public sealed class CatalogoDispositivoCrearDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría válida.")]
    public int IdCategoria { get; set; }

    [Required(ErrorMessage = "La marca es requerida.")]
    [StringLength(100)]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es requerido.")]
    [StringLength(150)]
    public string Modelo { get; set; } = string.Empty;

    public IFormFile? Imagen { get; set; }

    [StringLength(500)]
    [Url]
    public string? ImagenUrl { get; set; }
}

public sealed class CatalogoDispositivoUpdateDto
{
    [Range(1, int.MaxValue)]
    public int IdCategoria { get; init; }

    [Required]
    [StringLength(100)]
    public string Marca { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Modelo { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Estado { get; init; } = string.Empty;

    [StringLength(500)]
    [Url]
    public string? ImagenUrl { get; init; }

    [Required]
    [RegularExpression("^(COLECCION|DESEADO)$")]
    public string EstadoColeccion { get; init; } = "COLECCION";
}

public sealed class CatalogoJuegoDto
{
    public int IdJuego { get; init; }
    public int IdCategoria { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Plataforma { get; init; } = string.Empty;
    public string? Genero { get; init; }
    public string? Estado { get; init; }
    public string? ImagenUrl { get; init; }
}

public sealed class CatalogoJuegoUpdateDto
{
    [Range(1, int.MaxValue)]
    public int IdCategoria { get; init; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Plataforma { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Genero { get; init; }

    [StringLength(100)]
    public string? Estado { get; init; }

    [StringLength(500)]
    [Url]
    public string? ImagenUrl { get; init; }
}

public sealed class CatalogoMutationResponseDto
{
    public bool Success { get; init; } = true;
    public string Message { get; init; } = string.Empty;
    public string Seccion { get; init; } = "my-collection";
    public object? Data { get; init; }
}

public sealed class CatalogoAntiforgeryDto
{
    public string RequestToken { get; init; } = string.Empty;
    public string HeaderName { get; init; } = "X-CSRF-TOKEN";
}
