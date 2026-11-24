using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>A crop frame on a page and the page's size in PDF points (for its shape on screen and the centimetres).</summary>
public sealed record CropShape(CropQuad Quad, double WidthPt, double HeightPt)
{
    /// <summary>
    /// What the frame cuts off and how far it turns the sheet, on the page as it is seen: turned by
    /// <paramref name="rotation"/>, the stored left edge is the top one.
    /// </summary>
    public CropMeasure Measure(PageRotationDto rotation = PageRotationDto.None)
    {
        CropPoint Seen(CropCorner corner) => CropGeometry.VisualPoint(Quad, rotation, corner);
        var quarter = rotation is PageRotationDto.Right or PageRotationDto.Left;
        var seen = new CropQuad(Seen(CropCorner.TopLeft), Seen(CropCorner.TopRight), Seen(CropCorner.BottomRight), Seen(CropCorner.BottomLeft));
        return CropMeasure.Of(seen, quarter ? HeightPt : WidthPt, quarter ? WidthPt : HeightPt);
    }
}
