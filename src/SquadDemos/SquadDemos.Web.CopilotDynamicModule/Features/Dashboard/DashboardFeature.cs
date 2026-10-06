using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.HttpResults;
using SquadDemos.Web.CopilotDynamicModule.Features.Copilot;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Dashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    public Dictionary<string, string> Configuration { get; init; } = [];
}

public sealed record DashboardModuleResponse(int Order, string Name, string Html);

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
                module.GetType().Name,
                await module.GetGeneratedHtmlAsync(cancellationToken)));
        }

        return generated;
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
            .Validate(
                options => options.MaxModuleFileSizeBytes > 0,
                "Modules:MaxModuleFileSizeBytes must be greater than zero.")
            .Validate(
                options => options.MaxGenerationPromptLength > 0,
                "Modules:MaxGenerationPromptLength must be greater than zero.")
            .ValidateOnStart();
        services.AddMemoryCache();
        services.AddSingleton<ICopilotModuleLoader, CopilotModuleLoader>();
        services.AddSingleton<IDashboardModuleGenerationNotifier, SignalRDashboardModuleGenerationNotifier>();
        services.AddScoped<ICopilotModuleService, CopilotModuleService>();
        services.AddScoped<CopilotModuleRunner>();
        return services;
    }

    public static void MapDashboardFeatureApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/dashboard")
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
                catch (CopilotRequestException)
                {
                    return TypedResults.Problem(
                        "GitHub Copilot could not generate dashboard content.",
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
        .WithTags("Dashboard")
        .WithName("RefreshDashboardModules")
        .WithSummary("Invalidates the module cache and runs the refreshed dashboard module list.");

        endpoints.MapGet("/api/dashboard/module-files", (ICopilotModuleLoader moduleLoader) =>
            TypedResults.Ok(moduleLoader.GetModuleFiles()))
            .WithTags("Dashboard")
            .WithName("GetDashboardModuleFiles")
            .WithSummary("Lists module assemblies that can be deleted from the dashboard.");

        endpoints.MapPost("/api/dashboard/module-files", async Task<Results<Ok<string>, BadRequest<string>, ProblemHttpResult>> (
            IFormFile moduleFile,
            ICopilotModuleService moduleService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(await moduleService.UploadAsync(moduleFile, cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return TypedResults.BadRequest(exception.Message);
            }
        })
            .WithTags("Dashboard")
            .WithName("UploadDashboardModuleFile")
            .WithSummary("Validates and deploys a dashboard module assembly.");

        endpoints.MapPost("/api/dashboard/modules", async Task<Results<Ok<string>, BadRequest<string>, ProblemHttpResult>> (
            CreateDashboardModuleRequest request,
            ICopilotService copilot,
            ICopilotModuleService moduleService,
            CancellationToken cancellationToken) =>
        {
            var status = await copilot.GetAuthenticationStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                return TypedResults.Problem("Authenticate with GitHub to use Copilot.", statusCode: StatusCodes.Status401Unauthorized);
            }

            try
            {
                return TypedResults.Ok(await moduleService.CreateAsync(status.Login!, request.Prompt, cancellationToken));
            }
            catch (ArgumentException exception)
            {
                return TypedResults.BadRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (CopilotRequestException)
            {
                return TypedResults.Problem(
                    "GitHub Copilot could not create the dashboard module.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        })
            .WithTags("Dashboard")
            .WithName("CreateDashboardModule")
            .WithSummary("Creates, compiles, validates, and deploys a Copilot-authored dashboard module.");

        endpoints.MapDelete("/api/dashboard/module-files/{moduleFileName}", (
            string moduleFileName,
            ICopilotModuleLoader moduleLoader) =>
        {
            IResult result = moduleLoader.DeleteModule(moduleFileName)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
            return result;
        })
            .WithTags("Dashboard")
            .WithName("DeleteDashboardModuleFile")
            .WithSummary("Deletes one module assembly and invalidates the module cache.");
    }
}
