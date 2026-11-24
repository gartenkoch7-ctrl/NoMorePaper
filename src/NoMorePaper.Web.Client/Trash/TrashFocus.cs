using NoMorePaper.Contracts.Trash;

namespace NoMorePaper.Web.Client.Trash;

/// <summary>
/// Where focus goes on the trash page after an action, a cancelled question or a reload. The table (from
/// 48 rem) and the narrow list both hold each button under ids that differ by a prefix, so a button target
/// names both ids; focusing the hidden one does nothing, so the visible one takes it.
/// </summary>
public static class TrashFocus
{
    public const string Heading = "trash-heading";

    public const string Retry = "trash-retry";

    public const string EmptyButton = "trash-empty-button";

    public const string RestoreAction = "restore";

    public const string PurgeAction = "purge";

    /// <summary>The ids of one button of an entry, in the table and in the narrow list.</summary>
    public static string[] Button(string action, Guid id) => [$"trash-{action}-{id:N}", $"trash-card-{action}-{id:N}"];

    /// <summary>After the list was loaded (again) without a row to return to: the heading, or "retry" when it failed.</summary>
    public static string[] AfterReload(bool loadFailed) => [loadFailed ? Retry : Heading];

    /// <summary>After "Abbrechen" or Escape: the button that opened the question.</summary>
    public static string[] AfterCancel(TrashQuestion question) => question.Kind switch
    {
        TrashQuestionKind.Empty => [EmptyButton],
        TrashQuestionKind.Swap => Button(RestoreAction, question.Id),
        _ => Button(PurgeAction, question.Id),
    };

    /// <summary>
    /// After an entry was restored or deleted and the list loaded again: the same button of the entry that took
    /// its place (<paramref name="index"/> is where it stood before), the previous one when it was the last, the
    /// heading when nothing is left.
    /// </summary>
    public static string[] AfterAction(IReadOnlyList<TrashItemDto> items, int index, string action, bool loadFailed)
    {
        if (loadFailed || items.Count == 0)
        {
            return AfterReload(loadFailed);
        }

        var next = items[Math.Clamp(index, 0, items.Count - 1)];
        // A file of a trashed document has no restore button; its deletion button is the closest.
        return Button(action == RestoreAction && next.DocumentInTrash ? PurgeAction : action, next.Id);
    }
}
