using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Preferences in memory; a sender address belongs to whichever account saved it first.</summary>
internal sealed class FakeAccountPreferencesStore : IAccountPreferencesStore
{
    public Dictionary<Guid, AccountPreferences> Saved { get; } = [];

    public Task<AccountPreferences> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.GetValueOrDefault(accountId, AccountPreferences.Default));

    /// <summary>The account is removed while saving: the database refuses its rows.</summary>
    public bool RemovedWhileSaving { get; set; }

    public Task<PreferencesResult> SaveAsync(Guid accountId, AccountPreferences preferences, CancellationToken cancellationToken)
    {
        if (RemovedWhileSaving)
        {
            return Task.FromResult(new PreferencesResult(PreferencesOutcome.AccountGone));
        }

        var taken = preferences.MailSenders.FirstOrDefault(address =>
            Saved.Any(other => other.Key != accountId && other.Value.MailSenders.Contains(address)));
        if (taken is not null)
        {
            return Task.FromResult(new PreferencesResult(PreferencesOutcome.SenderTaken, taken));
        }

        Saved[accountId] = preferences;
        return Task.FromResult(new PreferencesResult(PreferencesOutcome.Saved));
    }

    public Task<Guid?> FindOwnerOfSenderAsync(string address, CancellationToken cancellationToken)
    {
        var normalized = AccountPreferences.NormalizeAddress(address);
        var owner = Saved.FirstOrDefault(entry => normalized is not null && entry.Value.MailSenders.Contains(normalized));
        return Task.FromResult<Guid?>(owner.Value is null ? null : owner.Key);
    }
}
