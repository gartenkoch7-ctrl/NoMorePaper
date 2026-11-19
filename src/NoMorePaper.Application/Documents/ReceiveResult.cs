namespace NoMorePaper.Application.Documents;

/// <summary>Outcome of <see cref="ReceiveDocument"/>; the Server maps each status to one HTTP status.</summary>
/// <param name="DocumentId">
/// The new document (<see cref="ReceiveStatus.Received"/>), the one with the same file
/// (<see cref="ReceiveStatus.Duplicate"/>), or <c>null</c> (<see cref="ReceiveStatus.AccountGone"/>).
/// </param>
public sealed record ReceiveResult(ReceiveStatus Status, Guid? DocumentId)
{
    /// <summary>The signed-in account was removed; nothing was stored.</summary>
    public static ReceiveResult AccountGone { get; } = new(ReceiveStatus.AccountGone, null);

    public static ReceiveResult Received(Guid id) => new(ReceiveStatus.Received, id);

    /// <param name="existingId">The document the uploader may see whose original has the same bytes.</param>
    public static ReceiveResult Duplicate(Guid existingId) => new(ReceiveStatus.Duplicate, existingId);
}
