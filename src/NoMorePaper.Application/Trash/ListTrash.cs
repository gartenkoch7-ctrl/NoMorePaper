using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Trash;

/// <summary>The trash page: the entries of the documents the signed-in account owns, newest first.</summary>
public sealed class ListTrash(ITrashStore trash, ICurrentAccount current)
{
    public Task<IReadOnlyList<TrashEntry>> ListAsync(CancellationToken cancellationToken) => trash.ListAsync(current.Id, cancellationToken);
}
