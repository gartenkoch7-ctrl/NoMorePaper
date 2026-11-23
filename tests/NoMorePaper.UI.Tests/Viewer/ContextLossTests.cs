using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// The browser may take the WebGL context away at any time (GPU reset, driver update). The user
/// hears about it, the page comes back when the context does, and otherwise the CSS fallback
/// takes over. Forced with WEBGL_lose_context.
/// </summary>
public sealed class ContextLossTests(ServerFixture server)
{
    private const string LoseContext = """
        () => {
          const gl = document.querySelector('.viewer-canvas canvas').getContext('webgl2');
          window.loseContextExtension = gl.getExtension('WEBGL_lose_context');
          window.loseContextExtension.loseContext();
        }
        """;

    private const string RestoreContext = "() => window.loseContextExtension.restoreContext()";

    // Holds the analysis renders (the only ones recording images): page 2 until the context is
    // back, page 1 until finishPageOneAnalysis(). That one returns whether page 1 was analysed
    // again, once the analysis is through and the viewer has used or dropped it.
    private const string HoldAnalyses = """
        async (url) => {
          const { openDocument } = await import('/js/viewer/pdf-document.js');
          const loading = openDocument(url);
          const proto = Object.getPrototypeOf(await (await loading.promise).getPage(1));
          await loading.destroy();
          const canvas = document.querySelector('.viewer-canvas canvas');
          const restored = new Promise((resolve) => canvas.addEventListener('webglcontextrestored', resolve, { once: true }));
          let release;
          const released = new Promise((resolve) => { release = resolve; });
          let pageOneDone = null;
          window.finishPageOneAnalysis = async () => {
            release();
            if (!pageOneDone) return false;
            await pageOneDone;
            await new Promise((resolve) => setTimeout(resolve, 0));
            return true;
          };
          const render = proto.render;
          proto.render = function (params) {
            const task = render.call(this, params);
            if (params.recordImages) {
              const gate = this.pageNumber === 1 ? released : restored;
              const done = task.promise.then(async (value) => { await gate; return value; });
              if (this.pageNumber === 1) pageOneDone = done;
              Object.defineProperty(task, 'promise', { value: done });
            }
            return task;
          };
        }
        """;

    [Fact]
    public async Task LostContext_IsAnnounced_AndThePageIsDrawnAgainWhenItIsBack()
    {
        await using var viewer = await OpenAsync();
        var page = viewer.Page;

        await page.EvaluateAsync(LoseContext);
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die Darstellung wurde unterbrochen");
        await page.EvaluateAsync(RestoreContext);

        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die Darstellung ist wieder da.");
        await NextFrameAsync(page);
        var colours = await ScreenPixels.ReadAsync(page, ViewerPdfs.BoxPoint, ViewerPdfs.PaperPoint);
        BlueBoxColours.AssertShader(colours[0], colours[1]);
    }

    [Fact]
    public async Task ContextThatCannotBeRebuilt_SwitchesToTheCssFallback()
    {
        await using var viewer = await OpenAsync();
        var page = viewer.Page;
        await page.EvaluateAsync(LoseContext);
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die Darstellung wurde unterbrochen");

        // From now on no shader compiles, so rebuilding the renderer fails.
        await page.EvaluateAsync("() => { WebGL2RenderingContext.prototype.createShader = () => null; }");
        await page.EvaluateAsync(RestoreContext);

        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("einfachen Modus");
        var canvas = page.Locator(".viewer-canvas canvas");
        await Assertions.Expect(canvas).ToHaveCountAsync(1);
        Assert.Equal("invert(1) hue-rotate(180deg)", await canvas.EvaluateAsync<string>("canvas => canvas.style.filter"));
        await NextFrameAsync(page);
        var colours = await ScreenPixels.ReadAsync(page, ViewerPdfs.BoxPoint, ViewerPdfs.PaperPoint);
        BlueBoxColours.AssertFallback(colours[0], colours[1]);
    }

    [Fact]
    public async Task ThumbnailDrawnWhileTheContextIsLost_IsDrawnAgain_InsteadOfShownBlack()
    {
        await using var session = await ThumbnailPages.OpenListAsync(server, ViewerPdfs.BlueBox(595, 842), page => page.EvaluateAsync("""
            async () => {
              const { ShaderRenderer } = await import('/js/viewer/shader-renderer.js');
              const thumbnail = ShaderRenderer.prototype.thumbnail;
              let first = true;
              ShaderRenderer.prototype.thumbnail = function (...args) {
                if (first) {
                  // The GPU goes away right before the first thumbnail reads its pixels back.
                  first = false;
                  const extension = this.gl.getExtension('WEBGL_lose_context');
                  extension.loseContext();
                  setTimeout(() => extension.restoreContext(), 500);
                }
                return thumbnail.apply(this, args);
              };
            }
            """));
        var page = session.Page;

        await Assertions.Expect(ThumbnailPages.Canvas(page)).ToBeVisibleAsync();

        var colours = await ThumbnailPages.ReadAsync(page, ViewerPdfs.BoxPoint, ViewerPdfs.PaperPoint);
        BlueBoxColours.AssertShader(colours[0], colours[1]);
    }

    [Fact]
    public async Task ContextBackDuringAPageChange_DrawsTheNewPageWithItsOwnAnalysis()
    {
        await using var viewer = await OpenAsync(ViewerPdfs.GreyPaperThenBlueBox());
        var page = viewer.Page;
        await page.EvaluateAsync(HoldAnalyses, $"api/documents/{viewer.Id}/files/archive");
        await page.EvaluateAsync(LoseContext);
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Die Darstellung wurde unterbrochen");

        // The page change starts while the context is lost; the restore then re-analyses the
        // page still shown (page 1), and that analysis finishes only after page 2 is on screen.
        await page.Locator(ViewerPages.Scroller).FocusAsync();
        await page.Keyboard.PressAsync("PageDown");
        await page.EvaluateAsync(RestoreContext);
        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Seite 2 von 2");
        Assert.True(await page.EvaluateAsync<bool>("() => window.finishPageOneAnalysis()"), "Page 1 was not analysed again");

        await NextFrameAsync(page);
        var colours = await ScreenPixels.ReadAsync(page, ViewerPdfs.BoxPoint, ViewerPdfs.PaperPoint);
        BlueBoxColours.AssertShader(colours[0], colours[1]);
    }

    private async Task<ViewerSession> OpenAsync(byte[]? pdf = null)
    {
        // Tall enough that the whole page is inside the screenshot, and wide enough for it: the viewer column is only
        // ~530 px wide next to the tabs, but A4 at 100 % is wider, so at 1280 px the measuring points lie outside it.
        var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, pdf ?? ViewerPdfs.BlueBox(595, 842),
            prepare: page => page.SetViewportSizeAsync(1800, 1600));
        await ViewerPages.WaitUntilShownAsync(viewer.Page);
        return viewer;
    }

    private static Task NextFrameAsync(IPage page) =>
        page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
}
