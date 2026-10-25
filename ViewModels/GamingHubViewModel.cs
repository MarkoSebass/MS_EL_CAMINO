namespace MS_EL_CAMINO.ViewModels;

public class GamingHubViewModel
{
    public IReadOnlyList<GamingHubGameViewModel> Juegos { get; init; } = [];
    public IReadOnlyList<GamingHubYearViewModel> Timeline { get; init; } = [];
    public int TotalJuegos => Juegos.Count;
    public int TotalCompletados => Juegos.Count(juego => juego.EstadoKey == "COMPLETADO");
    public int TotalJugando => Juegos.Count(juego => juego.EstadoKey == "JUGANDO");
    public int TotalPendientes => Juegos.Count(juego => juego.EstadoKey == "PENDIENTE");
    public GamingHubGameViewModel? JuegoActual => Juegos.FirstOrDefault(juego => juego.EstadoKey == "JUGANDO");
}

public class GamingHubGameViewModel
{
    public string Titulo { get; init; } = string.Empty;
    public string Plataforma { get; init; } = string.Empty;
    public string? Genero { get; init; }
    public string Estado { get; set; } = "Pendiente";
    public string EstadoKey { get; set; } = "PENDIENTE";
    public string? ImagenUrl { get; set; }
    public DateOnly? FechaRegistro { get; init; }
}

public class GamingHubYearViewModel
{
    public int? Anio { get; init; }
    public IReadOnlyList<GamingHubGameViewModel> Juegos { get; init; } = [];
}
