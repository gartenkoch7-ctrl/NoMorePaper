namespace NoMorePaper.Application.Editing;

/// <summary>A file that goes to the trash once the new version of an edit is stored (<see cref="EditData.Finish"/>).</summary>
/// <param name="TrashItemId">The trash entry recorded for it in the same transaction.</param>
public sealed record FinishMove(FinishMoveKind Kind, Guid TrashItemId);
