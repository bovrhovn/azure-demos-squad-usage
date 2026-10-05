# Azure Demos: Squad and GitHub Copilot SDK

<p align="center">
  Three focused .NET demos for exploring agent interactions with the
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

The solution contains two console applications and an authenticated Razor web application:

| Demo | What it shows |
| --- | --- |
| [`SquadDemos.GHCopilot`](src/SquadDemos/SquadDemos.GHCopilot) | Creating a streaming `CopilotClient` session, handling assistant-message events, and obtaining the completed response. |
| [`SquadDemos.SquadHello`](src/SquadDemos/SquadDemos.SquadHello) | Hosting a `SquadAgent` with the .NET Generic Host, resolving it from dependency injection, and running a session against a Squad folder. |
| [`SquadDemos.Web.CopilotDynamicModule`](src/SquadDemos/SquadDemos.Web.CopilotDynamicModule) | A GitHub-authenticated Razor Pages app with a Vue chat interface and an ordered dynamic dashboard-module pipeline. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- GitHub Copilot access for the GitHub Copilot SDK demo
- A Squad-configured folder for the Squad demo
- GitHub Copilot access for the web demo

## Build

Build both demos from the repository root:

```powershell
dotnet build src\SquadDemos\SquadDemos.slnx
```

## Run the demos

### Local launch scripts

The numbered scripts in [`scripts`](scripts) run each demo from any working directory and forward application
arguments. Use `-Configuration Release` to select a release build and `-NoBuild` after building the solution.
The [scripts README](scripts/README.md) documents all parameters and test commands.

```powershell
.\scripts\01-ghcopilot.ps1
.\scripts\02-squadhello.ps1 -ProjectDir C:\path\to\squad-folder
.\scripts\03-copilotdynamicmodule.ps1 -ApplicationArguments @('--urls', 'http://localhost:5050')
```

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

### Copilot Dynamic Module web app

The web app uses the GitHub Copilot SDK's GitHub authentication. On first visit, it redirects to the
**Authenticate with GitHub** page, which opens GitHub's device sign-in flow. Complete that flow and return to
the page to continue to chat. No Entra ID app registration or Foundry configuration is required.

```powershell
dotnet run --project src\SquadDemos\SquadDemos.Web.CopilotDynamicModule\SquadDemos.Web.CopilotDynamicModule.csproj
```

The chat page keeps the signed-in user's in-memory sessions and sends messages through minimal APIs to
GitHub Copilot. The **Dashboard** link executes registered `ICopilotModule` instances in ascending `Order`; each
module receives dashboard configuration through `SetConfiguration` and returns model-generated HTML. That HTML
is shown in a sandboxed iframe so it cannot execute in the application origin.

#### Dynamic dashboard modules

The dashboard uses `CopilotModuleLoader` to discover modules from the configured
`wwwroot\modules` folder. It caches the discovered module types in `IMemoryCache`, then creates a new module
instance for every dashboard request so a module's mutable configuration is never shared between users.

- A module assembly can contain one or more public, non-abstract implementations of
  `SquadDemos.Web.CopilotDynamicModule.Features.Dashboard.ICopilotModule`.
- `GET /api/dashboard/modules` runs the cached module list. `POST /api/dashboard/modules/refresh` removes the
  cached registration list and immediately runs the newly discovered list.
- The Dashboard **Refresh** button calls the refresh endpoint. Its module management panel lists deployed DLLs
  and uses a confirmation dialog before deleting one. Deletion invalidates the cache and refreshes the view.
  A `FileSystemWatcher` also invalidates the cache for module DLL creation, updates, renames, and deletion.
- Configure the folder name with `Modules:FolderName`; the default is `modules`, relative to the web root.

Assemblies under `wwwroot` are publicly served and execute with the application's identity when loaded. Only
deploy reviewed assemblies through the trusted deployment pipeline. The [`wwwroot\modules`](src/SquadDemos/SquadDemos.Web.CopilotDynamicModule/wwwroot/modules)
folder contains the deployment guidance.

## Technology references

The project uses .NET console applications and C# top-level statements in both demos. The Squad demo additionally uses the Generic Host and built-in dependency injection to configure and resolve its agent.

| Technology | Learn more |
| --- | --- |
| <a href="https://learn.microsoft.com/dotnet/standard/building-console-apps"><img src="https://img.shields.io/badge/.NET-Console%20apps-512BD4?logo=dotnet&logoColor=white" alt=".NET console apps"></a> | [Console apps in .NET](https://learn.microsoft.com/dotnet/standard/building-console-apps) |
| <a href="https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements"><img src="https://img.shields.io/badge/C%23-Top--level%20statements-239120?logo=csharp&logoColor=white" alt="C# top-level statements"></a> | [Top-level statements](https://learn.microsoft.com/dotnet/csharp/fundamentals/program-structure/top-level-statements) |
| <a href="https://learn.microsoft.com/dotnet/core/extensions/generic-host"><img src="https://img.shields.io/badge/.NET-Generic%20Host-512BD4?logo=dotnet&logoColor=white" alt=".NET Generic Host"></a> | [.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) |
| <a href="https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/usage"><img src="https://img.shields.io/badge/.NET-Dependency%20injection-512BD4?logo=dotnet&logoColor=white" alt=".NET dependency injection"></a> | [Use dependency injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/usage) |
