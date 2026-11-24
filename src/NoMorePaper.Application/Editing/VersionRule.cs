using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Which files a document keeps after an edit (ADR-0025): always the newest edited version only; the
/// original too unless the policy says "only edited"; with "only original" the new edited version goes
/// to the trash right away (as the account page promises) and the archive is made from the original.
/// A document always keeps a source, so without original the edit is kept whatever the policy. An append
/// always keeps its edited version (user, 2026-11-24): under "only original" the appended pages would be lost
/// with the trash, so the document keeps both from then on. Pure logic.
/// </summary>
public static class VersionRule
{
    /// <summary>
    /// The policy an edit of <paramref name="kind"/> follows when the document's is <paramref name="policy"/>. Without
    /// original nothing changes: the edit is kept anyway (<see cref="KeepsEdited"/>).
    /// </summary>
    public static VersionPolicy For(Document document, EditKind kind, VersionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(document);
        return kind == EditKind.Append && policy == VersionPolicy.OriginalOnly && document.FindFile(DocumentFileKind.Original) is not null
            ? VersionPolicy.KeepBoth
            : policy;
    }

    /// <summary>Whether the new edited version becomes the document's source.</summary>
    public static bool KeepsEdited(Document document, VersionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(document);
        return policy != VersionPolicy.OriginalOnly || document.FindFile(DocumentFileKind.Original) is null;
    }

    /// <summary>Whether the original goes to the trash with this edit.</summary>
    public static bool DropsOriginal(Document document, VersionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(document);
        return policy == VersionPolicy.EditedOnly && document.FindFile(DocumentFileKind.Original) is not null;
    }

    /// <summary>Whether the files of <paramref name="tracked"/> are still those of <paramref name="read"/> (same hashes).</summary>
    public static bool SameFiles(Document read, Document tracked)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(tracked);
        return Enum.GetValues<DocumentFileKind>().All(kind => read.FindFile(kind)?.Sha256 == tracked.FindFile(kind)?.Sha256);
    }
}
