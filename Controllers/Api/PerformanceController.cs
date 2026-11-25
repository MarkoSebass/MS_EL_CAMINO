using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MS_EL_CAMINO.Data;

namespace MS_EL_CAMINO.Controllers.Api;

[ApiController]
[Route("api/performance")]
[Produces("application/json")]
public sealed class PerformanceController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PerformanceController> _logger;

    public PerformanceController(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IConfiguration configuration,
        ILogger<PerformanceController> logger)
    {
        _contextFactory = contextFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Devuelve métricas en tiempo real de conexión PostgreSQL, tiempo de consulta, registros, estado de Supabase y estado del Pool.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var totalSw = Stopwatch.StartNew();
        long connectionMs = 0;
        long queryMs = 0;
        bool dbConnected = false;
        string? dbError = null;

        int totalUsuarios = 0;
        int totalDispositivos = 0;
        int totalComponentes = 0;
        int totalJuegos = 0;
        int totalCategorias = 0;

        var connString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var csb = new NpgsqlConnectionStringBuilder(connString);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var conn = context.Database.GetDbConnection();

            var connSw = Stopwatch.StartNew();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }
            connSw.Stop();
            connectionMs = connSw.ElapsedMilliseconds;
            dbConnected = true;

            var querySw = Stopwatch.StartNew();
            totalUsuarios = await context.Usuarios.AsNoTracking().CountAsync(cancellationToken);
            totalDispositivos = await context.Dispositivos.AsNoTracking().CountAsync(cancellationToken);
            totalComponentes = await context.Componentes.AsNoTracking().CountAsync(cancellationToken);
            totalJuegos = await context.Juegos.AsNoTracking().CountAsync(cancellationToken);
            totalCategorias = await context.Categorias.AsNoTracking().CountAsync(cancellationToken);
            querySw.Stop();
            queryMs = querySw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en /api/performance/health al consultar PostgreSQL.");
            dbError = ex.Message;
        }

        totalSw.Stop();

        var supabaseUrl = _configuration["Supabase:Url"];
        var supabaseStatus = dbConnected && !string.IsNullOrWhiteSpace(supabaseUrl)
            ? "Healthy"
            : dbConnected ? "Degraded (Missing Supabase:Url)" : "Unhealthy";

        return Ok(new
        {
            status = dbConnected ? "Healthy" : "Unhealthy",
            timestampUtc = DateTime.UtcNow,
            totalElapsedMs = totalSw.ElapsedMilliseconds,
            postgreSql = new
            {
                connected = dbConnected,
                connectionTimeMs = connectionMs,
                queryTimeMs = queryMs,
                error = dbError
            },
            records = new
            {
                usuarios = totalUsuarios,
                dispositivos = totalDispositivos,
                componentes = totalComponentes,
                juegos = totalJuegos,
                categorias = totalCategorias,
                total = totalUsuarios + totalDispositivos + totalComponentes + totalJuegos + totalCategorias
            },
            supabase = new
            {
                status = supabaseStatus,
                url = supabaseUrl,
                poolerMode = csb.Port == 6543 ? "Transaction Pooler (PgBouncer :6543)" : $"Direct/Session (:{csb.Port})"
            },
            pool = new
            {
                poolingEnabled = csb.Pooling,
                minPoolSize = csb.MinPoolSize,
                maxPoolSize = csb.MaxPoolSize,
                connectionIdleLifetimeSec = csb.ConnectionIdleLifetime,
                commandTimeoutSec = csb.CommandTimeout,
                keepAliveSec = csb.KeepAlive,
                noResetOnClose = csb.NoResetOnClose,
                maxAutoPrepare = csb.MaxAutoPrepare
            }
        });
    }
}
