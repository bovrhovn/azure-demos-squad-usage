# Project Context

- **Owner:** Bojan Vrhovnik
- **Project:** azure-demos-squad-usage — three demos of GitHub Copilot SDK and Squad.Agents.AI.
- **Stack:** .NET 10/C#, two console apps, Razor Pages, GitHub OAuth device flow, Cosmos DB, SignalR, xUnit.
- **Created:** 2026-10-19T12:14:23.642Z

## Learnings

- Infra owns Azure/container assets, GitHub Actions, PowerShell launch scripts and delivery how-to.
- Delivery directories are `azure`, `containers` and `scripts`; README scripts accept configuration, no-build and application arguments.
- Device flow requires the public OAuth client ID, not an application client secret; Cosmos configuration must not expose credentials.
- Module assemblies execute with application identity and must be trusted before deployment.
- README validation: `dotnet build src\SquadDemos\SquadDemos.slnx`; `dotnet test src\SquadDemos\SquadDemos.slnx`.
