using Microsoft.AspNetCore.HttpOverrides;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;
using SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddCopilotFeature(builder.Configuration);
builder.Services.AddChatFeature(builder.Configuration);
builder.Services.AddDashboardFeature(builder.Configuration);
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
app.UseGitHubAuthentication();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();
app.MapChatFeatureApi();
app.MapDashboardFeatureApi();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program;
