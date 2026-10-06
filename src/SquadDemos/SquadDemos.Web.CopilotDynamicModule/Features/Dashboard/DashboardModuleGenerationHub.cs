using Microsoft.AspNetCore.SignalR;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public sealed record DashboardModuleGenerationProgress(
    string Stage,
    string Message,
    bool IsComplete = false,
    bool IsError = false);

public interface IDashboardModuleGenerationNotifier
{
    Task NotifyAsync(
        string login,
        DashboardModuleGenerationProgress progress,
        CancellationToken cancellationToken);
}

public sealed class DashboardModuleGenerationHub(ICopilotService copilot) : Hub
{
    public async Task JoinDashboard()
    {
        var status = await copilot.GetAuthenticationStatusAsync(Context.ConnectionAborted);
        if (!status.IsAuthenticated)
        {
            throw new HubException("Authenticate with GitHub to receive module-generation updates.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroup(status.Login!),
            Context.ConnectionAborted);
    }

    public static string UserGroup(string login) => $"dashboard-user-{login}";
}

public sealed class SignalRDashboardModuleGenerationNotifier(
    IHubContext<DashboardModuleGenerationHub> hubContext) : IDashboardModuleGenerationNotifier
{
    public Task NotifyAsync(
        string login,
        DashboardModuleGenerationProgress progress,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DashboardModuleGenerationHub.UserGroup(login))
            .SendAsync("ModuleGenerationProgress", progress, cancellationToken);
}
