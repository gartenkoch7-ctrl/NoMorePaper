namespace NoMorePaper.Domain.Documents;

// The stored files of a document (ADR-0003, ADR-0025): the original as received, the newest edited
// version, and the archive PDF/A made from the current source (edited, else original).
public sealed partial class Document
{
    private readonly List<DocumentFile> _files = [];

    public IReadOnlyList<DocumentFile> Files => _files.AsReadOnly();

    /// <summary>When the newest edited version was made, in UTC; <c>null</c> without one.</summary>
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    /// What the archive is made from and pages are edited from: the newest edited version, else the
    /// original; <c>null</c> only for a document without files.
    /// </summary>
    public DocumentFile? CurrentSource => FindFile(DocumentFileKind.Edited) ?? FindFile(DocumentFileKind.Original);

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

    /// <summary>
    /// Stores a new edited version or archive in place of the old one (or as the first one). The
    /// original is never replaced.
    /// </summary>
    /// <param name="at">When it was made; an edited version records it as <see cref="EditedAt"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="file"/> is an original.</exception>
    public void ReplaceFile(DocumentFile file, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Kind == DocumentFileKind.Original)
        {
            throw new ArgumentException("The original is never replaced.", nameof(file));
        }

        _files.RemoveAll(existing => existing.Kind == file.Kind);
        _files.Add(file);
        if (file.Kind == DocumentFileKind.Edited)
        {
            EditedAt = at.ToUniversalTime();
        }
    }

    /// <summary>
    /// Takes a file away, e.g. into the trash; the document must keep a source (original or edited
    /// version) to show and edit.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no such file, or no source would be left.</exception>
    public void RemoveFile(DocumentFileKind kind)
    {
        if (FindFile(kind) is null)
        {
            throw new InvalidOperationException($"Document {Id} has no {kind} file.");
        }

        var sourceLeft = _files.Exists(file => file.Kind != kind && file.Kind is DocumentFileKind.Original or DocumentFileKind.Edited);
        if (!sourceLeft)
        {
            throw new InvalidOperationException($"Document {Id} needs its original or an edited version.");
        }

        _files.RemoveAll(file => file.Kind == kind);
        if (kind == DocumentFileKind.Edited)
        {
            EditedAt = null;
        }
    }

    /// <summary>The text recognized again after an edit or a rebuild of the archive; the fields stay.</summary>
    /// <param name="contentText">Recognized text; empty when the document contains none.</param>
    /// <exception cref="InvalidOperationException">The document is not done.</exception>
    public void ReplaceContent(string contentText)
    {
        ArgumentNullException.ThrowIfNull(contentText);
        RequireState(nameof(ReplaceContent), ProcessingState.Done);
        ContentText = contentText;
    }
}
