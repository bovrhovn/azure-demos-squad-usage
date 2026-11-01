# Project Context

- **Owner:** Bojan Vrhovnik
- **Project:** azure-demos-squad-usage — three demos of GitHub Copilot SDK and Squad.Agents.AI.
- **Stack:** .NET 10/C#, two console apps, Razor Pages, GitHub OAuth device flow, Cosmos DB, SignalR, xUnit.
- **Created:** 2026-10-19T12:14:23.642Z

## Learnings

- All three demos have xUnit test projects; Tester owns dedicated quality and regression tasks.
- High-value boundaries include per-user Cosmos partitioning, browser session isolation, per-request module configuration and cache invalidation.
- README full command: `dotnet test src\SquadDemos\SquadDemos.slnx`.
- README targeted web command: `dotnet test src\SquadDemos\SquadDemos.Web.CopilotDynamicModule.Tests\SquadDemos.Web.CopilotDynamicModule.Tests.csproj`.
- Initial team setup is docs/config only; no application build or test execution is required.
