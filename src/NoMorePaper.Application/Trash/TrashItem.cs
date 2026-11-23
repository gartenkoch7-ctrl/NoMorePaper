using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// An entry of the trash: a whole document, or a file a document no longer keeps. It belongs to the
/// document's owner (<c>documents.owner_id</c>), so a handed-over document's entries follow it.
/// </summary>
/// <param name="Id">Version 7 id; files of the entry lie in a trash folder named after it.</param>
/// <param name="File">The file that went to the trash; <c>null</c> for a whole document.</param>
/// <param name="Title">The document's title when the entry was made.</param>
/// <param name="PurgeAfter">From then on the entry is removed for good (<see cref="TrashRules"/>).</param>
public sealed record TrashItem(
    Guid Id, TrashItemKind Kind, Guid DocumentId, DocumentFile? File, string Title, DateTimeOffset DeletedAt, DateTimeOffset PurgeAfter)
{
    /// <summary>The entry for a whole document moved to the trash at <paramref name="at"/>.</summary>
    public static TrashItem ForDocument(Document document, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(document);
        return New(TrashItemKind.Document, document, null, at);
    }

    /// <summary>The entry for the original or an edited version of <paramref name="document"/> moved to the trash at <paramref name="at"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="file"/> is the archive, which is always made anew.</exception>
    public static TrashItem ForFile(Document document, DocumentFile file, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(file);
        var kind = file.Kind switch
        {
            DocumentFileKind.Original => TrashItemKind.Original,
            DocumentFileKind.Edited => TrashItemKind.Edited,
            _ => throw new ArgumentException("Only originals and edited versions go to the trash.", nameof(file)),
        };
        return New(kind, document, file, at);
    }

    private static TrashItem New(TrashItemKind kind, Document document, DocumentFile? file, DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return new TrashItem(Guid.CreateVersion7(utc), kind, document.Id, file, document.Title, utc, TrashRules.PurgeAfter(utc));
    }
}
