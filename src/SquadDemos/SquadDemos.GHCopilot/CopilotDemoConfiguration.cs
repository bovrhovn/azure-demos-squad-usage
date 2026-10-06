using GitHub.Copilot;

internal static class CopilotDemoConfiguration
{
    internal const string DefaultQuestion = "Identify yourself and tell me how much credits will you use.";

    internal static SessionConfig CreateSessionConfig() =>
        new()
        {
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll
        };
}
