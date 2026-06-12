using GitHub.Copilot;
using Spectre.Console;

AnsiConsole.MarkupLine("[green]GitHub Copilot demo![/]");
await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    OnPermissionRequest = PermissionHandler.ApproveAll
});
var question = AnsiConsole.Ask<string>("Ask a question","What is 2+2?");
AnsiConsole.MarkupLine("[blue]Sending question to Copilot: [/] " + question);
var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = question });
if (response?.Data.Content != null)
{
    AnsiConsole.MarkupLine("[blue]Response from Copilot:[/]");
    AnsiConsole.WriteLine(response.Data.Content);
}