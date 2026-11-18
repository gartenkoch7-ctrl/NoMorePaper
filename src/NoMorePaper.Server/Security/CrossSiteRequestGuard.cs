using System.Diagnostics.Metrics;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Security;

/// <summary>
/// Refuses changing API requests (everything but GET and HEAD below <c>/api</c>) that another site
/// may have triggered in the user's browser, with 403 ProblemDetails. M1 has no sign-in, so any page
/// the user visits could otherwise post to the local server.
/// </summary>
/// <remarks>
/// Allowed are requests the browser marks <c>Sec-Fetch-Site: same-origin</c>, and requests without
/// that header that carry <see cref="RequestHeader"/>: other sites cannot set a custom header
/// without a CORS preflight, which the server never answers. Clients outside a browser (scripts,
/// Home Assistant) send the custom header. DNS rebinding (a foreign name pointing at this server)
/// is stopped earlier by the host filter (<c>AllowedHosts</c>).
/// </remarks>
internal sealed class CrossSiteRequestGuard(RequestDelegate next, ILogger<CrossSiteRequestGuard> logger)
{
    public const string RequestHeader = "X-NoMorePaper-Request";

    private const string FetchSiteHeader = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";

    private static readonly string[] s_knownFetchSites = ["same-site", "cross-site", "none"];

    private static readonly Counter<long> s_rejected = ServerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.request_guard.rejected", description: "Changing API requests refused as possibly cross-site");

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.Request;
        if (!NeedsCheck(request) || IsAllowed(request.Headers))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var fetchSite = Describe(request.Headers);
        logger.CrossSiteRejected(fetchSite);
        s_rejected.Add(1, new KeyValuePair<string, object?>("nomorepaper.request_guard.fetch_site", fetchSite));
        await Results.Problem(title: "Cross-site request refused.", statusCode: StatusCodes.Status403Forbidden)
            .ExecuteAsync(context).ConfigureAwait(false);
    }

    private static bool NeedsCheck(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method);

    private static bool IsAllowed(IHeaderDictionary headers)
    {
        var fetchSite = headers[FetchSiteHeader];
        return fetchSite.Count > 0
            ? fetchSite.Count == 1 && string.Equals(fetchSite[0], SameOrigin, StringComparison.OrdinalIgnoreCase)
            : headers[RequestHeader] is { Count: 1 } marker && string.Equals(marker[0], "1", StringComparison.Ordinal);
    }

    // Only fixed words reach logs and tags, never what the client sent.
    private static string Describe(IHeaderDictionary headers)
    {
        var fetchSite = headers[FetchSiteHeader];
        if (fetchSite.Count == 0)
        {
            return "absent";
        }

        return s_knownFetchSites.FirstOrDefault(known => fetchSite.Count == 1 && string.Equals(known, fetchSite[0], StringComparison.OrdinalIgnoreCase))
            ?? "other";
    }
}
