namespace NoMorePaper.Domain.Documents;

/// <summary>Which file of a document is meant (ADR-0003).</summary>
public enum DocumentFileKind
{
    /// <summary>The file exactly as received; never modified.</summary>
    Original,

    /// <summary>The searchable PDF/A produced by the Worker.</summary>
    Archive,
}
