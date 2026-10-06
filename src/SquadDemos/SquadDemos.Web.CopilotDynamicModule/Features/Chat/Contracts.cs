namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed record ChatTokenUsage(long? InputTokens, long? OutputTokens, long? ReasoningTokens);

public sealed record CopilotChatResponse(string Content, ChatTokenUsage? TokenUsage);

public sealed record ChatMessage(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    ChatTokenUsage? TokenUsage = null);

public sealed record ChatSession(Guid Id, string Title, DateTimeOffset UpdatedAt, IReadOnlyList<ChatMessage> Messages);

public sealed record ChatSessionSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);

public sealed record SendChatMessageRequest(Guid? SessionId, string Message);

public interface ICopilotChatClient
{
    Task<CopilotChatResponse> GetResponseAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model,
        CancellationToken cancellationToken);
}

public interface IChatService
{
    Task<IReadOnlyList<ChatSessionSummary>> GetSessionsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ChatSession?> GetSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ChatSession> SendMessageAsync(
        string userId,
        SendChatMessageRequest request,
        string? model,
        CancellationToken cancellationToken);
}

public interface IChatSessionStore
{
    Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ChatSession?> GetSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task SaveSessionAsync(
        string userId,
        ChatSession session,
        CancellationToken cancellationToken);
}
