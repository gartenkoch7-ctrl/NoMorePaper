using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Seiten bearbeiten": loading, errors, missing rights and the previews of a changed document.</summary>
public sealed class PageEditorLoadTests(ServerFixture server)
{
    private static ILocator Card(IPage page, int number, int total) => FakePageEditor.Card(page, number, total);

    [Fact]
    public async Task Retry_AfterALoadError_FocusesTheHeading_OnceThePagesAreThere()
    {
        var fake = new FakePageEditor { FailedPageLoads = 1 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Die Seiten konnten nicht geladen werden");

        await page.Locator("#pe-retry").ClickAsync();

        // The retry button is gone with the error; the keyboard user must not be left on the page body.
        await Assertions.Expect(Card(page, 1, 3)).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#pe-heading")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Previews_AreRequestedWithTheVersionOfTheDocument_SoAnEditShowsFreshPages()
    {
        var first = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(1);
        var fake = new FakePageEditor { EditedAt = first };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await Assertions.Expect(page.Locator("canvas[data-title=\"Seite 1 von 3\"]")).ToBeVisibleAsync();
        Assert.Contains(fake.ArchiveRequests, url => url.EndsWith($"?v={first.UtcTicks}", StringComparison.Ordinal));

        // The thumbnails keep their cache while the app stays open; only another URL makes them draw again.
        fake.EditedAt = second;
        await page.GetByRole(AriaRole.Navigation, new() { Name = "Pfad" }).GetByRole(AriaRole.Link, new() { Name = "Dokumente" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/dokumente$"));
        var again = page.WaitForRequestAsync(request => request.Url.EndsWith($"?v={second.UtcTicks}", StringComparison.Ordinal));
        await page.GoBackAsync();

        await again;
    }

    [Fact]
    public async Task NoRight_ShowsTheNotice_AndNoList()
    {
        var fake = new FakePageEditor { CanEdit = false };
        await using var session = await fake.OpenAsync(server);

        await Assertions.Expect(session.Page.Locator("main")).ToContainTextAsync("Du darfst dieses Dokument nicht bearbeiten.");
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task EditRunning_ShowsTheNoticeWithALinkToTheDocument()
    {
        var fake = new FakePageEditor { EditPending = true };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Das Dokument wird gerade bearbeitet.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zum Dokument" })).ToHaveAttributeAsync("href", $"dokumente/{fake.Id}");
        await Assertions.Expect(page.GetByRole(AriaRole.Article)).ToHaveCountAsync(0);
    }
}
