namespace NoMorePaper.Application.Editing;

/// <summary>
/// An edit waiting for the Worker (table <c>document_edits</c>, one per document); the job of the
/// document is at <see cref="Processing.JobStage.Edit"/> meanwhile.
/// </summary>
/// <param name="Data">The plan as JSON; its shape depends on <paramref name="Kind"/>.</param>
/// <param name="RequestedBy">The account that asked for it.</param>
/// <param name="RecognizeAfter">Recognition follows the edit, e.g. because a waiting recognition was taken over.</param>
public sealed record DocumentEdit(Guid DocumentId, EditKind Kind, string Data, Guid RequestedBy, DateTimeOffset RequestedAt, bool RecognizeAfter);
