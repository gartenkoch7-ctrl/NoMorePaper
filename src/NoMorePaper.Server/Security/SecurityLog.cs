namespace NoMorePaper.Server.Security;

/// <summary>Log events of the request checks (Server 1200–1299, docs/kb/telemetrie.md). Fixed words only.</summary>
internal static partial class SecurityLog
{
    [LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "Refused a changing API request that may come from another site (Sec-Fetch-Site: {FetchSite})")]
    public static partial void CrossSiteRejected(this ILogger logger, string fetchSite);
}
