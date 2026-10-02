namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public interface ICopilotModule
{
    int Order { get; }

    Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default);

    void SetConfiguration(KeyValuePair<string, string> configuration);
}
