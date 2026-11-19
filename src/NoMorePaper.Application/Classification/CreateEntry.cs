using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// Adds an entry to a list ("neu anlegen"): the entry visible to the signed-in account with the same
/// normalized name, or a new one that only this account sees until a document uses it.
/// </summary>
public sealed class CreateEntry(IClassificationStore store, ICurrentAccount current, ILogger<CreateEntry> logger)
{
    private const string Action = "create-entry";

    /// <returns><see cref="ChangeOutcome.Changed"/> with the entry, <see cref="ChangeOutcome.Invalid"/> for an invalid name, or <see cref="ChangeOutcome.AccountGone"/>.</returns>
    public async Task<CreatedEntry> CreateAsync(ClassificationKind kind, string? name, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return new CreatedEntry(ChangeOutcome.AccountGone, null);
        }

        if (!EntryNames.TryClean(name, out var cleaned))
        {
            logger.EntryChangeRefused(current.Id, ChangeOutcome.Invalid, Action, kind);
            return new CreatedEntry(ChangeOutcome.Invalid, null);
        }

        var entry = await store.FindOrCreateAsync(current.Id, kind, cleaned, EntryAudience.Account, cancellationToken).ConfigureAwait(false);
        logger.EntryFoundOrCreated(current.Id, entry.Id, kind);
        return new CreatedEntry(ChangeOutcome.Changed, entry);
    }
}
