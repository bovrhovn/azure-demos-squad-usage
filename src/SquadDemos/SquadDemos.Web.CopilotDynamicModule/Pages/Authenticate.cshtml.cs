using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Pages;

public sealed class AuthenticateModel(ICopilotService copilot) : PageModel
{
    public string AuthenticationUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
        if (status.IsAuthenticated)
        {
            return RedirectToPage("/Index");
        }

        AuthenticationUrl = status.AuthenticationUrl;
        return Page();
    }
}
