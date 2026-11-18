using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace NoMorePaper.Server.Hosting;

/// <summary>
/// Trusts the forwarded headers of the reverse proxy on the same host only (<c>tailscale serve</c>,
/// ADR-0020), so <c>Request.IsHttps</c> and the host name reflect what the browser used. Headers from
/// any other sender are ignored. In Docker the proxy's connection arrives from the bridge network, so
/// <see cref="TrustedProxiesKey"/> adds networks (CIDR) to trust as well; empty by default.
/// </summary>
internal static class ForwardedHeadersRegistration
{
    /// <summary>Networks to trust, separated by <c>;</c> or <c>,</c> (env <c>NoMorePaper__TrustedProxies</c>), e.g. <c>172.16.0.0/12</c>.</summary>
    public const string TrustedProxiesKey = "NoMorePaper:TrustedProxies";

    public static IServiceCollection AddServerForwardedHeaders(this IServiceCollection services)
    {
        // Configuration is read when the options are first used, so test hosts can still add sources.
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            // The framework trusts the loopback range by default; state exactly the two addresses instead.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
            foreach (var network in TrustedNetworks(configuration[TrustedProxiesKey]))
            {
                options.KnownIPNetworks.Add(network);
            }

            // Host filtering (AllowedHosts) runs before this middleware and only saw the proxy's Host;
            // the forwarded host must pass the same list, as it ends up in invitation and reset links.
            var allowedHosts = (configuration["AllowedHosts"] ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var host in allowedHosts)
            {
                if (host == "*")
                {
                    options.AllowedHosts.Clear();
                    break;
                }

                options.AllowedHosts.Add(host);
            }
        });
        return services;
    }

    /// <exception cref="InvalidOperationException">An entry is not a network in CIDR notation; the host does not start.</exception>
    internal static IEnumerable<System.Net.IPNetwork> TrustedNetworks(string? setting)
    {
        var entries = (setting ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            yield return System.Net.IPNetwork.TryParse(entry, out var network)
                ? network
                : throw new InvalidOperationException($"{TrustedProxiesKey} contains '{entry}', which is not a network like 172.16.0.0/12.");
        }
    }
}
