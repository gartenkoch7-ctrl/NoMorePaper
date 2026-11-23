using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>One page of a composed PDF: taken from <paramref name="Path"/> and turned clockwise.</summary>
/// <param name="Path">A PDF in the working folder; never a received file name.</param>
/// <param name="Page">The page of that PDF, starting at 1.</param>
public sealed record PdfPageSource(string Path, int Page, PageRotation Rotation);
