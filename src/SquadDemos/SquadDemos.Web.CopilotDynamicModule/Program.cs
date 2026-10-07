using Microsoft.AspNetCore.Authentication.Cookies;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;
using SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Authenticate";
        options.Cookie.Name = "__Host-SquadDemos.GitHub";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddRazorPages();
builder.Services.AddCopilotFeature(builder.Configuration);
builder.Services.AddChatFeature(builder.Configuration);
builder.Services.AddDashboardFeature(builder.Configuration);
builder.Services.AddPagingFeature(builder.Configuration);
builder.Services.AddReverseProxySupport(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseGitHubAuthentication();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok()).AllowAnonymous();
app.MapRazorPages();
app.MapControllers();
app.MapCopilotFeatureApi();
app.MapChatFeatureApi();
app.MapDashboardFeatureApi();
app.MapSkillsFeatureApi();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<DashboardModuleGenerationHub>("/hubs/dashboard");

app.Run();

public partial class Program;
