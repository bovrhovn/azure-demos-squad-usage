# Azure Demos: Squad and GitHub Copilot SDK

## Build, test, and run

- Build the complete .NET 10 solution:
  ```powershell
  dotnet build src\SquadDemos\SquadDemos.slnx
  ```
- Run all tests:
  ```powershell
  dotnet test src\SquadDemos\SquadDemos.slnx
  ```
- Run one xUnit test by fully qualified name:
  ```powershell
  dotnet test src\SquadDemos\SquadDemos.Web.CopilotDynamicModule.Tests\SquadDemos.Web.CopilotDynamicModule.Tests.csproj --filter "FullyQualifiedName~ChatAndModuleTests.SendMessageAsync_creates_a_session_and_keeps_conversation_history"
  ```
- No standalone lint or formatting command is configured.
- Run the GitHub Copilot SDK demo:
  ```powershell
  dotnet run --project src\SquadDemos\SquadDemos.GHCopilot\SquadDemos.GHCopilot.csproj
  ```
- Run the Squad SDK demo. `PROJECTDIR` must identify the folder that contains the Squad configuration/state the agent should use:
  ```powershell
  $env:PROJECTDIR = 'C:\path\to\squad-folder'
  dotnet run --project src\SquadDemos\SquadDemos.SquadHello\SquadDemos.SquadHello.csproj
  ```
- Run the web demo after configuring its `AzureAd` and `Foundry` settings through user secrets or environment variables:
  ```powershell
  dotnet run --project src\SquadDemos\SquadDemos.Web.CopilotDynamicModule\SquadDemos.Web.CopilotDynamicModule.csproj
  ```

## Architecture

The solution at `src\SquadDemos\SquadDemos.slnx` contains three independent .NET 10 samples and one xUnit test project:

- `SquadDemos.GHCopilot` directly uses `GitHub.Copilot.SDK`. It creates a streaming `CopilotClient` session, approves permission requests through `PermissionHandler.ApproveAll`, prints assistant-message events, then displays the result returned by `SendAndWaitAsync`.
- `SquadDemos.SquadHello` hosts a `SquadAgent` through `Microsoft.Extensions.Hosting` and `AddSquadAgent`. It obtains the Squad folder from the required `PROJECTDIR` environment variable, creates a session, and sends a fixed prompt through the agent. The project is explicitly configured for the `win-x64` runtime.
- `SquadDemos.Web.CopilotDynamicModule` is a Razor Pages host with independently registered `Copilot`, `Chat`, and `Dashboard` features. Razor Pages provide the UI; each feature maps its own minimal API endpoints from `Program.cs`.
- The web app's `GitHubCopilotService` owns all Copilot SDK client/session creation. Chat sessions are in-memory and partitioned by the authenticated GitHub login. The selected model is kept in the `copilot-default-model` HTTP-only cookie.
- Dashboard modules implement `ICopilotModule`. `CopilotModuleLoader` discovers public implementations from DLLs in the configured `wwwroot\modules` folder, caches their types, and creates new instances for every request. Modules run in ascending `Order`; their generated HTML is displayed through sandboxed iframes.
- Uploaded skill Markdown is stored as `wwwroot\<configured-skills-folder>\<skill-name>\SKILL.md`, then supplied to Copilot as its working directory and skill directory.

## Repository conventions

- Keep these as focused, runnable SDK samples rather than adding application layers or shared abstractions without a demo need.
- Use `Spectre.Console` for user-facing interactive prompts and formatted output, matching the existing console color/markup style.
- Treat `PROJECTDIR` as required input for the Squad demo. Fail immediately when it is absent rather than introducing a fallback location.
- Preserve the current Copilot session lifecycle: configure event handlers for streamed messages, retain the disposable subscription, and dispose it after `SendAndWaitAsync`.
- Keep web feature registration and endpoint mapping together in the corresponding `Add*Feature` and `Map*FeatureApi` extension methods. Add feature-facing contracts next to their feature service.
- Validate configuration when registering web feature options with `ValidateOnStart`; do not embed credentials, model choices, deployment names, or tenant values in source. The web demo expects values from configuration, user secrets, or environment variables.
- Preserve the Copilot error boundary: translate `CopilotRequestException` to the API's existing 502 response and configuration/empty-response `InvalidOperationException` to 503 rather than swallowing failures.
- For dashboard plugins, cache module *types* rather than module instances. Plugin instances can retain per-request configuration and must not be shared across users; keep the collectible load-context cleanup coupled to cache eviction.
- Retain the path-containment checks in `SkillStore` and `CopilotModuleLoader`: configured skills and modules folders must resolve underneath `wwwroot`.
- Keep uploaded skills and deployable dashboard assemblies reviewed. Assemblies under `wwwroot` are publicly served and execute with the application identity when loaded.
- Runtime Squad state under `.squad/` is local and ignored; do not add generated logs, sessions, caches, or decision inbox content to source control.
