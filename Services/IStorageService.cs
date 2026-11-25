namespace MS_EL_CAMINO.Services;

/// <summary>
/// Contrato para el servicio de almacenamiento de imágenes (Supabase Storage / Local).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Sube una imagen a Supabase Storage y retorna su URL pública accesible.
    /// Si las credenciales de Supabase no están configuradas, guarda en almacenamiento local de respaldo.
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);
}
