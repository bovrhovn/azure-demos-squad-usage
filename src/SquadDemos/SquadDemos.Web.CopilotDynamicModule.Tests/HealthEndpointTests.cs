using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace SquadDemos.Web.CopilotDynamicModule.Tests;

public sealed class HealthEndpointTests : IClassFixture<HealthEndpointFactory>
{
    private readonly HttpClient client;

    public HealthEndpointTests(HealthEndpointFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_returns_ok_without_authentication()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class HealthEndpointFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("GitHubDeviceFlow:ClientId", "test-client-id");
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
    }
}
