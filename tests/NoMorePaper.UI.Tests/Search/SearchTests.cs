using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Search;

/// <summary>Search from the header: "/" shortcut, keyboard submit, hits as headings, count announced.</summary>
public sealed class SearchTests(ServerFixture server)
{
    private const string SearchInput = "#header-search-input";

    [Theory]
    [InlineData(SupportedCultures.German, "Dokumente durchsuchen")]
    [InlineData(SupportedCultures.English, "Search documents")]
    public async Task SearchField_HasAccessibleName_AndIsInSearchLandmark(string culture, string name)
    {
        await using var session = await server.OpenAsync("/", culture);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Search).GetByRole(AriaRole.Searchbox, new() { Name = name }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(SearchInput)).ToHaveAttributeAsync("aria-keyshortcuts", "/");
    }

    [Fact]
    public async Task Slash_FocusesSearchField_WithVisibleFocus()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;

        await page.Keyboard.PressAsync("/");

        await Assertions.Expect(page.Locator(SearchInput)).ToBeFocusedAsync();
        Assert.True((await FocusState.ReadAsync(page)).IndicatorWidth >= 3);
        // The key itself must not end up in the field.
        await Assertions.Expect(page.Locator(SearchInput)).ToHaveValueAsync("");
    }

    [Fact]
    public async Task Slash_InsideTextField_IsTyped_NotIntercepted()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;

        await page.Locator(SearchInput).FocusAsync();
        await page.Keyboard.TypeAsync("a/b");

        await Assertions.Expect(page.Locator(SearchInput)).ToHaveValueAsync("a/b");
    }

    [Fact]
    public async Task Enter_InSearchField_ShowsHitsAsHeadings_AndAnnouncesCount()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new[]
        {
            new SearchHitDto(Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, "Betrag <b>120</b> Euro", 0.9),
            new SearchHitDto(Guid.NewGuid(), "Mietvertrag", DateTimeOffset.UtcNow, "Rechnung für Miete", 0.4),
        });

        await page.Locator(SearchInput).FillAsync("Rechnung");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/suche\?q=Rechnung$"));
        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("2 Treffer für „Rechnung“");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveCountAsync(2);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Stromrechnung" })).ToBeVisibleAsync();
        // Snippets are plain text: markup in them is shown, never interpreted.
        await Assertions.Expect(page.Locator(".search-hit").First).ToContainTextAsync("Betrag <b>120</b> Euro");
        // The field keeps the query, so it can be edited and searched again.
        await Assertions.Expect(page.Locator(SearchInput)).ToHaveValueAsync("Rechnung");
    }

    [Fact]
    public async Task NoHits_AnnouncesZeroHits()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, Array.Empty<SearchHitDto>());

        await page.Locator(SearchInput).FillAsync("Nichts");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("0 Treffer für „Nichts“");
    }

    [Fact]
    public async Task OneHit_UsesSingular_InEnglish()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.English);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new[] { new SearchHitDto(Guid.NewGuid(), "Invoice", DateTimeOffset.UtcNow, "…", 1) });

        await page.Locator(SearchInput).FillAsync("invoice");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("1 result for \"invoice\"");
    }

    [Fact]
    public async Task ApiError_ShowsAlert_InsteadOfCrashing()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new { }, 500);

        await page.Locator(SearchInput).FillAsync("Rechnung");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync("Die Suche ist fehlgeschlagen.");
        await Assertions.Expect(page.Locator("h1")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EmptySearchPage_ExplainsHowToSearch()
    {
        await using var session = await server.OpenAsync("/suche", SupportedCultures.German);

        await Assertions.Expect(session.Page.Locator("main")).ToContainTextAsync("Gib im Feld „Dokumente durchsuchen“");
    }
}
