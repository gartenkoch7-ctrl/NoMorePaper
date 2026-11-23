using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>axe-core finds no violations on "Seiten bearbeiten" in its states, in both languages; narrow screens do not scroll sideways.</summary>
public sealed class PageEditorAccessibilityTests(ServerFixture server)
{
    public static TheoryData<string> Cultures() => new(SupportedCultures.All);

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Normal_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor().OpenAsync(server, culture);
        await Assertions.Expect(session.Page.Locator(".pe-card")).ToHaveCountAsync(3);

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task WithAChangedPage_ADeletedPageAndASplitPoint_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor().OpenAsync(server, culture);
        var page = session.Page;

        await page.Locator("#pe-rr-1").ClickAsync();
        await page.Locator("#pe-up-3").ClickAsync();
        await page.Locator("#pe-split-1").ClickAsync();
        await page.Locator("#pe-del-2").ClickAsync();

        await Assertions.Expect(page.Locator(".pe-split")).ToBeVisibleAsync();
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task WithTheDiscardQuestionOpen_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor().OpenAsync(server, culture);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();

        await page.Locator("#pe-discard-button").ClickAsync();

        await Assertions.Expect(page.Locator("#pe-discard-heading")).ToBeFocusedAsync();
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task WithTheAppendSectionAndItsQuestionOpen_HasNoAxeViolations(string culture)
    {
        var hit = new SearchHitDto(Guid.NewGuid(), "Mietvertrag", DateTimeOffset.UtcNow, "…", 1, IsPrivate: true, IsMine: true);
        await using var session = await new FakePageEditor { Hits = [hit] }.OpenAsync(server, culture);
        var page = session.Page;

        await page.Locator("#pe-append-button").ClickAsync();
        await page.Locator("#pe-append-search").FillAsync("miet");
        await page.Locator($"#pe-hit-{hit.Id:N}").ClickAsync();

        await Assertions.Expect(page.Locator($"#pe-append-{hit.Id:N}-heading")).ToBeFocusedAsync();
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task AfterSaving_HasNoAxeViolations(string culture)
    {
        await using var session = await new FakePageEditor().OpenAsync(server, culture);
        var page = session.Page;
        await page.Locator("#pe-rr-1").ClickAsync();

        await page.Locator("#pe-save").ClickAsync();

        await Assertions.Expect(page.Locator("#pe-saved-link")).ToBeFocusedAsync();
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task NoticeStates_HaveNoAxeViolations(string culture)
    {
        await using (var forbidden = await new FakePageEditor { CanEdit = false }.OpenAsync(server, culture))
        {
            await AxeCheck.NoViolationsAsync(forbidden.Page);
        }

        await using var pending = await new FakePageEditor { EditPending = true }.OpenAsync(server, culture);
        await AxeCheck.NoViolationsAsync(pending.Page);
    }

    [Fact]
    public async Task OnAPhone_TheListDoesNotScrollSideways()
    {
        var fake = new FakePageEditor();
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await page.SetViewportSizeAsync(375, 812);
        await page.Locator("#pe-split-1").ClickAsync();

        var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        Assert.False(overflow);
        // The buttons wrap below the preview instead of running out of the card.
        var card = await page.Locator(".pe-card").First.BoundingBoxAsync();
        var last = await page.Locator("#pe-split-1").BoundingBoxAsync();
        Assert.NotNull(card);
        Assert.NotNull(last);
        Assert.True(last.X + last.Width <= card.X + card.Width + 1);
    }
}
