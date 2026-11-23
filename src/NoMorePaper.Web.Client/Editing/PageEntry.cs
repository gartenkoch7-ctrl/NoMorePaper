using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>One page of the document in the page editor: which source page it is and what is done to it.</summary>
/// <param name="SourcePage">Page number (from 1) in the document as it is stored.</param>
/// <param name="Rotation">Clockwise turn on top of the stored page.</param>
/// <param name="Deleted">The page is left out when saving; it stays in the list so the deletion can be undone.</param>
/// <param name="ReplacedJpegKey">Key of the cropped or straightened image that replaces the page; <c>null</c> when untouched.</param>
/// <param name="CropText">Spoken summary of the crop (angle, removed margins) shown on the card; <c>null</c> without a crop.</param>
public sealed record PageEntry(
    int SourcePage,
    PageRotationDto Rotation = PageRotationDto.None,
    bool Deleted = false,
    string? ReplacedJpegKey = null,
    string? CropText = null);
