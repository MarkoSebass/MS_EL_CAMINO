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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
        Description = "API de Autenticación y Registro Tecnológico - El Camino",
        Contact = new OpenApiContact
        {
            Name = "El Camino Team",
            Email = "admin@elcamino.com"
        }
    });
});

var app = builder.Build();

// ============================================================================
// WARMUP DE CONEXIÓN A SUPABASE (Evita el fallo inicial de conexión en frío)
// ============================================================================
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var canConnect = dbContext.Database.CanConnect();
    app.Logger.LogInformation("Verificación inicial de base de datos Supabase: {Status}", canConnect ? "Conectado" : "Pendiente");
}
catch (Exception ex)
{
    app.Logger.LogWarning("Calentamiento inicial de BD diferido: {Message}", ex.Message);
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
