using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class SkillStoreTests : IDisposable
{
    private readonly string webRootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_stores_a_markdown_skill_in_the_configured_skills_folder()
    {
        Directory.CreateDirectory(webRootPath);
        var store = CreateStore();
        await using var content = new MemoryStream("Use concise responses."u8.ToArray());
        var file = new FormFile(content, 0, content.Length, "skillFile", "concise-responses.md");

        var result = await store.SaveAsync(file, CancellationToken.None);

        Assert.Equal("concise-responses", result.Name);
        Assert.Equal(
            "Use concise responses.",
            await File.ReadAllTextAsync(Path.Combine(webRootPath, "skills", "concise-responses", "SKILL.md")));
        Assert.Collection(store.GetSkills(), skill => Assert.Equal("concise-responses", skill.Name));
    }

    [Fact]
    public async Task SaveAsync_rejects_an_invalid_skill_filename()
    {
        Directory.CreateDirectory(webRootPath);
        var store = CreateStore();
        await using var content = new MemoryStream("content"u8.ToArray());
        var file = new FormFile(content, 0, content.Length, "skillFile", "invalid skill!.md");

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(file, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(webRootPath))
        {
            Directory.Delete(webRootPath, recursive: true);
        }
    }

    private SkillStore CreateStore() =>
        new(
            new TestWebHostEnvironment(webRootPath),
            Options.Create(new CopilotOptions
            {
                SkillsFolderName = "skills",
                MaxSkillFileSizeBytes = 1024
            }));

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
