using System.Text.Json;
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

/// <summary>What happens around a finished edit: no loading state, focus, and the right announcement.</summary>
public sealed class VersionsReloadTests(ServerFixture server)
{
    private const string De = SupportedCultures.German;
    private const float PollWaitMs = 20_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static DocumentDetailDto Detail(bool edited, bool pending)
    {
        var file = new DocumentFileDto("brief.pdf", "application/pdf", 100, "0");
        return new DocumentDetailDto(
            Guid.NewGuid(), ViewerPages.Title, new DateTimeOffset(2026, 11, 2, 9, 30, 0, TimeSpan.Zero),
            DocumentProcessingState.Done, null, file, file, IsMine: true,
            Edited: edited ? file : null, EditedAt: edited ? new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.Zero) : null,
            EditPending: pending, VersionPolicy: VersionPolicyDto.KeepBoth);
    }

    private Task<ViewerSession> OpenAsync(DocumentDetailDto detail, bool canEdit = false) =>
        ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), detail: detail, fields: ClassificationSamples.Automatic(canEdit));

    [Fact]
    public async Task AFinishedEdit_KeepsFocus_AndAnUnsavedFieldValue()
    {
        var detail = Detail(edited: false, pending: true);
        await using var viewer = await OpenAsync(detail, canEdit: true);
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        await page.Locator("#fields-edit").ClickAsync();
        var title = page.Locator("#fields-title");
        await title.FillAsync("Noch nicht gespeichert");
        await Assertions.Expect(title).ToBeFocusedAsync();

        // Polling sees the finished edit without any action of the user.
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{detail.Id}$"), "GET",
            detail with { EditPending = false, Edited = detail.Original, EditedAt = DateTimeOffset.UtcNow });

        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die neue Fassung ist fertig.", new() { Timeout = PollWaitMs });
        await Assertions.Expect(title).ToBeFocusedAsync();
        await Assertions.Expect(title).ToHaveValueAsync("Noch nicht gespeichert");
    }

    [Fact]
    public async Task AFinishedEdit_ReloadsTheDocumentOnce()
    {
        var detail = Detail(edited: false, pending: true);
        await using var viewer = await OpenAsync(detail);
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        var finished = JsonSerializer.Serialize(
            detail with { EditPending = false, Edited = detail.Original, EditedAt = DateTimeOffset.UtcNow },
            Json);
        var gets = 0;
        // Counted where the finished detail is served: only the requests that see the finished edit.
        await page.RouteAsync(new Regex($"/api/documents/{detail.Id}$"), route =>
        {
            if (route.Request.Method != "GET")
            {
                return route.ContinueAsync();
            }

            Interlocked.Increment(ref gets);
            return route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = finished });
        });
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die neue Fassung ist fertig.", new() { Timeout = PollWaitMs });
        // Longer than one poll interval (5 s): a second poll started during the reload would ask and reload again.
        await page.WaitForTimeoutAsync(6_500);

        // The poll that saw the finished edit, and the reload of the viewer.
        Assert.Equal(2, Volatile.Read(ref gets));
    }

    [Fact]
    public async Task KeepingOnlyTheOriginal_FocusesTheHeading_WhenTheChoiceDisappears()
    {
        var mocks = new AccountMocks();
        var detail = Detail(edited: true, pending: false);
        await using var viewer = await OpenAsync(detail);
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        await mocks.RespondAsync(page, "PUT", $"/api/documents/{detail.Id}/versions", 204);
        // The server dropped the edited version and rebuilds the archive from the original.
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{detail.Id}$"), "GET",
            detail with { Edited = null, EditedAt = null, EditPending = true, VersionPolicy = VersionPolicyDto.OriginalOnly });

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.Locator("#versions-save").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb legen" }).ClickAsync();

        await Assertions.Expect(page.Locator("#versions-save")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#versions-heading")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task TheRebuildAfterKeepingTheOriginal_DoesNotAnnounceANewVersion()
    {
        var mocks = new AccountMocks();
        var detail = Detail(edited: true, pending: false);
        await using var viewer = await OpenAsync(detail);
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        await mocks.RespondAsync(page, "PUT", $"/api/documents/{detail.Id}/versions", 204);
        var rebuilding = detail with { Edited = null, EditedAt = null, EditPending = true, VersionPolicy = VersionPolicyDto.OriginalOnly };
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{detail.Id}$"), "GET", rebuilding);

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.Locator("#versions-save").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb legen" }).ClickAsync();
        await Assertions.Expect(page.Locator("#versions-heading")).ToBeFocusedAsync();
        await ApiMocks.RespondAsync(page, new Regex($"/api/documents/{detail.Id}$"), "GET", rebuilding with { EditPending = false });

        var announcement = ViewerPages.Announcement(page);
        await Assertions.Expect(announcement).ToContainTextAsync("Das Dokument wird wieder aus dem Original angezeigt.", new() { Timeout = PollWaitMs });
        await Assertions.Expect(announcement).Not.ToContainTextAsync("Die neue Fassung ist fertig.");
    }
}
