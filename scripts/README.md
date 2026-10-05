# Local launch scripts

These PowerShell scripts run the three demos from any working directory. Each accepts `-Configuration`
(`Debug` by default), `-NoBuild`, and application arguments after the named parameters.

| Script | Demo | Required parameter |
| --- | --- | --- |
| `01-ghcopilot.ps1` | GitHub Copilot SDK console demo | None |
| `02-squadhello.ps1` | Squad SDK console demo | `-ProjectDir` |
| `03-copilotdynamicmodule.ps1` | Copilot Dynamic Module web app | None |

Run the scripts from the repository root:

```powershell
.\scripts\01-ghcopilot.ps1
.\scripts\02-squadhello.ps1 -ProjectDir C:\path\to\squad-folder
.\scripts\03-copilotdynamicmodule.ps1 -ApplicationArguments @('--urls', 'http://localhost:5050')
```

Use `-NoBuild` after building the solution when you only need to launch an existing build:

```powershell
.\scripts\03-copilotdynamicmodule.ps1 -Configuration Release -NoBuild
```

The Squad launcher validates `ProjectDir`, supplies its resolved path as `PROJECTDIR` only while the
application runs, and then restores the previous environment value. Configure the web application's
`AzureAd` and `Foundry` settings before starting it.

## Tests

The Pester tests validate project selection, `dotnet run` argument forwarding, and the scoped `PROJECTDIR`
behavior without starting the interactive demos:

```powershell
Invoke-Pester .\scripts\tests\DemoLaunchers.Tests.ps1
```
