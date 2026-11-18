using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Pages far larger than paper (a photo packed into a PDF without a resolution): the analysis
/// render is capped below every canvas and texture limit, so viewer and thumbnail still work.
/// </summary>
public sealed class LargePageTests(ServerFixture server)
{
    // 9000 x 12000 points: at 144 dpi 18000 x 24000 pixels, over the canvas area limit of
    // browsers and the texture limit of every GPU.
    private static byte[] HugePage() => ViewerPdfs.BlueBox(9000, 12000);

    [Fact]
    public async Task AnalysisScale_Is144Dpi_UntilTheLongerSideReaches4096PixelsOrTheTextureLimit()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var scales = await session.Page.EvaluateAsync<double[]>("""
            async () => {
              const { analysisScale } = await import('/js/viewer/analysis.js');
              return [
                analysisScale([595, 842], 16384),
                analysisScale([9000, 12000], 16384),
                analysisScale([9000, 12000], 2048),
                analysisScale([3000, 2000]),
              ];
            }
            """);

        double[] expected = [2, 4096.0 / 12000, 2048.0 / 12000, 4096.0 / 3000];
        Assert.Equal(expected.Length, scales.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], scales[i], 9);
        }
    }

    [Fact]
    public async Task HugePage_IsShownInTheViewer_WithItsText()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, HugePage());
        var page = viewer.Page;

        await ViewerPages.WaitUntilShownAsync(page);

        await Assertions.Expect(page.Locator(".viewer-text")).ToContainTextAsync("Blaue Box");
        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task HugePage_GetsAThumbnail_ThroughTheShader()
    {
        await using var session = await ThumbnailPages.OpenListAsync(server, HugePage());
        var page = session.Page;

        await Assertions.Expect(ThumbnailPages.Canvas(page)).ToBeVisibleAsync();

        var colours = await ThumbnailPages.ReadAsync(page, ViewerPdfs.BoxPoint, ViewerPdfs.PaperPoint);
        BlueBoxColours.AssertShader(colours[0], colours[1]);
    }
}
