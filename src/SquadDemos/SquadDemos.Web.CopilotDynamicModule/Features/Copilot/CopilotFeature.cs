using GitHub.Copilot;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
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
    IHttpContextAccessor httpContextAccessor,
    IOptions<CopilotOptions> options) : ICopilotService, ICopilotChatClient
{
    private readonly CopilotOptions options = options.Value;

    public async Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken)
    {
        var authentication = await GetAuthenticationAsync();
        var login = authentication.Principal?.Identity?.Name;
        if (!authentication.Succeeded || string.IsNullOrWhiteSpace(login))
        {
            return new CopilotAuthenticationStatus(
                false,
                null,
                options.AuthenticationUrl,
                options.DefaultAvatarPath);
        }

        var avatarUrl = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            options.AvatarUrlTemplate,
            Uri.EscapeDataString(login));

        return new CopilotAuthenticationStatus(
            true,
            login,
            options.AuthenticationUrl,
            avatarUrl);
    }

    public async Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken)
    {
        await using var client = CreateClient(await GetRequiredAccessTokenAsync());
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
            await using var client = CreateClient(await GetRequiredAccessTokenAsync());
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
            await using var client = CreateClient(await GetRequiredAccessTokenAsync());
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

    private async Task<string?> GetAccessTokenAsync()
    {
        var authentication = await GetAuthenticationAsync();
        return authentication.Succeeded
            ? authentication.Properties?.GetTokenValue("access_token")
            : null;
    }

    private async Task<AuthenticateResult> GetAuthenticationAsync()
    {
        var context = httpContextAccessor.HttpContext;
        return context is null
            ? AuthenticateResult.NoResult()
            : await context.AuthenticateAsync();
    }

    private async Task<string> GetRequiredAccessTokenAsync() =>
        await GetAccessTokenAsync()
        ?? throw new InvalidOperationException("Authenticate with GitHub to use Copilot.");

    private CopilotClient CreateClient(string accessToken) =>
        new(new CopilotClientOptions
        {
            WorkingDirectory = environment.ContentRootPath,
            GitHubToken = accessToken,
            UseLoggedInUser = false
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
        services.AddHttpContextAccessor();
        services.AddOptions<AwesomeCopilotOptions>()
            .Bind(configuration.GetSection(AwesomeCopilotOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _),
                "AwesomeCopilot:ApiBaseUrl must be an absolute URI.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Branch),
                "AwesomeCopilot:Branch must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent),
                "AwesomeCopilot:UserAgent must be configured.")
            .ValidateOnStart();
        services.AddHttpClient<IAwesomeCopilotSkillCatalog, AwesomeCopilotSkillCatalog>((serviceProvider, client) =>
        {
            var awesomeCopilotOptions = serviceProvider.GetRequiredService<IOptions<AwesomeCopilotOptions>>().Value;
            client.BaseAddress = new Uri(awesomeCopilotOptions.ApiBaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(awesomeCopilotOptions.UserAgent);
        });
        services.AddHttpClient<IGitHubDeviceFlowService, GitHubDeviceFlowService>();
        services.AddOptions<GitHubDeviceFlowOptions>()
            .Bind(configuration.GetSection(GitHubDeviceFlowOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientId),
                "GitHubDeviceFlow:ClientId must be configured with the client ID of a GitHub OAuth app.")
            .ValidateOnStart();
        services.AddSingleton<ISkillStore, SkillStore>();
        services.AddSingleton<ICopilotService, GitHubCopilotService>();
        services.AddSingleton<ICopilotChatClient>(provider => (GitHubCopilotService)provider.GetRequiredService<ICopilotService>());
        return services;
    }

    public static RouteGroupBuilder MapCopilotFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiEndpoints.Copilot).WithTags("Copilot");
        group.MapGet("/authentication", (ICopilotService copilot, CancellationToken cancellationToken) =>
            copilot.GetAuthenticationStatusAsync(cancellationToken)).WithName("GetCopilotAuthentication");
        group.MapGet("/models", async Task<Results<Ok<IReadOnlyList<CopilotModel>>, ProblemHttpResult>> (
            ICopilotService copilot, CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            return status.IsAuthenticated
                ? TypedResults.Ok(await copilot.GetModelsAsync(cancellationToken))
                : TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: 401);
        }).WithName("GetCopilotModels");
        group.MapGet("/selected-model", (HttpRequest request, IOptions<CopilotOptions> options) =>
            TypedResults.Ok(request.Cookies[CopilotOptions.ModelCookieName] ?? options.Value.DefaultModel)).WithName("GetSelectedCopilotModel");
        group.MapPut("/selected-model", async Task<Results<NoContent, BadRequest<string>, ProblemHttpResult>> (
            SelectedModelRequest request, HttpRequest httpRequest, HttpResponse httpResponse, ICopilotService copilot, IOptions<CopilotOptions> options, CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            if (!status.IsAuthenticated) return TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: 401);
            var models = await copilot.GetModelsAsync(cancellationToken);
            if (!string.IsNullOrEmpty(request.Model) && models.All(model => model.Id != request.Model)) return TypedResults.BadRequest("Choose one of the available Copilot models.");
            httpResponse.Cookies.Append(CopilotOptions.ModelCookieName, request.Model ?? string.Empty, new CookieOptions { HttpOnly = true, IsEssential = true, Secure = httpRequest.IsHttps, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(options.Value.ModelCookieDurationDays) });
            return TypedResults.NoContent();
        }).WithName("SetSelectedCopilotModel");
        group.MapPost("/device-flow/begin", async Task<Results<Ok<GitHubDeviceAuthorization>, ProblemHttpResult>> (IGitHubDeviceFlowService flow, CancellationToken cancellationToken) =>
        {
            try { return TypedResults.Ok(await flow.BeginAsync(cancellationToken)); }
            catch (InvalidOperationException exception) { return TypedResults.Problem(exception.Message, statusCode: 503); }
        }).WithName("BeginGitHubDeviceFlow");
        group.MapPost("/device-flow/complete", async Task<Results<Ok<GitHubDeviceFlowResult>, ProblemHttpResult>> (
            CompleteDeviceFlowRequest request, IGitHubDeviceFlowService flow, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await flow.CompleteAsync(request.DeviceCode, cancellationToken);
                if (result.Kind != GitHubDeviceFlowResultKind.Authorized) return TypedResults.Ok(result);
                var login = await flow.GetLoginAsync(result.AccessToken!, cancellationToken);
                var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, login), new Claim(ClaimTypes.NameIdentifier, login)], CookieAuthenticationDefaults.AuthenticationScheme);
                var properties = new AuthenticationProperties();
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = result.AccessToken! }]);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
                return TypedResults.Ok(result);
            }
            catch (InvalidOperationException exception) { return TypedResults.Problem(exception.Message, statusCode: 503); }
        }).WithName("CompleteGitHubDeviceFlow");
        group.MapPost("/sign-out", () => TypedResults.SignOut(new AuthenticationProperties { RedirectUri = "/Authenticate" }, [CookieAuthenticationDefaults.AuthenticationScheme])).WithName("SignOutGitHub");
        return group;
    }
}

public sealed record SelectedModelRequest(string? Model);
public sealed record CompleteDeviceFlowRequest(string DeviceCode);
