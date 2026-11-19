namespace NoMorePaper.Application.Classification;

/// <summary>Outcome of renaming or merging list entries; the Server maps each to one HTTP status.</summary>
public enum EntryChange
{
    Changed,

    /// <summary>An entry does not exist or the account may not see it; its existence stays hidden.</summary>
    NotFound,

    /// <summary>The account sees the entries but may not edit every document that uses them.</summary>
    Forbidden,
}
