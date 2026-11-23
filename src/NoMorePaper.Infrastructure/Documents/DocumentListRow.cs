using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>Shape of a document list row; property names equal the column aliases in <see cref="EfDocumentRepository"/>.</summary>
internal sealed class DocumentListRow : LabelRow
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }

    public string State { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    public bool IsMine { get; set; }

    public string Source { get; set; } = nameof(DocumentSource.Upload);

    public string? Excerpt { get; set; }
}
