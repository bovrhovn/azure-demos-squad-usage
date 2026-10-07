using Microsoft.Extensions.Options;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

public sealed class PagingOptions
{
    public const string SectionName = "Paging";

    public int PageSize { get; init; } = 5;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int CurrentPage,
    int TotalPages,
    int TotalCount)
{
    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;
}

public interface IPagingService
{
    PagedResult<T> CreatePage<T>(IReadOnlyList<T> items, int pageNumber);
}

public sealed class PagingService(IOptions<PagingOptions> options) : IPagingService
{
    private readonly int pageSize = options.Value.PageSize;

    public PagedResult<T> CreatePage<T>(IReadOnlyList<T> items, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(items);

        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)pageSize));
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var pageItems = items
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PagedResult<T>(pageItems, currentPage, totalPages, items.Count);
    }
}

public static class PagingFeature
{
    public static IServiceCollection AddPagingFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PagingOptions>()
            .Bind(configuration.GetSection(PagingOptions.SectionName))
            .Validate(
                options => options.PageSize > 0,
                "Paging:PageSize must be greater than zero.")
            .ValidateOnStart();
        services.AddSingleton<IPagingService, PagingService>();
        return services;
    }
}
