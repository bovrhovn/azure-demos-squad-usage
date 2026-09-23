# Azure Demos: Squad and GitHub Copilot SDK

<p align="center">
  Two focused .NET console demos for exploring agent interactions with the
  <a href="https://www.nuget.org/packages/GitHub.Copilot.SDK">GitHub Copilot SDK</a>
  and <a href="https://www.nuget.org/packages/Squad.Agents.AI">Squad.Agents.AI</a>.
</p>

<p align="center">
  <a href="https://learn.microsoft.com/dotnet/standard/building-console-apps"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 10"></a>
  <a href="https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements"><img src="https://img.shields.io/badge/C%23-Top--level%20programs-239120?logo=csharp&logoColor=white" alt="C# top-level programs"></a>
  <a href="https://github.com/features/copilot"><img src="https://img.shields.io/badge/GitHub-Copilot-181717?logo=github&logoColor=white" alt="GitHub Copilot"></a>
  <a href="https://www.nuget.org/packages/Spectre.Console"><img src="https://img.shields.io/badge/Console-Spectre.Console-7B2CBF" alt="Spectre.Console"></a>
</p>

## Overview

The solution contains two independent, interactive C# console applications:

| Demo | What it shows |
| --- | --- |
| [`SquadDemos.GHCopilot`](src/SquadDemos/SquadDemos.GHCopilot) | Creating a streaming `CopilotClient` session, handling assistant-message events, and obtaining the completed response. |
| [`SquadDemos.SquadHello`](src/SquadDemos/SquadDemos.SquadHello) | Hosting a `SquadAgent` with the .NET Generic Host, resolving it from dependency injection, and running a session against a Squad folder. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- GitHub Copilot access for the GitHub Copilot SDK demo
- A Squad-configured folder for the Squad demo

## Build

Build both demos from the repository root:

```powershell
dotnet build src\SquadDemos\SquadDemos.slnx
```

## Run the demos

### GitHub Copilot SDK

The demo prompts for a question, creates a streaming Copilot session, prints received assistant messages, and then prints the final response.

```powershell
dotnet run --project src\SquadDemos\SquadDemos.GHCopilot\SquadDemos.GHCopilot.csproj
```

### Squad SDK

Set `PROJECTDIR` to the root of the Squad folder to use. The demo passes that path to `AddSquadAgent`, creates a session, and sends a simple prompt.

```powershell
$env:PROJECTDIR = 'C:\path\to\squad-folder'
dotnet run --project src\SquadDemos\SquadDemos.SquadHello\SquadDemos.SquadHello.csproj
```

## Technology references

The project uses .NET console applications and C# top-level statements in both demos. The Squad demo additionally uses the Generic Host and built-in dependency injection to configure and resolve its agent.

| Technology | Learn more |
| --- | --- |
| <a href="https://learn.microsoft.com/dotnet/standard/building-console-apps"><img src="https://img.shields.io/badge/.NET-Console%20apps-512BD4?logo=dotnet&logoColor=white" alt=".NET console apps"></a> | [Console apps in .NET](https://learn.microsoft.com/dotnet/standard/building-console-apps) |
| <a href="https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements"><img src="https://img.shields.io/badge/C%23-Top--level%20statements-239120?logo=csharp&logoColor=white" alt="C# top-level statements"></a> | [Top-level statements](https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements) |
| <a href="https://learn.microsoft.com/dotnet/core/extensions/generic-host"><img src="https://img.shields.io/badge/.NET-Generic%20Host-512BD4?logo=dotnet&logoColor=white" alt=".NET Generic Host"></a> | [.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) |
| <a href="https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/usage"><img src="https://img.shields.io/badge/.NET-Dependency%20injection-512BD4?logo=dotnet&logoColor=white" alt=".NET dependency injection"></a> | [Use dependency injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/usage) |
