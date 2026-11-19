namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>A row of <c>account_preferences</c>; an account without a row has the defaults.</summary>
internal sealed class AccountPreferencesRow
{
    public Guid AccountId { get; set; }

    public bool NewDocumentsPrivate { get; set; }
}
