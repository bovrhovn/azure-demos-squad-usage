# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Scope, architecture and triage | Lead | Requirements, cross-domain contracts, issue classification, risk trade-offs |
| Code review and architecture review | Lead | PR review, trust-boundary review, acceptance criteria |
| C# services and console demos | Backend | Generic Host, dependency injection, streaming sessions, minimal APIs |
| Copilot and Squad SDK integration | Backend | CopilotClient, SquadAgent, model selection, skill integration |
| Authentication and data isolation | Backend | GitHub OAuth device flow, browser sessions, Cosmos DB `/userId` partitioning |
| Dynamic module backend | Backend | C# generation/compilation, assembly loading, cache invalidation, trusted module execution |
| Razor Pages and web UI | Frontend | Chat, dashboard, skills screens, paging, module management controls |
| SignalR and accessibility | Frontend | Client progress/status interactions, keyboard navigation, accessible feedback |
| Azure deployment and containers | Infra | `azure` and `containers`, environment configuration, deployment guidance |
| CI/CD and launch tooling | Infra | GitHub Actions, PowerShell scripts under `scripts`, reproducible launch commands |
| Tests and regression analysis | Tester | xUnit for all three demos, integration coverage, isolation and edge cases |
| Product documentation and how-to | Lead | README concepts, architecture explanations, usage walkthroughs |
| Delivery documentation and how-to | Infra | Azure/container setup, launch-script parameters, CI/CD instructions |

Each row names exactly one accountable specialist. For mixed work, choose the owner of
the primary deliverable; Lead resolves ambiguity. Request a bounded contribution only
for a concrete dependency, not speculative downstream work. SignalR server changes
remain Backend work; Frontend owns browser interactions.

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:lead` | Scope, triage or review | Lead |
| `squad:backend` | Service, SDK, authentication or storage work | Backend |
| `squad:frontend` | Razor UI, client interactions or accessibility | Frontend |
| `squad:infra` | Delivery, containers, CI/CD or scripts | Infra |
| `squad:tester` | Test coverage or regression investigation | Tester |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Reassignment replaces the specialist label; retain one accountable owner. Unknown
   or conflicting specialist labels return to Lead for triage.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **One owner first.** Select the specialist for the primary deliverable. Simple facts
   and small bounded work need no fan-out.
2. **Bounded collaboration.** Add help only for a named dependency or independent
   deliverable; "Team, ..." is not permission for eager or speculative fan-out.
3. **Proportionate review.** Follow the active review policy: ordinarily at most one
   selected gate; high-risk work may use a primary and one orthogonal gate.
4. **Support identities are not specialist routes.** Scribe merges accepted durable
   decisions only; Ralph monitors queues; Rai and Fact Checker are on-demand or
   selected bounded reviewers. None is an automatic participant on every task.
5. **No routine logging ceremony.** Do not create session/orchestration logs or summon
   Scribe just because work occurred. Record only accepted durable decisions when needed.
6. **Coding Agent stays separate.** Preserve `@copilot` capabilities in `team.md`;
   `copilot-auto-assign: false` prevents automatic assignment.
7. **Issue-labeled work** routes to its single approved specialist; Lead handles the
   base `squad` inbox and ambiguous requests. Monitoring never authorizes a merge or deployment.
