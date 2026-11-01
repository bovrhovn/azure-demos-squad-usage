# Project Context

- **Owner:** Bojan Vrhovnik
- **Project:** azure-demos-squad-usage — three demos of GitHub Copilot SDK and Squad.Agents.AI.
- **Stack:** .NET 10/C#, two console apps, Razor Pages, GitHub OAuth device flow, Cosmos DB, SignalR, xUnit.
- **Created:** 2026-10-19T12:14:23.642Z

## Learnings

- README identifies `src\SquadDemos\SquadDemos.slnx`; build/test use `dotnet build` and `dotnet test` against that solution.
- Five descriptive specialists own product domains; four fixed support identities are not specialist routes. `@copilot` auto-assignment is disabled.
- Lead owns scope, architecture, triage, reviews and product how-to documentation.
- Generated HTML is iframe-sandboxed; loaded module assemblies still execute with application identity.
