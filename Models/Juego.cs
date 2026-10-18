using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MS_EL_CAMINO.Models;

[Table("juegos")]
public class Juego
{
    [Key]
    [Column("id_juego")]
    public int IdJuego { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Column("id_categoria")]
    public int IdCategoria { get; set; }

    [Required]
    [Column("plataforma")]
    public string Plataforma { get; set; } = string.Empty;

    [Required]
    [Column("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [Column("genero")]
    public string? Genero { get; set; }

    [Column("precio")]
    public decimal? Precio { get; set; }

    [Column("fecha_compra", TypeName = "date")]
    public DateOnly? FechaCompra { get; set; }

    [Column("estado")]
    public string? Estado { get; set; }

    [Column("imagen_url")]
    public string? ImagenUrl { get; set; }

    public Categoria Categoria { get; set; } = null!;
}
