using Microsoft.AspNetCore.Hosting;
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
