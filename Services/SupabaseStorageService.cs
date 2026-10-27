using System.Net.Http.Headers;

namespace MS_EL_CAMINO.Services;

/// <summary>
/// Implementación de almacenamiento con soporte para Supabase Storage (API REST)
/// y fallback automático de alta velocidad a almacenamiento local para garantizar 0 fallos.
/// </summary>
public class SupabaseStorageService : IStorageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        HttpClient httpClient,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("El archivo proporcionado está vacío o es nulo.", nameof(file));
        }

        var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
        var apiKey = _configuration["Supabase:ApiKey"]?.Trim();
        var bucket = _configuration["Supabase:Bucket"] ?? "dispositivos";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowedExtensions.Contains(extension))
        {
            extension = ".jpg";
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var objectPath = string.IsNullOrWhiteSpace(folder) ? uniqueFileName : $"{folder.Trim('/')}/{uniqueFileName}";

        // Si Supabase URL y ApiKey están configurados, intentamos subir directamente a Supabase Storage
        if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(apiKey))
        {
            try
            {
                var uploadEndpoint = $"{supabaseUrl}/storage/v1/object/{bucket}/{objectPath}";
                using var request = new HttpRequestMessage(HttpMethod.Post, uploadEndpoint);
                request.Headers.Add("apikey", apiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType);
                request.Content = content;

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    // URL Pública de Supabase Storage
                    var publicUrl = $"{supabaseUrl}/storage/v1/object/public/{bucket}/{objectPath}";
                    _logger.LogInformation("Imagen subida exitosamente a Supabase Storage: {Url}", publicUrl);
                    return publicUrl;
                }

                var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Supabase Storage respondió {StatusCode}: {Error}. Usando almacenamiento local seguro...", response.StatusCode, errorMsg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comunicarse con Supabase Storage. Usando almacenamiento local de respaldo.");
            }
        }

        // Fallback rápido local para garantizar que la aplicación nunca se caiga y la respuesta sea inmediata
        var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", folder.Trim('/'));
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var physicalPath = Path.Combine(uploadsDir, uniqueFileName);
        using (var localStream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(localStream, cancellationToken);
        }

        var localUrl = $"/uploads/{folder.Trim('/')}/{uniqueFileName}";
        _logger.LogInformation("Imagen guardada localmente: {LocalUrl}", localUrl);
        return localUrl;
    }
}
