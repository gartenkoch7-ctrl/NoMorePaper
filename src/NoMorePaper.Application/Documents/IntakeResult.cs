namespace NoMorePaper.Application.Documents;

/// <summary>Outcome of <see cref="IntakeDocument"/>.</summary>
/// <param name="DocumentId">
/// The new document (<see cref="IntakeStatus.Received"/>), the one with the same file
/// (<see cref="IntakeStatus.AlreadyThere"/>), otherwise <c>null</c>.
/// </param>
/// <param name="IsPrivate">For <see cref="IntakeStatus.Received"/>: whether the new document is private, as applied.</param>
public sealed record IntakeResult(IntakeStatus Status, Guid? DocumentId, bool IsPrivate = false)
{
    public static IntakeResult OwnerGone { get; } = new(IntakeStatus.OwnerGone, null);

    public static IntakeResult NoAdministrator { get; } = new(IntakeStatus.NoAdministrator, null);

    public static IntakeResult Received(Guid id, bool isPrivate) => new(IntakeStatus.Received, id, isPrivate);

    public static IntakeResult AlreadyThere(Guid existingId) => new(IntakeStatus.AlreadyThere, existingId);
}
