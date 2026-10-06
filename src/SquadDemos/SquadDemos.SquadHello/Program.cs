using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Squad.Agents.AI;

AnsiConsole.MarkupLine("[green]Hello, Squad![/]");

var squadFolderPath = SquadDemoConfiguration.GetRequiredFolderPath(
    () => Environment.GetEnvironmentVariable("PROJECTDIR"));
AnsiConsole.MarkupLine("[grey] Squad folder path is [/] " + squadFolderPath);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning); // keep sample output clean
builder.Services.AddSquadAgent(o =>
{
    o.SquadFolderPath = squadFolderPath;
    o.AgentName = "SampleSquad";
    o.Instructions = "You are a helpful assistant. Respond concisely.";
});

using var app1 = builder.Build();
var agent1 = app1.Services.GetRequiredService<SquadAgent>();

AnsiConsole.WriteLine($"Agent name : {agent1.Name}");
AnsiConsole.MarkupLine("[grey]Sending [/]: What is 2 + 2?");

var session1 = await agent1.CreateSessionAsync();
var result1 = await agent1.RunAsync(
    SquadDemoConfiguration.Prompt,
    session1);

if (result1 is not null)
    Console.WriteLine($"Response  : {result1.Text}");