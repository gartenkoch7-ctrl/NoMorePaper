using System.Globalization;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>An sRGB colour with 8 bits per channel.</summary>
internal readonly record struct Rgb(int R, int G, int B)
{
    public static Rgb Parse(string hex) => new(
        int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    /// <summary>Largest difference of one channel.</summary>
    public int DistanceTo(Rgb other) =>
        Math.Max(Math.Abs(R - other.R), Math.Max(Math.Abs(G - other.G), Math.Abs(B - other.B)));

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");
}
