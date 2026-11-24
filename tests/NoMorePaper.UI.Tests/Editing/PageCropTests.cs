using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>
/// Cropping in "Seiten bearbeiten" with the real edge detection (OpenCV.js) on <see cref="EditingPdfs.SkewedSheets"/>:
/// the automatic crop and what it says, no sheet found, and setting the corners by keyboard only.
/// </summary>
public sealed partial class PageCropTests(ServerFixture server)
{
    /// <summary>Loading OpenCV.js (11 MB) in a Debug run takes a while on a busy runner (as for the camera).</summary>
    internal const float CropTimeoutMs = 90_000;

    [Fact]
    public async Task Automatic_StraightensTheSheet_SaysDegreesAndEdges_AndCanBeTakenBack()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var status = FakePageEditor.Status(page);

        await page.Locator("#pe-auto-1").ClickAsync();

        await Assertions.Expect(status).ToHaveTextAsync(AutoResult(), new() { Timeout = CropTimeoutMs });
        var card = FakePageEditor.Card(page, 1, 3);
        await Assertions.Expect(card.Locator(".pe-states")).ToContainTextAsync("Um 3 Grad gedreht, Rand oben");
        await Assertions.Expect(page.Locator("#pe-auto-1")).ToBeFocusedAsync();
        // The preview shows the frame of what is kept.
        await Assertions.Expect(card.Locator(".pe-crop-line")).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeEnabledAsync();

        await page.Locator("#pe-uncrop-1").ClickAsync();

        await Assertions.Expect(status).ToHaveTextAsync("Zuschnitt von Seite 1 zurückgenommen.");
        await Assertions.Expect(page.Locator("#pe-crop-1")).ToBeFocusedAsync();
        await Assertions.Expect(card.Locator(".pe-states")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Automatic_WithoutASheet_AsksToSetTheCornersByHand()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.Locator("#pe-auto-3").ClickAsync();

        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Kein Blattrand gefunden. Ecken von Hand setzen?", new() { Timeout = CropTimeoutMs });
        await page.GetByRole(AriaRole.Button, new() { Name = "Ecken von Hand setzen" }).ClickAsync();

        var topLeft = page.Locator("#pe-corner-0-3");
        await Assertions.Expect(topLeft).ToBeFocusedAsync(new() { Timeout = CropTimeoutMs });
        await Assertions.Expect(topLeft).ToHaveAccessibleNameAsync("Ecke oben links: 0 % von links, 0 % von oben");
        await Assertions.Expect(page.GetByText("Die Seite wird als Bild ersetzt und neu erkannt.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Manual_StartsAtTheFoundSheet()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.Locator("#pe-crop-2").ClickAsync();

        var topLeft = page.Locator("#pe-corner-0-2");
        await Assertions.Expect(topLeft).ToBeFocusedAsync(new() { Timeout = CropTimeoutMs });
        // The sheet's top left corner is about 9 % from the left and 13 % from the top of the page.
        await Assertions.Expect(topLeft).ToHaveAccessibleNameAsync(new Regex(@"^Ecke oben links: (8|9|10)(,\d)? % von links, 1[2-4](,\d)? % von oben$"));
    }

    [Fact]
    public async Task Keyboard_MovesTheCorners_SaysWhereTheyAreOnceTheKeysRest_AndEnterApplies()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        var status = FakePageEditor.Status(page);
        await page.Locator("#pe-rr-3").FocusAsync();

        await page.Keyboard.PressAsync("z");

        var topLeft = page.Locator("#pe-corner-0-3");
        await Assertions.Expect(topLeft).ToBeFocusedAsync(new() { Timeout = CropTimeoutMs });
        await Assertions.Expect(FakePageEditor.Card(page, 3, 3)).ToContainTextAsync("Zuschnitt aktiv");
        foreach (var key in new[] { "ArrowRight", "ArrowRight", "ArrowRight", "ArrowDown", "ArrowDown" })
        {
            await page.Keyboard.PressAsync(key);
        }

        await Assertions.Expect(topLeft).ToHaveAccessibleNameAsync("Ecke oben links: 3 % von links, 2 % von oben");
        await Assertions.Expect(status).ToHaveTextAsync("Ecke oben links: 3 % von links, 2 % von oben");
        await page.Keyboard.PressAsync("Shift+ArrowRight");
        await Assertions.Expect(status).ToHaveTextAsync("Ecke oben links: 3,2 % von links, 2 % von oben");
        // Arrow keys move the corner, not the page.
        Assert.Equal(0, await page.EvaluateAsync<double>("document.scrollingElement.scrollLeft"));

        await page.Keyboard.PressAsync("Tab");
        var topRight = page.Locator("#pe-corner-1-3");
        await Assertions.Expect(topRight).ToBeFocusedAsync();
        await Assertions.Expect(topRight).ToHaveAccessibleNameAsync("Ecke oben rechts: 100 % von links, 0 % von oben");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(status).ToHaveTextAsync("Zuschnitt für Seite 3 übernommen. Nicht gedreht, keine Ränder entfernt.", new() { Timeout = CropTimeoutMs });
        await Assertions.Expect(page.Locator("#pe-crop-3")).ToBeFocusedAsync();
        await Assertions.Expect(FakePageEditor.Card(page, 3, 3).Locator(".pe-states")).ToContainTextAsync("Nicht gedreht");
    }

    [Fact]
    public async Task Escape_CancelsTheCrop_AndGivesTheFocusBack()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-crop-3").FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("#pe-corner-0-3")).ToBeFocusedAsync(new() { Timeout = CropTimeoutMs });
        await page.Keyboard.PressAsync("ArrowDown");

        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("#pe-crop-3")).ToBeFocusedAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync("Zuschnitt abgebrochen.");
        await Assertions.Expect(page.Locator(".pe-crop-handle")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern" })).ToBeDisabledAsync();
    }

    // The centimetres depend on a pixel or two of the found outline; the angle and the order do not.
    [GeneratedRegex(@"^Um 3 Grad gedreht, Rand oben \d cm entfernt, Rand rechts \d cm entfernt, Rand unten \d cm entfernt, Rand links \d cm entfernt\.$")]
    private static partial Regex AutoResult();
}
