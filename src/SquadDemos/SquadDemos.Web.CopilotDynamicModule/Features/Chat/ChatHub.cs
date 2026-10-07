using Microsoft.AspNetCore.SignalR;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class ChatHub(ICopilotService copilot, IChatService chatService) : Hub
{
    public async Task Subscribe()
    {
        var status = await copilot.GetAuthenticationStatusAsync(Context.ConnectionAborted);
        if (!status.IsAuthenticated)
        {
            throw new HubException("Authenticate with GitHub to use chat updates.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroup(status.Login!),
            Context.ConnectionAborted);
    }

    public async Task JoinSession(Guid sessionId)
    {
        var status = await copilot.GetAuthenticationStatusAsync(Context.ConnectionAborted);
        if (!status.IsAuthenticated ||
            await chatService.GetSessionAsync(status.Login!, sessionId, Context.ConnectionAborted) is null)
        {
            throw new HubException("The requested chat session is unavailable.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
    }

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);

    public static string SessionGroup(Guid sessionId) => $"chat-session-{sessionId}";

    public static string UserGroup(string userId) => $"chat-user-{userId}";
}
