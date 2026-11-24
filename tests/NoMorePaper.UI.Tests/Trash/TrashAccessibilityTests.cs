using Microsoft.Playwright;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Trash;

/// <summary>axe-core finds no violations on the trash page, in both languages and in each state.</summary>
public sealed class TrashAccessibilityTests(ServerFixture server)
{
    public static TheoryData<string> Cultures() => new(SupportedCultures.All);

    private static FakeTrash Sample() => new(
        FakeTrash.Entry("Mietvertrag"),
        FakeTrash.Entry("Stromrechnung", TrashItemKindDto.Original, swaps: true),
        FakeTrash.Entry("Brief Finanzamt", TrashItemKindDto.Edited, documentInTrash: true));

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task List_HasNoAxeViolations(string culture)
    {
        await using var session = await Sample().OpenAsync(server, culture);
        await Assertions.Expect(session.Page.GetByRole(AriaRole.Rowheader)).ToHaveCountAsync(3);

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task List_WithTheRowQuestionAndTheEmptyQuestionOpen_HasNoAxeViolations(string culture)
    {
        await using var session = await Sample().OpenAsync(server, culture);
        var page = session.Page;
        var german = culture == SupportedCultures.German;

        await page.GetByRole(AriaRole.Button, new() { Name = german ? "Wiederherstellen Stromrechnung" : "Restore Stromrechnung" }).ClickAsync();
        await Assertions.Expect(page.Locator(".nmp-confirm:visible")).ToHaveCountAsync(1);
        await AxeCheck.NoViolationsAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = german ? "Papierkorb leeren" : "Empty the trash" }).ClickAsync();
        await Assertions.Expect(page.Locator(".nmp-confirm:visible")).ToHaveCountAsync(1);
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Empty_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakeTrash().OpenAsync(server, culture);
        await Assertions.Expect(session.Page.Locator(".trash-empty-state")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task NarrowList_WithAQuestionOpen_HasNoAxeViolations(string culture)
    {
        await using var session = await Sample().OpenAsync(server, culture, width: 375);
        var page = session.Page;
        await Assertions.Expect(page.GetByRole(AriaRole.Article)).ToHaveCountAsync(3);
        await AxeCheck.NoViolationsAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = culture == SupportedCultures.German ? "Endgültig löschen Mietvertrag" : "Delete permanently Mietvertrag" }).ClickAsync();
        await Assertions.Expect(page.Locator(".nmp-confirm:visible")).ToHaveCountAsync(1);

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task LoadError_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakeTrash { ListStatus = 500 }.OpenAsync(server, culture);
        await Assertions.Expect(session.Page.Locator(".nmp-notice-error")).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
