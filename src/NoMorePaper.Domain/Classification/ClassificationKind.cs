namespace NoMorePaper.Domain.Classification;

/// <summary>The maintained lists a document is sorted by.</summary>
public enum ClassificationKind
{
    /// <summary>Who sent the document (<see cref="Correspondent"/>).</summary>
    Correspondent,

    /// <summary>What kind of document it is (<see cref="DocumentType"/>).</summary>
    DocumentType,

    /// <summary>Free keywords (<see cref="Tag"/>).</summary>
    Tag,
}
