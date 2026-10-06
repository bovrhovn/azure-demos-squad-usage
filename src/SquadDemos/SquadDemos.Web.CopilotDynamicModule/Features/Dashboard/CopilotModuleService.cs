using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public sealed record CreateDashboardModuleRequest(string Prompt);

public interface ICopilotModuleService
{
    Task<string> CreateAsync(string login, string prompt, CancellationToken cancellationToken);

    Task<string> UploadAsync(IFormFile moduleFile, CancellationToken cancellationToken);
}

public sealed class CopilotModuleService(
    ICopilotService copilotService,
    ICopilotModuleLoader moduleLoader,
    IDashboardModuleGenerationNotifier generationNotifier,
    IOptions<ModuleLoaderOptions> options) : ICopilotModuleService
{
    private readonly ModuleLoaderOptions options = options.Value;

    public async Task<string> CreateAsync(string login, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            await NotifyAsync(login, "Validating", "Validating the module request.", cancellationToken);
            ValidatePrompt(prompt);

            await NotifyAsync(login, "Generating", "Asking Copilot to generate the module source.", cancellationToken);
            var source = await copilotService.GetResponseAsync(
                [new ChatMessage(Guid.NewGuid(), "user", CreateModulePrompt(prompt), DateTimeOffset.UtcNow)],
                null,
                cancellationToken);

            await NotifyAsync(login, "Compiling", "Compiling the generated C# source.", cancellationToken);
            var assembly = Compile(AddRequiredUsings(source));
            var fileName = $"dashboard-module-{Guid.NewGuid():N}.dll";

            await NotifyAsync(login, "Deploying", "Validating and deploying the module assembly.", cancellationToken);
            await using var stream = new MemoryStream(assembly);
            var savedModule = await moduleLoader.SaveModuleAsync(stream, fileName, cancellationToken);
            await NotifyAsync(
                login,
                "Complete",
                $"Module {savedModule} was created and deployed.",
                cancellationToken,
                isComplete: true);
            return savedModule;
        }
        catch (Exception)
        {
            await NotifyAsync(
                login,
                "Failed",
                "Module creation did not complete. Review the error message for details.",
                CancellationToken.None,
                isComplete: true,
                isError: true);
            throw;
        }
    }

    public async Task<string> UploadAsync(IFormFile moduleFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moduleFile);
        if (moduleFile.Length <= 0)
        {
            throw new ArgumentException("A non-empty module DLL is required.", nameof(moduleFile));
        }

        if (moduleFile.Length > options.MaxModuleFileSizeBytes)
        {
            throw new ArgumentException(
                $"Module assemblies cannot exceed {options.MaxModuleFileSizeBytes} bytes.",
                nameof(moduleFile));
        }

        var fileName = Path.GetFileName(moduleFile.FileName);
        if (!string.Equals(Path.GetExtension(fileName), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Upload a DLL file.", nameof(moduleFile));
        }

        await using var stream = moduleFile.OpenReadStream();
        return await moduleLoader.SaveModuleAsync(stream, fileName, cancellationToken);
    }

    private void ValidatePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Describe the dashboard module to create.", nameof(prompt));
        }

        if (prompt.Length > options.MaxGenerationPromptLength)
        {
            throw new ArgumentException(
                $"Module requests cannot exceed {options.MaxGenerationPromptLength} characters.",
                nameof(prompt));
        }
    }

    private static byte[] Compile(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            $"DashboardModule_{Guid.NewGuid():N}",
            [syntaxTree],
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new ArgumentException($"Copilot generated a module that could not be compiled:{Environment.NewLine}{errors}");
        }

        return output.ToArray();
    }

    private static IEnumerable<MetadataReference> GetReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string
            ?? throw new InvalidOperationException("The runtime did not provide trusted platform assembly references.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(ICopilotModule).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
    }

    private Task NotifyAsync(
        string login,
        string stage,
        string message,
        CancellationToken cancellationToken,
        bool isComplete = false,
        bool isError = false) =>
        generationNotifier.NotifyAsync(
            login,
            new DashboardModuleGenerationProgress(stage, message, isComplete, isError),
            cancellationToken);

    private static string AddRequiredUsings(string source) =>
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """ + source;

    private static string CreateModulePrompt(string prompt) =>
        $$"""
          Create exactly one public C# class that implements
          SquadDemos.Web.CopilotDynamicModule.Features.Dashboard.ICopilotModule.

          Start the source with these using directives:
          using System;
          using System.Collections.Generic;
          using System.Threading;
          using System.Threading.Tasks;

          Return only compilable C# source. Do not use Markdown fences, package references, file access,
          networking, reflection, process execution, or dependency injection. The class must implement all
          interface members, including GetGeneratedHtmlAsync(CancellationToken) and
          SetConfiguration(KeyValuePair<string, string>). Return a complete HTML fragment from
          GetGeneratedHtmlAsync, and use only .NET APIs available from the shared runtime.

          Dashboard request:
          {{prompt}}
          """;
}
