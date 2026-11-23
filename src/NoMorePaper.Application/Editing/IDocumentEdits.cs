namespace NoMorePaper.Application.Editing;

/// <summary>Planned edits in the database (<c>document_edits</c>); every write joins the caller's open change.</summary>
public interface IDocumentEdits
{
    /// <summary>Records the edit, replacing a planned edit of the same document.</summary>
    Task SaveAsync(DocumentEdit edit, CancellationToken cancellationToken);

    /// <returns>The planned edit of the document, or <c>null</c>.</returns>
    Task<DocumentEdit?> GetAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Removes the planned edit of the document; does nothing when there is none.</summary>
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken);
}
