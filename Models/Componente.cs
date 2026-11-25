using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MS_EL_CAMINO.Models;

[Table("componente")]
public class Componente
{
    [Key]
    [Column("id_componente")]
    public int IdComponente { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Required]
    [Column("tipo_componente")]
    public string TipoComponente { get; set; } = string.Empty;

    [Column("marca")]
    public string? Marca { get; set; }

    [Column("modelo")]
    public string? Modelo { get; set; }

    [Column("precio")]
    public decimal? Precio { get; set; }

    [Column("fecha_compra", TypeName = "date")]
    public DateOnly? FechaCompra { get; set; }

    [Column("imagen_url")]
    public string? ImagenUrl { get; set; }

    [Column("estado")]
    public string? Estado { get; set; }
}
