using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>"Zu prüfen": confirm, edit and resolve possible duplicates without opening the viewer.</summary>
public sealed class ReviewPageTests(ServerFixture server)
{
    private static string Heading(ReviewItemDto item) => $"#review-h-{item.Id:N}";

    private static ReviewItemDto Duplicate(string title, bool confirmed = false) => ClassificationSamples.Review(
        title,
        confirmed ? ClassificationSamples.Automatic() with { Recognition = DocumentRecognitionState.Confirmed } : null,
        ClassificationSamples.Candidate("Stromrechnung (Original)"));

    [Fact]
    public async Task List_ShowsTheSummaryFirst_TheFieldsAsText_AndButtonsThatNameTheDocument()
    {
        var (first, second) = (ClassificationSamples.Review("Stromrechnung November"), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(first, second);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Stromrechnung November", "Brief Finanzamt"]);
        var article = page.GetByRole(AriaRole.Article).First;
        var text = await article.InnerTextAsync();
        Assert.True(text.IndexOf("Zusammenfassung", StringComparison.Ordinal) < text.IndexOf("Absender", StringComparison.Ordinal));
        Assert.Contains("Automatisch erkannt, noch nicht geprüft.", text, StringComparison.Ordinal);
        Assert.Contains("84,20 EUR", text, StringComparison.Ordinal);
        await Assertions.Expect(page.Locator(".review-summary")).ToHaveTextAsync("2 Dokumente zu prüfen.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen: Brief Finanzamt" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten: Brief Finanzamt" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu prüfen (2)" })).ToHaveAttributeAsync("aria-current", "page");
    }


    [Fact]
    public async Task Confirm_OfAnItemChangedMeanwhile_LoadsTheListAgain_AndSaysSo()
    {
        var (first, second) = (ClassificationSamples.Review("Stromrechnung November"), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(first, second) { ConfirmStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen: Stromrechnung November" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .form-error"))
            .ToContainTextAsync("Die Angaben wurden inzwischen geändert und neu geladen. Bitte prüfe sie noch einmal.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Brief Finanzamt"]);
        await Assertions.Expect(page.Locator("#review-heading")).ToBeFocusedAsync();
        Assert.Contains("\"Automatic\"", fake.Mocks.BodyOf("POST", $"/api/documents/{first.Id}/confirm"), StringComparison.Ordinal);
    }
    [Fact]
    public async Task Confirm_RemovesTheItem_AnnouncesWhatIsLeft_AndFocusesTheNextOne()
    {
        var (first, second) = (ClassificationSamples.Review("Stromrechnung November"), ClassificationSamples.Review("Brief Finanzamt"));
        var fake = new FakeReview(first, second);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen: Stromrechnung November" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToHaveTextAsync("„Stromrechnung November“ bestätigt. 1 Dokument zu prüfen.");
        await Assertions.Expect(page.Locator(Heading(second))).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu prüfen (1)" })).ToBeVisibleAsync();
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{first.Id}/confirm"));

        await page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen: Brief Finanzamt" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToHaveTextAsync("„Brief Finanzamt“ bestätigt. Nichts mehr zu prüfen.");
        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Nichts zu prüfen. Alle Dokumente sind bestätigt.");
        await Assertions.Expect(page.Locator("#review-heading")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu prüfen", Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PossibleDuplicate_ShowsAComparison_AndKeepRemovesTheHint()
    {
        var item = Duplicate("Stromrechnung (Scan)", confirmed: true);
        var fake = new FakeReview(item);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var table = page.GetByRole(AriaRole.Table, new() { Name = "Vergleich mit dem vorhandenen Dokument" });

        await Assertions.Expect(table.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Angabe", "Dieses Dokument", "Vorhandenes Dokument"]);
        await Assertions.Expect(table.GetByRole(AriaRole.Row, new() { Name = "Titel" })).ToContainTextAsync("Stromrechnung (Scan)");
        await Assertions.Expect(table.GetByRole(AriaRole.Row, new() { Name = "Titel" }).GetByRole(AriaRole.Link))
            .ToHaveTextAsync("Stromrechnung (Original)");
        await Assertions.Expect(table.GetByRole(AriaRole.Row, new() { Name = "Dokumentdatum" })).ToContainTextAsync("01.11.2026");
        await page.GetByRole(AriaRole.Button, new() { Name = "Behalten: Stromrechnung (Scan)" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToHaveTextAsync("„Stromrechnung (Scan)“ wird behalten. Nichts mehr zu prüfen.");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{item.Id}/duplicate/keep"));
    }

    [Fact]
    public async Task Delete_AsksFirst_TheSafeChoiceTakesFocus_EscapeCancels_ConfirmDeletes()
    {
        var item = Duplicate("Stromrechnung (Scan)", confirmed: true);
        var fake = new FakeReview(item);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var delete = page.GetByRole(AriaRole.Button, new() { Name = "Löschen: Stromrechnung (Scan)" });

        await delete.ClickAsync();
        var box = page.GetByRole(AriaRole.Group, new() { Name = "„Stromrechnung (Scan)“ wirklich löschen?" });
        await Assertions.Expect(box).ToBeVisibleAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(box).ToHaveCountAsync(0);
        await Assertions.Expect(delete).ToBeFocusedAsync();
        Assert.False(fake.Mocks.WasCalled("DELETE", $"/api/documents/{item.Id}"));

        await delete.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, löschen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First).ToHaveTextAsync("„Stromrechnung (Scan)“ gelöscht. Nichts mehr zu prüfen.");
        Assert.True(fake.Mocks.WasCalled("DELETE", $"/api/documents/{item.Id}"));
    }

    [Fact]
    public async Task Delete_Refused_SaysWhy_AndKeepsTheItem()
    {
        var item = Duplicate("Stromrechnung (Scan)", confirmed: true);
        var fake = new FakeReview(item) { DeleteStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Löschen: Stromrechnung (Scan)" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, löschen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .form-error").First)
            .ToContainTextAsync("Fehler: Gelöscht werden kann nur ein Dokument, das als mögliches Duplikat markiert ist.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Löschen: Stromrechnung (Scan)" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Edit_InTheList_OpensTheForm_AndSavingConfirmsAndRemovesTheItem()
    {
        var item = ClassificationSamples.Review("Stromrechnung November");
        var fake = new FakeReview(item);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten: Stromrechnung November" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "Angaben bearbeiten" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator($"#review-{item.Id:N}-title")).ToBeFocusedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator("main .live-region").First)
            .ToHaveTextAsync("Angaben von „Stromrechnung November“ gespeichert und bestätigt. Nichts mehr zu prüfen.");
        Assert.True(fake.Mocks.WasCalled("PUT", $"/api/documents/{item.Id}/fields"));
    }

    [Fact]
    public async Task Edit_Cancel_ReturnsFocusToTheEditButton()
    {
        var item = ClassificationSamples.Review("Stromrechnung November");
        var fake = new FakeReview(item);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten: Stromrechnung November" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Abbrechen", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten: Stromrechnung November" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task EmptyList_SaysSo_InEnglishToo()
    {
        var fake = new FakeReview();
        await using var session = await fake.OpenAsync(server, SupportedCultures.English);

        await Assertions.Expect(session.Page.Locator("main")).ToContainTextAsync("Nothing to review. All documents are confirmed.");
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Link, new() { Name = "To review", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
    }
}
