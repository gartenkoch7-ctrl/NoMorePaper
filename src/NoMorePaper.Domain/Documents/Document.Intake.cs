namespace NoMorePaper.Domain.Documents;

// Where a document came from (M4, ADR-0024): its source, "from the mail", and the owner check for
// documents whose owner was unknown on arrival (they go to the administrator, private, until checked).
public sealed partial class Document
{
    private const string FallbackTitle = "document";

    /// <summary>How the document came in.</summary>
    public DocumentSource Source { get; private set; }

    /// <summary>"From the mail": sender, subject and a note; <c>null</c> unless it came by mail.</summary>
    public MailOrigin? MailOrigin { get; private set; }

    /// <summary>
    /// The owner was unknown on arrival (e.g. a mail from an unknown sender), so the administrator got
    /// it, privately; it is listed in the owner's "to review" until checked (<see cref="ConfirmOwnerCheck"/>)
    /// or handed over (<see cref="HandOver"/>).
    /// </summary>
    public bool NeedsOwnerCheck { get; private set; }

    /// <summary>Notes where a document that came by mail comes from.</summary>
    public void AttachMailOrigin(MailOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        MailOrigin = origin;
    }

    /// <summary>
    /// Marks a document whose owner was unknown on arrival: it waits for the owner's check, and it is
    /// private until then, whatever the owner chose for new documents.
    /// </summary>
    public void MarkOwnerCheck()
    {
        NeedsOwnerCheck = true;
        IsPrivate = true;
    }

    /// <summary>The owner keeps the document as it is ("fits"); it stays private or shared as it is now.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner, or nothing waits for a check.</exception>
    public void ConfirmOwnerCheck(Guid actor)
    {
        RequireOwner(actor);
        RequireOwnerCheck();
        NeedsOwnerCheck = false;
    }

    /// <summary>
    /// The owner hands a document waiting for its check to another account, which becomes its owner.
    /// Editors are dropped; they were chosen by the previous owner. Fields that nobody confirmed are
    /// dropped too and recognition starts over (<see cref="RecognitionState.None"/>): they were made
    /// with the previous owner's lists, which may hold private entries (ADR-0023).
    /// </summary>
    /// <param name="isPrivate">The new owner's choice for new documents.</param>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner, or nothing waits for a check.</exception>
    /// <exception cref="ArgumentException"><paramref name="newOwner"/> is empty or the owner already.</exception>
    public void HandOver(Guid actor, Guid newOwner, bool isPrivate)
    {
        RequireOwner(actor);
        RequireOwnerCheck();
        if (newOwner == Guid.Empty || newOwner == actor)
        {
            throw new ArgumentException("Hand a document over to another account.", nameof(newOwner));
        }

        OwnerId = newOwner;
        IsPrivate = isPrivate;
        NeedsOwnerCheck = false;
        _editors.Clear();
        if (Recognition == RecognitionState.Confirmed)
        {
            return;
        }

        Title = ReceivedTitle();
        CorrespondentId = null;
        DocumentTypeId = null;
        Summary = null;
        _tags.Clear();
        Recognition = RecognitionState.None;
    }

    private void RequireOwnerCheck()
    {
        if (!NeedsOwnerCheck)
        {
            throw new InvalidOperationException($"Document {Id} does not wait for its owner's check.");
        }
    }

    // The title the document got on arrival: the received file name without its extension. Recognition
    // may have replaced it with a title made from the previous owner's view.
    private string ReceivedTitle()
    {
        var name = FindFile(DocumentFileKind.Original)?.FileName ?? string.Empty;
        var dot = name.LastIndexOf('.');
        var stem = (dot >= 0 ? name[..dot] : name).Trim();
        if (stem.Length == 0)
        {
            return FallbackTitle;
        }

        return stem.Length > MaxTitleLength ? stem[..MaxTitleLength] : stem;
    }
}
