using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SquadDemos.Web.CopilotDynamicModule.Features.Hosting;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    public bool TrustAllForwardedHeaders { get; init; }

    public IReadOnlyList<string> TrustedProxyAddresses { get; init; } = [];
}

public static class ReverseProxyFeature
{
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ReverseProxyOptions.SectionName);
        services.AddOptions<ReverseProxyOptions>()
            .Bind(section)
            .Validate(
                options => options.TrustedProxyAddresses.All(address => IPAddress.TryParse(address, out _)),
                "ReverseProxy:TrustedProxyAddresses must contain valid IP addresses.")
            .ValidateOnStart();

        var options = section.Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();
        services.Configure<ForwardedHeadersOptions>(forwardedHeaders =>
        {
            forwardedHeaders.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (options.TrustAllForwardedHeaders)
            {
                forwardedHeaders.KnownIPNetworks.Clear();
                forwardedHeaders.KnownProxies.Clear();
            }
            else
            {
                foreach (var address in options.TrustedProxyAddresses)
                {
                    forwardedHeaders.KnownProxies.Add(IPAddress.Parse(address));
                }
            }
        });

        return services;
    }
}
