namespace NoMorePaper.Application.Accounts;

/// <summary>The preferences of the signed-in account.</summary>
public sealed class GetPreferences(IAccountPreferencesStore preferences, ICurrentAccount current)
{
    public Task<AccountPreferences> GetAsync(CancellationToken cancellationToken) =>
        preferences.GetAsync(current.Id, cancellationToken);
}
