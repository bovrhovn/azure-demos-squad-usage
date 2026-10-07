using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class ChatAndModuleTests
{
    [Fact]
    public async Task SendMessageAsync_creates_a_session_and_keeps_conversation_history()
    {
        var tokenUsage = new ChatTokenUsage(24, 12, 8);
        var skills = new[] { new ChatSkill("review-code", "/skills/review-code/SKILL.md") };
        var notifier = new FakeChatSessionNotifier();
        var store = new InMemoryChatSessionStore();
        var service = new ChatService(
            new FakeCopilotChatClient("Hello from Copilot.", tokenUsage, skills),
            notifier,
            store);

        var session = await service.SendMessageAsync(
            "user-1",
            new SendChatMessageRequest(null, "Explain modules."),
            null,
            CancellationToken.None);

        Assert.Equal("Explain modules.", session.Title);
        Assert.Collection(
            session.Messages,
            message => Assert.Equal(("user", "Explain modules."), (message.Role, message.Content)),
            message =>
            {
                Assert.Equal(("assistant", "Hello from Copilot."), (message.Role, message.Content));
                Assert.Equal(tokenUsage, message.TokenUsage);
                Assert.Equal(skills, message.Skills);
            });
        Assert.Equal(
            session.Id,
            Assert.Single(await service.GetSessionsAsync("user-1", CancellationToken.None)).Id);
        Assert.Equal(session, Assert.Single(notifier.UpdatedSessions));
        Assert.Collection(
            notifier.MessageStatuses,
            status => Assert.Equal((session.Id, "Message received."), (status.SessionId, status.Message)),
            status => Assert.Equal(
                (session.Id, "Waiting for GitHub Copilot to respond..."),
                (status.SessionId, status.Message)),
            status => Assert.Equal((session.Id, "GitHub Copilot responded."), (status.SessionId, status.Message)));
    }

    [Fact]
    public async Task SendMessageAsync_persists_sessions_for_a_new_chat_service_instance()
    {
        var store = new InMemoryChatSessionStore();
        var session = await new ChatService(
            new FakeCopilotChatClient("Initial response."),
            new FakeChatSessionNotifier(),
            store)
            .SendMessageAsync(
                "user-1",
                new SendChatMessageRequest(null, "Persist this conversation."),
                null,
                CancellationToken.None);

        var reloadedService = new ChatService(
            new FakeCopilotChatClient("Unused response."),
            new FakeChatSessionNotifier(),
            store);
        var persisted = await reloadedService.GetSessionAsync(
            "user-1",
            session.Id,
            CancellationToken.None);

        Assert.Equal(session, persisted);
    }

    [Fact]
    public async Task DeleteSessionAsync_removes_the_session_and_notifies_the_user()
    {
        var notifier = new FakeChatSessionNotifier();
        var service = new ChatService(
            new FakeCopilotChatClient("Response."),
            notifier,
            new InMemoryChatSessionStore());
        var session = await service.SendMessageAsync(
            "user-1",
            new SendChatMessageRequest(null, "Delete this conversation."),
            null,
            CancellationToken.None);

        var deleted = await service.DeleteSessionAsync("user-1", session.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await service.GetSessionAsync("user-1", session.Id, CancellationToken.None));
        Assert.Equal(("user-1", session.Id), Assert.Single(notifier.DeletedSessions));
    }

    [Fact]
    public void CosmosChatSessionDocument_uses_the_user_id_as_the_partition_key()
    {
        var session = new ChatSession(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Persisted conversation",
            DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"),
            []);

        var document = CosmosChatSessionDocument.From("octocat", session);
        var serialized = JObject.FromObject(document);

        Assert.Equal(session.Id.ToString("D"), serialized.Value<string>("id"));
        Assert.Equal("octocat", serialized.Value<string>("userId"));
        Assert.Equal(session, document.ToChatSession());
    }

    [Theory]
    [InlineData("AccountEndpoint=https://localhost:8081/;AccountKey=emulator-key;", "LOCAL")]
    [InlineData("AccountEndpoint=https://example.documents.azure.com:443/;AccountKey=production-key;", "PRODUCTION")]
    public void ChatStorageEnvironment_labels_the_Cosmos_emulator_as_local(
        string connectionString,
        string expectedLabel)
    {
        Assert.Equal(expectedLabel, ChatStorageEnvironment.GetLabel(connectionString));
    }

    [Fact]
    public async Task Runner_configures_modules_and_executes_them_by_order()
    {
        var first = new TestModule(10, "first");
        var second = new TestModule(20, "second");
        var runner = new CopilotModuleRunner(
            new StaticModuleLoader([second, first]),
            Options.Create(new DashboardOptions
            {
                Configuration = new Dictionary<string, string> { ["name"] = "dashboard" }
            }));

        var result = await runner.RunAsync(CancellationToken.None);

        Assert.Collection(
            result,
            module => Assert.Equal((10, "TestModule", "<section>first-dashboard</section>"), (module.Order, module.Name, module.Html)),
            module => Assert.Equal((20, "TestModule", "<section>second-dashboard</section>"), (module.Order, module.Name, module.Html)));
    }

    private sealed class FakeCopilotChatClient(
        string response,
        ChatTokenUsage? tokenUsage = null,
        IReadOnlyList<ChatSkill>? skills = null) : ICopilotChatClient
    {
        public Task<CopilotChatResponse> GetResponseAsync(
            IReadOnlyList<ChatMessage> messages,
            string? model,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotChatResponse(response, tokenUsage, skills));
    }

    private sealed class FakeChatSessionNotifier : IChatSessionNotifier
    {
        public List<ChatSession> UpdatedSessions { get; } = [];
        public List<ChatMessageStatus> MessageStatuses { get; } = [];
        public List<(string UserId, Guid SessionId)> DeletedSessions { get; } = [];

        public Task NotifySessionUpdatedAsync(ChatSession session, CancellationToken cancellationToken)
        {
            UpdatedSessions.Add(session);
            return Task.CompletedTask;
        }

        public Task NotifyMessageStatusAsync(
            string userId,
            ChatMessageStatus status,
            CancellationToken cancellationToken)
        {
            MessageStatuses.Add(status);
            return Task.CompletedTask;
        }

        public Task NotifySessionDeletedAsync(
            string userId,
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            DeletedSessions.Add((userId, sessionId));
            return Task.CompletedTask;
        }

    }

    private sealed class InMemoryChatSessionStore : IChatSessionStore
    {
        private readonly Dictionary<(string UserId, Guid SessionId), ChatSession> sessions = [];

        public Task<IReadOnlyList<ChatSession>> GetSessionsAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ChatSession>>(
                sessions
                    .Where(entry => entry.Key.UserId == userId)
                    .Select(entry => entry.Value)
                    .ToArray());

        public Task<ChatSession?> GetSessionAsync(
            string userId,
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                sessions.GetValueOrDefault((userId, sessionId)));

        public Task SaveSessionAsync(
            string userId,
            ChatSession session,
            CancellationToken cancellationToken)
        {
            sessions[(userId, session.Id)] = session;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteSessionAsync(
            string userId,
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(sessions.Remove((userId, sessionId)));
    }

    private sealed class TestModule(int order, string name) : ICopilotModule
    {
        private string? configuredName;

        public int Order => order;

        public Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult($"<section>{name}-{configuredName}</section>");

        public void SetConfiguration(KeyValuePair<string, string> configuration)
        {
            if (configuration.Key == "name")
            {
                configuredName = configuration.Value;
            }
        }
    }

    private sealed class StaticModuleLoader(IReadOnlyList<ICopilotModule> modules) : ICopilotModuleLoader
    {
        public IReadOnlyList<ICopilotModule> GetModules() => modules;

        public IReadOnlyList<string> GetModuleFiles() => [];

        public bool DeleteModule(string moduleFileName) => false;

        public Task<string> SaveModuleAsync(
            Stream assemblyStream,
            string moduleFileName,
            CancellationToken cancellationToken,
            string? source = null) =>
            Task.FromResult(moduleFileName);

        public void Refresh()
        {
        }
    }
}
