using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MS_EL_CAMINO.Data;
using MS_EL_CAMINO.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. CONFIGURACIÓN DE BASE DE DATOS (Entity Framework Core con PostgreSQL / Supabase)
// ============================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Reintentos automáticos para conexiones remotas / cloud (Supabase Pooler)
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
});

// ============================================================================
// 2. INYECCIÓN DE DEPENDENCIAS (Servicios de Aplicación)
// ============================================================================
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middlewares de Autenticación y Autorización en el orden correcto
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// 7. ENRUTAMIENTO (El Login es la primera pantalla del sistema)
// ============================================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
