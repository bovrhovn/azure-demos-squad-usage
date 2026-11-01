# Project Context

- **Owner:** Bojan Vrhovnik
- **Project:** azure-demos-squad-usage — three demos of GitHub Copilot SDK and Squad.Agents.AI.
- **Stack:** .NET 10/C#, two console apps, Razor Pages, GitHub OAuth device flow, Cosmos DB, SignalR, xUnit.
- **Created:** 2026-10-19T12:14:23.642Z

## Learnings

- README identifies `SquadDemos.GHCopilot`, `SquadDemos.SquadHello` and `SquadDemos.Web.CopilotDynamicModule`, each with tests.
- Backend owns services/SDKs, device-flow authentication, Cosmos `/userId` isolation and dynamic module compilation/loading.
- Module types are cached but module instances are per request; DLL changes invalidate discovery.
- `wwwroot` assemblies, generated source and skills are public. Iframe sandboxing is not assembly isolation.
- Solution validation: `dotnet build src\SquadDemos\SquadDemos.slnx`; `dotnet test src\SquadDemos\SquadDemos.slnx`.
