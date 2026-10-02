using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Chat;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public Dictionary<string, string> Configuration { get; init; } = [];
}

public sealed record DashboardModuleResponse(int Order, string Html);

public sealed class CopilotModuleRunner(
    ICopilotModuleLoader moduleLoader,
    IOptions<DashboardOptions> options)
{
    public async Task<IReadOnlyList<DashboardModuleResponse>> RunAsync(CancellationToken cancellationToken)
    {
        var modules = moduleLoader.GetModules();

        foreach (var module in modules)
        {
            foreach (var configuration in options.Value.Configuration)
            {
                module.SetConfiguration(configuration);
            }
        }

        var generated = new List<DashboardModuleResponse>();
        foreach (var module in modules.OrderBy(module => module.Order))
        {
            generated.Add(new DashboardModuleResponse(
                module.Order,
                await module.GetGeneratedHtmlAsync(cancellationToken)));
        }

        return generated;
    }
}

public sealed class FoundryDashboardModule(IFoundryChatClient chatClient) : ICopilotModule
{
    private readonly Dictionary<string, string> configuration = [];

    public int Order => 100;

    public void SetConfiguration(KeyValuePair<string, string> configuration) =>
        this.configuration[configuration.Key] = configuration.Value;

    public Task<string> GetGeneratedHtmlAsync(CancellationToken cancellationToken = default)
    {
        var prompt = configuration.GetValueOrDefault("dashboardPrompt")
            ?? throw new InvalidOperationException("Dashboard:Configuration:dashboardPrompt must be configured.");
        var message = new ChatMessage(Guid.NewGuid(), "user", prompt, DateTimeOffset.UtcNow);
        return chatClient.GetResponseAsync([message], cancellationToken);
    }
}

public static class DashboardFeature
{
    public static IServiceCollection AddDashboardFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DashboardOptions>()
            .Bind(configuration.GetSection(DashboardOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<ModuleLoaderOptions>()
            .Bind(configuration.GetSection(ModuleLoaderOptions.SectionName))
            .ValidateOnStart();
        services.AddMemoryCache();
        services.AddSingleton<ICopilotModuleLoader, CopilotModuleLoader>();
        services.AddScoped<CopilotModuleRunner>();
        return services;
    }

    public static void MapDashboardFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/dashboard")
            .RequireAuthorization()
            .WithTags("Dashboard")
            .MapGet("/modules", async Task<IResult> (CopilotModuleRunner runner, CancellationToken cancellationToken) =>
            {
                try
                {
                    return TypedResults.Ok(await runner.RunAsync(cancellationToken));
                }
                catch (InvalidOperationException exception)
                {
                    return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
                catch (FoundryRequestException)
                {
                    return TypedResults.Problem(
                        "The Foundry service could not generate dashboard content.",
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("GetDashboardModules")
            .WithSummary("Runs dashboard modules in their configured order.");

        endpoints.MapPost("/api/dashboard/modules/refresh", (
            ICopilotModuleLoader moduleLoader,
            CopilotModuleRunner runner,
            CancellationToken cancellationToken) =>
        {
            moduleLoader.Refresh();
            return runner.RunAsync(cancellationToken);
        })
        .RequireAuthorization()
        .WithTags("Dashboard")
        .WithName("RefreshDashboardModules")
        .WithSummary("Invalidates the module cache and runs the refreshed dashboard module list.");
    }
}
