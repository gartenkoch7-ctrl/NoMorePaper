using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The signed-in account saves its preferences: new documents private or shared, and its mail senders.
/// On purpose any account may enter any address that no other account has: the household trusts each
/// other, and mails from that address then belong to the account (docs/kb/eingaenge.md).
/// </summary>
public sealed class ChangePreferences(IAccountPreferencesStore preferences, ICurrentAccount current, ILogger<ChangePreferences> logger)
{
    /// <param name="mailSenders">All sender addresses; <c>null</c> is invalid, so addresses are never removed by accident.</param>
    public async Task<PreferencesResult> ChangeAsync(bool newDocumentsPrivate, IReadOnlyList<string?>? mailSenders, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, "change-preferences");
            return new PreferencesResult(PreferencesOutcome.AccountGone);
        }

        if (mailSenders is null)
        {
            return new PreferencesResult(PreferencesOutcome.Invalid);
        }

        if (AccountPreferences.Create(newDocumentsPrivate, mailSenders, out var invalid) is not { } wanted)
        {
            return new PreferencesResult(PreferencesOutcome.Invalid, invalid);
        }

        var saved = await preferences.SaveAsync(current.Id, wanted, cancellationToken).ConfigureAwait(false);
        if (saved.Outcome == PreferencesOutcome.AccountGone)
        {
            // Removed between the check above and the save: the database refused the account's rows.
            logger.AccountGone(current.Id, "change-preferences");
        }

        return saved;
    }
}
