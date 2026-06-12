using GitHub.Copilot;
using Spectre.Console;

AnsiConsole.MarkupLine("[green]GitHub Copilot demo![/]");
await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "Claude Haiku 4.5",
    Streaming = true,
    OnPermissionRequest = PermissionHandler.ApproveAll
});
var question = AnsiConsole.Ask<string>("Ask a question", "What is 2+2?");
AnsiConsole.MarkupLine("[blue]Sending question to Copilot: [/] " + question);

var events = session.On<SessionEvent>(ev =>
{
    switch (ev)
    {
        case AssistantMessageEvent msg:
            AnsiConsole.MarkupLine("[grey]Message[/]: " + msg.Data.Content);
            break;
        case AssistantMessageDeltaEvent deltaEvent:
            AnsiConsole.Write(deltaEvent.Data.DeltaContent);
            break;
        case SessionIdleEvent:
            Console.WriteLine();
            break;
    }
});

var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = question });

if (response?.Data.Content != null)
{
    AnsiConsole.MarkupLine("[blue]Response from Copilot:[/]");
    AnsiConsole.WriteLine(response.Data.Content);
}
events.Dispose();//unsubscribe