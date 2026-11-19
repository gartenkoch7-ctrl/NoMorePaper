using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Preferences in memory; a sender address belongs to whichever account saved it first.</summary>
internal sealed class FakeAccountPreferencesStore : IAccountPreferencesStore
{
    public Dictionary<Guid, AccountPreferences> Saved { get; } = [];

    public Task<AccountPreferences> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.GetValueOrDefault(accountId, AccountPreferences.Default));

    public Task<string?> SaveAsync(Guid accountId, AccountPreferences preferences, CancellationToken cancellationToken)
    {
        var taken = preferences.MailSenders.FirstOrDefault(address =>
            Saved.Any(other => other.Key != accountId && other.Value.MailSenders.Contains(address)));
        if (taken is null)
        {
            Saved[accountId] = preferences;
        }

        return Task.FromResult(taken);
    }

    public Task<Guid?> FindOwnerOfSenderAsync(string address, CancellationToken cancellationToken)
    {
        var normalized = AccountPreferences.NormalizeAddress(address);
        var owner = Saved.FirstOrDefault(entry => normalized is not null && entry.Value.MailSenders.Contains(normalized));
        return Task.FromResult<Guid?>(owner.Value is null ? null : owner.Key);
    }
}
