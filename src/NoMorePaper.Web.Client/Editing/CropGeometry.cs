using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// Moves the corners of a crop frame as the user sees the page. The editor shows a page turned by its
/// <see cref="PageRotationDto"/>, while the frame is kept on the stored page; "top left" and "to the right" always
/// mean what is seen. Pure logic.
/// </summary>
public static class CropGeometry
{
    /// <summary>Corners stay this far apart (5 %), so the frame never folds over or collapses.</summary>
    public const int MinGap = 50;

    /// <summary>The stored corner that is shown at <paramref name="visual"/> when the page is turned.</summary>
    public static CropCorner StoredCorner(CropCorner visual, PageRotationDto rotation) =>
        (CropCorner)(((int)visual - Turns(rotation) + 4) % 4);

    /// <summary>Where a point of the stored page is seen on the turned page.</summary>
    public static CropPoint ToVisual(CropPoint point, PageRotationDto rotation) => Turns(rotation) switch
    {
        1 => new CropPoint(CropPoint.Scale - point.Y, point.X),
        2 => new CropPoint(CropPoint.Scale - point.X, CropPoint.Scale - point.Y),
        3 => new CropPoint(point.Y, CropPoint.Scale - point.X),
        _ => point,
    };

    /// <summary>The point of the stored page that is seen at <paramref name="point"/> on the turned page.</summary>
    public static CropPoint ToStored(CropPoint point, PageRotationDto rotation) =>
        ToVisual(point, (PageRotationDto)((360 - (int)rotation) % 360));

    /// <summary>Where the corner seen at <paramref name="visual"/> is, on the turned page.</summary>
    public static CropPoint VisualPoint(CropQuad quad, PageRotationDto rotation, CropCorner visual)
    {
        ArgumentNullException.ThrowIfNull(quad);
        return ToVisual(quad[StoredCorner(visual, rotation)], rotation);
    }

    /// <summary>Moves the corner seen at <paramref name="visual"/> by a step on the turned page (arrow keys).</summary>
    public static CropQuad MoveBy(CropQuad quad, PageRotationDto rotation, CropCorner visual, int dx, int dy)
    {
        var point = VisualPoint(quad, rotation, visual);
        return MoveTo(quad, rotation, visual, new CropPoint(point.X + dx, point.Y + dy));
    }

    /// <summary>
    /// Puts the corner seen at <paramref name="visual"/> at <paramref name="target"/> on the turned page (dragged
    /// with the mouse). It stays on the page and <see cref="MinGap"/> left of, right of, above or below the
    /// neighbouring corners, so the frame keeps its order.
    /// </summary>
    public static CropQuad MoveTo(CropQuad quad, PageRotationDto rotation, CropCorner visual, CropPoint target)
    {
        ArgumentNullException.ThrowIfNull(quad);
        CropPoint Seen(CropCorner corner) => VisualPoint(quad, rotation, corner);

        var left = visual is CropCorner.TopLeft or CropCorner.BottomLeft;
        var top = visual is CropCorner.TopLeft or CropCorner.TopRight;
        var across = Seen(Mirror(visual, horizontal: true));
        var below = Seen(Mirror(visual, horizontal: false));
        var x = left ? Math.Min(target.X, across.X - MinGap) : Math.Max(target.X, across.X + MinGap);
        var y = top ? Math.Min(target.Y, below.Y - MinGap) : Math.Max(target.Y, below.Y + MinGap);
        var moved = new CropPoint(x, y).Clamped();
        return quad.With(StoredCorner(visual, rotation), ToStored(moved, rotation));
    }

    private static int Turns(PageRotationDto rotation) => (int)rotation / 90 % 4;

    // The neighbour on the same row (horizontal) or in the same column.
    private static CropCorner Mirror(CropCorner corner, bool horizontal) => (corner, horizontal) switch
    {
        (CropCorner.TopLeft, true) => CropCorner.TopRight,
        (CropCorner.TopRight, true) => CropCorner.TopLeft,
        (CropCorner.BottomRight, true) => CropCorner.BottomLeft,
        (CropCorner.BottomLeft, true) => CropCorner.BottomRight,
        (CropCorner.TopLeft, false) => CropCorner.BottomLeft,
        (CropCorner.TopRight, false) => CropCorner.BottomRight,
        (CropCorner.BottomRight, false) => CropCorner.TopRight,
        _ => CropCorner.TopLeft,
    };
}
