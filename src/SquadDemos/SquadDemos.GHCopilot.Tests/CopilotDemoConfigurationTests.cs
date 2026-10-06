using GitHub.Copilot;

namespace SquadDemos.GHCopilot.Tests;

public sealed class CopilotDemoConfigurationTests
{
    [Fact]
    public void CreateSessionConfig_enables_streaming_and_approves_permission_requests()
    {
        var config = CopilotDemoConfiguration.CreateSessionConfig();

        Assert.True(config.Streaming);
        Assert.NotNull(config.OnPermissionRequest);
    }

    [Fact]
    public void DefaultQuestion_identifies_the_expected_demo_request()
    {
        Assert.Equal(
            "Identify yourself and tell me how much credits will you use.",
            CopilotDemoConfiguration.DefaultQuestion);
    }
}
