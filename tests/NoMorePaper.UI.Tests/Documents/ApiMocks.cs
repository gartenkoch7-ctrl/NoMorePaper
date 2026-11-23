using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>
/// Answers API calls inside the browser so page behaviour (states, errors, announcements) can be
/// tested without the real API. Routes are set after the first page is open and the page under
/// test is reached by clicking its navigation link, so the mock is in place before it fetches.
/// </summary>
internal static partial class ApiMocks
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public static Task RespondAsync(IPage page, Regex url, string method, object? body, int status = 200)
        => page.RouteAsync(url, route => route.Request.Method == method
            ? route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                ContentType = status >= 400 ? "application/problem+json" : "application/json",
                Body = JsonSerializer.Serialize(body ?? new { }, _json),
            })
            : route.ContinueAsync());

    // The UI test server has a real database: an unmocked count would pair real numbers with the mocked
    // list ("0 documents found"), so the count is switched off first. Tests that need numbers call
    // CountsAsync afterwards (the later route wins in Playwright).
    public static async Task ListAsync(IPage page, object body, int status = 200)
    {
        await NoCountsAsync(page);
        await RespondAsync(page, ListUrl(), "GET", body, status);
    }

    public static Task CountsAsync(IPage page, object body, int status = 200)
        => RespondAsync(page, CountsUrl(), "GET", body, status);

    public static Task NoCountsAsync(IPage page) => CountsAsync(page, new { }, 404);

    public static Task SearchAsync(IPage page, object body, int status = 200)
        => RespondAsync(page, SearchUrl(), "GET", body, status);

    public static Task UploadAsync(IPage page, object body, int status = 202)
        => RespondAsync(page, ListUrl(), "POST", body, status);

    /// <summary>Opens the page behind <paramref name="linkName"/> by clicking the navigation link, after <paramref name="mock"/> ran.</summary>
    public static async Task<BrowserSession> OpenViaNavigationAsync(
        ServerFixture server, string culture, string linkName, Func<IPage, Task> mock)
    {
        var session = await server.OpenAsync("/", culture);
        try
        {
            await mock(session.Page);
            await session.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = linkName, Exact = true }).ClickAsync();
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    [GeneratedRegex(@"/api/documents(\?.*)?$")]
    private static partial Regex ListUrl();

    [GeneratedRegex(@"/api/documents/counts(\?.*)?$")]
    private static partial Regex CountsUrl();

    [GeneratedRegex(@"/api/search\?.*$")]
    private static partial Regex SearchUrl();
}
