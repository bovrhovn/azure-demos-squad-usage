using Microsoft.AspNetCore.Http.HttpResults;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public static class ChatFeature
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IChatService, ChatService>();
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
                ? TypedResults.Ok(chatService.GetSessions(status.Login!))
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

            var session = chatService.GetSession(status.Login!, sessionId);
            return session is null ? TypedResults.NotFound() : TypedResults.Ok(session);
        }).WithName("GetChatSession").WithSummary("Gets one chat session.");

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
