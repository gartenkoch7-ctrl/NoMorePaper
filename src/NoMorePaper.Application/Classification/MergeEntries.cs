using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// Merges list entries into one: their documents move to the target, the sources are deleted. Only
/// allowed for an account that may edit every document using any of them.
/// </summary>
public sealed class MergeEntries(IClassificationStore store, ICurrentAccount current, ILogger<MergeEntries> logger)
{
    private const string Action = "merge-entries";

    /// <param name="sourceIds">At least one entry, not the target; <c>null</c> is invalid.</param>
    public async Task<ChangeOutcome> MergeAsync(
        ClassificationKind kind, Guid targetId, IReadOnlyCollection<Guid>? sourceIds, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return ChangeOutcome.AccountGone;
        }

        var sources = sourceIds?.Distinct().ToList() ?? [];
        if (sources.Count == 0 || sources.Contains(targetId))
        {
            logger.EntryChangeRefused(current.Id, ChangeOutcome.Invalid, Action, kind);
            return ChangeOutcome.Invalid;
        }

        var outcome = EntryOutcomes.From(await store.MergeAsync(current.Id, kind, targetId, sources, cancellationToken).ConfigureAwait(false));
        if (outcome == ChangeOutcome.Changed)
        {
            logger.EntriesMerged(current.Id, sources.Count, kind, targetId);
        }
        else
        {
            logger.EntryChangeRefused(current.Id, outcome, Action, kind);
        }

        return outcome;
    }
}
