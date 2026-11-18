using Microsoft.Playwright;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Reads colours from what is really on the screen (a screenshot), so the test sees the composited
/// output of the shader. The PNG is decoded in the browser; no image library is needed.
/// </summary>
internal static class ScreenPixels
{
    private const string Decode = """
        async ([data, points]) => {
          const bytes = Uint8Array.from(atob(data), (c) => c.charCodeAt(0));
          const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }),
            { colorSpaceConversion: 'none', premultiplyAlpha: 'none' });
          const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
          const ctx = canvas.getContext('2d', { willReadFrequently: true });
          ctx.drawImage(bitmap, 0, 0);
          return points.map(([x, y]) => Array.from(ctx.getImageData(Math.round(x), Math.round(y), 1, 1).data.slice(0, 3)));
        }
        """;

    /// <summary>Page position (fractions of width and height) → colour on the screen.</summary>
    public static async Task<Rgb[]> ReadAsync(IPage page, params (double X, double Y)[] pageFractions)
    {
        // The text layer covers exactly the page, so its box is where the page is drawn.
        var box = await page.Locator(".viewer-text").BoundingBoxAsync()
            ?? throw new InvalidOperationException("Text layer not found.");
        var points = pageFractions
            .Select(p => new[] { box.X + (p.X * box.Width), box.Y + (p.Y * box.Height) })
            .ToArray();
        var png = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });
        var colours = await page.EvaluateAsync<int[][]>(Decode, new object[] { Convert.ToBase64String(png), points });
        return [.. colours.Select(c => new Rgb(c[0], c[1], c[2]))];
    }
}
