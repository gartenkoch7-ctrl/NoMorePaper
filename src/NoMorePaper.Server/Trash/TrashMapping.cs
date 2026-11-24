using NoMorePaper.Application.Trash;
using NoMorePaper.Contracts.Trash;

namespace NoMorePaper.Server.Trash;

/// <summary>Maps trash entries to their contract. Pure logic.</summary>
internal static class TrashMapping
{
    public static TrashItemDto ToDto(this TrashEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var item = entry.Item;
        return new TrashItemDto(item.Id, item.Kind.ToDto(), item.Title, item.DeletedAt, item.PurgeAfter, entry.RestoreSwaps, entry.DocumentInTrash);
    }

    public static TrashItemKindDto ToDto(this TrashItemKind kind) => kind switch
    {
        TrashItemKind.Document => TrashItemKindDto.Document,
        TrashItemKind.Original => TrashItemKindDto.Original,
        TrashItemKind.Edited => TrashItemKindDto.Edited,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of trash entry."),
    };
}
