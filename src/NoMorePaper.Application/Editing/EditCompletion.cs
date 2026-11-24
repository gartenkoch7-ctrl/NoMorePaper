using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// What a carried-out edit stores, in one transaction under the row locks of its documents
/// (<see cref="Processing.IJobQueue.CompleteEditAsync"/>).
/// </summary>
/// <param name="Text">The text recognized anew; <c>null</c> keeps the text (e.g. a split, whose document goes to the trash).</param>
/// <param name="Pages">The page information of the new archive; <c>null</c> keeps it.</param>
/// <param name="OtherId">A second document the edit changes (the appended one), locked too, in the order of the ids.</param>
/// <param name="TrashItems">Entries for what goes to the trash; recorded in the same transaction.</param>
/// <param name="Data">The new <c>document_edits.data</c> (with <see cref="EditData.Finish"/>) while files are left to move.</param>
/// <param name="Final">Nothing is left to do afterwards: the edit and its job end in the same transaction.</param>
/// <param name="Apply">
/// Changes the locked, tracked documents (the job's, and the other one or <c>null</c> when it is gone or
/// in the trash); returns <c>false</c> when they are no longer as they were read, so nothing is stored.
/// </param>
public sealed record EditCompletion(
    string? Text,
    IReadOnlyList<PageInfo>? Pages,
    Guid? OtherId,
    IReadOnlyList<TrashItem> TrashItems,
    string Data,
    bool Final,
    Func<Document, Document?, bool> Apply);
