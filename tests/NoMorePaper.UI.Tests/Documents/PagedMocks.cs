using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>A list that answers page by page, like the real one, and remembers the addresses it was asked for.</summary>
internal static partial class PagedMocks
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>Answers <c>/api/documents?page=…&amp;pageSize=…</c> with "Dokument n" rows out of <paramref name="total"/>.</summary>
    public static async Task ListAsync(IPage page, int total, ConcurrentQueue<string> urls)
    {
        await ApiMocks.NoCountsAsync(page);
        await page.RouteAsync(ListUrl(), route =>
        {
            urls.Enqueue(route.Request.Url);
            var number = Value(route.Request.Url, "page", 1);
            var size = Value(route.Request.Url, "pageSize", 50);
            var rows = Math.Clamp(total - (number - 1) * size, 0, size);
            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(SampleDocuments.Many(rows), _json),
            });
        });
    }

    private static int Value(string url, string name, int fallback)
    {
        var match = Regex.Match(url, $@"[?&]{name}=(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }

    [GeneratedRegex(@"/api/documents\?")]
    private static partial Regex ListUrl();
}
