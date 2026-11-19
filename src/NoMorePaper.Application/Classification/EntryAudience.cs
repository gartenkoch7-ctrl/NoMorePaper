namespace NoMorePaper.Application.Classification;

/// <summary>
/// Whose view of the lists (and of the recognition examples) a query takes (ADR-0023, addendum
/// 2026-11-19). Every caller chooses, so a private-only entry never reaches a shared document by default.
/// </summary>
public enum EntryAudience
{
    /// <summary>What the account may see: the visibility rule of the lists, private-only entries of its own included.</summary>
    Account,

    /// <summary>
    /// Only what the whole household sees already: entries used by a document that is not private, and
    /// only such documents as examples. For the recognition of a shared document, which must not bring
    /// a private-only entry (or its name) into it.
    /// </summary>
    Household,
}
