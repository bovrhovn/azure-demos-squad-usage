using Microsoft.AspNetCore.SignalR;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public interface IChatSessionNotifier
{
    Task NotifySessionUpdatedAsync(ChatSession session, CancellationToken cancellationToken);

    Task NotifyMessageStatusAsync(
        string userId,
        ChatMessageStatus status,
        CancellationToken cancellationToken);

    Task NotifySessionDeletedAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public sealed class SignalRChatSessionNotifier(IHubContext<ChatHub> hubContext) : IChatSessionNotifier
{
    public Task NotifySessionUpdatedAsync(ChatSession session, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ChatHub.SessionGroup(session.Id))
            .SendAsync("SessionUpdated", session, cancellationToken);

    public Task NotifyMessageStatusAsync(
        string userId,
        ChatMessageStatus status,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ChatHub.UserGroup(userId))
            .SendAsync("MessageStatusUpdated", status, cancellationToken);

    public Task NotifySessionDeletedAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ChatHub.UserGroup(userId))
            .SendAsync("SessionDeleted", sessionId, cancellationToken);
}
