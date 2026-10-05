using GitHub.Copilot;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public sealed class CopilotOptions
{
    public const string SectionName = "Copilot";
    public const string ModelCookieName = "copilot-default-model";

    public string AuthenticationUrl { get; init; } = string.Empty;

    public string AvatarUrlTemplate { get; init; } = string.Empty;

    public string DefaultAvatarPath { get; init; } = string.Empty;

    public string DefaultModel { get; init; } = string.Empty;

    public string SkillsFolderName { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; }

    public int ModelCookieDurationDays { get; init; } = 30;

    public long MaxSkillFileSizeBytes { get; init; } = 65_536;

    public string SystemPrompt { get; init; } = string.Empty;
}

public sealed record CopilotAuthenticationStatus(
    bool IsAuthenticated,
    string? Login,
    string AuthenticationUrl,
    string AvatarUrl);

public sealed record CopilotModel(string Id, string Name);

public interface ICopilotService
{
    Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken);

    Task<string> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model,
        CancellationToken cancellationToken);
}

public sealed class CopilotRequestException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class GitHubCopilotService(
    IWebHostEnvironment environment,
    ISkillStore skillStore,
    IOptions<CopilotOptions> options) : ICopilotService, ICopilotChatClient
{
    private readonly CopilotOptions options = options.Value;

    public async Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken)
    {
        await using var client = CreateClient();
        var status = await client.GetAuthStatusAsync(cancellationToken);
        var isAuthenticated = status.IsAuthenticated && !string.IsNullOrWhiteSpace(status.Login);
        var avatarUrl = isAuthenticated
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, options.AvatarUrlTemplate, Uri.EscapeDataString(status.Login!))
            : options.DefaultAvatarPath;

        return new CopilotAuthenticationStatus(
            isAuthenticated,
            status.Login,
            options.AuthenticationUrl,
            avatarUrl);
    }

    public async Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken)
    {
        await using var client = CreateClient();
        var models = await client.ListModelsAsync(cancellationToken);
        return models
            .Select(model => new CopilotModel(model.Id, model.Name))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var client = CreateClient();
            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = string.IsNullOrWhiteSpace(model) ? options.DefaultModel : model,
                WorkingDirectory = skillStore.FolderPath,
                SkillDirectories = [skillStore.FolderPath],
                EnableSkills = true,
                SystemMessage = new SystemMessageConfig
                {
                    Content = options.SystemPrompt
                },
                OnPermissionRequest = PermissionHandler.ApproveAll
            }, cancellationToken);

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = FormatConversation(messages) },
                TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
                cancellationToken);

            return response?.Data.Content
                ?? throw new InvalidOperationException("GitHub Copilot returned an empty response.");
        }
        catch (CopilotRequestException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new CopilotRequestException("GitHub Copilot did not respond before the configured timeout.", exception);
        }
    }

    async Task<CopilotChatResponse> ICopilotChatClient.GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var client = CreateClient();
            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = string.IsNullOrWhiteSpace(model) ? options.DefaultModel : model,
                WorkingDirectory = skillStore.FolderPath,
                SkillDirectories = [skillStore.FolderPath],
                EnableSkills = true,
                SystemMessage = new SystemMessageConfig
                {
                    Content = options.SystemPrompt
                },
                OnPermissionRequest = PermissionHandler.ApproveAll
            }, cancellationToken);

            ChatTokenUsage? tokenUsage = null;
            using var usageSubscription = session.On<AssistantUsageEvent>(usageEvent =>
            {
                tokenUsage = new ChatTokenUsage(
                    usageEvent.Data.InputTokens,
                    usageEvent.Data.OutputTokens,
                    usageEvent.Data.ReasoningTokens);
            });

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = FormatConversation(messages) },
                TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
                cancellationToken);
            var content = response?.Data.Content
                ?? throw new InvalidOperationException("GitHub Copilot returned an empty response.");

            return new CopilotChatResponse(
                content,
                tokenUsage ?? new ChatTokenUsage(null, response.Data.OutputTokens, null));
        }
        catch (CopilotRequestException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new CopilotRequestException("GitHub Copilot did not respond before the configured timeout.", exception);
        }
    }

    private CopilotClient CreateClient() =>
        new(new CopilotClientOptions
        {
            WorkingDirectory = environment.ContentRootPath
        });

    private static string FormatConversation(IReadOnlyList<ChatMessage> messages) =>
        string.Join(
            Environment.NewLine,
            messages.Select(message => $"{message.Role}: {message.Content}"));
}

public static class CopilotFeature
{
    public static IServiceCollection AddCopilotFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CopilotOptions>()
            .Bind(configuration.GetSection(CopilotOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.AuthenticationUrl, UriKind.Absolute, out _),
                "Copilot:AuthenticationUrl must be an absolute URI.")
            .Validate(
                options => options.AvatarUrlTemplate.Contains("{0}", StringComparison.Ordinal),
                "Copilot:AvatarUrlTemplate must contain a {0} login placeholder.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DefaultAvatarPath),
                "Copilot:DefaultAvatarPath must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SkillsFolderName),
                "Copilot:SkillsFolderName must be configured.")
            .Validate(
                options => options.RequestTimeoutSeconds > 0,
                "Copilot:RequestTimeoutSeconds must be greater than zero.")
            .ValidateOnStart();
        services.AddSingleton<ISkillStore, SkillStore>();
        services.AddSingleton<ICopilotService, GitHubCopilotService>();
        services.AddSingleton<ICopilotChatClient>(provider => (GitHubCopilotService)provider.GetRequiredService<ICopilotService>());
        return services;
    }
}
