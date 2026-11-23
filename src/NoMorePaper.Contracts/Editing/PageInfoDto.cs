namespace NoMorePaper.Contracts.Editing;

/// <summary>What is known about one page.</summary>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="Head">The start of its recognized text (at most 300 characters).</param>
/// <param name="ProbablyBlank">Hardly any text and ink.</param>
/// <param name="HadTextLayer">The original page had text already; cropping replaces it by an image that is recognized anew.</param>
public sealed record PageInfoDto(int Page, string Head, bool ProbablyBlank, bool HadTextLayer);
