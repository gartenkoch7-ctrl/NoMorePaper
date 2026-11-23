namespace NoMorePaper.Application.Editing;

/// <summary>The stored page information of documents (<c>document_pages</c>), written with the archive by the queue.</summary>
public interface IDocumentPages
{
    /// <returns>The pages in order; empty for documents processed before page information existed.</returns>
    Task<IReadOnlyList<PageInfo>> ListAsync(Guid id, CancellationToken cancellationToken);
}
