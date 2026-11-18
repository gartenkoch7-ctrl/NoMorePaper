using System.Globalization;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accessibility;

/// <summary>axe-core finds no violations on any page, in every UI language.</summary>
public sealed class AccessibilityTests(ServerFixture server)
{
    // Every routable page plus an unknown route (rendered by the not-found page).
    private static readonly string[] _paths = ["/", "/not-found", "/does-not-exist"];

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

    [Theory]
    [MemberData(nameof(PagesInAllCultures))]
    public async Task Page_HasNoAxeViolations(string path, string culture)
    {
        await using var session = await server.OpenAsync(path, culture);

        var result = await session.Page.RunAxe(AxeRules.Strict());

        Assert.True(result.Violations.Length == 0, Describe(result.Violations));
        // Guards against a misconfigured run silently skipping the AAA contrast rule.
        Assert.Contains(result.Passes, rule => rule.Id == AxeRules.EnhancedContrastRule);
    }

    [Theory]
    [MemberData(nameof(PagesInAllCultures))]
    public async Task Page_DeclaresLanguage_HasLandmarksTitleAndOneH1(string path, string culture)
    {
        await using var session = await server.OpenAsync(path, culture);
        var page = session.Page;

        // Screen readers pick the voice from <html lang>.
        Assert.Equal(culture, await page.Locator("html").GetAttributeAsync("lang"));
        Assert.Equal(1, await page.Locator("h1").CountAsync());
        Assert.Equal(1, await page.Locator("header").CountAsync());
        Assert.Equal(1, await page.Locator("nav").CountAsync());
        Assert.Equal(1, await page.Locator("main").CountAsync());
        Assert.False(string.IsNullOrWhiteSpace(await page.TitleAsync()));
    }

    [Theory]
    [MemberData(nameof(PagesInAllCultures))]
    public async Task Page_IsDarkWithLargeText_EvenWhenSystemPrefersLight(string path, string culture)
    {
        await using var session = await server.OpenAsync(path, culture);

        // Body text of the page content (MudText body1), at default browser settings.
        var style = await session.Page.EvaluateAsync<string[]>("""
            () => {
              const body = getComputedStyle(document.body);
              const text = getComputedStyle(document.querySelector('main p'));
              return [body.backgroundColor, text.color, text.fontSize];
            }
            """);

        Assert.Equal(["rgb(0, 0, 0)", "rgb(255, 255, 255)", "18px"], style);
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
