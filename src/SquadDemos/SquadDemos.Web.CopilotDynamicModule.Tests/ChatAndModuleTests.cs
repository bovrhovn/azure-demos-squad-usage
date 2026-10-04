using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class ChatAndModuleTests
{
    [Fact]
    public async Task SendMessageAsync_creates_a_session_and_keeps_conversation_history()
    {
        var service = new ChatService(new FakeCopilotChatClient("Hello from Copilot."));

        var session = await service.SendMessageAsync(
            "user-1",
            new SendChatMessageRequest(null, "Explain modules."),
            null,
            CancellationToken.None);

        Assert.Equal("Explain modules.", session.Title);
        Assert.Collection(
            session.Messages,
            message => Assert.Equal(("user", "Explain modules."), (message.Role, message.Content)),
            message => Assert.Equal(("assistant", "Hello from Copilot."), (message.Role, message.Content)));
        Assert.Equal(session.Id, Assert.Single(service.GetSessions("user-1")).Id);
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
            module => Assert.Equal((10, "<section>first-dashboard</section>"), (module.Order, module.Html)),
            module => Assert.Equal((20, "<section>second-dashboard</section>"), (module.Order, module.Html)));
    }

    private sealed class FakeCopilotChatClient(string response) : ICopilotChatClient
    {
        public Task<string> GetResponseAsync(
            IReadOnlyList<ChatMessage> messages,
            string? model,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
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

        public void Refresh()
        {
        }
    }
}
