# Azure Demos squad usage

## Build and run

- Build the complete solution:
  ```powershell
  dotnet build src\SquadDemos\SquadDemos.slnx
  ```
- Run the GitHub Copilot SDK demo:
  ```powershell
  dotnet run --project src\SquadDemos\SquadDemos.GHCopilot\SquadDemos.GHCopilot.csproj
  ```
- Run the Squad SDK demo. `PROJECTDIR` must identify the folder that contains the Squad configuration/state the agent should use:
  ```powershell
  $env:PROJECTDIR = 'C:\path\to\squad-folder'
  dotnet run --project src\SquadDemos\SquadDemos.SquadHello\SquadDemos.SquadHello.csproj
  ```

## Architecture

The solution at `src\SquadDemos\SquadDemos.slnx` contains two independent .NET 10 top-level console demos:

- `SquadDemos.GHCopilot` directly uses `GitHub.Copilot.SDK`. It creates a streaming `CopilotClient` session, approves permission requests through `PermissionHandler.ApproveAll`, prints assistant-message events, then displays the result returned by `SendAndWaitAsync`.
- `SquadDemos.SquadHello` hosts a `SquadAgent` through `Microsoft.Extensions.Hosting` and `AddSquadAgent`. It obtains the Squad folder from the required `PROJECTDIR` environment variable, creates a session, and sends a fixed prompt through the agent. The project is explicitly configured for the `win-x64` runtime.

## Repository conventions

- Keep these as focused, runnable SDK samples rather than adding application layers or shared abstractions without a demo need.
- Use `Spectre.Console` for user-facing interactive prompts and formatted output, matching the existing console color/markup style.
- Treat `PROJECTDIR` as required input for the Squad demo. Fail immediately when it is absent rather than introducing a fallback location.
- Preserve the current Copilot session lifecycle: configure event handlers for streamed messages, retain the disposable subscription, and dispose it after `SendAndWaitAsync`.
- Runtime Squad state under `.squad/` is local and ignored; do not add generated logs, sessions, caches, or decision inbox content to source control.
