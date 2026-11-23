using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Trash;

/// <summary>A row of <c>trash_items</c>; its owner is the owner of its document.</summary>
internal sealed class TrashItemRow
{
    public Guid Id { get; set; }

    public TrashItemKind Kind { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>The file that went to the trash (<c>jsonb</c>); <c>null</c> for a whole document.</summary>
    public DocumentFile? File { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset DeletedAt { get; set; }

    public DateTimeOffset PurgeAfter { get; set; }

    public static TrashItemRow From(TrashItem item) => new()
    {
        Id = item.Id,
        Kind = item.Kind,
        DocumentId = item.DocumentId,
        File = item.File,
        Title = item.Title,
        DeletedAt = item.DeletedAt,
        PurgeAfter = item.PurgeAfter,
    };

    public TrashItem ToItem() => new(Id, Kind, DocumentId, File, Title, DeletedAt, PurgeAfter);
}
