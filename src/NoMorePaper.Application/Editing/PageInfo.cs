namespace NoMorePaper.Application.Editing;

/// <summary>What is known about one page of a document (table <c>document_pages</c>).</summary>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="Head">The start of the recognized text, at most <see cref="MaxHeadLength"/> characters.</param>
/// <param name="ProbablyBlank">Hardly any text and hardly any ink: probably an empty back side or separator sheet.</param>
/// <param name="HadTextLayer">The page of the original already had text (a digital PDF, not a scan).</param>
public sealed record PageInfo(int Page, string Head, bool ProbablyBlank, bool HadTextLayer)
{
    /// <summary>Longest start of text kept per page.</summary>
    public const int MaxHeadLength = 300;
}
