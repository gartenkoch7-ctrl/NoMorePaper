namespace NoMorePaper.Web.Client.Trash;

/// <summary>What the trash page asks the user before it acts.</summary>
public enum TrashQuestionKind
{
    /// <summary>Delete one entry for good.</summary>
    Purge,

    /// <summary>Restore an entry whose place is taken: both swap places.</summary>
    Swap,

    /// <summary>Empty the whole trash.</summary>
    Empty,
}
