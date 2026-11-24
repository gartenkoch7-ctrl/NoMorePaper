namespace NoMorePaper.Application.Trash;

/// <summary>What <see cref="TrashEraser.EraseAsync"/> did with an entry.</summary>
internal enum EraseOutcome
{
    /// <summary>The entry was removed for good.</summary>
    Erased,

    /// <summary>There is no such entry (any more), or it belongs to another account.</summary>
    NotFound,

    /// <summary>
    /// A file entry whose document has a planned edit: kept for now, because the edit moves files of the
    /// document into trash entries after its commit and would leave them in a folder nothing points to.
    /// </summary>
    EditPending,
}
