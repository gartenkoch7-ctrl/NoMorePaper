namespace NoMorePaper.Domain.Documents;

/// <summary>
/// A received document with its files and processing state. Allowed transitions:
/// <c>Queued → Processing → Done | Failed</c>; <c>Processing → Processing</c> is allowed, because a
/// retry or a Worker taking over an abandoned job starts the same document again.
/// </summary>
public sealed class Document
{
    /// <summary>Longest title kept; longer titles must be shortened by the caller.</summary>
    public const int MaxTitleLength = 500;

    /// <summary>Longer failure reasons are cut, so recording a failure never fails itself.</summary>
    public const int MaxFailureReasonLength = 1000;

    private readonly List<DocumentFile> _files = [];

    private Document(Guid id, string title, DateTimeOffset receivedAt)
    {
        Id = id;
        Title = title;
        ReceivedAt = receivedAt;
        State = ProcessingState.Queued;
    }

    /// <summary>Version 7 GUID: sortable by time, and the archive folder (year/month) derives from it.</summary>
    public Guid Id { get; }

    public string Title { get; private set; }

    /// <summary>When the document arrived, always in UTC.</summary>
    public DateTimeOffset ReceivedAt { get; }

    public ProcessingState State { get; private set; }

    /// <summary>Recognized text; <c>null</c> until processing is done, empty when the document has no text.</summary>
    public string? ContentText { get; private set; }

    /// <summary>Technical reason of the last failure (no document content); <c>null</c> unless failed.</summary>
    public string? FailureReason { get; private set; }

    public IReadOnlyList<DocumentFile> Files => _files.AsReadOnly();

    /// <summary>Creates a queued document; the time comes from the caller's clock.</summary>
    public static Document Receive(string title, DateTimeOffset receivedAt)
    {
        RequireTitle(title);
        var utc = receivedAt.ToUniversalTime();
        return new Document(Guid.CreateVersion7(utc), title, utc);
    }

    /// <summary>Returns the file of the given kind, or <c>null</c> if it has not been stored yet.</summary>
    public DocumentFile? FindFile(DocumentFileKind kind) => _files.Find(file => file.Kind == kind);

    /// <summary>Adds a file; each kind exists at most once, because originals are never replaced.</summary>
    public void AttachFile(DocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (FindFile(file.Kind) is not null)
        {
            throw new InvalidOperationException($"Document {Id} already has a {file.Kind} file.");
        }

        _files.Add(file);
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

    private static void RequireTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"At most {MaxTitleLength} characters.", nameof(title));
        }
    }
}
