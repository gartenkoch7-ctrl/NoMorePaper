using Microsoft.Playwright;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Trash;

/// <summary>The trash page: list, restore, delete for good, empty (Stitch 15, M4b P3c).</summary>
public sealed class TrashPageTests(ServerFixture server)
{
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private static ILocator Status(IPage page) => page.Locator("main .live-region").First;

    private static (TrashItemDto, TrashItemDto, TrashItemDto) Three() => (
        FakeTrash.Entry("Mietvertrag"), FakeTrash.Entry("Stromrechnung", TrashItemKindDto.Original), FakeTrash.Entry("Brief Finanzamt", TrashItemKindDto.Edited));

    [Fact]
    public async Task List_ShowsATableWithTextColumns_AndButtonsThatNameTheTitle()
    {
        var (first, second, third) = Three();
        await using var session = await new FakeTrash(first, second, third).OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Papierkorb");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync("Gelöschte Einträge (3)");
        await Assertions.Expect(page.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Titel", "Was", "Gelöscht am", "Endgültig gelöscht am", "Aktionen"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Rowheader)).ToHaveTextAsync(["Mietvertrag", "Stromrechnung", "Brief Finanzamt"]);
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Stromrechnung" });
        await Assertions.Expect(row).ToContainTextAsync("Original");
        await Assertions.Expect(row).ToContainTextAsync("28.02.2026");
        await Assertions.Expect(row).ToContainTextAsync("30.03.2026");
        await Assertions.Expect(page.Locator(".trash-table-scroll")).ToHaveAttributeAsync("tabindex", "0");
        await Assertions.Expect(Button(page, "Wiederherstellen Mietvertrag")).ToContainTextAsync("Wiederherstellen");
        await Assertions.Expect(Button(page, "Endgültig löschen Mietvertrag")).ToContainTextAsync("Endgültig löschen");
        await Assertions.Expect(Button(page, "Papierkorb leeren")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Restore_RemovesTheEntry_AnnouncesIt_AndFocusesTheSameButtonOfTheNextRow()
    {
        var (first, second, third) = Three();
        var fake = new FakeTrash(first, second, third);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();

        await Assertions.Expect(Status(page)).ToHaveTextAsync("„Mietvertrag“ wiederhergestellt.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync("Gelöschte Einträge (2)");
        await Assertions.Expect(Button(page, "Wiederherstellen Stromrechnung")).ToBeFocusedAsync();
        Assert.Contains($"POST /api/trash/{first.Id}/restore", fake.Calls);
    }

    [Fact]
    public async Task Restore_OfTheLastRow_FocusesThePreviousOne_AndOfTheOnlyEntryTheHeading()
    {
        var (first, second, _) = Three();
        await using var session = await new FakeTrash(first, second).OpenAsync(server);
        var page = session.Page;

        await Button(page, "Wiederherstellen Stromrechnung").ClickAsync();
        await Assertions.Expect(Button(page, "Wiederherstellen Mietvertrag")).ToBeFocusedAsync();
        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByText("Der Papierkorb ist leer.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Restore_WhenThePlaceIsTaken_AsksForTheSwapFirst_AndSaysWhereTheOldFileWent()
    {
        var swap = FakeTrash.Entry("Mietvertrag", TrashItemKindDto.Original, swaps: true);
        var fake = new FakeTrash(swap, FakeTrash.Entry("Brief"));
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();

        var question = page.GetByRole(AriaRole.Group, new() { Name = "„Mietvertrag“ wiederherstellen?" });
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Assertions.Expect(question).ToContainTextAsync("Beim Wiederherstellen tauschen beide die Plätze");
        Assert.Empty(fake.Calls);
        Assert.DoesNotContain("button-primary", await Button(page, "Abbrechen").GetAttributeAsync("class"), StringComparison.Ordinal);
        await Assertions.Expect(question.GetByRole(AriaRole.Button)).ToHaveTextAsync(["Abbrechen", "Ja, tauschen"]);

        await question.GetByRole(AriaRole.Button, new() { Name = "Ja, tauschen" }).ClickAsync();

        await Assertions.Expect(Status(page)).ToHaveTextAsync("„Mietvertrag“ wiederhergestellt. Die bisherige Fassung liegt jetzt im Papierkorb.");
        await Assertions.Expect(page.Locator(".nmp-confirm:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Rowheader)).ToHaveTextAsync(["Brief", "Mietvertrag (ältere Fassung)"]);
    }

    [Fact]
    public async Task Purge_AsksUnderTheRow_EscapeCancelsAndReturnsToTheButton_ConfirmDeletes()
    {
        var (first, second, _) = Three();
        var fake = new FakeTrash(first, second);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Endgültig löschen Mietvertrag").ClickAsync();
        var question = page.GetByRole(AriaRole.Group, new() { Name = "„Mietvertrag“ endgültig löschen?" });
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Assertions.Expect(question).ToContainTextAsync("Das kann nicht rückgängig gemacht werden.");
        await Assertions.Expect(page.GetByRole(AriaRole.Row)).ToHaveCountAsync(4);

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(question).ToHaveCountAsync(0);
        await Assertions.Expect(Button(page, "Endgültig löschen Mietvertrag")).ToBeFocusedAsync();
        Assert.Empty(fake.Calls);

        await Button(page, "Endgültig löschen Mietvertrag").ClickAsync();
        await question.GetByRole(AriaRole.Button, new() { Name = "Ja, endgültig löschen" }).ClickAsync();

        await Assertions.Expect(Status(page)).ToHaveTextAsync("„Mietvertrag“ endgültig gelöscht.");
        await Assertions.Expect(Button(page, "Endgültig löschen Stromrechnung")).ToBeFocusedAsync();
        Assert.Equal([$"DELETE /api/trash/{first.Id}"], fake.Calls);
    }

    [Fact]
    public async Task Purge_AskingForAnotherRow_ReplacesTheQuestion_AndReadsTheNewOne()
    {
        var (first, second, _) = Three();
        await using var session = await new FakeTrash(first, second).OpenAsync(server);
        var page = session.Page;

        await Button(page, "Endgültig löschen Mietvertrag").ClickAsync();
        await Button(page, "Endgültig löschen Stromrechnung").ClickAsync();

        await Assertions.Expect(page.Locator(".nmp-confirm:visible")).ToHaveCountAsync(1);
        var question = page.GetByRole(AriaRole.Group, new() { Name = "„Stromrechnung“ endgültig löschen?" });
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Empty_AsksWithTheCount_ThenEmptiesAndFocusesTheHeading()
    {
        var (first, second, third) = Three();
        var fake = new FakeTrash(first, second, third);
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Papierkorb leeren").ClickAsync();
        var question = page.GetByRole(AriaRole.Group, new() { Name = "Papierkorb wirklich leeren?" });
        await Assertions.Expect(question).ToContainTextAsync("3 Einträge werden endgültig gelöscht.");
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Button(page, "Papierkorb leeren")).ToBeFocusedAsync();
        await Button(page, "Papierkorb leeren").ClickAsync();
        await question.GetByRole(AriaRole.Button, new() { Name = "Ja, leeren" }).ClickAsync();

        await Assertions.Expect(Status(page)).ToHaveTextAsync("Papierkorb geleert.");
        await Assertions.Expect(page.GetByText("Der Papierkorb ist leer.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
        await Assertions.Expect(Button(page, "Papierkorb leeren")).ToHaveCountAsync(0);
        Assert.Equal(["DELETE /api/trash"], fake.Calls);
    }

    [Fact]
    public async Task AFileOfATrashedDocument_ShowsTheReasonInsteadOfARestoreButton()
    {
        var file = FakeTrash.Entry("Mietvertrag", TrashItemKindDto.Edited, documentInTrash: true);
        await using var session = await new FakeTrash(file).OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.Locator(".trash-table .trash-note")).ToBeVisibleAsync();
        await Assertions.Expect(Button(page, "Wiederherstellen Mietvertrag")).ToHaveCountAsync(0);
        await Assertions.Expect(Button(page, "Endgültig löschen Mietvertrag")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NarrowScreen_ShowsCardsInsteadOfTheTable_WithTheSameButtons()
    {
        var (first, second, _) = Three();
        await using var session = await new FakeTrash(first, second).OpenAsync(server, width: 375);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Article)).ToHaveCountAsync(2);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 3 })).ToHaveTextAsync(["Mietvertrag", "Stromrechnung"]);
        var card = page.GetByRole(AriaRole.Article).Nth(1);
        await Assertions.Expect(card).ToContainTextAsync("Original");
        await Assertions.Expect(card.GetByRole(AriaRole.Term)).ToHaveTextAsync(["Gelöscht am", "Endgültig gelöscht am"]);

        await Button(page, "Endgültig löschen Stromrechnung").ClickAsync();
        var question = card.GetByRole(AriaRole.Group, new() { Name = "„Stromrechnung“ endgültig löschen?" });
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Button(page, "Endgültig löschen Stromrechnung")).ToBeFocusedAsync();
        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();
        await Assertions.Expect(Button(page, "Wiederherstellen Stromrechnung")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task EnglishTexts_AreUsed()
    {
        await using var session = await new FakeTrash(FakeTrash.Entry("Lease")).OpenAsync(server, "en");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Trash");
        await Assertions.Expect(Button(page, "Restore Lease")).ToContainTextAsync("Restore");
    }
}
