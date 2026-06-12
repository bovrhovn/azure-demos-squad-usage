using GitHub.Copilot;
using Spectre.Console;

AnsiConsole.MarkupLine("[green]GitHub Copilot demo![/]");
await using var client = new CopilotClient();

await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Streaming = true,
    OnPermissionRequest = PermissionHandler.ApproveAll
});
var question =
    AnsiConsole.Ask<string>("Ask a question", "Identify yourself and tell me how much credits will you use.");
AnsiConsole.MarkupLine("[blue]Sending question to Copilot: [/] " + question);

var events = session.On<SessionEvent>(ev =>
{
    switch (ev)
    {
        case AssistantMessageEvent msg:
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Message from Copilot in the event management[/]: "
                                   + msg.Data.Content);
            break;
        // STREAM DATA
        // case AssistantMessageDeltaEvent deltaEvent:
        //     AnsiConsole.Write(deltaEvent.Data.DeltaContent);
        //     break;
        case SessionIdleEvent:
            Console.WriteLine();
            break;
    }
});

var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = question });
if (response?.Data.Content != null)
{
    AnsiConsole.MarkupLine("[blue]Response from Copilot from method:[/]");
    AnsiConsole.WriteLine(response.Data.Content);
}

events.Dispose(); //unsubscribe