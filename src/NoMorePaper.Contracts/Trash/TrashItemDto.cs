namespace NoMorePaper.Contracts.Trash;

/// <summary>An entry of the trash of the signed-in account (<c>GET /api/trash</c>).</summary>
/// <param name="Title">The title of the document.</param>
/// <param name="PurgeAfter">From then on it is removed for good.</param>
/// <param name="RestoreSwaps">Restoring puts the file the document keeps now into the trash in its place.</param>
/// <param name="DocumentInTrash">The entry is a file of a document that lies in the trash itself; it cannot be restored alone.</param>
public sealed record TrashItemDto(
    Guid Id,
    TrashItemKindDto Kind,
    string Title,
    DateTimeOffset DeletedAt,
    DateTimeOffset PurgeAfter,
    bool RestoreSwaps,
    bool DocumentInTrash);
