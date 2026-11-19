using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// "To review": the documents the signed-in account may edit that were recognized automatically or are a
/// possible duplicate of a document it may see, newest first.
/// </summary>
public sealed class ListReview(IDocumentFieldsStore documents, ICurrentAccount current)
{
    public Task<IReadOnlyList<ReviewItem>> ListAsync(CancellationToken cancellationToken) =>
        documents.ListReviewAsync(current.Id, cancellationToken);
}
