# Project Context

- **Owner:** Bojan Vrhovnik
- **Project:** azure-demos-squad-usage — three demos of GitHub Copilot SDK and Squad.Agents.AI.
- **Stack:** .NET 10/C#, two console apps, Razor Pages, GitHub OAuth device flow, Cosmos DB, SignalR, xUnit.
- **Created:** 2026-10-19T12:14:23.642Z

## Learnings

- Frontend owns Razor chat/dashboard/skills UI, browser SignalR feedback and accessibility.
- README describes module creation progress over SignalR and generated HTML shown in a sandboxed iframe.
- Skills paging defaults to five items; `Paging__PageSize` configures paged web surfaces.
- Backend owns tokens, storage and server contracts; request only concrete dependencies.
- Solution validation: `dotnet build src\SquadDemos\SquadDemos.slnx`; `dotnet test src\SquadDemos\SquadDemos.slnx`.
