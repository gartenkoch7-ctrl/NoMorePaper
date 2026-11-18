using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Search;

/// <summary>Log events of the search (Infrastructure 3200–3299). Counts and durations only, never the query.</summary>
internal static partial class SearchLog
{
    [LoggerMessage(EventId = 3210, Level = LogLevel.Information, Message = "Search found {HitCount} documents in {ElapsedMs} ms (similarity fallback: {Fallback})")]
    public static partial void SearchCompleted(this ILogger logger, int hitCount, long elapsedMs, bool fallback);
}
