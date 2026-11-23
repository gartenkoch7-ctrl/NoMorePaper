namespace NoMorePaper.Domain.Editing;

/// <summary>How far a page is turned clockwise, in degrees, relative to how it is stored now.</summary>
public enum PageRotation
{
    None = 0,
    Right = 90,
    Half = 180,
    Left = 270,
}
