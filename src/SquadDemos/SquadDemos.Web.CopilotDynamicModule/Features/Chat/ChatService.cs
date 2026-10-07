using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Chat;

public sealed class ChatService(
    ICopilotChatClient copilotChatClient,
    IChatSessionNotifier chatSessionNotifier,
    IChatSessionStore chatSessionStore) : IChatService
{
    public async Task<IReadOnlyList<ChatSessionSummary>> GetSessionsAsync(
        string userId,
        CancellationToken cancellationToken) =>
        (await chatSessionStore.GetSessionsAsync(userId, cancellationToken))
            .OrderByDescending(session => session.UpdatedAt)
            .Select(session => new ChatSessionSummary(session.Id, session.Title, session.UpdatedAt))
            .ToArray();

    public Task<ChatSession?> GetSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        chatSessionStore.GetSessionAsync(userId, sessionId, cancellationToken);

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

        var session = request.SessionId is { } sessionId
            ? await chatSessionStore.GetSessionAsync(userId, sessionId, cancellationToken)
                ?? throw new KeyNotFoundException("The requested chat session does not exist.")
            : CreateSession(request.Message);

        {
            var userMessage = new ChatMessage(Guid.NewGuid(), "user", request.Message, DateTimeOffset.UtcNow);
            session = session with
            {
                Messages = [.. session.Messages, userMessage],
                UpdatedAt = userMessage.CreatedAt
            };
        }

        await chatSessionStore.SaveSessionAsync(userId, session, cancellationToken);
        await chatSessionNotifier.NotifyMessageStatusAsync(
            userId,
            new ChatMessageStatus(session.Id, "Message received."),
            cancellationToken);
        await chatSessionNotifier.NotifyMessageStatusAsync(
            userId,
            new ChatMessageStatus(session.Id, "Waiting for GitHub Copilot to respond..."),
            cancellationToken);

        CopilotChatResponse response;
        try
        {
            response = await copilotChatClient.GetResponseAsync(session.Messages, model, cancellationToken);
        }
        catch (CopilotRequestException)
        {
            await chatSessionNotifier.NotifyMessageStatusAsync(
                userId,
                new ChatMessageStatus(session.Id, "GitHub Copilot could not complete the response."),
                cancellationToken);
            throw;
        }
        catch (InvalidOperationException)
        {
            await chatSessionNotifier.NotifyMessageStatusAsync(
                userId,
                new ChatMessageStatus(session.Id, "GitHub Copilot returned an unavailable response."),
                cancellationToken);
            throw;
        }

        {
            var agentMessage = new ChatMessage(
                Guid.NewGuid(),
                "assistant",
                response.Content,
                DateTimeOffset.UtcNow,
                response.TokenUsage,
                response.Skills);
            session = session with
            {
                Messages = [.. session.Messages, agentMessage],
                UpdatedAt = agentMessage.CreatedAt
            };
        }

        await chatSessionStore.SaveSessionAsync(userId, session, cancellationToken);
        await chatSessionNotifier.NotifySessionUpdatedAsync(session, cancellationToken);
        await chatSessionNotifier.NotifyMessageStatusAsync(
            userId,
            new ChatMessageStatus(session.Id, "GitHub Copilot responded."),
            cancellationToken);
        return session;
    }

    public async Task<bool> DeleteSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var deleted = await chatSessionStore.DeleteSessionAsync(userId, sessionId, cancellationToken);
        if (deleted)
        {
            await chatSessionNotifier.NotifySessionDeletedAsync(userId, sessionId, cancellationToken);
        }

        return deleted;
    }

    private static ChatSession CreateSession(string question) =>
        new(Guid.NewGuid(), CreateTitle(question), DateTimeOffset.UtcNow, []);

    private static string CreateTitle(string question) =>
        question.Length <= 48 ? question : $"{question[..45]}...";
}
