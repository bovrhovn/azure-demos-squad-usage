# Squad Team

> azure-demos-squad-usage

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Lead | Architecture & Review | [Charter](agents/lead/charter.md) | Active specialist |
| Backend | .NET & Agents | [Charter](agents/backend/charter.md) | Active specialist |
| Frontend | Web Experience | [Charter](agents/frontend/charter.md) | Active specialist |
| Infra | Azure & Delivery | [Charter](agents/infra/charter.md) | Active specialist |
| Tester | Quality Engineering | [Charter](agents/tester/charter.md) | Active specialist |
| Scribe | Decision Steward | [Charter](agents/scribe/charter.md) | Support — accepted decisions only |
| Ralph | Work Monitor | [Charter](agents/ralph/charter.md) | Support — work queues |
| Rai | Responsible AI Reviewer | [Charter](agents/rai/charter.md) | Support — on demand or selected gate |
| Fact Checker | Verification & Challenge | [Charter](agents/fact-checker/charter.md) | Support — on demand or selected gate |

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** azure-demos-squad-usage
- **Created:** 2026-10-19
- **Requested by:** Bojan Vrhovnik
- **Team confirmed:** 2026-10-19; descriptive universe, five specialists and four fixed support identities.
- **Purpose:** Three focused .NET 10 demos exploring GitHub Copilot SDK and Squad.Agents.AI interactions.
- **Solution:** `src\SquadDemos\SquadDemos.slnx`; console demos `SquadDemos.GHCopilot` and `SquadDemos.SquadHello`, plus Razor Pages `SquadDemos.Web.CopilotDynamicModule`, with xUnit coverage for all three.
- **Web stack:** GitHub OAuth device flow, per-browser Copilot sessions, Cosmos DB chat partitioned by authenticated `/userId`, SignalR progress, skill management, and dynamically compiled/loaded dashboard modules.
- **Trust boundaries:** Generated HTML is sandboxed in an iframe; module assemblies execute with the application identity. Assemblies, generated source, and skills under `wwwroot` are public; deploy only trusted assemblies and review generated source and skills.
- **Delivery:** Azure and container assets under `azure` and `containers`; PowerShell launch scripts under `scripts`.
- **Verified README commands:** `dotnet build src\SquadDemos\SquadDemos.slnx`; `dotnet test src\SquadDemos\SquadDemos.slnx`.
- **Ownership:** One accountable specialist per task; bounded collaboration only when needed. Support identities are not specialist routing destinations. The separate `@copilot` entry and its disabled auto-assignment remain unchanged.
