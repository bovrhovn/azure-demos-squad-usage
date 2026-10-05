using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Pages;

public sealed class ModelSelectorModel(
    ICopilotService copilot,
    IOptions<CopilotOptions> options) : PageModel
{
    private readonly CopilotOptions options = options.Value;

    [BindProperty]
    public string? SelectedModel { get; set; }

    public bool IsAuthenticated { get; private set; }

    public IReadOnlyList<SelectListItem> Models { get; private set; } = [];

    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await PopulateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await PopulateAsync(cancellationToken);
        if (!IsAuthenticated)
        {
            return RedirectToPage("/Authenticate");
        }

        if (!string.IsNullOrEmpty(SelectedModel) && Models.All(model => model.Value != SelectedModel))
        {
            ModelState.AddModelError(nameof(SelectedModel), "Choose one of the available Copilot models.");
            return Page();
        }

        Response.Cookies.Append(
            CopilotOptions.ModelCookieName,
            SelectedModel ?? string.Empty,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(options.ModelCookieDurationDays)
            });
        StatusMessage = "Default model saved.";
        return Page();
    }

    private async Task PopulateAsync(CancellationToken cancellationToken)
    {
        var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
        IsAuthenticated = status.IsAuthenticated;
        SelectedModel ??= Request.Cookies[CopilotOptions.ModelCookieName] ?? options.DefaultModel;
        if (IsAuthenticated)
        {
            Models = (await copilot.GetModelsAsync(cancellationToken))
                .Select(model => new SelectListItem(model.Name, model.Id))
                .ToArray();
        }
    }
}
