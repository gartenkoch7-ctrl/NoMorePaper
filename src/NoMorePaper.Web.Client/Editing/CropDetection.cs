namespace NoMorePaper.Web.Client.Editing;

/// <summary>What <see cref="CropInterop.DetectAsync"/> found on a page.</summary>
/// <param name="Found">A sheet edge was found; otherwise <see cref="Shape"/> covers the whole page.</param>
/// <param name="Shape">The frame to start from; <c>null</c> when it failed.</param>
/// <param name="ElapsedMs">Time for loading the edge detection, drawing the page and looking for the sheet.</param>
/// <param name="Error">
/// Why it failed: <c>opencv</c> (the edge detection could not be loaded), <c>load</c> (the page could not be
/// drawn) or <c>interop</c>; <c>null</c> when it worked.
/// </param>
public sealed record CropDetection(bool Found, CropShape? Shape, int ElapsedMs, string? Error);
