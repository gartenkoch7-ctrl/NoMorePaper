using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Trash;

/// <summary>The trash page when a call fails: conflict, other errors, a failed load (Stitch 15, M4b P3c).</summary>
public sealed class TrashErrorTests(ServerFixture server)
{
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private static ILocator Status(IPage page) => page.Locator("main .live-region").First;

    [Fact]
    public async Task ConflictOnRestore_SaysSo_AndShowsTheListAsItIsNow()
    {
        var fake = new FakeTrash(FakeTrash.Entry("Mietvertrag"), FakeTrash.Entry("Stromrechnung")) { RestoreStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();

        await Assertions.Expect(page.Locator("main .form-error")).ToContainTextAsync("Der Papierkorb hat sich inzwischen geändert. Die Liste ist neu geladen.");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task OtherFailure_IsAnnouncedAsAnError_AndFocusStaysOnTheButton()
    {
        var fake = new FakeTrash(FakeTrash.Entry("Mietvertrag")) { PurgeStatus = 500 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Button(page, "Endgültig löschen Mietvertrag").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, endgültig löschen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main .form-error")).ToContainTextAsync("Das war nicht möglich:");
        await Assertions.Expect(Button(page, "Endgültig löschen Mietvertrag")).ToBeFocusedAsync();

        // A later success must not leave the old error on screen.
        fake.PurgeStatus = 204;
        await Button(page, "Endgültig löschen Mietvertrag").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, endgültig löschen" }).ClickAsync();
        await Assertions.Expect(Status(page)).ToHaveTextAsync("„Mietvertrag“ endgültig gelöscht.");
        await Assertions.Expect(page.Locator("main .form-error")).ToBeEmptyAsync();
    }

    [Fact]
    public async Task LoadError_ShowsAnAlert_AndRetryLoadsTheList_AndFocusesTheHeading()
    {
        var fake = new FakeTrash(FakeTrash.Entry("Mietvertrag")) { ListStatus = 500 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Der Papierkorb konnte nicht geladen werden");
        fake.ListStatus = 200;
        await Button(page, "Erneut versuchen").ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Rowheader)).ToHaveTextAsync(["Mietvertrag"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ConflictWhoseReloadFails_ShowsOnlyTheLoadError()
    {
        var fake = new FakeTrash(FakeTrash.Entry("Mietvertrag")) { RestoreStatus = 409 };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        fake.ListStatus = 500;

        await Button(page, "Wiederherstellen Mietvertrag").ClickAsync();

        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Der Papierkorb konnte nicht geladen werden");
        await Assertions.Expect(page.Locator("main .form-error")).ToBeEmptyAsync();
    }
}
