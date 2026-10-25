using System.ComponentModel.DataAnnotations;
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
