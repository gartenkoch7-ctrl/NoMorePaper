using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Dokument anhängen" inside the page editor: search, the question and the answers.</summary>
public sealed class PageEditorAppendTests(ServerFixture server)
{
    private static SearchHitDto Hit(string title, bool isPrivate = false)
        => new(Guid.NewGuid(), title, DateTimeOffset.UtcNow, "…", 1, IsPrivate: isPrivate, IsMine: true);

    private static async Task SearchAsync(IPage page, string text)
    {
        await page.Locator("#pe-append-button").ClickAsync();
        await Assertions.Expect(page.Locator("#pe-append-heading")).ToBeFocusedAsync();
        await page.Locator("#pe-append-search").FillAsync(text);
    }

    [Fact]
    public async Task Search_ListsTheHitsAsButtons_WithoutTheDocumentItself_AndAnnouncesTheCount()
    {
        var id = Guid.NewGuid();
        var own = Hit("Stromrechnung") with { Id = id };
        var fake = new FakePageEditor { Id = id, Hits = [own, Hit("Mietvertrag"), Hit("Brief Finanzamt", isPrivate: true)] };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await SearchAsync(page, "brief");

        var hits = page.GetByRole(AriaRole.List, new() { Name = "Treffer" });
        await Assertions.Expect(hits.GetByRole(AriaRole.Button)).ToHaveCountAsync(2);
        await Assertions.Expect(hits.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Mietvertrag" })).ToBeVisibleAsync();
        await Assertions.Expect(hits).ToContainTextAsync("Privat");
        await Assertions.Expect(page.Locator("#pe-append-button")).ToHaveAttributeAsync("aria-expanded", "true");
    }

    [Fact]
    public async Task Choose_AsksInThePage_NamesBothDocuments_AndAPrivateOneBecomesPrivate()
    {
        var fake = new FakePageEditor { Hits = [Hit("Brief Finanzamt", isPrivate: true)] };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await SearchAsync(page, "brief");

        await page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Brief Finanzamt" }).ClickAsync();

        var box = page.GetByRole(AriaRole.Group, new() { Name = "„Brief Finanzamt“ an „Stromrechnung“ anhängen?" });
        await Assertions.Expect(box).ToBeVisibleAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Assertions.Expect(box).ToContainTextAsync("Die Seiten kommen ans Ende. „Brief Finanzamt“ kommt danach in den Papierkorb.");
        await Assertions.Expect(box).ToContainTextAsync("„Brief Finanzamt“ ist privat. Das Ergebnis wird deshalb privat.");
        await Assertions.Expect(box.GetByRole(AriaRole.Button, new() { Name = "Ja, anhängen" })).Not.ToHaveClassAsync(new Regex("nmp-button-primary"));
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(box).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Brief Finanzamt" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Choose_OfAPublicDocument_HasNoPrivacyNote()
    {
        var fake = new FakePageEditor { Hits = [Hit("Mietvertrag")] };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await SearchAsync(page, "miet");

        await page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Mietvertrag" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "„Mietvertrag“ an „Stromrechnung“ anhängen?" })).Not.ToContainTextAsync("deshalb privat");
    }

    [Fact]
    public async Task Confirm_SendsTheAppend_ShowsTheSuccessWithALink_AndLocksTheList()
    {
        var hit = Hit("Mietvertrag");
        var fake = new FakePageEditor { Hits = [hit] };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await SearchAsync(page, "miet");
        await page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Mietvertrag" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, anhängen" }).ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("„Mietvertrag“ wird angehängt. Die neue Fassung erscheint im Dokument, sobald sie fertig ist.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zum Dokument" })).ToBeFocusedAsync();
        Assert.Contains(hit.Id.ToString(), fake.PlanBody("append"), StringComparison.Ordinal);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Nach rechts drehen" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Confirm_Refused_ShowsTheErrorAndKeepsTheHit()
    {
        var fake = new FakePageEditor { Hits = [Hit("Mietvertrag")], SaveStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await SearchAsync(page, "miet");
        await page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Mietvertrag" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, anhängen" }).ClickAsync();

        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Anhängen nicht möglich: Das Dokument wird gerade bearbeitet");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Anhängen: Mietvertrag" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task WithUnsavedChanges_OnlyTheHintShows_NoSearch()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();

        await page.Locator("#pe-append-button").ClickAsync();

        await Assertions.Expect(page.Locator(".pe-append")).ToContainTextAsync("Speichere oder verwirf zuerst deine Änderungen.");
        await Assertions.Expect(page.Locator("#pe-append-search")).ToHaveCountAsync(0);
    }
}
