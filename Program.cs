using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Npgsql;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.Hubs;
using MS_EL_CAMINO.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. CONFIGURACIÓN DE BASE DE DATOS (Entity Framework Core con PostgreSQL / Supabase)
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
{
    Pooling = true,
    MinPoolSize = 5,
    MaxPoolSize = 50,
    ConnectionIdleLifetime = 300,
    ConnectionPruningInterval = 10,
    Timeout = 15,
    CommandTimeout = 30,
    KeepAlive = 30,
    NoResetOnClose = true,
    MaxAutoPrepare = 0 // Recomendado para PgBouncer Transaction Pooler (puerto 6543)
};

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseNpgsql(npgsqlBuilder.ConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        // Reintentos automáticos para conexiones remotas / cloud (Supabase Pooler)
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// ============================================================================
// 2. INYECCIÓN DE DEPENDENCIAS (Servicios de Aplicación, Caché y SignalR)
// ============================================================================
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddSignalR();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddHttpClient<IStorageService, SupabaseStorageService>();

// ============================================================================
// 3. CONFIGURACIÓN DE AUTENTICACIÓN Y COOKIES
// ============================================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ElCamino.AuthSession";
        options.LoginPath = "/Login";
        options.LogoutPath = "/Login/Logout";
        options.AccessDeniedPath = "/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            // Si la petición proviene de un endpoint de API (/api/), responder 401 en vez de redirigir a HTML
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// ============================================================================
// 4. CONTROLADORES MVC Y API
// ============================================================================
builder.Services.AddControllersWithViews();

// ============================================================================
// 5. CONFIGURACIÓN DE SWAGGER / OPENAPI
// ============================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EL CAMINO API",
        Version = "v1",
        Description = "EL CAMINO es una API para consultar y administrar tu colección tecnológica y tus futuras adquisiciones. Inicia sesión en la aplicación para acceder a las rutas protegidas. Las operaciones administrativas de escritura requieren el rol ADMIN y el token antifalsificación X-CSRF-TOKEN, que se obtiene en GET /api/catalogo/antiforgery. Los recursos del catálogo se limitan al usuario autenticado.",
        Contact = new OpenApiContact
        {
            Name = "El Camino Team",
            Email = "admin@elcamino.com"
        }
    });
    options.TagActionsBy(api =>
    {
        var controller = api.ActionDescriptor.RouteValues.TryGetValue("controller", out var value)
            ? value
            : null;
        var tag = string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase)
            ? "LOGIN"
            : controller?.StartsWith("Catalogo", StringComparison.OrdinalIgnoreCase) == true
                ? "CATÁLOGO TECNOLÓGICO"
                : controller ?? "API";
        return [tag];
    });
    var xmlDocumentation = Path.Combine(
        AppContext.BaseDirectory,
        $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlDocumentation))
    {
        options.IncludeXmlComments(xmlDocumentation);
    }
});

var app = builder.Build();

// ============================================================================
// VERIFICACIÓN DE CONEXIÓN A SUPABASE
// ============================================================================
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var canConnect = await dbContext.Database.CanConnectAsync(app.Lifetime.ApplicationStopping);
    app.Logger.LogInformation("Verificación inicial de base de datos Supabase: {Status}", canConnect ? "Conectado" : "Pendiente");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Verificación inicial de base de datos diferida.");
}

// ============================================================================
// 6. PIPELINE DE PETICIONES HTTP
// ============================================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Habilitar Swagger tanto en Desarrollo como para pruebas en localhost
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EL CAMINO API v1");
    c.RoutePrefix = "swagger"; // Disponible en /swagger
});

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});

app.UseRouting();
app.UseResponseCaching();

// Middlewares de Autenticación y Autorización en el orden correcto
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// 7. ENRUTAMIENTO (El Login es la primera pantalla del sistema)
// ============================================================================
app.MapHub<CatalogoHub>("/hubs/catalogo");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
