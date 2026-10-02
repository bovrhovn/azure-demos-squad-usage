using System.Security.Claims;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public static class ChatFeature
{
    public static IServiceCollection AddChatFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FoundryOptions>()
            .Bind(configuration.GetSection(FoundryOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddHttpClient<IFoundryChatClient, FoundryChatClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<FoundryOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });
        services.AddSingleton<IChatService, ChatService>();
        return services;
    }

    public static RouteGroupBuilder MapChatFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat")
            .RequireAuthorization()
            .WithTags("Chat");

        group.MapGet("/sessions", (ClaimsPrincipal user, IChatService chatService) =>
            TypedResults.Ok(chatService.GetSessions(GetUserId(user)))
        ).WithName("GetChatSessions").WithSummary("Gets the signed-in user's chat sessions.");

        group.MapGet("/sessions/{sessionId:guid}", Results<Ok<ChatSession>, NotFound> (
            Guid sessionId,
            ClaimsPrincipal user,
            IChatService chatService) =>
        {
            var session = chatService.GetSession(GetUserId(user), sessionId);
            return session is null ? TypedResults.NotFound() : TypedResults.Ok(session);
        }).WithName("GetChatSession").WithSummary("Gets one chat session.");

        group.MapPost("/messages", async Task<Results<Ok<ChatSession>, ValidationProblem, NotFound, ProblemHttpResult>> (
            SendChatMessageRequest request,
            ClaimsPrincipal user,
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

            try
            {
                return TypedResults.Ok(await chatService.SendMessageAsync(GetUserId(user), request, cancellationToken));
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound();
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (FoundryRequestException)
            {
                return TypedResults.Problem(
                    "The Foundry service could not complete the request.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }).WithName("SendChatMessage").WithSummary("Sends a user message to Microsoft Foundry.");

        return group;
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue("oid")
        ?? user.Identity?.Name
        ?? throw new InvalidOperationException("The signed-in user does not have an identifier.");
}
