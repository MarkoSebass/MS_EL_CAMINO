using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MS_EL_CAMINO.Models;

[Table("dispositivo")]
public class Dispositivo
{
    [Key]
    [Column("id_dispositivo")]
    public int IdDispositivo { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Column("id_categoria")]
    public int IdCategoria { get; set; }

    [Column("marca")]
    public string? Marca { get; set; }

    [Column("modelo")]
    public string? Modelo { get; set; }

    [Column("precio")]
    public decimal Precio { get; set; }

    [Column("fecha_compra", TypeName = "date")]
    public DateOnly? FechaCompra { get; set; }

    [Column("estado")]
    public string? Estado { get; set; }

    [Column("imagen_url")]
    public string? ImagenUrl { get; set; }

    [Column("especificaciones")]
    public string? Especificaciones { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("estado_coleccion")]
    public string EstadoColeccion { get; set; } = "COLECCION";

    public Categoria Categoria { get; set; } = null!;
}
