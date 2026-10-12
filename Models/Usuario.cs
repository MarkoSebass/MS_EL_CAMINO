using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MS_EL_CAMINO.Models;

/// <summary>
/// Entidad que representa la tabla USUARIO en PostgreSQL.
/// Mapeada a la tabla existente para el sistema 'El Camino'.
/// </summary>
[Table("usuario")]
public class Usuario
{
    [Key]
    [Column("id_usuario")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int IdUsuario { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("nombres")]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("apellidos")]
    public string Apellidos { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("usu_lg")]
    public string UsuLg { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("correo")]
    public string Correo { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("activo")]
    public bool Activo { get; set; } = true;

    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
