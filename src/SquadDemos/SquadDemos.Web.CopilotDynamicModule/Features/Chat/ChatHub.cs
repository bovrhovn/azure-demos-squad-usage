using Microsoft.AspNetCore.SignalR;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class ChatHub(ICopilotService copilot, IChatService chatService) : Hub
{
    public async Task JoinSession(Guid sessionId)
    {
        var status = await copilot.GetAuthenticationStatusAsync(Context.ConnectionAborted);
        if (!status.IsAuthenticated || chatService.GetSession(status.Login!, sessionId) is null)
        {
            throw new HubException("The requested chat session is unavailable.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
    }

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);

    public static string SessionGroup(Guid sessionId) => $"chat-session-{sessionId}";
}
