using NoMorePaper.Application.Editing;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>Planned edits in memory.</summary>
internal sealed class FakeDocumentEdits : IDocumentEdits
{
    public Dictionary<Guid, DocumentEdit> Edits { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task SaveAsync(DocumentEdit edit, CancellationToken cancellationToken)
    {
        Edits[edit.DocumentId] = edit;
        return Task.CompletedTask;
    }

    public Task<DocumentEdit?> GetAsync(Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(Edits.GetValueOrDefault(documentId));

    public Task DeleteAsync(Guid documentId, CancellationToken cancellationToken)
    {
        Deleted.Add(documentId);
        Edits.Remove(documentId);
        return Task.CompletedTask;
    }
}
