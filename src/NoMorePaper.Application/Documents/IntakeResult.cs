namespace NoMorePaper.Application.Documents;

/// <summary>Outcome of <see cref="IntakeDocument"/>.</summary>
/// <param name="DocumentId">
/// The new document (<see cref="IntakeStatus.Received"/>), the one with the same file
/// (<see cref="IntakeStatus.AlreadyThere"/>), otherwise <c>null</c>.
/// </param>
public sealed record IntakeResult(IntakeStatus Status, Guid? DocumentId)
{
    public static IntakeResult OwnerGone { get; } = new(IntakeStatus.OwnerGone, null);

    public static IntakeResult NoAdministrator { get; } = new(IntakeStatus.NoAdministrator, null);

    public static IntakeResult Received(Guid id) => new(IntakeStatus.Received, id);

    public static IntakeResult AlreadyThere(Guid existingId) => new(IntakeStatus.AlreadyThere, existingId);
}
