using NoMorePaper.Application.Editing;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>A row of <c>document_edits</c>: the edit a document waits for, at most one.</summary>
internal sealed class DocumentEditRow
{
    public Guid DocumentId { get; set; }

    public EditKind Kind { get; set; }

    /// <summary>The plan as JSON (<c>jsonb</c>).</summary>
    public string Data { get; set; } = "{}";

    public Guid RequestedBy { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public bool RecognizeAfter { get; set; }
}
