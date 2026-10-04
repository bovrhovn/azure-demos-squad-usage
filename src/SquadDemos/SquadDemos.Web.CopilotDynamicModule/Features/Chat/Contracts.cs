namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed record ChatMessage(Guid Id, string Role, string Content, DateTimeOffset CreatedAt);

public sealed record ChatSession(Guid Id, string Title, DateTimeOffset UpdatedAt, IReadOnlyList<ChatMessage> Messages);

public sealed record ChatSessionSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);

public sealed record SendChatMessageRequest(Guid? SessionId, string Message);

public interface ICopilotChatClient
{
    Task<string> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model,
        CancellationToken cancellationToken);
}

public interface IChatService
{
    IReadOnlyList<ChatSessionSummary> GetSessions(string userId);

    ChatSession? GetSession(string userId, Guid sessionId);

    Task<ChatSession> SendMessageAsync(
        string userId,
        SendChatMessageRequest request,
        string? model,
        CancellationToken cancellationToken);
}
