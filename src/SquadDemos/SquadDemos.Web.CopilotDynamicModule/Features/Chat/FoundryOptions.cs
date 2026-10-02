namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string Endpoint { get; init; } = string.Empty;

    public string Deployment { get; init; } = string.Empty;

    public string ApiVersion { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; }

    public string SystemPrompt { get; init; } = string.Empty;
}
