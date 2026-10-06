namespace SquadDemos.SquadHello.Tests;

public sealed class SquadDemoConfigurationTests
{
    [Fact]
    public void GetRequiredFolderPath_returns_the_configured_project_directory()
    {
        var folderPath = SquadDemoConfiguration.GetRequiredFolderPath(
            () => @"C:\Projects\sample-squad");

        Assert.Equal(@"C:\Projects\sample-squad", folderPath);
    }

    [Fact]
    public void GetRequiredFolderPath_throws_when_project_directory_is_not_configured()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SquadDemoConfiguration.GetRequiredFolderPath(() => null));

        Assert.Equal("PROJECTDIR", exception.ParamName);
        Assert.Contains("PROJECTDIR environment variable is not set.", exception.Message);
    }

    [Fact]
    public void Prompt_matches_the_sample_agent_request()
    {
        Assert.Equal(
            "What is 2 + 2? Return result and let me know who you are who did the execution.",
            SquadDemoConfiguration.Prompt);
    }
}
