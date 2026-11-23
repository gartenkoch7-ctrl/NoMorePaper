using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>"Listen": rename and merge senders, document types and tags, with a confirmation and clear refusals.</summary>
public sealed class ListsPageTests(ServerFixture server)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private async Task<BrowserSession> OpenAsync(
        AccountMocks mocks, Func<EntryDto[]> correspondents, int renameStatus = 204, int mergeStatus = 204, string culture = SupportedCultures.German)
    {
        var fake = new FakeAccounts();
        var telekom = ClassificationSamples.Telekom.Id;
        return await server.OpenAsync("/listen", culture, signedIn: true, prepare: async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await mocks.RespondAsync(page, "GET", "/api/review", 200, Array.Empty<ReviewItemDto>());
            await page.RouteAsync(new Regex("/api/classification/correspondents$"), route => route.Request.Method == "GET"
                ? route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(correspondents(), _json),
                })
                : route.FallbackAsync());
            await mocks.RespondAsync(page, "GET", "/api/classification/document-types", 200, ClassificationSamples.Types());
            await mocks.RespondAsync(page, "GET", "/api/classification/tags", 200, ClassificationSamples.Tags());
            await mocks.RespondAsync(page, "PUT", $"/api/classification/correspondents/{telekom}", renameStatus);
            await mocks.RespondAsync(page, "POST", "/api/classification/correspondents/merge", mergeStatus);
        });
    }

    [Fact]
    public async Task Page_HasASectionPerList_WithRowsThatNameTheirButtonsAndCheckboxes()
    {
        await using var session = await OpenAsync(new AccountMocks(), ClassificationSamples.Correspondents);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Absender", "Dokumentarten", "Tags"]);
        var table = page.GetByRole(AriaRole.Table, new() { Name = "Absender, alphabetisch" });
        await Assertions.Expect(table.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Name", "Zum Zusammenführen", "Aktionen"]);
        await Assertions.Expect(table.GetByRole(AriaRole.Rowheader)).ToHaveTextAsync(["Stadtwerke München", "Telekom", "Vodafone"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Telekom" })).Not.ToBeCheckedAsync();
    }

    [Fact]
    public async Task Rename_AsksForTheNewName_SavesWithEnter_AnnouncesIt_AndKeepsFocusOnTheRow()
    {
        var mocks = new AccountMocks();
        var current = ClassificationSamples.Correspondents();
        await using var session = await OpenAsync(mocks, () => current);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" }).ClickAsync();
        var box = page.GetByRole(AriaRole.Group, new() { Name = "„Telekom“ umbenennen" });
        var input = box.GetByLabel("Neuer Name");
        await Assertions.Expect(input).ToBeFocusedAsync();
        await Assertions.Expect(input).ToHaveValueAsync("Telekom");
        current = [ClassificationSamples.StadtwerkeMuenchen, new EntryDto(ClassificationSamples.Telekom.Id, "Deutsche Telekom"), ClassificationSamples.Vodafone];
        await input.FillAsync("Deutsche Telekom");
        await input.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".list-section").First.Locator(".live-region").First).ToHaveTextAsync("„Telekom“ heißt jetzt „Deutsche Telekom“.");
        Assert.Equal("""{"name":"Deutsche Telekom"}""", mocks.BodyOf("PUT", $"/api/classification/correspondents/{ClassificationSamples.Telekom.Id}"));
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Deutsche Telekom" })).ToBeFocusedAsync();
        await Assertions.Expect(box).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Rename_EnterOnTheCancelButton_DoesNotSave()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, ClassificationSamples.Correspondents);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" }).ClickAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "„Telekom“ umbenennen" })).ToHaveCountAsync(0);
        Assert.False(mocks.WasCalled("PUT", $"/api/classification/correspondents/{ClassificationSamples.Telekom.Id}"));
    }

    [Fact]
    public async Task Path_LinksBackToTheSettings()
    {
        await using var session = await OpenAsync(new AccountMocks(), ClassificationSamples.Correspondents);

        await Assertions.Expect(session.Page.Locator(".nmp-breadcrumb").GetByRole(AriaRole.Link, new() { Name = "Einstellungen" }))
            .ToHaveAttributeAsync("href", "einstellungen");
    }

    [Fact]
    public async Task Rename_EscapeCancels_WithoutAskingTheServer()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, ClassificationSamples.Correspondents);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" }).ClickAsync();
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "„Telekom“ umbenennen" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" })).ToBeFocusedAsync();
        Assert.False(mocks.WasCalled("PUT", $"/api/classification/correspondents/{ClassificationSamples.Telekom.Id}"));
    }

    [Fact]
    public async Task Rename_WithoutTheRight_ExplainsWhy()
    {
        await using var session = await OpenAsync(new AccountMocks(), ClassificationSamples.Correspondents, renameStatus: 403);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Umbenennen: Telekom" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Neuen Namen speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-section").First.Locator(".form-error"))
            .ToContainTextAsync("Du darfst diesen Eintrag nicht ändern, weil du nicht alle Dokumente bearbeiten darfst");
    }

    [Fact]
    public async Task Merge_NeedsTwoEntries_ThenAsksWithTheSafeChoiceFirst_AndAnnouncesTheResult()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, ClassificationSamples.Correspondents);
        var page = session.Page;
        var section = page.Locator(".list-section").First;

        await section.GetByRole(AriaRole.Button, new() { Name = "Ausgewählte zusammenführen" }).ClickAsync();
        await Assertions.Expect(section.Locator(".form-error")).ToContainTextAsync("Wähle mindestens zwei Einträge zum Zusammenführen aus.");

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Telekom" }).CheckAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Vodafone" }).CheckAsync();
        await Assertions.Expect(section.Locator(".list-selected")).ToHaveTextAsync("2 ausgewählt");
        await section.GetByRole(AriaRole.Button, new() { Name = "Ausgewählte zusammenführen" }).ClickAsync();

        var box = page.GetByRole(AriaRole.Group, new() { Name = "2 Einträge zusammenführen?" });
        await Assertions.Expect(box).ToContainTextAsync("Das lässt sich nicht rückgängig machen.");
        await Assertions.Expect(box.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await box.GetByLabel("Dieser Eintrag bleibt erhalten").SelectOptionAsync(new SelectOptionValue { Label = "Vodafone" });
        await box.GetByRole(AriaRole.Button, new() { Name = "Ja, zusammenführen" }).ClickAsync();

        await Assertions.Expect(section.Locator(".live-region").First).ToHaveTextAsync("2 Einträge zu „Vodafone“ zusammengeführt.");
        using var body = JsonDocument.Parse(mocks.BodyOf("POST", "/api/classification/correspondents/merge")!);
        Assert.Equal(ClassificationSamples.Vodafone.Id, body.RootElement.GetProperty("targetId").GetGuid());
        Assert.Equal([ClassificationSamples.Telekom.Id], body.RootElement.GetProperty("sourceIds").EnumerateArray().Select(e => e.GetGuid()));
    }

    [Fact]
    public async Task Merge_EscapeCancels_AndReturnsFocusToTheMergeButton()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, ClassificationSamples.Correspondents);
        var page = session.Page;

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Telekom" }).CheckAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Vodafone" }).CheckAsync();
        await page.Locator(".list-section").First.GetByRole(AriaRole.Button, new() { Name = "Ausgewählte zusammenführen" }).ClickAsync();
        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "2 Einträge zusammenführen?" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".list-section").First.GetByRole(AriaRole.Button, new() { Name = "Ausgewählte zusammenführen" })).ToBeFocusedAsync();
        Assert.False(mocks.WasCalled("POST", "/api/classification/correspondents/merge"));
    }

    [Fact]
    public async Task Merge_WithoutTheRight_ExplainsWhy()
    {
        await using var session = await OpenAsync(new AccountMocks(), ClassificationSamples.Correspondents, mergeStatus: 403);
        var page = session.Page;

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Telekom" }).CheckAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Zum Zusammenführen auswählen: Vodafone" }).CheckAsync();
        await page.Locator(".list-section").First.GetByRole(AriaRole.Button, new() { Name = "Ausgewählte zusammenführen" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, zusammenführen" }).ClickAsync();

        await Assertions.Expect(page.Locator(".list-section").First.Locator(".form-error"))
            .ToContainTextAsync("Du darfst diese Einträge nicht zusammenführen, weil du nicht alle Dokumente bearbeiten darfst");
    }

    [Fact]
    public async Task Page_InEnglish_UsesEnglishTexts()
    {
        await using var session = await OpenAsync(new AccountMocks(), ClassificationSamples.Correspondents, culture: SupportedCultures.English);

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Senders", "Document types", "Tags"]);
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Button, new() { Name = "Rename: Telekom" })).ToBeVisibleAsync();
    }
}
