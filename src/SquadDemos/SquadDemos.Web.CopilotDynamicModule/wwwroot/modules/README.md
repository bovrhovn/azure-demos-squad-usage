# Dashboard modules

Deploy trusted .NET assemblies to this folder to extend the dashboard. Every public, non-abstract type
that implements `SquadDemos.Web.CopilotDynamicModule.Features.Dashboard.ICopilotModule` is discovered.

The application detects DLL additions, updates, renames, and deletions, then invalidates the cached
module registration list. Use **Refresh** on the dashboard to invalidate and reload immediately.

The dashboard can also ask Copilot to create a module from a short request. The generated C# source is
compiled, checked for a public, non-abstract `ICopilotModule` implementation, and then deployed here.
The **Upload module** action performs the same contract check before copying a DLL to this folder.

Assemblies in `wwwroot` are publicly served and execute with the application's identity when loaded.
Only deploy reviewed assemblies through the trusted deployment pipeline.
