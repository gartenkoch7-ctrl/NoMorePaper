using System.Globalization;
using System.Text;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accessibility;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using NoMorePaper.Web.Client.Viewer;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>Viewer page: accessible in both languages, downloads stay original, states without a PDF, settings.</summary>
public sealed class ViewerPageTests(ServerFixture server)
{
    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task Viewer_HasNoAxeViolations(string culture)
    {
        await using var viewer = await ViewerPages.OpenAsync(server, culture, ViewerPdfs.TwoPages());
        await ViewerPages.WaitUntilShownAsync(viewer.Page);

        await AssertNoViolationsAsync(viewer.Page);
    }

    [Fact]
    public async Task DownloadLinks_PointToTheUnchangedFiles()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Original herunterladen" }))
            .ToHaveAttributeAsync("href", $"api/documents/{viewer.Id}/files/original");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Archiv-PDF (PDF/A) herunterladen" }))
            .ToHaveAttributeAsync("href", $"api/documents/{viewer.Id}/files/archive");
    }

    [Fact]
    public async Task ImageStillQueued_SaysItIsBeingProcessed_InsteadOfAViewer()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, [0xFF, 0xD8, 0xFF],
            archive: false, originalType: "image/jpeg", state: DocumentProcessingState.Queued);
        var page = viewer.Page;

        await Assertions.Expect(page.Locator("main [data-ready]")).ToContainTextAsync("wird noch verarbeitet");
        await Assertions.Expect(page.Locator(ViewerPages.Stage)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Original herunterladen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PdfOriginal_IsShownBeforeProcessing()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages(),
            archive: false, state: DocumentProcessingState.Queued);

        await ViewerPages.WaitUntilShownAsync(viewer.Page);
        await Assertions.Expect(viewer.Page.Locator(".viewer-text")).ToContainTextAsync("Seite eins mit Text");
    }

    [Fact]
    public async Task BrokenPdf_ShowsAlert_AndKeepsDownloads()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, [.. "kein pdf"u8]);
        var page = viewer.Page;

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync("konnte nicht angezeigt werden");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Original herunterladen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task OriginalColors_IsAToggleButton_RememberedOnThisDevice()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Originalfarben" });
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");

        await toggle.ClickAsync();

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Equal("true", await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", ViewerPreferences.StorageKey));
    }

    [Fact]
    public async Task WithoutWebGl_FallsBackToTheCssFilter()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages(),
            prepare: page => page.EvaluateAsync("""
                () => {
                  const original = HTMLCanvasElement.prototype.getContext;
                  HTMLCanvasElement.prototype.getContext = function (type, ...rest) {
                    return type === 'webgl2' || type === 'webgl' ? null : original.call(this, type, ...rest);
                  };
                }
                """));
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        var filter = await page.Locator(".viewer-canvas canvas").EvaluateAsync<string>("canvas => canvas.style.filter");
        Assert.Equal("invert(1) hue-rotate(180deg)", filter);
        await Assertions.Expect(page.Locator(".viewer-text")).ToContainTextAsync("Seite eins mit Text");
    }

    internal static async Task AssertNoViolationsAsync(IPage page)
    {
        var result = await page.RunAxe(AxeRules.Strict());
        var text = new StringBuilder();
        foreach (var violation in result.Violations)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{violation.Id} ({violation.Impact}): {violation.Help}");
            foreach (var node in violation.Nodes)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  {node.Target}: {node.Html}");
            }
        }

        Assert.True(result.Violations.Length == 0, text.ToString());
    }
}
