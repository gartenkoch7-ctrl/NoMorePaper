using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>"Seiten bearbeiten": the question when the page is left with unsaved changes.</summary>
public sealed class PageEditorLeaveTests(ServerFixture server)
{
    [Fact]
    public async Task LeavingWithChanges_AsksInThePage_CancelStays_ConfirmLeaves()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();
        var box = page.GetByRole(AriaRole.Group, new() { Name = "Seite verlassen?" });

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Pfad" }).GetByRole(AriaRole.Link, new() { Name = "Dokumente" }).ClickAsync();
        await Assertions.Expect(box).ToBeVisibleAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await box.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" }).ClickAsync();
        await Assertions.Expect(box).ToHaveCountAsync(0);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/seiten$"));

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Pfad" }).GetByRole(AriaRole.Link, new() { Name = "Dokumente" }).ClickAsync();
        await box.GetByRole(AriaRole.Button, new() { Name = "Ja, verlassen" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/dokumente$"));
    }

    [Fact]
    public async Task LeavingWithoutChanges_NeedsNoQuestion()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Pfad" }).GetByRole(AriaRole.Link, new() { Name = "Dokumente" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/dokumente$"));
    }
}
