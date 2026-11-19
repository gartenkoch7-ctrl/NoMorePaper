using System.Text.Json;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>The combobox (ARIA 1.2) and the tag picker, reached through the edit form of the viewer.</summary>
public sealed class EntryComboboxTests(ServerFixture server)
{
    private const string Input = "#fields-correspondent";

    private async Task<Opened> OpenEditorAsync()
    {
        var fake = new FakeFields(Guid.NewGuid(), ClassificationSamples.Automatic());
        var session = await fake.OpenViewerAsync(server);
        try
        {
            await session.Page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
            await Assertions.Expect(session.Page.Locator(Input)).ToBeVisibleAsync();
            return new Opened(fake, session);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    [Fact]
    public async Task Input_FollowsTheComboboxPattern_AndIsClosedAtFirst()
    {
        await using var opened = await OpenEditorAsync();
        var input = opened.Page.Locator(Input);

        await Assertions.Expect(opened.Page.GetByRole(AriaRole.Combobox, new() { Name = "Absender" })).ToHaveValueAsync("Stadtwerke München");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-controls", "fields-correspondent-list");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-autocomplete", "list");
        await Assertions.Expect(input).Not.ToHaveAttributeAsync("aria-activedescendant", new System.Text.RegularExpressions.Regex(".+"));
        await Assertions.Expect(opened.Page.Locator("#fields-correspondent-list")).ToHaveAttributeAsync("role", "listbox");
    }

    [Fact]
    public async Task ArrowDown_OpensTheListWithEveryEntry_AndActivatesTheFirst()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;

        await page.Locator(Input).PressAsync("ArrowDown");

        var options = page.GetByRole(AriaRole.Option);
        await Assertions.Expect(page.Locator(Input)).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(page.Locator("#fields-correspondent-list").GetByRole(AriaRole.Option)).ToHaveTextAsync(
            ["Stadtwerke München", "Telekom", "Vodafone"]);
        await Assertions.Expect(page.Locator(Input)).ToHaveAttributeAsync("aria-activedescendant", "fields-correspondent-option-0");
        await Assertions.Expect(page.Locator("#fields-correspondent-option-0")).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(options.First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".combobox .live-region").First).ToContainTextAsync("3 Treffer.");
    }

    [Fact]
    public async Task Typing_FiltersAndOffersToCreateLast_WithTheCountAnnounced()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;

        await page.Locator(Input).FillAsync("tele");

        var options = page.Locator("#fields-correspondent-list").GetByRole(AriaRole.Option);
        await Assertions.Expect(options).ToHaveTextAsync(["Telekom", "„tele“ neu anlegen"]);
        await Assertions.Expect(page.Locator(".combobox .live-region").First)
            .ToHaveTextAsync("1 Treffer. Die letzte Option legt einen neuen Eintrag an.");
    }

    [Fact]
    public async Task Typing_AnExistingName_OffersNoCreateOption()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;

        await page.Locator(Input).FillAsync("telekom");

        await Assertions.Expect(page.Locator("#fields-correspondent-list").GetByRole(AriaRole.Option)).ToHaveTextAsync(["Telekom"]);
    }

    [Fact]
    public async Task Keys_MoveThroughTheOptions_AndEnterPicksTheActiveOne()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;
        var input = page.Locator(Input);

        await input.PressAsync("ArrowDown");
        await input.PressAsync("ArrowDown");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-activedescendant", "fields-correspondent-option-1");
        await input.PressAsync("ArrowUp");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-activedescendant", "fields-correspondent-option-0");
        await input.PressAsync("End");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-activedescendant", "fields-correspondent-option-2");
        await input.PressAsync("Home");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-activedescendant", "fields-correspondent-option-0");
        await input.PressAsync("End");
        await input.PressAsync("Enter");

        await Assertions.Expect(input).ToHaveValueAsync("Vodafone");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.Locator(".combobox .live-region").First).ToHaveTextAsync("„Vodafone“ ausgewählt.");
        await Assertions.Expect(input).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Escape_ClosesTheList_AndASecondEscapeTakesBackWhatWasTyped()
    {
        await using var opened = await OpenEditorAsync();
        var input = opened.Page.Locator(Input);

        await input.FillAsync("xyz");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-expanded", "true");
        await input.PressAsync("Escape");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(input).ToHaveValueAsync("xyz");
        await input.PressAsync("Escape");

        await Assertions.Expect(input).ToHaveValueAsync("Stadtwerke München");
    }

    [Fact]
    public async Task ClickingAnOption_PicksIt()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;

        await page.Locator(Input).PressAsync("ArrowDown");
        await page.GetByRole(AriaRole.Option, new() { Name = "Telekom" }).ClickAsync();

        await Assertions.Expect(page.Locator(Input)).ToHaveValueAsync("Telekom");
    }

    [Fact]
    public async Task CreateOption_CreatesTheEntry_ThroughTheApi_AndSelectsIt()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;
        var created = new EntryDto(Guid.NewGuid(), "Neue Firma");
        await opened.Fake.Mocks.RespondAsync(page, "POST", "/api/classification/correspondents", 200, created);
        var input = page.Locator(Input);

        await input.FillAsync("Neue Firma");
        await input.PressAsync("ArrowDown");
        await Assertions.Expect(page.GetByRole(AriaRole.Option, new() { Name = "„Neue Firma“ neu anlegen" })).ToHaveAttributeAsync("aria-selected", "true");
        await input.PressAsync("Enter");

        await Assertions.Expect(input).ToHaveValueAsync("Neue Firma");
        Assert.Equal("""{"name":"Neue Firma"}""", opened.Fake.Mocks.BodyOf("POST", "/api/classification/correspondents"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Angaben gespeichert und bestätigt.");
        using var body = JsonDocument.Parse(opened.Fake.Mocks.BodyOf("PUT", $"/api/documents/{opened.Fake.Id}/fields")!);
        Assert.Equal(created.Id, body.RootElement.GetProperty("correspondentId").GetGuid());
    }

    [Fact]
    public async Task Enter_WithATypedButUnpickedName_NeverCreatesAnEntry()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;
        var input = page.Locator(Input);

        await input.FillAsync("Halbfertig");
        await input.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".combobox .live-region").First).ToHaveTextAsync("Wähle mit den Pfeiltasten einen Eintrag aus.");
        Assert.False(opened.Fake.Mocks.WasCalled("POST", "/api/classification/correspondents"));
    }

    [Fact]
    public async Task EmptyingTheField_AndLeavingIt_ClearsTheValue()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;

        await page.Locator(Input).FillAsync("");
        await page.Locator(Input).PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Angaben gespeichert und bestätigt.");
        using var body = JsonDocument.Parse(opened.Fake.Mocks.BodyOf("PUT", $"/api/documents/{opened.Fake.Id}/fields")!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("correspondentId").ValueKind);
    }

    [Fact]
    public async Task Tags_AreAListWithRemoveButtonsThatNameTheTag_AndAddThroughTheCombobox()
    {
        await using var opened = await OpenEditorAsync();
        var page = opened.Page;
        var tags = page.Locator(".tag-list");

        await Assertions.Expect(tags.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        await Assertions.Expect(tags.GetByRole(AriaRole.Listitem)).ToContainTextAsync("Strom");
        var add = page.Locator("#fields-tags-add");
        await add.FillAsync("Steu");
        await add.PressAsync("ArrowDown");
        await add.PressAsync("Enter");

        await Assertions.Expect(tags.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        await Assertions.Expect(add).ToHaveValueAsync("");
        await page.GetByRole(AriaRole.Button, new() { Name = "Tag entfernen: Strom" }).ClickAsync();
        await Assertions.Expect(page.Locator(".tag-picker .live-region").Last).ToHaveTextAsync("Tag „Strom“ entfernt.");
        await Assertions.Expect(add).ToBeFocusedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator(".fields-panel .live-region").First).ToHaveTextAsync("Angaben gespeichert und bestätigt.");
        using var body = JsonDocument.Parse(opened.Fake.Mocks.BodyOf("PUT", $"/api/documents/{opened.Fake.Id}/fields")!);
        Assert.Equal([ClassificationSamples.Steuer.Id], body.RootElement.GetProperty("tagIds").EnumerateArray().Select(e => e.GetGuid()));
    }

    private sealed class Opened(FakeFields fake, BrowserSession session) : IAsyncDisposable
    {
        public FakeFields Fake { get; } = fake;

        public IPage Page => session.Page;

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
