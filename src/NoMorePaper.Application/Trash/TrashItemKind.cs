namespace NoMorePaper.Application.Trash;

/// <summary>What lies in the trash (ADR-0025).</summary>
public enum TrashItemKind
{
    /// <summary>A whole document; its row and files stay where they are, only hidden.</summary>
    Document,

    /// <summary>The original of a document that keeps only its edited version.</summary>
    Original,

    /// <summary>An edited version replaced by a newer one or dropped by the owner.</summary>
    Edited,
}
