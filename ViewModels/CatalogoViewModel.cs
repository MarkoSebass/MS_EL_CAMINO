namespace MS_EL_CAMINO.ViewModels;

public class CatalogoViewModel
{
    public IReadOnlyList<CatalogoProductoViewModel> Productos { get; init; } = [];
    public int TotalDispositivos { get; init; }
    public int TotalComponentes { get; init; }
    public int TotalJuegos { get; init; }
    public decimal InversionTotal { get; init; }
}

public class CatalogoProductoViewModel
{
    public string Tipo { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? Estado { get; init; }
    public string? ImagenUrl { get; set; }
    public DateOnly? FechaCompra { get; init; }
    public decimal? Precio { get; init; }
    public string Icono { get; set; } = "bi-box-seam";
}
