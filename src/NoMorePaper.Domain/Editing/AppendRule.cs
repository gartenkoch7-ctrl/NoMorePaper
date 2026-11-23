using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Domain.Editing;

/// <summary>
/// The rules of appending one document to the open one (ADR-0025): the pages go to the end of the
/// target, the appended document goes to the trash. Pure logic.
/// </summary>
public static class AppendRule
{
    /// <summary>
    /// Whether the result must be private: when either document is. A private document appended to a
    /// shared one would otherwise show its pages to the household; the app says so beforehand.
    /// </summary>
    public static bool ResultIsPrivate(Document target, Document appended)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(appended);
        return target.IsPrivate || appended.IsPrivate;
    }

    /// <summary>Why <paramref name="actor"/> may not append <paramref name="appended"/> to <paramref name="target"/>.</summary>
    /// <returns>The first reason found, or <c>null</c> when appending is allowed.</returns>
    public static AppendRefusal? Check(Document target, Document appended, Guid actor)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(appended);
        if (target.Id == appended.Id)
        {
            return AppendRefusal.Same;
        }

        if (target.IsInTrash || appended.IsInTrash)
        {
            return AppendRefusal.InTrash;
        }

        if (!target.CanEdit(actor) || !appended.CanEdit(actor))
        {
            return AppendRefusal.NotEditable;
        }

        return target.State != ProcessingState.Done || appended.State != ProcessingState.Done ? AppendRefusal.NotDone : null;
    }
}
