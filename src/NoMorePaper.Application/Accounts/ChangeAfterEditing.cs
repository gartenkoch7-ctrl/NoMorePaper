using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The signed-in account chooses which versions its edited documents keep by default (ADR-0025): both,
/// only the edited version, or only the original. Each document may differ (owner's choice in the viewer).
/// </summary>
public sealed class ChangeAfterEditing(IAccountPreferencesStore preferences, ICurrentAccount current, ILogger<ChangeAfterEditing> logger)
{
    private const string Action = "change-after-editing";

    /// <returns><see cref="PreferencesOutcome.Saved"/>, <see cref="PreferencesOutcome.Invalid"/> for an unknown value, or <see cref="PreferencesOutcome.AccountGone"/>.</returns>
    public async Task<PreferencesOutcome> ChangeAsync(VersionPolicy policy, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return PreferencesOutcome.AccountGone;
        }

        if (!Enum.IsDefined(policy))
        {
            return PreferencesOutcome.Invalid;
        }

        if (!await preferences.SetAfterEditingAsync(current.Id, policy, cancellationToken).ConfigureAwait(false))
        {
            // Removed between the check above and the save: the database refused the account's row.
            logger.AccountGone(current.Id, Action);
            return PreferencesOutcome.AccountGone;
        }

        logger.AfterEditingChanged(current.Id, policy);
        return PreferencesOutcome.Saved;
    }
}
