namespace NoMorePaper.Domain.Documents;

/// <summary>A tag on a document (table <c>document_tags</c>); the tag itself is a list entry.</summary>
public sealed class DocumentTag
{
    /// <param name="tagId">Id of the <see cref="Classification.Tag"/>.</param>
    public DocumentTag(Guid tagId) => TagId = tagId;

    public Guid TagId { get; }
}
