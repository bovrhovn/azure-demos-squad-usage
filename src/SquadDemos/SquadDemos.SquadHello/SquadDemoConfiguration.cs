namespace SquadDemos.SquadHello;

internal static class SquadDemoConfiguration
{
    internal const string Prompt =
        "Summarize the team. Return result and let me know who you are who did the execution.";

    internal static string GetRequiredFolderPath(Func<string?> getEnvironmentVariable) =>
        getEnvironmentVariable() ?? throw new ArgumentException(
            "PROJECTDIR environment variable is not set.",
            "PROJECTDIR");
}