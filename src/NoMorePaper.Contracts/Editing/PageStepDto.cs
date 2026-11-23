namespace NoMorePaper.Contracts.Editing;

/// <summary>One page of an edited document in its new place.</summary>
/// <param name="SourcePage">The page of the current version it comes from, starting at 1.</param>
/// <param name="Replaced">Cropped or straightened in the browser; its JPEG comes as form part <c>page-&lt;SourcePage&gt;</c>.</param>
public sealed record PageStepDto(int SourcePage, PageRotationDto Rotation, bool Replaced);
