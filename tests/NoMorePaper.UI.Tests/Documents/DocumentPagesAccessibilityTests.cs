using System.Globalization;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accessibility;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>axe-core finds no violations on the upload, document and search pages, in both languages.</summary>
public sealed class DocumentPagesAccessibilityTests(ServerFixture server)
{
    private static readonly string[] _paths = ["/hochladen", "/dokumente", "/suche", "/suche?q=Rechnung"];

    public static TheoryData<string, string> PagesInAllCultures()
    {
        var data = new TheoryData<string, string>();
        foreach (var path in _paths)
        {
            foreach (var culture in SupportedCultures.All)
            {
                data.Add(path, culture);
            }
        }

        return data;
    }

    // Without the API these pages show their error state; with it, their real content.
    // Both must be accessible, so the page is checked once its content has settled.
    [Theory]
    [MemberData(nameof(PagesInAllCultures))]
    public async Task Page_HasNoAxeViolations(string path, string culture)
    {
        await using var session = await server.OpenAsync(path, culture);
        await WaitUntilSettledAsync(session.Page, path);

        await AssertNoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task DocumentList_WithAllStates_HasNoAxeViolations(string culture)
    {
        var link = culture == SupportedCultures.German ? "Dokumente" : "Documents";
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, culture, link, page =>
            ApiMocks.ListAsync(page, SampleDocuments.OneOfEachState()));
        await WaitUntilSettledAsync(session.Page, "/dokumente");

        Assert.Equal(4, await session.Page.Locator("tbody tr").CountAsync());
        await AssertNoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task SearchResults_HaveNoAxeViolations(string culture)
    {
        await using var session = await server.OpenAsync("/", culture);
        await ApiMocks.SearchAsync(session.Page, new[]
        {
            new SearchHitDto(Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, "… Betrag 120 Euro …", 0.9),
            new SearchHitDto(Guid.NewGuid(), "Mietvertrag", DateTimeOffset.UtcNow, "… Rechnung für …", 0.4),
        });
        await session.Page.Locator("#header-search-input").FillAsync("Rechnung");
        await session.Page.Keyboard.PressAsync("Enter");
        await WaitUntilSettledAsync(session.Page, "/suche?q=Rechnung");

        Assert.Equal(2, await session.Page.Locator("main h2").CountAsync());
        await AssertNoViolationsAsync(session.Page);
    }

    private static async Task WaitUntilSettledAsync(IPage page, string path)
    {
        if (path.StartsWith("/hochladen", StringComparison.Ordinal))
        {
            await page.Locator("#upload-input").WaitForAsync();
            return;
        }

        await page.Locator("main [data-ready]").First.WaitForAsync();
    }

    private static async Task AssertNoViolationsAsync(IPage page)
    {
        var result = await page.RunAxe(AxeRules.Strict());

        Assert.True(result.Violations.Length == 0, Describe(result.Violations));
    }

    private static string Describe(AxeResultItem[] violations)
    {
        var text = new StringBuilder();
        foreach (var violation in violations)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{violation.Id} ({violation.Impact}): {violation.Help}");
            foreach (var node in violation.Nodes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  {node.Target}: {node.Html}");
            }
        }

        return text.ToString();
    }
}
