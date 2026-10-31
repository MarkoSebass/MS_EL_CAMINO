using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using MS_EL_CAMINO.Models;

namespace MS_EL_CAMINO.ViewModels;

public class CatalogoViewModel
{
    public IReadOnlyList<CatalogoProductoViewModel> Productos { get; init; } = [];
    public IReadOnlyList<CatalogoProductoViewModel> FuturasColecciones { get; init; } = [];
    public IReadOnlyList<Categoria> Categorias { get; init; } = [];
    public int TotalDispositivos { get; init; }
    public int TotalComponentes { get; init; }
    public int TotalJuegos { get; init; }
    public bool EsAdministrador { get; init; }
    public string SeccionInicial { get; init; } = "my-collection";
}

public class CatalogoProductoViewModel
{
    public string Tipo { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public int? CategoriaId { get; init; }
    public string Nombre { get; set; } = string.Empty;
    public int? DispositivoId { get; init; }
    public int? JuegoId { get; init; }
    public string EstadoColeccion { get; init; } = "COLECCION";
    public string? Marca { get; init; }
    public string? Modelo { get; init; }
    public string? Plataforma { get; init; }
    public string? Genero { get; init; }
    public string? Detalle { get; set; }
    public string? Estado { get; init; }
    public string? ImagenUrl { get; set; }
    public DateOnly? FechaCompra { get; init; }
    public string Icono { get; set; } = "bi-box-seam";
}

public class DispositivoCrearFuturaColeccionViewModel
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría válida.")]
    [Display(Name = "Categoría")]
    public int IdCategoria { get; set; }

    [Required(ErrorMessage = "La marca es requerida.")]
    [StringLength(100, ErrorMessage = "La marca no puede superar los 100 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es requerido.")]
    [StringLength(150, ErrorMessage = "El modelo no puede superar los 150 caracteres.")]
    public string Modelo { get; set; } = string.Empty;

    [Display(Name = "Imagen")]
    public IFormFile? Imagen { get; set; }

    public bool HasValidImage =>
        Imagen == null
        || Imagen.Length is > 0 and <= MaxImageSizeBytes
        && AllowedImageExtensions.Contains(Path.GetExtension(Imagen.FileName));
}

public class DispositivoEditarViewModel
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    [Required]
    public int IdDispositivo { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría válida.")]
    [Display(Name = "Categoría")]
    public int IdCategoria { get; set; }

    [Required(ErrorMessage = "La marca es requerida.")]
    [StringLength(100, ErrorMessage = "La marca no puede superar los 100 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es requerido.")]
    [StringLength(150, ErrorMessage = "El modelo no puede superar los 150 caracteres.")]
    public string Modelo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El estado es requerido.")]
    [StringLength(100, ErrorMessage = "El estado no puede superar los 100 caracteres.")]
    public string Estado { get; set; } = string.Empty;

    [Required(ErrorMessage = "El estado de colección es requerido.")]
    [RegularExpression("^(COLECCION|DESEADO)$", ErrorMessage = "El estado de colección debe ser COLECCION o DESEADO.")]
    [Display(Name = "Estado de Colección")]
    public string EstadoColeccion { get; set; } = "COLECCION";

    [StringLength(500, ErrorMessage = "La URL de la imagen no puede superar los 500 caracteres.")]
    [Display(Name = "Imagen actual")]
    public string? ImagenUrl { get; set; }

    [Display(Name = "Nueva imagen")]
    public IFormFile? Imagen { get; set; }

    public bool HasValidImage =>
        Imagen == null
        || Imagen.Length is > 0 and <= MaxImageSizeBytes
        && AllowedImageExtensions.Contains(Path.GetExtension(Imagen.FileName));
}

public class DispositivoEliminarViewModel
{
    [Required]
    public int IdDispositivo { get; set; }
}

public class DispositivoMarcarAdquiridoViewModel
{
    [Required]
    public int IdDispositivo { get; set; }
}
