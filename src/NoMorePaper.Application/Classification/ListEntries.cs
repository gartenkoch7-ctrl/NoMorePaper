using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>The entries of a list that the signed-in account may see (visibility rule of the lists), ordered by name.</summary>
public sealed class ListEntries(IClassificationStore store, ICurrentAccount current)
{
    public Task<IReadOnlyList<EntrySummary>> ListAsync(ClassificationKind kind, CancellationToken cancellationToken) =>
        store.VisibleEntriesAsync(current.Id, kind, cancellationToken);
}
