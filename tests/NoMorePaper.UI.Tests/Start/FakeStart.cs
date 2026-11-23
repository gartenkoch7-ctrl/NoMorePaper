using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Start;

/// <summary>
/// An in-browser stand-in for the three calls of the start page: the review counter, the five recent documents
/// and the state of the inputs. The answers can be changed between calls, so "try again" sees them.
/// </summary>
internal sealed class FakeStart
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>The review count; <c>null</c> answers 500.</summary>
    public int? ReviewCount { get; set; }

    public DocumentSummaryDto[] Recent { get; set; } = [];

    public int RecentStatus { get; set; } = 200;

    public List<InputStatusDto> Inputs { get; } = [];

    public int InputsStatus { get; set; } = 200;

    public async Task<BrowserSession> OpenAsync(ServerFixture server, string culture = SupportedCultures.German)
    {
        var accounts = new FakeAccounts();
        return await server.OpenAsync("/", culture, signedIn: true, prepare: async page =>
        {
            await accounts.InstallAsync(page, signedIn: true);
            await InstallAsync(page);
        });
    }

    // Registered after the account mocks, so these answers win for the same address.
    private async Task InstallAsync(IPage page)
    {
        await page.RouteAsync(new Regex("/api/review/count$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : ReviewCount is { } count
                ? Fulfil(route, 200, count.ToString(CultureInfo.InvariantCulture))
                : Fulfil(route, 500, "{}"));
        await page.RouteAsync(new Regex(@"/api/documents\?page=1&pageSize=5$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : Fulfil(route, RecentStatus, RecentStatus == 200 ? JsonSerializer.Serialize(Recent, _json) : "{}"));
        await page.RouteAsync(new Regex("/api/inputs$"), route => route.Request.Method != "GET"
            ? route.FallbackAsync()
            : Fulfil(route, InputsStatus, InputsStatus == 200 ? JsonSerializer.Serialize(Inputs, _json) : "{}"));
    }

    private static Task Fulfil(IRoute route, int status, string body) => route.FulfillAsync(new RouteFulfillOptions
    {
        Status = status,
        ContentType = status >= 400 ? "application/problem+json" : "application/json",
        Body = body,
    });
}
