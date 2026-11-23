using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Accounts;

/// <summary>The signed-in account's default which versions an edited document keeps (ADR-0025).</summary>
public sealed class GetAfterEditing(IAccountPreferencesStore preferences, ICurrentAccount current)
{
    public Task<VersionPolicy> GetAsync(CancellationToken cancellationToken) =>
        preferences.GetAfterEditingAsync(current.Id, cancellationToken);
}
