using Microsoft.Extensions.Options;
using SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class PagingFeatureTests
{
    [Fact]
    public void CreatePage_uses_the_configured_page_size_and_clamps_the_page_number()
    {
        var service = new PagingService(Options.Create(new PagingOptions { PageSize = 3 }));

        var page = service.CreatePage([1, 2, 3, 4, 5, 6, 7], 10);

        Assert.Equal(3, page.CurrentPage);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal([7], page.Items);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }
}
