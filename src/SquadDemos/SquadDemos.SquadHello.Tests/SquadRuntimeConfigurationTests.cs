using GitHub.Copilot;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Squad.Agents.AI;

namespace SquadDemos.SquadHello.Tests;

public sealed class SquadRuntimeConfigurationTests
{
    [InstalledCopilotFact]
    public async Task Installed_CLI_starts_the_registered_Squad_session_and_loads_the_coordinator()
    {
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("SQUADHELLO_INTEGRATION_ROOT")!);
        var expectedAgentPath = Path.Combine(root, ".github", "agents", "squad.agent.md");
        Assert.True(File.Exists(expectedAgentPath), "The integration root must contain an initialized Squad coordinator.");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSquadDemo(root);
        SessionConfig? sessionConfig = null;
        builder.Services.Configure<SquadAgentOptions>(o => o.ConfigureSession = config => sessionConfig = config);
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredService<SquadAgent>();

        // MAF's CreateSessionAsync is lazy. Exercise the owned client and actual session
        // configuration without RunAsync, which would submit a billable prompt.
        var client = Assert.IsType<CopilotClient>(typeof(SquadAgent)
            .GetField("_copilotClient", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(agent));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await client.StartAsync(timeout.Token);
        var session = await client.CreateSessionAsync(sessionConfig!, timeout.Token);
        try
        {
            var selected = await session.Rpc.Agent.GetCurrentAsync(timeout.Token);
            Assert.NotNull(selected.Agent);
            Assert.Equal(
                expectedAgentPath,
                Path.GetFullPath(selected.Agent.Path!),
                ignoreCase: OperatingSystem.IsWindows());
        }
        finally
        {
            await session.DisposeAsync();
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.StopAsync().WaitAsync(cleanupTimeout.Token);
            // No prompt means no session journal, so DeleteSessionAsync cannot remove
            // this workspace-only session. Remove only this check's generated files.
            var sessionId = Guid.Parse(session.SessionId).ToString();
            var stateDirectory = Path.Combine(root, ".squad", "session-state");
            var sessionDirectory = Path.Combine(stateDirectory, sessionId);
            if (Directory.Exists(sessionDirectory))
            {
                Directory.Delete(sessionDirectory, recursive: true);
            }
            File.Delete(Path.Combine(stateDirectory, ".session-operation-locks", $"{sessionId}.lock"));
        }
    }

    [Fact]
    public async Task Registration_routes_Squad_flags_to_the_selected_CLI_and_preserves_team_session_defaults()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, $"squad-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(folder, ".github", "agents"));
        var cliPath = Path.Combine(folder, OperatingSystem.IsWindows() ? "copilot.exe" : "copilot");
        try
        {
            File.WriteAllText(cliPath, "");
            File.WriteAllText(Path.Combine(folder, ".github", "agents", "squad.agent.md"), "# Squad");
            CopilotClientOptions? clientOptions = null;
            SessionConfig? sessionConfig = null;
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddLogging();
            services.AddSquadDemo(folder, cliPath);
            services.Configure<SquadAgentOptions>(o =>
            {
                o.ConfigureCopilotClient = options => clientOptions = options;
                o.ConfigureSession = config => sessionConfig = config;
            });

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var agent = scope.ServiceProvider.GetRequiredService<SquadAgent>();

            Assert.Equal("SampleSquad", agent.Name);
            var connection = Assert.IsType<StdioRuntimeConnection>(clientOptions!.Connection);
            Assert.Equal(cliPath, connection.Path);
            Assert.Equal(["--allow-all", "--agent", "squad"], connection.Args);
            Assert.Equal(folder, clientOptions.WorkingDirectory);
            Assert.Equal(folder, sessionConfig!.WorkingDirectory);
            Assert.Equal(Path.Combine(folder, ".squad"), sessionConfig.ConfigDirectory);
            Assert.True(sessionConfig.EnableConfigDiscovery);
            Assert.NotNull(sessionConfig.OnPermissionRequest);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Resolving_a_missing_CLI_fails_before_the_bundled_runtime_can_be_selected()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SquadDemoConfiguration.ResolveCliPath(null, ""));

        Assert.Contains("full GitHub Copilot CLI was not found", exception.Message);
        Assert.Contains("COPILOT_CLI_PATH", exception.Message);
    }

    [Fact]
    public void An_explicit_missing_CLI_is_not_silently_replaced_by_a_default()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SquadDemoConfiguration.ResolveCliPath(
                Path.Combine(Environment.CurrentDirectory, $"missing-{Guid.NewGuid():N}", "copilot.exe"),
                Environment.GetEnvironmentVariable("PATH")));

        Assert.Contains("absolute path to an existing full Copilot CLI", exception.Message);
    }

    [Fact]
    public void Resolving_from_PATH_finds_the_full_CLI_in_a_directory_containing_spaces()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, $"squad cli test {Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var cliPath = Path.Combine(folder, OperatingSystem.IsWindows() ? "copilot.exe" : "copilot");
        try
        {
            File.WriteAllText(cliPath, "");

            Assert.Equal(cliPath, SquadDemoConfiguration.ResolveCliPath(null, folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    public sealed class InstalledCopilotFactAttribute : FactAttribute
    {
        public InstalledCopilotFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQUADHELLO_INTEGRATION_ROOT")))
            {
                Skip = "Set SQUADHELLO_INTEGRATION_ROOT to a trusted initialized team root and install/sign in to Copilot CLI.";
            }
        }
    }
}
