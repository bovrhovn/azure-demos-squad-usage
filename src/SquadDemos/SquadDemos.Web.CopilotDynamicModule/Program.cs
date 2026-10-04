using SquadDemos.Web.CopilotDynamicModule.Features.Chat;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;
using SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddCopilotFeature(builder.Configuration);
builder.Services.AddChatFeature(builder.Configuration);
builder.Services.AddDashboardFeature(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapControllers();
app.MapChatFeatureApi();
app.MapDashboardFeatureApi();

app.Run();

public partial class Program;
