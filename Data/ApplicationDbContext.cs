using Microsoft.EntityFrameworkCore;
using MS_EL_CAMINO.Models;

namespace MS_EL_CAMINO.Data;

/// <summary>
/// Contexto principal de Entity Framework Core para la base de datos PostgreSQL de 'El Camino'.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Mapeo explícito y robusto de la tabla de usuarios
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuario");

            entity.HasKey(e => e.IdUsuario)
                .HasName("pk_usuario");

            entity.Property(e => e.IdUsuario)
                .HasColumnName("id_usuario")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Nombres)
                .HasColumnName("nombres")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Apellidos)
                .HasColumnName("apellidos")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.UsuLg)
                .HasColumnName("usu_lg")
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.UsuLg)
                .IsUnique()
                .HasDatabaseName("ix_usuario_usu_lg");

            entity.Property(e => e.Correo)
                .HasColumnName("correo")
                .HasMaxLength(150)
                .IsRequired();

            entity.HasIndex(e => e.Correo)
                .IsUnique()
                .HasDatabaseName("ix_usuario_correo");

            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Rol)
                .HasColumnName("rol")
                .HasMaxLength(50)
                .HasDefaultValue("USER")
                .IsRequired();

            entity.Property(e => e.Activo)
                .HasColumnName("activo")
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(e => e.FechaRegistro)
                .HasColumnName("fecha_registro")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();
        });
    }
}
