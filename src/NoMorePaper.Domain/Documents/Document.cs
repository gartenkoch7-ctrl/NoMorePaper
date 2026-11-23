namespace NoMorePaper.Domain.Documents;

/// <summary>
/// A received document with its files and processing state. Allowed transitions:
/// <c>Queued → Processing → Done | Failed</c>; <c>Processing → Processing</c> is allowed, because a
/// retry or a Worker taking over an abandoned job starts the same document again. Its fields
/// (correspondent, type, date, …) and who may edit them are in <c>Document.Classification.cs</c>; its
/// origin and the owner check in <c>Document.Intake.cs</c>; its files in <c>Document.Files.cs</c>; the
/// trash in <c>Document.Trash.cs</c>; versions and the split suggestion in <c>Document.Editing.cs</c>.
/// </summary>
public sealed partial class Document
{
    /// <summary>Longest title kept; longer titles must be shortened by the caller.</summary>
    public const int MaxTitleLength = 500;

    /// <summary>Longer failure reasons are cut, so recording a failure never fails itself.</summary>
    public const int MaxFailureReasonLength = 1000;

    private Document(Guid id, string title, DateTimeOffset receivedAt, Guid? ownerId, bool isPrivate, DocumentSource source)
    {
        Id = id;
        Title = title;
        ReceivedAt = receivedAt;
        OwnerId = ownerId;
        IsPrivate = isPrivate;
        Source = source;
        State = ProcessingState.Queued;
    }

    /// <summary>Version 7 GUID: sortable by time, and the archive folder (year/month) derives from it.</summary>
    public Guid Id { get; }

    public string Title { get; private set; }

    /// <summary>When the document arrived, always in UTC.</summary>
    public DateTimeOffset ReceivedAt { get; }

    public ProcessingState State { get; private set; }

    /// <summary>
    /// Account that received the document. <c>null</c> only for documents from before accounts existed
    /// (M1) until the household is set up, which hands them to the administrator.
    /// </summary>
    public Guid? OwnerId { get; private set; }

    /// <summary>A private document is visible to its owner only, not even to the administrator.</summary>
    public bool IsPrivate { get; private set; }

    /// <summary>Recognized text; <c>null</c> until processing is done, empty when the document has no text.</summary>
    public string? ContentText { get; private set; }

    /// <summary>Technical reason of the last failure (no document content); <c>null</c> unless failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creates a queued document; the time comes from the caller's clock.</summary>
    /// <param name="ownerId">The account that received it.</param>
    /// <param name="isPrivate">Whether only <paramref name="ownerId"/> may see it.</param>
    /// <param name="source">How it came in.</param>
    public static Document Receive(
        string title, DateTimeOffset receivedAt, Guid ownerId, bool isPrivate, DocumentSource source = DocumentSource.Upload)
    {
        RequireTitle(title);
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A document needs an owner.", nameof(ownerId));
        }

        var utc = receivedAt.ToUniversalTime();
        return new Document(Guid.CreateVersion7(utc), title, utc, ownerId, isPrivate, source);
    }

    /// <summary>
    /// Whether the account may see this document: its owner always, everybody else only when it is not
    /// private. A document in the trash is visible nowhere but on its owner's trash page.
    /// </summary>
    public bool IsVisibleTo(Guid accountId) => !IsInTrash && (!IsPrivate || OwnerId == accountId);

    /// <summary>Hides the document from everybody but its owner.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner.</exception>
    public void MakePrivate(Guid actor)
    {
        RequireOwner(actor);
        IsPrivate = true;
    }

    /// <summary>Shows the document to every member of the household again.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner.</exception>
    public void Share(Guid actor)
    {
        RequireOwner(actor);
        IsPrivate = false;
    }

    public void StartProcessing()
    {
        RequireState(nameof(StartProcessing), ProcessingState.Queued, ProcessingState.Processing);
        State = ProcessingState.Processing;
    }

    /// <param name="contentText">Recognized text; empty when the document contains none.</param>
    public void Complete(string contentText)
    {
        ArgumentNullException.ThrowIfNull(contentText);
        RequireState(nameof(Complete), ProcessingState.Processing);
        ContentText = contentText;
        State = ProcessingState.Done;
    }

    /// <param name="reason">Technical reason without document content, e.g. "OCR timed out".</param>
    public void Fail(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RequireState(nameof(Fail), ProcessingState.Processing);
        FailureReason = reason.Length > MaxFailureReasonLength ? reason[..MaxFailureReasonLength] : reason;
        State = ProcessingState.Failed;
    }

    private void RequireState(string operation, params ProcessingState[] allowed)
    {
        if (!allowed.Contains(State))
        {
            throw new InvalidOperationException($"{operation} is not allowed for document {Id} in state {State}.");
        }
    }

    // A document without owner (M1, before setup) has nobody who may change it.
    private void RequireOwner(Guid actor)
    {
        if (OwnerId is not { } owner || owner != actor)
        {
            throw new InvalidOperationException($"Only the owner may do this with document {Id}.");
        }
    }

    internal static void RequireTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"At most {MaxTitleLength} characters.", nameof(title));
        }
    }
}
