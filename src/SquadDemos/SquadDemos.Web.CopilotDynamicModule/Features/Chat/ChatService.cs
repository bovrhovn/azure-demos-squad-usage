using System.Collections.Concurrent;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class ChatService(ICopilotChatClient copilotChatClient) : IChatService
{
    private readonly ConcurrentDictionary<string, List<ChatSession>> sessionsByUser = new();

    public IReadOnlyList<ChatSessionSummary> GetSessions(string userId) =>
        GetUserSessions(userId)
            .OrderByDescending(session => session.UpdatedAt)
            .Select(session => new ChatSessionSummary(session.Id, session.Title, session.UpdatedAt))
            .ToArray();

    public ChatSession? GetSession(string userId, Guid sessionId) =>
        GetUserSessions(userId).SingleOrDefault(session => session.Id == sessionId);

    public async Task<ChatSession> SendMessageAsync(
        string userId,
        SendChatMessageRequest request,
        string? model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("A message is required.", nameof(request));
        }

        var sessions = GetUserSessions(userId);
        ChatSession session;

        lock (sessions)
        {
            session = request.SessionId is { } sessionId
                ? sessions.SingleOrDefault(existing => existing.Id == sessionId)
                    ?? throw new KeyNotFoundException("The requested chat session does not exist.")
                : CreateSession(request.Message);

            if (!sessions.Contains(session))
            {
                sessions.Add(session);
            }

            var userMessage = new ChatMessage(Guid.NewGuid(), "user", request.Message, DateTimeOffset.UtcNow);
            session = session with
            {
                Messages = [.. session.Messages, userMessage],
                UpdatedAt = userMessage.CreatedAt
            };
            ReplaceSession(sessions, session);
        }

        var response = await copilotChatClient.GetResponseAsync(session.Messages, model, cancellationToken);

        lock (sessions)
        {
            var agentMessage = new ChatMessage(Guid.NewGuid(), "assistant", response, DateTimeOffset.UtcNow);
            session = session with
            {
                Messages = [.. session.Messages, agentMessage],
                UpdatedAt = agentMessage.CreatedAt
            };
            ReplaceSession(sessions, session);
        }

        return session;
    }

    private List<ChatSession> GetUserSessions(string userId) =>
        sessionsByUser.GetOrAdd(userId, _ => []);

    private static ChatSession CreateSession(string question) =>
        new(Guid.NewGuid(), CreateTitle(question), DateTimeOffset.UtcNow, []);

    private static string CreateTitle(string question) =>
        question.Length <= 48 ? question : $"{question[..45]}...";

    private static void ReplaceSession(List<ChatSession> sessions, ChatSession replacement)
    {
        var index = sessions.FindIndex(session => session.Id == replacement.Id);
        sessions[index] = replacement;
    }
}
