using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>"In den Papierkorb" in the viewer head: owner only, question first, no page change afterwards.</summary>
public sealed class MoveToTrashTests(ServerFixture server)
{
    private async Task<(ViewerSession Viewer, List<string> Calls)> OpenAsync(
        bool isMine, int status = 204, string culture = SupportedCultures.German, int? width = null)
    {
        var id = Guid.NewGuid();
        var calls = new List<string>();
        var file = new DocumentFileDto("brief.pdf", "application/pdf", 10, "0");
        var detail = new DocumentDetailDto(id, ViewerPages.Title, DateTimeOffset.UtcNow, DocumentProcessingState.Done, null, file, file, IsMine: isMine);
        var viewer = await ViewerPages.OpenAsync(server, culture, ViewerPdfs.TwoPages(), detail: detail, isMine: isMine,
            prepare: async page =>
            {
                if (width is { } w)
                {
                    await page.SetViewportSizeAsync(w, 900);
                }

                await page.RouteAsync(new Regex($"/api/documents/{id}/trash$"), route =>
                {
                    calls.Add(route.Request.Method);
                    return route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = status,
                        ContentType = status >= 400 ? "application/problem+json" : "application/json",
                        Body = status == 204 ? null : "{}",
                    });
                });
            });
        return (viewer, calls);
    }

    [Fact]
    public async Task NotTheOwner_HasNoButton()
    {
        var (viewer, _) = await OpenAsync(isMine: false);
        await using var _ = viewer;

        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Owner_AsksFirst_AndCancelReturnsToTheButton()
    {
        var (viewer, calls) = await OpenAsync(isMine: true);
        await using var _ = viewer;
        var page = viewer.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" }).ClickAsync();

        var question = page.GetByRole(AriaRole.Group, new() { Name = "„Testbrief“ in den Papierkorb legen?" });
        await Assertions.Expect(question.GetByRole(AriaRole.Heading)).ToBeFocusedAsync();
        await Assertions.Expect(question).ToContainTextAsync("Es bleibt 30 Tage im Papierkorb und lässt sich bis dahin wiederherstellen.");
        Assert.DoesNotContain("button-primary", await question.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb" }).GetAttributeAsync("class"), StringComparison.Ordinal);

        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(question).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" })).ToBeFocusedAsync();
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Confirm_ShowsTheResultWithALinkToTheList_FocusesIt_AndStaysOnThePage()
    {
        var (viewer, calls) = await OpenAsync(isMine: true);
        await using var _ = viewer;
        var page = viewer.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb" }).ClickAsync();

        var done = page.Locator(".trash-move-done");
        await Assertions.Expect(done).ToContainTextAsync("„Testbrief“ liegt jetzt im Papierkorb.");
        await Assertions.Expect(done).ToBeFocusedAsync();
        await Assertions.Expect(done.GetByRole(AriaRole.Link, new() { Name = "Zur Dokumentliste" })).ToHaveAttributeAsync("href", "dokumente");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" })).ToHaveCountAsync(0);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/dokumente/{viewer.Id}$"));
        Assert.Equal(["POST"], calls);
    }

    [Fact]
    public async Task Failure_IsAnnouncedAsAlert_AndTheButtonStaysWithFocus()
    {
        var (viewer, _) = await OpenAsync(isMine: true, status: 409);
        await using var _ = viewer;
        var page = viewer.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, in den Papierkorb" }).ClickAsync();

        await Assertions.Expect(page.Locator(".nmp-notice-error")).ToContainTextAsync("Das Dokument konnte nicht in den Papierkorb gelegt werden:");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb" })).ToBeFocusedAsync();
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(1024)]
    public async Task Layout_ButtonSitsWithTheDownloads_AndTheQuestionSpansTheHead(int width)
    {
        var (viewer, _) = await OpenAsync(isMine: true, width: width);
        await using var _ = viewer;
        var page = viewer.Page;
        var button = page.GetByRole(AriaRole.Button, new() { Name = "In den Papierkorb", Exact = true });

        await button.ClickAsync();
        await Assertions.Expect(page.Locator(".doc-view-head .nmp-confirm")).ToBeVisibleAsync();

        var original = (await page.Locator(".doc-view-downloads a").First.BoundingBoxAsync())!;
        var trash = (await button.BoundingBoxAsync())!;
        var head = (await page.Locator(".doc-view-head").BoundingBoxAsync())!;
        var question = (await page.Locator(".doc-view-head .nmp-confirm").BoundingBoxAsync())!;
        Assert.Equal(original.Y, trash.Y, 1);
        Assert.True(question.Y >= trash.Y + trash.Height, "The question is below the buttons.");
        Assert.True(question.Width >= head.Width * 0.8, $"The question spans the head ({question.Width} of {head.Width}).");
    }
}
