using System.Text.Json;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>The "Angaben" panel in the viewer: summary first, text for the origin, edit, confirm, editors.</summary>
public sealed class FieldsPanelTests(ServerFixture server)
{
    private static FakeFields Fake(bool canEdit = true, bool isMine = false, IReadOnlyList<Guid>? editors = null)
        => new(Guid.NewGuid(), ClassificationSamples.Automatic(canEdit, editors), isMine);

    [Fact]
    public async Task Panel_PutsTheSummaryFirst_ThenTheFieldsAsAList_AndSaysAutomatic()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;
        var panel = page.GetByRole(AriaRole.Region, new() { Name = "Angaben" });

        await Assertions.Expect(panel).ToContainTextAsync("Stromrechnung der Stadtwerke München");
        var text = await panel.InnerTextAsync();
        Assert.True(text.IndexOf("Zusammenfassung", StringComparison.Ordinal) < text.IndexOf("Absender", StringComparison.Ordinal));
        Assert.Contains("Automatisch erkannt, noch nicht geprüft.", text, StringComparison.Ordinal);
        await Assertions.Expect(panel.GetByRole(AriaRole.Term)).ToHaveTextAsync(["Absender", "Dokumentart", "Dokumentdatum", "Betrag", "Tags"]);
        await Assertions.Expect(panel.GetByRole(AriaRole.Definition)).ToHaveTextAsync(
            ["Stadtwerke München", "Rechnung", "01.11.2026", "84,20 EUR", "Strom"]);
    }

    [Fact]
    public async Task Panel_InEnglish_UsesEnglishTexts()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server, SupportedCultures.English);

        var panel = session.Page.GetByRole(AriaRole.Region, new() { Name = "Details" });
        await Assertions.Expect(panel).ToContainTextAsync("Recognized automatically, not checked yet.");
        await Assertions.Expect(panel.GetByRole(AriaRole.Term).First).ToHaveTextAsync("Sender");
    }

    [Fact]
    public async Task Confirm_SendsTheRequest_Announces_AndMovesFocusToEdit()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Angaben bestätigt.");
        await Assertions.Expect(page.Locator(".fields-recognition")).ToContainTextAsync("Geprüft und bestätigt.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bestätigen", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true })).ToBeFocusedAsync();
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{fake.Id}/confirm"));
    }

    [Fact]
    public async Task WithoutEditRight_ThereAreNoButtons_AndNoEditorChoice()
    {
        var fake = Fake(canEdit: false);
        await using var session = await fake.OpenViewerAsync(server);
        var panel = session.Page.GetByRole(AriaRole.Region, new() { Name = "Angaben" });

        await Assertions.Expect(panel).ToContainTextAsync("Stadtwerke München");
        await Assertions.Expect(panel.GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
        await Assertions.Expect(panel.GetByRole(AriaRole.Group)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Edit_OpensTheForm_FocusesTheTitle_AndSavesEveryField()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#fields-title")).ToBeFocusedAsync();
        await page.Locator("#fields-amount").FillAsync("99,5");
        await page.Locator("#fields-currency").FillAsync("chf");
        await page.Locator("#fields-summary").FillAsync("Neue Zusammenfassung");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Angaben gespeichert und bestätigt.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true })).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Definition).Nth(3)).ToHaveTextAsync("99,50 CHF");
        using var body = JsonDocument.Parse(fake.Mocks.BodyOf("PUT", $"/api/documents/{fake.Id}/fields")!);
        var root = body.RootElement;
        Assert.Equal(ClassificationSamples.StadtwerkeMuenchen.Id, root.GetProperty("correspondentId").GetGuid());
        Assert.Equal(ClassificationSamples.Rechnung.Id, root.GetProperty("documentTypeId").GetGuid());
        Assert.Equal("2026-11-01", root.GetProperty("documentDate").GetString());
        Assert.Equal(99.5m, root.GetProperty("amount").GetProperty("value").GetDecimal());
        Assert.Equal("CHF", root.GetProperty("amount").GetProperty("currency").GetString());
        Assert.Equal("Neue Zusammenfassung", root.GetProperty("summary").GetString());
        Assert.Equal(ClassificationSamples.Strom.Id, root.GetProperty("tagIds")[0].GetGuid());
    }

    [Fact]
    public async Task Edit_WithAnInvalidAmount_AnnouncesTheProblem_FocusesTheField_AndSendsNothing()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
        await page.Locator("#fields-amount").FillAsync("abc");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-editor .form-error")).ToContainTextAsync("Fehler: Der Betrag ist keine gültige Zahl.");
        await Assertions.Expect(page.Locator("#fields-amount")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#fields-amount")).ToHaveAttributeAsync("aria-invalid", "true");
        Assert.False(fake.Mocks.WasCalled("PUT", $"/api/documents/{fake.Id}/fields"));
    }

    [Fact]
    public async Task Edit_Refused_ExplainsAndKeepsTheForm()
    {
        var fake = Fake();
        fake.SaveStatus = 403;
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-editor .form-error")).ToContainTextAsync("Dafür fehlt dir die Berechtigung.");
        await Assertions.Expect(page.Locator("#fields-title")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Cancel_ClosesTheForm_AndReturnsFocusToEdit()
    {
        var fake = Fake();
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Abbrechen", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-editor")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Editors_AreOnlyForTheOwner_ListOthersAsCheckboxes_AndSaveTheChoice()
    {
        var clara = Guid.Parse("00000000-0000-0000-0000-0000000000d2");
        var fake = Fake(isMine: true, editors: [clara]);
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;
        var group = page.GetByRole(AriaRole.Group, new() { Name = "Wer darf diese Angaben bearbeiten?" });

        await Assertions.Expect(group.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(2);
        await Assertions.Expect(group.GetByRole(AriaRole.Checkbox, new() { Name = "Anna Beispiel" })).ToHaveCountAsync(0);
        await Assertions.Expect(group.GetByRole(AriaRole.Checkbox, new() { Name = "Clara Beispiel" })).ToBeCheckedAsync();
        await group.GetByRole(AriaRole.Checkbox, new() { Name = "Ben Beispiel" }).CheckAsync();
        await group.GetByRole(AriaRole.Button, new() { Name = "Bearbeiter speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Bearbeiter gespeichert.");
        using var body = JsonDocument.Parse(fake.Mocks.BodyOf("PUT", $"/api/documents/{fake.Id}/editors")!);
        var ids = body.RootElement.GetProperty("editorIds").EnumerateArray().Select(e => e.GetGuid()).Order().ToArray();
        Assert.Equal([Guid.Parse("00000000-0000-0000-0000-0000000000d1"), clara], ids);
    }

    [Fact]
    public async Task Editors_NotShown_ToSomeoneWhoDoesNotOwnTheDocument()
    {
        var fake = Fake(isMine: false);
        await using var session = await fake.OpenViewerAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Wer darf diese Angaben bearbeiten?")).ToHaveCountAsync(0);
    }
}
