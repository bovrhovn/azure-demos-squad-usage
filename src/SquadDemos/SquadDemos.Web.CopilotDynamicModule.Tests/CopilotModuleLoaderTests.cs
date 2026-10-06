using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class CopilotModuleLoaderTests : IDisposable
{
    private readonly string webRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetModules_returns_no_built_in_modules_when_the_module_folder_is_empty()
    {
        Directory.CreateDirectory(webRootPath);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);

        Assert.Empty(loader.GetModules());
    }

    [Fact]
    public void Refresh_removes_deleted_modules_from_the_cached_list()
    {
        var modulesPath = Path.Combine(webRootPath, "modules");
        Directory.CreateDirectory(modulesPath);
        var assemblyPath = Path.Combine(modulesPath, "TestModule.dll");
        File.Copy(typeof(DiskModule).Assembly.Location, assemblyPath);

        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICopilotService>(new FakeCopilotService())
            .BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);

        Assert.Contains(loader.GetModules(), module => module.GetType().FullName == typeof(DiskModule).FullName);

        File.Delete(assemblyPath);
        loader.Refresh();

        Assert.DoesNotContain(loader.GetModules(), module => module.GetType().FullName == typeof(DiskModule).FullName);
    }

    [Fact]
    public void DeleteModule_deletes_the_module_file_and_refreshes_the_cache()
    {
        var modulesPath = Path.Combine(webRootPath, "modules");
        Directory.CreateDirectory(modulesPath);
        var assemblyPath = Path.Combine(modulesPath, "TestModule.dll");
        File.Copy(typeof(DiskModule).Assembly.Location, assemblyPath);

        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICopilotService>(new FakeCopilotService())
            .BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);

        Assert.Contains("TestModule.dll", loader.GetModuleFiles());
        Assert.Contains(loader.GetModules(), module => module.GetType().FullName == typeof(DiskModule).FullName);

        Assert.True(loader.DeleteModule("TestModule.dll"));

        Assert.Empty(loader.GetModuleFiles());
        Assert.DoesNotContain(loader.GetModules(), module => module.GetType().FullName == typeof(DiskModule).FullName);
    }

    [Fact]
    public void DeleteModule_rejects_paths_outside_the_module_folder()
    {
        Directory.CreateDirectory(webRootPath);
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<ICopilotService>(new FakeCopilotService())
            .BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);

        Assert.False(loader.DeleteModule(@"..\outside.dll"));
    }

    [Fact]
    public async Task CreateAsync_compiles_and_deploys_the_module_returned_by_Copilot()
    {
        Directory.CreateDirectory(webRootPath);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);
        var service = new CopilotModuleService(
            new GeneratedModuleCopilotService(),
            loader,
            Options.Create(new ModuleLoaderOptions
            {
                FolderName = "modules",
                MaxModuleFileSizeBytes = 1024,
                MaxGenerationPromptLength = 100
            }));

        var moduleFile = await service.CreateAsync("Show a delivery summary.", CancellationToken.None);

        Assert.Contains(moduleFile, loader.GetModuleFiles());
        Assert.Contains(loader.GetModules(), module => module.GetType().Name == "GeneratedModule");
    }

    [Fact]
    public async Task UploadAsync_rejects_assemblies_without_an_ICopilotModule_implementation()
    {
        Directory.CreateDirectory(webRootPath);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var loader = new CopilotModuleLoader(
            new TestWebHostEnvironment(webRootPath),
            cache,
            serviceProvider,
            Options.Create(new ModuleLoaderOptions { FolderName = "modules" }),
            NullLogger<CopilotModuleLoader>.Instance);
        var service = new CopilotModuleService(
            new FakeCopilotService(),
            loader,
            Options.Create(new ModuleLoaderOptions
            {
                FolderName = "modules",
                MaxModuleFileSizeBytes = 1024,
                MaxGenerationPromptLength = 100
            }));
        await using var content = new MemoryStream([0x00]);
        var file = new FormFile(content, 0, content.Length, "moduleFile", "invalid.dll");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAsync(file, CancellationToken.None));

        Assert.Contains("ICopilotModule", exception.Message);
        Assert.Empty(loader.GetModuleFiles());
    }

    public void Dispose()
    {
        if (Directory.Exists(webRootPath))
        {
            try
            {
                Directory.Delete(webRootPath, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                // The file watcher releases its directory handle asynchronously on Windows.
            }
        }
    }

    private sealed class FakeCopilotService : ICopilotService
    {
        public Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotAuthenticationStatus(true, "octocat", "https://github.com/login/device", "/images/default-avatar.svg"));

        public Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CopilotModel>>([]);

        public Task<string> GetResponseAsync(
            IReadOnlyList<ChatMessage> messages,
            string? model,
            CancellationToken cancellationToken) =>
            Task.FromResult("<section>Copilot module</section>");
    }

    private sealed class GeneratedModuleCopilotService : ICopilotService
    {
        public Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotAuthenticationStatus(true, "octocat", "https://github.com/login/device", "/images/default-avatar.svg"));

        public Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CopilotModel>>([]);

        public Task<string> GetResponseAsync(
            IReadOnlyList<ChatMessage> messages,
            string? model,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                """
                using System.Collections.Generic;
                using System.Threading;
                using System.Threading.Tasks;
                using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

                public sealed class GeneratedModule : ICopilotModule
                {
                    public int Order => 100;

                    public Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default) =>
                        Task.FromResult("<section>Generated module</section>");

                    public void SetConfiguration(KeyValuePair<string, string> configuration)
                    {
                    }
                }
                """);
    }

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test application";

        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);

        public string WebRootPath { get; set; } = webRootPath;

        public string EnvironmentName { get; set; } = "Testing";

        public string ContentRootPath { get; set; } = webRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);
    }
}

public sealed class DiskModule : ICopilotModule
{
    public int Order => 200;

    public Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("<section>disk module</section>");

    public void SetConfiguration(KeyValuePair<string, string> configuration)
    {
    }
}
