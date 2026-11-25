using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MS_EL_CAMINO.Hubs;

/// <summary>
/// Hub de SignalR para notificaciones en tiempo real del catálogo tecnológico.
/// </summary>
[Authorize]
public sealed class CatalogoHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
        await base.OnConnectedAsync();
    }
}
