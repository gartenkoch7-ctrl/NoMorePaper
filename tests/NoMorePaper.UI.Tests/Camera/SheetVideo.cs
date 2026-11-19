using System.Text;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// Writes the "video" Chromium's fake camera plays (<c>--use-file-for-fake-video-capture</c>): one Y4M
/// frame, 1280 × 720, of a white, slightly skewed sheet with a few text lines on a dark table. The sheet
/// lies right of the centre (hint "move right"), covers 39 % of the frame and no corner touches the
/// border, so the camera takes it automatically. Generated per run: no binary file in the repository.
/// </summary>
internal static class SheetVideo
{
    public const int Width = 1280;
    public const int Height = 720;

    /// <summary>The sheet's corners in pixels: top-left, top-right, bottom-right, bottom-left.</summary>
    public static readonly (double X, double Y)[] Corners = [(500, 50), (1120, 80), (1100, 670), (520, 660)];

    private const byte Table = 40;
    private const byte Paper = 235;
    private const byte Ink = 60;

    public static string Write(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "sheet.y4m");
        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"YUV4MPEG2 W{Width} H{Height} F10:1 Ip A1:1 C420jpeg\nFRAME\n"));
        file.Write(Luma());

        // 4:2:0 chroma at half resolution, neutral (grey, white and black have no colour).
        var chroma = new byte[Width / 2 * (Height / 2)];
        Array.Fill(chroma, (byte)128);
        file.Write(chroma);
        file.Write(chroma);
        return path;
    }

    private static byte[] Luma()
    {
        var luma = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                luma[(y * Width) + x] = !Inside(x + 0.5, y + 0.5) ? Table : IsText(x, y) ? Ink : Paper;
            }
        }

        return luma;
    }

    // Lines of "text" well inside the sheet: edges inside the outline, as on a real letter.
    private static bool IsText(int x, int y) =>
        x is >= 620 and <= 1000 && y is >= 150 and <= 590 && (y - 150) % 40 < 8;

    // Inside the convex quad: on the same side of all four edges (corners run clockwise on screen).
    private static bool Inside(double x, double y)
    {
        for (var i = 0; i < Corners.Length; i++)
        {
            var (ax, ay) = Corners[i];
            var (bx, by) = Corners[(i + 1) % Corners.Length];
            if (((bx - ax) * (y - ay)) - ((by - ay) * (x - ax)) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
