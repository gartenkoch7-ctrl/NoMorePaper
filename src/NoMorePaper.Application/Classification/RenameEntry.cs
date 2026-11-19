using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>Renames a list entry; only allowed for an account that may edit every document using it.</summary>
public sealed class RenameEntry(IClassificationStore store, ICurrentAccount current, ILogger<RenameEntry> logger)
{
    private const string Action = "rename-entry";

    public async Task<ChangeOutcome> RenameAsync(ClassificationKind kind, Guid entryId, string? name, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return ChangeOutcome.AccountGone;
        }

        if (!EntryNames.TryClean(name, out var cleaned))
        {
            logger.EntryChangeRefused(current.Id, ChangeOutcome.Invalid, Action, kind);
            return ChangeOutcome.Invalid;
        }

        var outcome = EntryOutcomes.From(await store.RenameAsync(current.Id, kind, entryId, cleaned, cancellationToken).ConfigureAwait(false));
        if (outcome == ChangeOutcome.Changed)
        {
            logger.EntryRenamed(current.Id, entryId, kind);
        }
        else
        {
            logger.EntryChangeRefused(current.Id, outcome, Action, kind);
        }

        return outcome;
    }
}
