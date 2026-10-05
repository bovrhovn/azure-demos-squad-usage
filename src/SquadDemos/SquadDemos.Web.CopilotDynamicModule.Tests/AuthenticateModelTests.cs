using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;
using SquadDemos.Web.CopilotDynamicModule.Pages;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class AuthenticateModelTests
{
    [Fact]
    public async Task OnGetAsync_returns_the_authentication_page_when_GitHub_is_not_authenticated()
    {
        var model = new AuthenticateModel(new FakeCopilotService(false));

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("https://github.com/login/device", model.AuthenticationUrl);
    }

    [Fact]
    public async Task OnGetAsync_redirects_authenticated_users_to_chat()
    {
        var model = new AuthenticateModel(new FakeCopilotService(true));

        var result = await model.OnGetAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    private sealed class FakeCopilotService(bool isAuthenticated) : ICopilotService
    {
        public Task<CopilotAuthenticationStatus> GetAuthenticationStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotAuthenticationStatus(
                isAuthenticated,
                isAuthenticated ? "octocat" : null,
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
