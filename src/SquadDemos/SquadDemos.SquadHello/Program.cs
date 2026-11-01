using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Squad.Agents.AI;
using SquadDemos.SquadHello;

AnsiConsole.MarkupLine("[green]Hello, Squad![/]");

var squadFolderPath = SquadDemoConfiguration.GetRequiredFolderPath(
    () => Environment.GetEnvironmentVariable("PROJECTDIR"));
AnsiConsole.MarkupLine("[grey] Squad folder path is [/] " + squadFolderPath);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning); // keep sample output clean
builder.Services.AddSquadDemo(squadFolderPath);

using var app1 = builder.Build();
await using var scope = app1.Services.CreateAsyncScope();
var agent1 = scope.ServiceProvider.GetRequiredService<SquadAgent>();

AnsiConsole.WriteLine($"Agent name : {agent1.Name}");
AnsiConsole.MarkupLine("[grey]Sending [/]: Summarize the team");

var session1 = await agent1.CreateSessionAsync();
var result1 = await agent1.RunAsync(
    SquadDemoConfiguration.Prompt,
    session1);

if (result1 is not null)
    Console.WriteLine($"Response  : {result1.Text}");