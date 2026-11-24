namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// A point on a page in tenths of a percent from the left and from the top (0–1000). Whole numbers keep the
/// steps of the arrow keys (1 % and 0.2 %) exact, however often a corner is moved.
/// </summary>
public readonly record struct CropPoint(int X, int Y)
{
    /// <summary>The far edge of the page: 100 %.</summary>
    public const int Scale = 1000;

    /// <summary>The point moved onto the page.</summary>
    public CropPoint Clamped() => new(Math.Clamp(X, 0, Scale), Math.Clamp(Y, 0, Scale));
}
