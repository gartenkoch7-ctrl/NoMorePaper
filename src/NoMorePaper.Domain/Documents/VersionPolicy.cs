namespace NoMorePaper.Domain.Documents;

/// <summary>Which files a document keeps after an edit (ADR-0025); each account has a default, each document may differ.</summary>
public enum VersionPolicy
{
    /// <summary>Original and newest edited version (default).</summary>
    KeepBoth,

    /// <summary>Only the edited version; the original goes to the trash.</summary>
    EditedOnly,

    /// <summary>Only the original; the edited version goes to the trash and the archive is made from the original again.</summary>
    OriginalOnly,
}
