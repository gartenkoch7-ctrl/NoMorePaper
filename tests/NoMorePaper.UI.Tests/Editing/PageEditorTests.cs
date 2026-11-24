using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Seiten bearbeiten": the cards, the buttons, saving and the states around them.</summary>
public sealed class PageEditorTests(ServerFixture server)
{
    private static ILocator Card(IPage page, int number, int total) => FakePageEditor.Card(page, number, total);

    private static ILocator Button(ILocator scope, string name) => scope.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Fact]
    public async Task Page_ShowsOneCardPerPage_WithTheStartOfItsText_AndNoButtonsThatMoveOutOfTheList()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Seiten bearbeiten – Stromrechnung");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Seite 1 von 3", "Seite 2 von 3", "Seite 3 von 3"]);
        await Assertions.Expect(Card(page, 1, 3)).ToContainTextAsync("Beginnt mit: Sehr geehrte Frau Muster");
        // A probably blank page says so and has no start of text.
        await Assertions.Expect(Card(page, 2, 3)).ToContainTextAsync("Vermutlich leer");
        await Assertions.Expect(Card(page, 2, 3)).Not.ToContainTextAsync("Beginnt mit");
        await Assertions.Expect(Button(Card(page, 1, 3), "Nach oben")).ToHaveCountAsync(0);
        await Assertions.Expect(Button(Card(page, 3, 3), "Nach unten")).ToHaveCountAsync(0);
        await Assertions.Expect(Button(Card(page, 3, 3), "Hier teilen")).ToHaveCountAsync(0);
        await Assertions.Expect(Button(Card(page, 2, 3), "Nach oben")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "Seite 1 bearbeiten" })).ToHaveAttributeAsync("aria-describedby", "pe-keys");
        // The preview is decoration: the heading and "Beginnt mit" name the page.
        await Assertions.Expect(page.Locator(".pe-card").First.Locator(".pe-preview[aria-hidden=true]")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Preview_OfPageTwo_ShowsThatPage()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        var canvas = page.Locator("canvas[data-title=\"Seite 2 von 3\"]");
        await Assertions.Expect(canvas).ToBeVisibleAsync();
        var (x, y) = EditingPdfs.BoxPoint;
        var pixel = await canvas.EvaluateAsync<int[]>(
            "(c, p) => Array.from(c.getContext('2d').getImageData(Math.floor(p[0] * c.width), Math.floor(p[1] * c.height), 1, 1).data.slice(0, 3))",
            new[] { x, y });
        Assert.True(pixel[2] > pixel[0] + 40 && pixel[2] > pixel[1] + 40, $"expected the blue box of page 2, got {string.Join(',', pixel)}");
    }

    [Fact]
    public async Task Rotate_TurnsThePreview_SaysSo_KeepsFocus_AndSaveSendsThePlan()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var rotate = Button(Card(page, 1, 3), "Nach rechts drehen");

        await rotate.ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Seite 1 nach rechts gedreht.");
        await Assertions.Expect(Card(page, 1, 3)).ToContainTextAsync("Nach rechts gedreht");
        await Assertions.Expect(Card(page, 1, 3).Locator(".pe-preview")).ToHaveClassAsync(new Regex("pe-rot-90"));
        await Assertions.Expect(rotate).ToBeFocusedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zum Dokument" })).ToBeFocusedAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToContainTextAsync("Gespeichert.");
        var body = fake.PlanBody("edit");
        Assert.Contains("\"sourcePage\":1,\"rotation\":\"Right\",\"replaced\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"sourcePage\":3,\"rotation\":\"None\"", body, StringComparison.Ordinal);
        // Read-only afterwards: the pages stay visible, nothing can be changed.
        await Assertions.Expect(page.GetByRole(AriaRole.Article)).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Nach rechts drehen" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MoveUp_KeepsFocusOnTheButtonOfTheMovedCard_UntilItIsFirst_ThenOnItsHeading()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.Locator("#pe-up-3").ClickAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Seite verschoben, jetzt Seite 2 von 3.");
        await Assertions.Expect(page.Locator("#pe-up-3")).ToBeFocusedAsync();
        await Assertions.Expect(Card(page, 2, 3)).ToContainTextAsync("Verschoben (vorher Seite 3)");

        await page.Locator("#pe-up-3").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Seite verschoben, jetzt Seite 1 von 3.");
        await Assertions.Expect(page.Locator("#pe-h-3")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator(".pe-card").First).ToContainTextAsync("Verschoben (vorher Seite 3)");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        Assert.Contains("\"steps\":[{\"sourcePage\":3,", fake.PlanBody("edit"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_CollapsesTheCard_RenumbersTheRest_AndCanBeUndone()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.Locator("#pe-del-2").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Seite 2 gelöscht.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Seite 1 von 2", "Gelöschte Seite (vorher Seite 2)", "Seite 2 von 2"]);
        await Assertions.Expect(page.Locator("#pe-del-2")).ToHaveTextAsync("Löschen rückgängig");
        await Assertions.Expect(page.Locator("#pe-del-2")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("canvas[data-title]")).ToHaveCountAsync(2);

        await page.Locator("#pe-del-2").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Löschen von Seite 2 rückgängig gemacht.");
        await Assertions.Expect(page.Locator("#pe-del-2")).ToHaveTextAsync("Seite löschen");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Delete_OfEveryPage_SaysOnePageMustStay_AndCannotBeSaved()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        foreach (var number in new[] { 1, 2, 3 })
        {
            await page.Locator($"#pe-del-{number}").ClickAsync();
        }

        await Assertions.Expect(page.Locator(".pe-explain").Last).ToHaveTextAsync("Alle Seiten sind gelöscht. Mindestens eine Seite muss bleiben.");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Save_WhenAnEditRuns_ShowsTheConflictText_AndKeepsTheChanges()
    {
        var fake = new FakePageEditor { SaveStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Das Dokument wird gerade bearbeitet oder hat sich geändert.");
        await Assertions.Expect(page.Locator("#pe-save")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#pe-save")).ToBeEnabledAsync();
        await Assertions.Expect(Card(page, 1, 3)).ToContainTextAsync("Nach rechts gedreht");
    }

    [Fact]
    public async Task Discard_AsksFirst_EscapeCancels_ConfirmDropsEveryChange()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();
        await page.Locator("#pe-del-3").ClickAsync();
        var box = page.GetByRole(AriaRole.Group, new() { Name = "Alle Änderungen verwerfen?" });

        await page.Locator("#pe-discard-button").ClickAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Button, new() { Name = "Ja, verwerfen" })).Not.ToHaveClassAsync(new Regex("nmp-button-primary"));
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(box).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#pe-discard-button")).ToBeFocusedAsync();
        await Assertions.Expect(Card(page, 1, 2)).ToBeVisibleAsync();

        await page.Locator("#pe-discard-button").ClickAsync();
        await box.GetByRole(AriaRole.Button, new() { Name = "Ja, verwerfen" }).ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Alle Änderungen verworfen.");
        await Assertions.Expect(page.Locator("#pe-heading")).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Seite 1 von 3", "Seite 2 von 3", "Seite 3 von 3"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
    }
}
