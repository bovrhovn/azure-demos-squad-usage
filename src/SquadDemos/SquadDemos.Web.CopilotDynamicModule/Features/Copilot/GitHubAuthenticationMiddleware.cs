using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

public sealed class GitHubAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICopilotService copilot)
    {
        var isRazorPage = context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>() is not null;
        if (!isRazorPage || context.Request.Path.StartsWithSegments("/Authenticate"))
        {
            await next(context);
            return;
        }

        var status = await copilot.GetAuthenticationStatusAsync(context.RequestAborted);
        if (!status.IsAuthenticated)
        {
            context.Response.Redirect("/Authenticate");
            return;
        }

        await next(context);
    }
}

public static class GitHubAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseGitHubAuthentication(this IApplicationBuilder app) =>
        app.UseMiddleware<GitHubAuthenticationMiddleware>();
}
