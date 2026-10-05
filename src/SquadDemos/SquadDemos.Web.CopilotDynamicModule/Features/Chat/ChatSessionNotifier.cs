using Microsoft.AspNetCore.SignalR;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public interface IChatSessionNotifier
{
    Task NotifySessionUpdatedAsync(ChatSession session, CancellationToken cancellationToken);
}

public sealed class SignalRChatSessionNotifier(IHubContext<ChatHub> hubContext) : IChatSessionNotifier
{
    public Task NotifySessionUpdatedAsync(ChatSession session, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ChatHub.SessionGroup(session.Id))
            .SendAsync("SessionUpdated", session, cancellationToken);
}
