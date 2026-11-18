using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>The documents the signed-in account may see, newest first, page by page.</summary>
public sealed class ListDocuments(IDocumentRepository documents, ICurrentAccount current)
{
    public const int MaxPageSize = 100;

    /// <summary>A page below 1 means the first page; <paramref name="pageSize"/> is clamped to 1–100.</summary>
    public Task<IReadOnlyList<DocumentSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        documents.ListAsync(current.Id, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);
}
