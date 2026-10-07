using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class ProfilePageRouteTests
{
    [Fact]
    public async Task Profile_pages_are_available_at_their_new_routes()
    {
        using var factory = new ProfilePageRouteFactory();
        using var client = factory.CreateClient();

        var profilePageResponses = await Task.WhenAll(
            client.GetAsync("/Profile/Dashboard"),
            client.GetAsync("/Profile/ModelSelector"),
            client.GetAsync("/Profile/SkillsUploader"));

        Assert.All(profilePageResponses, response => response.EnsureSuccessStatusCode());

        var skillsPage = await profilePageResponses[2].Content.ReadAsStringAsync();
        Assert.DoesNotContain("_ValidationScriptsPartial", skillsPage, StringComparison.Ordinal);
        Assert.Contains("required", skillsPage, StringComparison.Ordinal);
        Assert.Contains("/js/skills-uploader.js", skillsPage, StringComparison.Ordinal);
        Assert.Contains("LOCAL LIBRARY", skillsPage, StringComparison.Ordinal);
        Assert.True(
            skillsPage.IndexOf("Available skills", StringComparison.Ordinal)
            < skillsPage.IndexOf("Find skills from Awesome Copilot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Skills_api_returns_a_paged_uploaded_skill_result()
    {
        using var factory = new ProfilePageRouteFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/skills/uploaded?page=1");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"items\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"totalCount\"", payload, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProfilePageRouteFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("GitHubDeviceFlow:ClientId", "test-client-id");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ICopilotService>();
                services.AddSingleton<ICopilotService, TestCopilotService>();
            });
        }
    }

    private sealed class TestCopilotService : ICopilotService
    {
        public Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotAuthenticationStatus(
                true,
                "test-user",
                "https://github.com/login/device",
                "/images/default-avatar.svg"));

        public Task<IReadOnlyList<CopilotModel>> GetModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CopilotModel>>([]);

        public Task<string> GetResponseAsync(
            IReadOnlyList<ChatMessage> messages,
            string? model,
            CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
