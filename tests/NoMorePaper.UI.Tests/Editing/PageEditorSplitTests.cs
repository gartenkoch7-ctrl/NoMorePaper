using Microsoft.Playwright;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Seiten bearbeiten": split points, their rows and the split suggestion of the review list.</summary>
public sealed class PageEditorSplitTests(ServerFixture server)
{
    [Fact]
    public async Task SplitHere_AddsARow_SaveSendsTheParts_AndLinksToReview()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.Locator("#pe-split-1").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Trennstelle nach Seite 1 gesetzt.");
        await Assertions.Expect(page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Trennstelle: Dokument 1 endet nach Seite 1" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu „Zu prüfen“" })).ToHaveAttributeAsync("href", "zu-pruefen");
        await Assertions.Expect(FakePageEditor.Status(page)).ToContainTextAsync("in 2 Dokumente geteilt");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{fake.Id}/split"));
        Assert.Contains("{\"parts\":[{\"steps\":[{\"sourcePage\":1,", fake.PlanBody("split"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveSplit_TakesTheRowAway_AndFocusesTheSplitButtonOfTheCard()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-split-1").ClickAsync();
        await Assertions.Expect(page.Locator("#pe-split-1")).ToHaveAttributeAsync("aria-pressed", "true");

        await page.GetByRole(AriaRole.Button, new() { Name = "Trennstelle entfernen" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Trennstelle entfernen" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#pe-split-1")).ToBeFocusedAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Trennstelle nach Seite 1 entfernt.");
    }

    [Fact]
    public async Task SplitLink_SetsTheSplitPointsAndDeletesTheBlankPageOfTheSuggestion()
    {
        var fake = new FakePageEditor { Suggestion = new SplitSuggestionDto([new PageRangeDto(1, 1), new PageRangeDto(2, 3)], [2]) };
        await using var session = await fake.OpenAsync(server, query: "?trennen=1");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Trennstelle: Dokument 1 endet nach Seite 1" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Seite 1 von 2", "Gelöschte Seite (vorher Seite 2)", "Seite 2 von 2"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeEnabledAsync();
        // The deleted card's buttons are named by its heading, not by the number of the page before it.
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "Gelöschte Seite (vorher Seite 2)" })).ToBeVisibleAsync();

        // Undoing the suggestion removes its split row; focus must not fall to the page body with the removed button.
        await page.Locator("#pe-split-remove-0").FocusAsync();
        await page.Keyboard.PressAsync("Control+z");
        await Assertions.Expect(page.Locator("#pe-split-remove-0")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#pe-heading")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task SplitPoint_ThatOnlyClosesDeletedPages_SaysSo_ShowsNoRow_AndSavesAsAnEdit()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-del-3").ClickAsync();

        await page.Locator("#pe-split-2").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync(
            "Trennstelle nach Seite 2 gesetzt. Sie teilt das Dokument erst, wenn davor und danach eine Seite bleibt.");
        await Assertions.Expect(page.Locator(".pe-split")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#pe-split-2")).ToHaveAttributeAsync("aria-pressed", "true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToContainTextAsync("Gespeichert. Die neue Fassung");
        Assert.True(fake.Mocks.WasCalled("POST", $"/api/documents/{fake.Id}/edit"));
    }

    [Fact]
    public async Task RemoveSplit_AfterADeletedCard_FocusesTheHeadingOfThatCard()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-split-2").ClickAsync();
        await page.Locator("#pe-del-2").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Trennstelle: Dokument 1 endet nach Seite 1" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Trennstelle entfernen" }).ClickAsync();

        // The deleted card has no "Hier teilen" button; focus must not fall back to the page body.
        await Assertions.Expect(page.Locator("#pe-h-2")).ToBeFocusedAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Trennstelle nach Seite 1 entfernt.");
    }
}
