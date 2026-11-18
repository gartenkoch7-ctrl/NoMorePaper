using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NoMorePaper.Server.Hosting;

/// <summary>
/// Trusts the forwarded headers of the reverse proxy on the same host only (<c>tailscale serve</c>,
/// ADR-0020), so <c>Request.IsHttps</c> and the host name reflect what the browser used. Headers from
/// any other sender are ignored.
/// </summary>
internal static class ForwardedHeadersRegistration
{
    public static IServiceCollection AddServerForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            // The framework trusts the loopback range by default; state exactly the two addresses instead.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        });
}
