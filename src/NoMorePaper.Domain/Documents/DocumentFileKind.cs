namespace NoMorePaper.Domain.Documents;

/// <summary>Which file of a document is meant (ADR-0003, ADR-0025).</summary>
public enum DocumentFileKind
{
    /// <summary>The file exactly as received; never modified.</summary>
    Original,

    /// <summary>The searchable PDF/A produced by the Worker from the current source.</summary>
    Archive,

    /// <summary>The newest edited version (pages turned, removed, reordered, appended); replaced by every new edit.</summary>
    Edited,
}
