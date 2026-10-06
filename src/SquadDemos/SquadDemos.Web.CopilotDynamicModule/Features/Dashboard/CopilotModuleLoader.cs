using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public sealed class ModuleLoaderOptions
{
    public const string SectionName = "Modules";

    public string FolderName { get; init; } = string.Empty;

    public long MaxModuleFileSizeBytes { get; init; }

    public int MaxGenerationPromptLength { get; init; }
}

public interface ICopilotModuleLoader
{
    IReadOnlyList<ICopilotModule> GetModules();

    IReadOnlyList<string> GetModuleFiles();

    bool DeleteModule(string moduleFileName);

    Task<string> SaveModuleAsync(
        Stream assemblyStream,
        string moduleFileName,
        CancellationToken cancellationToken);

    void Refresh();
}

public sealed class CopilotModuleLoader : ICopilotModuleLoader, IDisposable
{
    private const string ModuleTypesCacheKey = "copilot-module-types";

    private readonly FileSystemWatcher watcher;

    public CopilotModuleLoader(
        IWebHostEnvironment environment,
        IMemoryCache cache,
        IServiceProvider serviceProvider,
        IOptions<ModuleLoaderOptions> options,
        ILogger<CopilotModuleLoader> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment.WebRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.FolderName);

        Cache = cache;
        ServiceProvider = serviceProvider;
        Logger = logger;
        ModuleFolderPath = GetModuleFolderPath(environment.WebRootPath, options.Value.FolderName);
        Directory.CreateDirectory(ModuleFolderPath);

        watcher = new FileSystemWatcher(ModuleFolderPath, "*.dll")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnModuleFileChanged;
        watcher.Created += OnModuleFileChanged;
        watcher.Deleted += OnModuleFileChanged;
        watcher.Renamed += OnModuleFileChanged;
    }

    private IMemoryCache Cache { get; }

    private IServiceProvider ServiceProvider { get; }

    private ILogger<CopilotModuleLoader> Logger { get; }

    private string ModuleFolderPath { get; }

    public IReadOnlyList<ICopilotModule> GetModules()
    {
        var cachedModules = Cache.GetOrCreate(
            ModuleTypesCacheKey,
            entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(30));
                var modules = DiscoverModuleTypes();
                entry.RegisterPostEvictionCallback(static (_, value, _, _) =>
                    (value as CachedModuleTypes)?.Dispose());
                return modules;
            })!;

        return cachedModules.Types
            .Select(type => (ICopilotModule)ActivatorUtilities.CreateInstance(ServiceProvider, type))
            .ToArray();
    }

    public void Refresh()
    {
        Cache.Remove(ModuleTypesCacheKey);
        Logger.LogInformation("Copilot module cache invalidated.");
    }

    public IReadOnlyList<string> GetModuleFiles() =>
        Directory.EnumerateFiles(ModuleFolderPath, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool DeleteModule(string moduleFileName)
    {
        if (string.IsNullOrWhiteSpace(moduleFileName)
            || !moduleFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(moduleFileName), moduleFileName, StringComparison.Ordinal))
        {
            return false;
        }

        var assemblyPath = Path.Combine(ModuleFolderPath, moduleFileName);
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        File.Delete(assemblyPath);
        Refresh();
        Logger.LogInformation("Deleted Copilot module {ModuleFileName}.", moduleFileName);
        return true;
    }

    public async Task<string> SaveModuleAsync(
        Stream assemblyStream,
        string moduleFileName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assemblyStream);

        if (string.IsNullOrWhiteSpace(moduleFileName)
            || !moduleFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(moduleFileName), moduleFileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Upload a DLL with a file name only.", nameof(moduleFileName));
        }

        var destinationPath = Path.Combine(ModuleFolderPath, moduleFileName);
        if (File.Exists(destinationPath))
        {
            throw new ArgumentException("A module with that file name already exists.", nameof(moduleFileName));
        }

        var temporaryPath = Path.Combine(ModuleFolderPath, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var destination = File.Create(temporaryPath))
            {
                await assemblyStream.CopyToAsync(destination, cancellationToken);
            }

            if (!ContainsCopilotModule(temporaryPath))
            {
                throw new ArgumentException(
                    "The assembly must contain a public, non-abstract implementation of ICopilotModule.",
                    nameof(assemblyStream));
            }

            File.Move(temporaryPath, destinationPath);
            Refresh();
            Logger.LogInformation("Saved Copilot module {ModuleFileName}.", moduleFileName);
            return moduleFileName;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void Dispose()
    {
        watcher.Dispose();
    }

    private CachedModuleTypes DiscoverModuleTypes()
    {
        var moduleTypes = new List<Type>();
        var loadContexts = new List<ModuleLoadContext>();

        foreach (var assemblyPath in Directory.EnumerateFiles(ModuleFolderPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var loadContext = new ModuleLoadContext(assemblyPath);
            try
            {
                var assembly = LoadPluginAssembly(loadContext, assemblyPath);
                moduleTypes.AddRange(GetModuleTypes(assembly));
                loadContexts.Add(loadContext);
            }
            catch (BadImageFormatException exception)
            {
                loadContext.Unload();
                Logger.LogWarning(exception, "Skipping invalid Copilot module assembly {AssemblyPath}.", assemblyPath);
            }
            catch (FileLoadException exception)
            {
                loadContext.Unload();
                Logger.LogWarning(exception, "Skipping unloadable Copilot module assembly {AssemblyPath}.", assemblyPath);
            }
        }

        return new CachedModuleTypes(
            moduleTypes
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray(),
            loadContexts);
    }

    private bool ContainsCopilotModule(string assemblyPath)
    {
        var loadContext = new ModuleLoadContext(assemblyPath);
        try
        {
            return GetModuleTypes(LoadPluginAssembly(loadContext, assemblyPath)).Any();
        }
        catch (BadImageFormatException exception)
        {
            Logger.LogWarning(exception, "Rejected invalid Copilot module assembly {AssemblyPath}.", assemblyPath);
            return false;
        }
        catch (FileLoadException exception)
        {
            Logger.LogWarning(exception, "Rejected unloadable Copilot module assembly {AssemblyPath}.", assemblyPath);
            return false;
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private IEnumerable<Type> GetModuleTypes(Assembly assembly)
    {
        try
        {
            return assembly.DefinedTypes
                .Where(type => type is { IsAbstract: false, IsPublic: true }
                    && typeof(ICopilotModule).IsAssignableFrom(type))
                .Select(type => type.AsType())
                .ToArray();
        }

        catch (ReflectionTypeLoadException exception)
        {
            Logger.LogWarning(exception, "Skipping types that could not be loaded from {AssemblyName}.", assembly.FullName);
            return exception.Types
                .OfType<Type>()
                .Where(type => type is { IsAbstract: false, IsPublic: true }
                    && typeof(ICopilotModule).IsAssignableFrom(type))
                .ToArray();
        }
    }

    private static Assembly LoadPluginAssembly(ModuleLoadContext loadContext, string assemblyPath)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(assemblyPath));
        return loadContext.LoadFromStream(stream);
    }

    private static string GetModuleFolderPath(string webRootPath, string folderName)
    {
        var webRoot = Path.GetFullPath(webRootPath);
        var moduleFolder = Path.GetFullPath(Path.Combine(webRoot, folderName));

        if (!moduleFolder.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Modules:FolderName must resolve beneath the web root.");
        }

        return moduleFolder;
    }

    private void OnModuleFileChanged(object sender, FileSystemEventArgs eventArgs)
    {
        Logger.LogInformation("Copilot module folder changed: {ChangeType} {Name}.", eventArgs.ChangeType, eventArgs.Name);
        Refresh();
    }

    private sealed class CachedModuleTypes(
        IReadOnlyList<Type> types,
        IReadOnlyList<ModuleLoadContext> loadContexts) : IDisposable
    {
        public IReadOnlyList<Type> Types { get; } = types;

        public void Dispose()
        {
            foreach (var loadContext in loadContexts)
            {
                loadContext.Unload();
            }
        }
    }

    private sealed class ModuleLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver dependencyResolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var defaultAssembly = Default.Assemblies.FirstOrDefault(assembly =>
                AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));

            if (defaultAssembly is not null)
            {
                return defaultAssembly;
            }

            var dependencyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);
            return dependencyPath is null ? null : LoadFromAssemblyPath(dependencyPath);
        }
    }
}
