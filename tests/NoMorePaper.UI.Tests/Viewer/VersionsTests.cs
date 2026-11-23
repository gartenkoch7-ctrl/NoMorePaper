using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>"Fassungen" in the tab "Angaben": the files, downloads, the owner's choice, a running edit.</summary>
public sealed class VersionsTests(ServerFixture server)
{
    private const string De = SupportedCultures.German;
    private const string Section = "section.versions";

    private static DocumentDetailDto Detail(
        bool edited = true, bool isMine = true, bool pending = false, VersionPolicyDto policy = VersionPolicyDto.KeepBoth)
    {
        var file = new DocumentFileDto("brief.pdf", "application/pdf", 100, "0");
        return new DocumentDetailDto(
            Guid.NewGuid(), ViewerPages.Title, new DateTimeOffset(2026, 11, 2, 9, 30, 0, TimeSpan.Zero),
            DocumentProcessingState.Done, null, file, file, IsMine: isMine,
            Edited: edited ? file : null, EditedAt: edited ? new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero) : null,
            EditPending: pending, VersionPolicy: policy);
    }

    private Task<ViewerSession> OpenAsync(DocumentDetailDto detail, string culture = De, bool canEdit = false) =>
        ViewerPages.OpenAsync(server, culture, ViewerPdfs.TwoPages(), detail: detail, fields: ClassificationSamples.Automatic(canEdit));

    [Fact]
    public async Task WithoutAnEdit_ShowsOnlyTheOriginal_AndNoChoice()
    {
        await using var viewer = await OpenAsync(Detail(edited: false));
        var section = viewer.Page.Locator(Section);

        await Assertions.Expect(section.GetByRole(AriaRole.Heading, new() { Name = "Fassungen" })).ToBeVisibleAsync();
        await Assertions.Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        await Assertions.Expect(section.GetByRole(AriaRole.Link, new() { Name = "Herunterladen: Original" }))
            .ToHaveAttributeAsync("href", $"api/documents/{viewer.Id}/files/original");
        await Assertions.Expect(section).ToContainTextAsync("Noch keine bearbeitete Fassung.");
        await Assertions.Expect(section.GetByRole(AriaRole.Radio)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task WithAnEdit_ListsBothVersions_WithDownloads_AndMarksTheShownOne()
    {
        await using var viewer = await OpenAsync(Detail());
        var section = viewer.Page.Locator(Section);

        await Assertions.Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        await Assertions.Expect(section.GetByText("Aktiv – wird angezeigt")).ToHaveCountAsync(1);
        await Assertions.Expect(section.GetByRole(AriaRole.Link, new() { Name = "Herunterladen: Bearbeitete Fassung" }))
            .ToHaveAttributeAsync("href", $"api/documents/{viewer.Id}/files/edited");
        await Assertions.Expect(section.GetByRole(AriaRole.Radio, new() { Name = "Beides" })).ToBeCheckedAsync();
        await Assertions.Expect(section.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }))
            .ToHaveAccessibleDescriptionAsync("Die bearbeitete Fassung kommt 30 Tage in den Papierkorb.");
    }

    [Fact]
    public async Task AnEditor_SeesTheList_ButNoChoice()
    {
        await using var viewer = await OpenAsync(Detail(isMine: false));
        var section = viewer.Page.Locator(Section);

        await Assertions.Expect(section.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        await Assertions.Expect(section.GetByRole(AriaRole.Radio)).ToHaveCountAsync(0);
        await Assertions.Expect(section.GetByRole(AriaRole.Button, new() { Name = "Auswahl speichern" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task KeepingOnlyOneVersion_AsksFirst_Escape_CancelsAndFocusesSave()
    {
        var mocks = new AccountMocks();
        await using var viewer = await OpenAsync(Detail());
        var page = viewer.Page;
        await mocks.RespondAsync(page, "PUT", $"/api/documents/{viewer.Id}/versions", 204);

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur bearbeitete Fassung" }).CheckAsync();
        await page.Locator("#versions-save").ClickAsync();

        var heading = page.GetByRole(AriaRole.Heading, new() { Name = "Original in den Papierkorb legen?" });
        await Assertions.Expect(heading).ToBeFocusedAsync();
        // Neutral: the confirm button is not the white main action; "Abbrechen" comes first.
        var buttons = page.Locator("[aria-labelledby=versions-confirm-heading] .nmp-confirm-actions button");
        await Assertions.Expect(buttons.First).ToHaveTextAsync("Abbrechen");
        await Assertions.Expect(buttons.Last).Not.ToHaveClassAsync(new Regex("nmp-button-primary"));

        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("#versions-confirm-heading")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#versions-save")).ToBeFocusedAsync();
        Assert.False(mocks.WasCalled("PUT", "/versions"));
    }

    [Fact]
    public async Task Confirming_SendsThePolicy_AndAnnouncesIt()
    {
        var mocks = new AccountMocks();
        await using var viewer = await OpenAsync(Detail());
        var page = viewer.Page;
        await mocks.RespondAsync(page, "PUT", $"/api/documents/{viewer.Id}/versions", 204);

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.Locator("#versions-save").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Bearbeitete Fassung in den Papierkorb legen?" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb legen" }).ClickAsync();

        await Assertions.Expect(page.Locator($"{Section} [role=status]").First).ToHaveTextAsync("Auswahl gespeichert.");
        Assert.Contains("\"policy\":\"OriginalOnly\"", mocks.BodyOf("PUT", "/versions"), StringComparison.Ordinal);
        await Assertions.Expect(page.Locator("#versions-save")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task AFailedSave_IsAnnouncedAsAnError()
    {
        var mocks = new AccountMocks();
        await using var viewer = await OpenAsync(Detail());
        var page = viewer.Page;
        await mocks.RespondAsync(page, "PUT", $"/api/documents/{viewer.Id}/versions", 409, new { status = 409 });

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.Locator("#versions-save").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb legen" }).ClickAsync();

        await Assertions.Expect(page.Locator($"{Section} .nmp-notice[role=alert]")).ToContainTextAsync("Die Auswahl konnte nicht gespeichert werden");
    }

    [Fact]
    public async Task EditPages_IsOffered_OnlyWhenEditable_AndNothingRuns()
    {
        await using var editable = await OpenAsync(Detail(), canEdit: true);
        await Assertions.Expect(editable.Page.GetByRole(AriaRole.Link, new() { Name = "Seiten bearbeiten" }))
            .ToHaveAttributeAsync("href", $"dokumente/{editable.Id}/seiten");

        await using var pending = await OpenAsync(Detail(pending: true), canEdit: true);
        await Assertions.Expect(pending.Page.Locator(Section)).ToContainTextAsync("Wird bearbeitet");
        await Assertions.Expect(pending.Page.GetByRole(AriaRole.Link, new() { Name = "Seiten bearbeiten" })).ToHaveCountAsync(0);

        await using var readOnly = await OpenAsync(Detail());
        await Assertions.Expect(readOnly.Page.GetByRole(AriaRole.Link, new() { Name = "Seiten bearbeiten" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task APendingEdit_IsPolled_AndTheNewVersionIsAnnounced()
    {
        var detail = Detail(pending: true);
        await using var viewer = await OpenAsync(detail);
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        // The server finishes the edit: the next poll sees it, the viewer loads the document again.
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{detail.Id}$"), "GET", detail with { EditPending = false });

        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die neue Fassung ist fertig.", new() { Timeout = 20_000 });
        await Assertions.Expect(page.Locator(Section)).Not.ToContainTextAsync("Wird bearbeitet");
    }

    [Theory]
    [InlineData(De)]
    [InlineData(SupportedCultures.English)]
    public async Task Versions_HaveNoAxeViolations(string culture)
    {
        await using var viewer = await OpenAsync(Detail(), culture, canEdit: true);
        await ViewerPages.WaitUntilShownAsync(viewer.Page);
        await AxeCheck.NoViolationsAsync(viewer.Page);

        await viewer.Page.Locator("#versions-edited").CheckAsync();
        await viewer.Page.Locator("#versions-save").ClickAsync();
        await Assertions.Expect(viewer.Page.Locator("#versions-confirm-heading")).ToBeVisibleAsync();
        await AxeCheck.NoViolationsAsync(viewer.Page);
    }

    [Theory]
    [InlineData(De)]
    [InlineData(SupportedCultures.English)]
    public async Task VersionsWithoutAnEditOrWhilePending_HaveNoAxeViolations(string culture)
    {
        await using (var none = await OpenAsync(Detail(edited: false), culture, canEdit: true))
        {
            await ViewerPages.WaitUntilShownAsync(none.Page);
            await AxeCheck.NoViolationsAsync(none.Page);
        }

        await using var pending = await OpenAsync(Detail(pending: true), culture, canEdit: true);
        await ViewerPages.WaitUntilShownAsync(pending.Page);
        await AxeCheck.NoViolationsAsync(pending.Page);
    }
}
