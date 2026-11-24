using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Seiten bearbeiten" by keyboard: keys on a card, undo for the whole page, Tab order.</summary>
public sealed class PageEditorKeyboardTests(ServerFixture server)
{
    [Fact]
    public async Task KeysOnACard_RotateDeleteMoveAndSplit_AndEveryOneIsAnnounced()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var status = FakePageEditor.Status(page);
        await page.Locator("#pe-rr-2").FocusAsync();

        await page.Keyboard.PressAsync("r");
        await Assertions.Expect(status).ToHaveTextAsync("Seite 2 nach rechts gedreht.");
        await page.Keyboard.PressAsync("Shift+R");
        await Assertions.Expect(status).ToHaveTextAsync("Seite 2 nach links gedreht.");
        await page.Keyboard.PressAsync("t");
        await Assertions.Expect(status).ToHaveTextAsync("Trennstelle nach Seite 2 gesetzt.");
        await page.Keyboard.PressAsync("Alt+ArrowUp");
        await Assertions.Expect(status).ToHaveTextAsync("Seite verschoben, jetzt Seite 1 von 3.");
        await Assertions.Expect(page.Locator("#pe-h-2")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Alt+ArrowDown");
        await Assertions.Expect(status).ToHaveTextAsync("Seite verschoben, jetzt Seite 2 von 3.");
        await page.Keyboard.PressAsync("Delete");
        await Assertions.Expect(status).ToHaveTextAsync("Seite 2 gelöscht.");
        await Assertions.Expect(page.Locator("#pe-del-2")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Delete");
        await Assertions.Expect(status).ToHaveTextAsync("Löschen von Seite 2 rückgängig gemacht.");
    }

    [Fact]
    public async Task KeysOutsideACard_ChangeNothing()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-append-button").FocusAsync();

        await page.Keyboard.PressAsync("r");
        await page.Keyboard.PressAsync("Delete");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToBeEmptyAsync();
    }

    [Fact]
    public async Task CtrlZ_UndoesTheLastChange_OnThePage_AndSaysWhenThereIsNothing()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var status = FakePageEditor.Status(page);
        await page.Locator("#pe-rr-1").FocusAsync();
        await page.Keyboard.PressAsync("r");
        await page.Locator("#pe-del-3").FocusAsync();
        await page.Keyboard.PressAsync("Delete");
        await Assertions.Expect(page.Locator("#pe-del-3")).ToHaveTextAsync("Löschen rückgängig");

        await page.Keyboard.PressAsync("Control+z");
        await Assertions.Expect(status).ToHaveTextAsync("Letzte Änderung rückgängig gemacht.");
        await Assertions.Expect(page.Locator("#pe-del-3")).ToHaveTextAsync("Seite löschen");
        // The button that had focus is gone or changed; focus is on the heading of the card the change was on.
        await Assertions.Expect(page.Locator("#pe-h-3")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Control+z");
        await Assertions.Expect(page.Locator(".pe-card").First).Not.ToContainTextAsync("Nach rechts gedreht");
        await page.Keyboard.PressAsync("Control+z");

        await Assertions.Expect(status).ToHaveTextAsync("Nichts zum Rückgängigmachen.");
    }

    [Fact]
    public async Task TabOrder_IsPath_ActionBar_ThenTheCardsInReadingOrder()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        // Start of the page content: the Tab order is counted from there.
        await page.Locator("main").EvaluateAsync("m => { m.setAttribute('tabindex', '-1'); m.focus(); m.removeAttribute('tabindex'); }");

        var order = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            order.Add((await FocusState.ReadAsync(page)).Id);
        }

        // Breadcrumb links have no id; the action bar follows, then the buttons of card 1 in reading order.
        Assert.Contains("pe-append-button", order);
        Assert.True(order.IndexOf("pe-append-button") < order.IndexOf("pe-rl-1"));
        Assert.True(order.IndexOf("pe-rl-1") < order.IndexOf("pe-rr-1"));
        Assert.True(order.IndexOf("pe-rr-1") < order.IndexOf("pe-down-1"));
        // Hidden or disabled controls never take a Tab stop: "Speichern" is disabled without a change.
        Assert.DoesNotContain("pe-save", order);
    }
}
