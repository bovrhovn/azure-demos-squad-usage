using Microsoft.Extensions.DependencyInjection;
using Squad.Agents.AI;

namespace SquadDemos.SquadHello;

internal static class SquadDemoConfiguration
{
    internal const string Prompt =
        "Summarize the team. Return result and let me know who you are who did the execution.";

    internal static string GetRequiredFolderPath(Func<string?> getEnvironmentVariable) =>
        getEnvironmentVariable() ?? throw new ArgumentException(
            "PROJECTDIR environment variable is not set.",
            "PROJECTDIR");

    internal static IServiceCollection AddSquadDemo(
        this IServiceCollection services,
        string squadFolderPath,
        string? cliPath = null)
    {
        var resolvedCliPath = ResolveCliPath(
            cliPath ?? Environment.GetEnvironmentVariable("COPILOT_CLI_PATH"),
            Environment.GetEnvironmentVariable("PATH"));

        return services.AddSquadAgent(o =>
        {
            o.SquadFolderPath = squadFolderPath;
            // Squad adds full CLI flags; the SDK's bundled copilot-runtime is not the CLI.
            o.CliPath = resolvedCliPath;
            o.AgentName = "SampleSquad";
            o.Instructions = "You are a helpful assistant. Respond concisely.";
        });
    }

    internal static string ResolveCliPath(string? configuredPath, string? searchPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathFullyQualified(configuredPath) && File.Exists(configuredPath) &&
                (!OperatingSystem.IsWindows() ||
                 Path.GetExtension(configuredPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(configuredPath).Equals(".js", StringComparison.OrdinalIgnoreCase)))
            {
                return Path.GetFullPath(configuredPath);
            }

            throw new InvalidOperationException(
                "COPILOT_CLI_PATH must be an absolute path to an existing full Copilot CLI executable " +
                "(on Windows, copilot.exe or the npm CLI's .js entry point; .cmd/.ps1 shims are not supported).");
        }

        var executableName = OperatingSystem.IsWindows() ? "copilot.exe" : "copilot";
        foreach (var directory in (searchPath ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), executableName);
            if (Path.IsPathFullyQualified(candidate) && File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new InvalidOperationException(
            "The full GitHub Copilot CLI was not found on PATH. Install and sign in to Copilot CLI, " +
            "or set COPILOT_CLI_PATH to its absolute executable path. " +
            "The SDK's bundled copilot-runtime does not support Squad's --allow-all and --agent flags.");
    }
}