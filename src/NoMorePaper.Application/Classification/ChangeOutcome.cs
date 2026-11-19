namespace NoMorePaper.Application.Classification;

/// <summary>Outcome of changing fields, editors, duplicates or list entries; the Server maps each to one HTTP status.</summary>
public enum ChangeOutcome
{
    Changed,

    /// <summary>No such document or entry, or the account may not see it; its existence stays hidden.</summary>
    NotFound,

    /// <summary>The account sees it but may not make this change.</summary>
    Forbidden,

    /// <summary>The request is invalid: a value, an unknown member, or an entry the account may not see.</summary>
    Invalid,

    /// <summary>The change does not fit the current state, e.g. deleting a document that is no possible duplicate.</summary>
    Conflict,

    /// <summary>The signed-in account was removed; its session is over.</summary>
    AccountGone,
}
