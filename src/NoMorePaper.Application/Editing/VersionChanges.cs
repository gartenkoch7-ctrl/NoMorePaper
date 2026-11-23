using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// What storing a new version changes (ADR-0025, <see cref="VersionRule"/>): the trash entries, the file
/// moves after the commit (<see cref="EditData.Finish"/>) and the change of the document under its lock.
/// Built from the document as read before the work began. Pure logic.
/// </summary>
public sealed class VersionChanges
{
    private readonly Document _read;
    private readonly DocumentFile? _edited;
    private readonly DocumentFile _archive;
    private readonly DateTimeOffset _at;

    private VersionChanges(
        Document read, DocumentFile? edited, DocumentFile archive, DateTimeOffset at, List<TrashItem> items, List<FinishMove> moves)
    {
        _read = read;
        _edited = edited;
        _archive = archive;
        _at = at;
        Items = items;
        Moves = moves;
    }

    /// <summary>Entries for the files that go to the trash.</summary>
    public IReadOnlyList<TrashItem> Items { get; }

    /// <summary>The moves after the commit, one per entry.</summary>
    public IReadOnlyList<FinishMove> Moves { get; }

    /// <summary>Whether the new edited version becomes the document's source.</summary>
    public bool KeepsEdited => _edited is not null && Moves.All(move => move.Kind != FinishMoveKind.NewEdited);

    /// <param name="read">The document as read before the work began.</param>
    /// <param name="policy">Which versions it keeps (its own choice, else its owner's default).</param>
    /// <param name="edited">The new edited version; <c>null</c> when only the archive is made anew.</param>
    /// <param name="archive">The new archive.</param>
    /// <param name="at">Now; when the edited version was made and the files went to the trash.</param>
    public static VersionChanges For(Document read, VersionPolicy policy, DocumentFile? edited, DocumentFile archive, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(archive);
        List<TrashItem> items = [];
        List<FinishMove> moves = [];
        void Trash(DocumentFile file, FinishMoveKind kind)
        {
            var item = TrashItem.ForFile(read, file, at);
            items.Add(item);
            moves.Add(new FinishMove(kind, item.Id));
        }

        if (edited is not null)
        {
            // Only the newest edited version is kept; the one before goes to the trash.
            if (read.FindFile(DocumentFileKind.Edited) is { } previous)
            {
                Trash(previous, FinishMoveKind.PreviousEdited);
            }

            if (!VersionRule.KeepsEdited(read, policy))
            {
                Trash(edited, FinishMoveKind.NewEdited);
            }
            else if (VersionRule.DropsOriginal(read, policy))
            {
                Trash(read.FindFile(DocumentFileKind.Original)!, FinishMoveKind.Original);
            }
        }

        return new VersionChanges(read, edited, archive, at, items, moves);
    }

    /// <summary>Changes the locked document's files.</summary>
    /// <returns><c>false</c>, changing nothing, when its files are no longer those that were read.</returns>
    public bool ApplyTo(Document tracked)
    {
        ArgumentNullException.ThrowIfNull(tracked);
        if (!VersionRule.SameFiles(_read, tracked))
        {
            return false;
        }

        if (KeepsEdited)
        {
            tracked.ReplaceFile(_edited!, _at);
            if (Moves.Any(move => move.Kind == FinishMoveKind.Original))
            {
                tracked.RemoveFile(DocumentFileKind.Original);
            }
        }
        else if (_edited is not null && tracked.FindFile(DocumentFileKind.Edited) is not null)
        {
            // "Only original": the archive is made from the original again, no edited version stays.
            tracked.RemoveFile(DocumentFileKind.Edited);
        }

        tracked.ReplaceFile(_archive, _at);
        return true;
    }
}
