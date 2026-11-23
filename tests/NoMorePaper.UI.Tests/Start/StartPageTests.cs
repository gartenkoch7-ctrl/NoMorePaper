using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Start;

/// <summary>The start page with its overview (Stitch screen 05): greeting, review card, recent documents, inputs, actions.</summary>
public sealed class StartPageTests(ServerFixture server)
{
    private static InputStatusDto Row(string kind, string? problem = null, string? account = null, bool attention = false) => new(
        kind, account, problem is null ? "ok" : "problem", DateTimeOffset.UtcNow, null, problem, attention);

    [Fact]
    public async Task Greets_ThePerson_ByName()
    {
        await using var session = await new FakeStart { ReviewCount = 0 }.OpenAsync(server);

        await Assertions.Expect(session.Page.Locator("h1")).ToHaveTextAsync("Guten Tag, Anna Beispiel");
    }

    [Fact]
    public async Task ReviewCard_WithDocuments_ShowsTheCount_AndLinksToReview()
    {
        await using var session = await new FakeStart { ReviewCount = 3 }.OpenAsync(server);
        var card = session.Page.Locator("section[aria-labelledby=start-review-heading]");

        await Assertions.Expect(card.Locator("h2")).ToHaveTextAsync("Zu prüfen (3)");
        await Assertions.Expect(card).ToContainTextAsync("3 Dokumente zu prüfen.");
        await Assertions.Expect(card.GetByRole(AriaRole.Link, new() { Name = "Jetzt prüfen" })).ToHaveAttributeAsync("href", "zu-pruefen");
    }

    [Fact]
    public async Task ReviewCard_WithNothingToReview_SaysSo_WithoutLink()
    {
        await using var session = await new FakeStart { ReviewCount = 0 }.OpenAsync(server);
        var card = session.Page.Locator("section[aria-labelledby=start-review-heading]");

        await Assertions.Expect(card).ToContainTextAsync("Nichts zu prüfen.");
        await Assertions.Expect(card.GetByRole(AriaRole.Link)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ReviewCard_WhenTheCountFails_SaysItIsUnknown()
    {
        await using var session = await new FakeStart().OpenAsync(server);

        await Assertions.Expect(session.Page.Locator("section[aria-labelledby=start-review-heading]"))
            .ToContainTextAsync("Wie viele Dokumente warten, konnte nicht geladen werden.");
    }

    [Fact]
    public async Task Recent_ShowsATable_WithStatusAsWords_TitleLinks_AndALinkToAllDocuments()
    {
        var fake = new FakeStart { ReviewCount = 0, Recent = SampleDocuments.OneOfEachState() };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        var table = page.GetByRole(AriaRole.Table, new() { Name = "Die fünf zuletzt eingegangenen Dokumente" });
        await Assertions.Expect(table.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Titel", "Absender", "Eingang", "Status"]);
        await Assertions.Expect(table.GetByRole(AriaRole.Link)).ToHaveTextAsync(["Stromrechnung", "Mietvertrag", "Steuerbescheid", "Kaputter Scan"]);
        var text = await table.InnerTextAsync();
        Assert.Contains("Fertig", text, StringComparison.Ordinal);
        Assert.Contains("Wird verarbeitet", text, StringComparison.Ordinal);
        Assert.Contains("Fehlgeschlagen", text, StringComparison.Ordinal);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Alle Dokumente anzeigen" })).ToHaveAttributeAsync("href", "dokumente");
    }

    [Fact]
    public async Task Recent_WithoutDocuments_SaysHowToAddOne()
    {
        await using var session = await new FakeStart { ReviewCount = 0 }.OpenAsync(server);

        await Assertions.Expect(session.Page.Locator("section[aria-labelledby=start-recent-heading]"))
            .ToContainTextAsync("Noch keine Dokumente.");
    }

    [Fact]
    public async Task Inputs_ShowTheOneWithAProblemFirst_NameTheAccount_AndLinkToIt()
    {
        var fake = new FakeStart { ReviewCount = 0 };
        fake.Inputs.AddRange([Row("scanner-folder"), Row("icloud", "sign-in-failed", "Ben Beispiel")]);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        var items = page.Locator("ul.start-inputs > li");
        await Assertions.Expect(items).ToHaveCountAsync(3);
        await Assertions.Expect(items.First).ToContainTextAsync("iCloud Drive");
        await Assertions.Expect(items.First).ToContainTextAsync("Ben Beispiel: ");
        await Assertions.Expect(items.First.GetByRole(AriaRole.Link, new() { Name = "Eingang iCloud Drive öffnen" }))
            .ToHaveAttributeAsync("href", "eingaenge?quelle=icloud");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Alle Eingänge anzeigen" })).ToHaveAttributeAsync("href", "eingaenge");
    }

    [Fact]
    public async Task Failures_AreNoticesWithoutAlert_AndTryAgainLoadsThem()
    {
        var fake = new FakeStart { RecentStatus = 500, InputsStatus = 500 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Die zuletzt hinzugefügten Dokumente konnten nicht geladen werden:");
        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Der Zustand der Eingänge konnte nicht geladen werden:");
        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveCountAsync(0);

        (fake.RecentStatus, fake.InputsStatus) = (200, 200);
        fake.Recent = SampleDocuments.Many(2);
        var retry = page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" });
        await retry.First.ClickAsync();
        await retry.First.ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Dokument 1" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("ul.start-inputs > li")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task Actions_AreLinks_ToUploadAndCamera()
    {
        await using var session = await new FakeStart { ReviewCount = 0 }.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Datei hochladen" }).Last).ToHaveAttributeAsync("href", "hochladen");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mit Kamera aufnehmen" }).Last).ToHaveAttributeAsync("href", "kamera");
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task HasNoAxeViolations_WithData(string culture)
    {
        var fake = new FakeStart { ReviewCount = 2, Recent = SampleDocuments.Rich() };
        fake.Inputs.AddRange([Row("scanner-folder"), Row("icloud", "code-needed", "Ben Beispiel", attention: true)]);
        await using var session = await fake.OpenAsync(server, culture);
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
        await Assertions.Expect(session.Page.Locator("ul.start-inputs > li")).ToHaveCountAsync(3);

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task HasNoAxeViolations_WithFailures(string culture)
    {
        await using var session = await new FakeStart { RecentStatus = 500, InputsStatus = 500 }.OpenAsync(server, culture);
        await Assertions.Expect(session.Page.Locator(".nmp-notice")).ToHaveCountAsync(2);

        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
