namespace NoMorePaper.Domain.Editing;

/// <summary>One page of an edited document, in its new place.</summary>
/// <param name="SourcePage">The page of the current source it comes from, starting at 1.</param>
/// <param name="Rotation">How far it is turned clockwise.</param>
/// <param name="Replaced">The browser cropped or straightened it; it is replaced by an uploaded image (ADR-0025).</param>
public sealed record PageStep(int SourcePage, PageRotation Rotation, bool Replaced);
