using Azure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public static class ChatFeature
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChatStorageOptions>()
            .Bind(configuration.GetSection(ChatStorageOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ConnectionString) ||
                    Uri.TryCreate(options.AccountEndpoint, UriKind.Absolute, out _),
                "Cosmos:ConnectionString or Cosmos:AccountEndpoint must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "Cosmos:DatabaseName must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ContainerName),
                "Cosmos:ContainerName must be configured.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ChatStorageOptions>>().Value;
            return !string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new CosmosClient(options.ConnectionString)
                : new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential());
        });
        services.AddSingleton<IChatSessionStore, CosmosChatSessionStore>();
        services.AddHostedService<CosmosChatStoreInitializer>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IChatSessionNotifier, SignalRChatSessionNotifier>();
        services.AddSignalR();
        return services;
    }

    public static RouteGroupBuilder MapChatFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat")
            .WithTags("Chat");

        group.MapGet("/sessions", async Task<Results<Ok<IReadOnlyList<ChatSessionSummary>>, ProblemHttpResult>> (
            ICopilotService copilot,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            return status.IsAuthenticated
                ? TypedResults.Ok(await chatService.GetSessionsAsync(status.Login!, cancellationToken))
                : TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: StatusCodes.Status401Unauthorized);
        }).WithName("GetChatSessions").WithSummary("Gets the authenticated GitHub user's chat sessions.");

        group.MapGet("/sessions/{sessionId:guid}", async Task<Results<Ok<ChatSession>, NotFound, ProblemHttpResult>> (
            Guid sessionId,
            ICopilotService copilot,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                return TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: StatusCodes.Status401Unauthorized);
            }

            var session = await chatService.GetSessionAsync(status.Login!, sessionId, cancellationToken);
            return session is null ? TypedResults.NotFound() : TypedResults.Ok(session);
        }).WithName("GetChatSession").WithSummary("Gets one chat session.");

        group.MapDelete("/sessions/{sessionId:guid}", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (
            Guid sessionId,
            ICopilotService copilot,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                return TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: StatusCodes.Status401Unauthorized);
            }

            return await chatService.DeleteSessionAsync(status.Login!, sessionId, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        }).WithName("DeleteChatSession").WithSummary("Deletes a chat session and all of its messages.");

        group.MapPost("/messages", async Task<Results<Ok<ChatSession>, ValidationProblem, NotFound, ProblemHttpResult>> (
            SendChatMessageRequest request,
            HttpRequest httpRequest,
            ICopilotService copilot,
            IChatService chatService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["message"] = ["A message is required."]
                });
            }

            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                return TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: StatusCodes.Status401Unauthorized);
            }

            try
            {
                return TypedResults.Ok(await chatService.SendMessageAsync(
                    status.Login!,
                    request,
                    httpRequest.Cookies[CopilotOptions.ModelCookieName],
                    cancellationToken));
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (CopilotRequestException)
            {
                return TypedResults.Problem(
                    "GitHub Copilot could not complete the request.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }).WithName("SendChatMessage").WithSummary("Sends a user message to GitHub Copilot.");

        return group;
    }
}
