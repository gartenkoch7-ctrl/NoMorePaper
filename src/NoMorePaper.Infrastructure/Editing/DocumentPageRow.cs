namespace NoMorePaper.Infrastructure.Editing;

/// <summary>A row of <c>document_pages</c>: what is known about one page of a document.</summary>
internal sealed class DocumentPageRow
{
    public Guid DocumentId { get; set; }

    public int Page { get; set; }

    public string Head { get; set; } = string.Empty;

    public bool ProbablyBlank { get; set; }

    public bool HadTextLayer { get; set; }
}
