using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;

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

    /// <summary>The choices of <see cref="SetAfterEditingAsync"/> by account.</summary>
    public Dictionary<Guid, VersionPolicy> AfterEditing { get; } = [];

    public Task<VersionPolicy> GetAfterEditingAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(AfterEditing.GetValueOrDefault(accountId, VersionPolicy.KeepBoth));

    public Task<bool> SetAfterEditingAsync(Guid accountId, VersionPolicy policy, CancellationToken cancellationToken)
    {
        if (RemovedWhileSaving)
        {
            return Task.FromResult(false);
        }

        AfterEditing[accountId] = policy;
        return Task.FromResult(true);
    }
}
