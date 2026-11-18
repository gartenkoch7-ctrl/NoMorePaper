using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The document list shows every state as text, reports errors accessibly and follows progress.</summary>
public sealed class DocumentListTests(ServerFixture server)
{
    [Fact]
    public async Task List_ShowsTitleDateAndStateAsText_ForEveryState()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => ApiMocks.ListAsync(page, SampleDocuments.OneOfEachState()));
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("caption")).ToHaveTextAsync("Alle Dokumente, neueste zuerst");
        await Assertions.Expect(page.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Titel", "Eingang", "Status"]);
        await ExpectRowAsync(page, "Stromrechnung", "In Warteschlange");
        await ExpectRowAsync(page, "Mietvertrag", "Wird verarbeitet");
        await ExpectRowAsync(page, "Steuerbescheid", "Fertig");
        await ExpectRowAsync(page, "Kaputter Scan", "Fehlgeschlagen");
    }

    [Fact]
    public async Task List_InEnglish_UsesEnglishStateTexts()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.English, "Documents",
            page => ApiMocks.ListAsync(page, SampleDocuments.OneOfEachState()));

        await ExpectRowAsync(session.Page, "Stromrechnung", "Queued");
        await ExpectRowAsync(session.Page, "Kaputter Scan", "Failed");
    }

    [Fact]
    public async Task List_Empty_SaysSo()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => ApiMocks.ListAsync(page, Array.Empty<DocumentSummaryDto>()));

        await Assertions.Expect(session.Page.Locator("main")).ToContainTextAsync("Noch keine Dokumente.");
    }

    [Fact]
    public async Task ApiError_ShowsAlert_InsteadOfCrashing_AndRetryRecovers()
    {
        var fail = true;
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => page.RouteAsync(new System.Text.RegularExpressions.Regex(@"/api/documents\?"), route => fail
                ? route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = "{}" })
                : route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "[]",
                })));
        var page = session.Page;

        var alert = page.Locator("main [role=alert]");
        await Assertions.Expect(alert).ToContainTextAsync("Die Dokumente konnten nicht geladen werden.");
        await Assertions.Expect(alert).ToContainTextAsync("Code 500");

        fail = false;
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Erneut versuchen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Noch keine Dokumente.");
        await Assertions.Expect(alert).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task StatusChange_WhileProcessing_IsAnnouncedAndShown()
    {
        var calls = 0;
        var id = Guid.NewGuid();
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => page.RouteAsync(new System.Text.RegularExpressions.Regex(@"/api/documents\?"), route =>
            {
                var state = Interlocked.Increment(ref calls) == 1 ? "Processing" : "Done";
                return route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = $$"""[{"id":"{{id}}","title":"Brief","receivedAt":"2026-11-18T10:00:00Z","state":"{{state}}"}]""",
                });
            }));
        var page = session.Page;

        await ExpectRowAsync(page, "Brief", "Wird verarbeitet");

        // The list polls every 3 seconds while a document is in progress.
        var announcement = page.Locator("main .live-region");
        await Assertions.Expect(announcement).ToContainTextAsync("Brief: Status Fertig");
        await ExpectRowAsync(page, "Brief", "Fertig");
    }

    private static Task ExpectRowAsync(IPage page, string title, string state)
        => Assertions.Expect(page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = title }))
            .ToContainTextAsync(state);
}
