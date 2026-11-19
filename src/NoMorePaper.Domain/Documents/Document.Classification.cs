namespace NoMorePaper.Domain.Documents;

// The fields of a document (correspondent, type, date, amount, summary, tags), where they come from,
// the duplicate hint, and who besides the owner may edit them. Processing, files and privacy are in Document.cs.
public sealed partial class Document
{
    private readonly List<DocumentTag> _tags = [];
    private readonly List<DocumentEditor> _editors = [];

    /// <summary>Who sent it: id of a <see cref="Classification.Correspondent"/>.</summary>
    public Guid? CorrespondentId { get; private set; }

    /// <summary>What it is: id of a <see cref="Classification.DocumentType"/>.</summary>
    public Guid? DocumentTypeId { get; private set; }

    /// <summary>Date printed on the document (not when it was received).</summary>
    public DateOnly? DocumentDate { get; private set; }

    public Money? Amount { get; private set; }

    /// <summary>One or two sentences about the content.</summary>
    public string? Summary { get; private set; }

    public IReadOnlyList<DocumentTag> Tags => _tags.AsReadOnly();

    /// <summary>Where the fields come from; <see cref="RecognitionState.Automatic"/> ones are listed for review.</summary>
    public RecognitionState Recognition { get; private set; }

    /// <summary>A document with (nearly) the same content; <c>null</c> when there is none or the hint was resolved.</summary>
    public Guid? PossibleDuplicateOf { get; private set; }

    /// <summary>Members besides the owner who may edit the fields (<see cref="CanEdit"/>).</summary>
    public IReadOnlyList<DocumentEditor> Editors => _editors.AsReadOnly();

    /// <summary>
    /// Whether the account may change the fields: the owner, and the chosen editors while they can see
    /// the document. A document without owner (M1, before setup) cannot be edited by anybody.
    /// </summary>
    public bool CanEdit(Guid accountId) =>
        OwnerId is { } owner && (owner == accountId || (!IsPrivate && _editors.Exists(editor => editor.AccountId == accountId)));

    /// <summary>Replaces the members who may edit the fields; the owner always may and is not listed.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> is not the owner.</exception>
    /// <exception cref="ArgumentException">An id is empty.</exception>
    public void SetEditors(Guid actor, IEnumerable<Guid> editorIds)
    {
        ArgumentNullException.ThrowIfNull(editorIds);
        RequireOwner(actor);
        var wanted = editorIds.Where(id => id != actor).Distinct().ToList();
        if (wanted.Contains(Guid.Empty))
        {
            throw new ArgumentException("An empty id refers to nobody.", nameof(editorIds));
        }

        // Unchanged editors stay the same instances, so persistence sees only real additions and removals.
        _editors.RemoveAll(editor => !wanted.Contains(editor.AccountId));
        var added = wanted.Where(id => !_editors.Exists(editor => editor.AccountId == id)).ToList();
        _editors.AddRange(added.Select(id => new DocumentEditor(id)));
    }

    /// <summary>Takes the fields the local LLM recognized and marks them <see cref="RecognitionState.Automatic"/>.</summary>
    /// <exception cref="InvalidOperationException">The fields are confirmed already; recognition never overrides them.</exception>
    public void Classify(DocumentFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (Recognition == RecognitionState.Confirmed)
        {
            throw new InvalidOperationException($"The fields of document {Id} are confirmed.");
        }

        Apply(fields);
        Recognition = RecognitionState.Automatic;
    }

    /// <summary>Sets the fields as an editor chose them; they count as confirmed.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> may not edit (<see cref="CanEdit"/>).</exception>
    public void Edit(Guid actor, DocumentFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        RequireEditor(actor);
        Apply(fields);
        Recognition = RecognitionState.Confirmed;
    }

    /// <summary>Confirms the fields as they are.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> may not edit (<see cref="CanEdit"/>).</exception>
    public void Confirm(Guid actor)
    {
        RequireEditor(actor);
        Recognition = RecognitionState.Confirmed;
    }

    /// <summary>Notes that the document <paramref name="documentId"/> has (nearly) the same content.</summary>
    /// <exception cref="ArgumentException">The id is empty or this document's own.</exception>
    public void MarkPossibleDuplicate(Guid documentId)
    {
        if (documentId == Guid.Empty || documentId == Id)
        {
            throw new ArgumentException("A document is no duplicate of itself or of nothing.", nameof(documentId));
        }

        PossibleDuplicateOf = documentId;
    }

    /// <summary>Resolves the duplicate hint, e.g. because both documents are kept.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="actor"/> may not edit (<see cref="CanEdit"/>).</exception>
    public void ClearPossibleDuplicate(Guid actor)
    {
        RequireEditor(actor);
        PossibleDuplicateOf = null;
    }

    private void Apply(DocumentFields fields)
    {
        Title = fields.Title ?? Title;
        CorrespondentId = fields.CorrespondentId;
        DocumentTypeId = fields.DocumentTypeId;
        DocumentDate = fields.DocumentDate;
        Amount = fields.Amount;
        Summary = fields.Summary;

        // Unchanged tags stay the same instances, so persistence sees only real additions and removals.
        _tags.RemoveAll(tag => !fields.TagIds.Contains(tag.TagId));
        var added = fields.TagIds.Where(id => !_tags.Exists(tag => tag.TagId == id)).ToList();
        _tags.AddRange(added.Select(id => new DocumentTag(id)));
    }

    private void RequireEditor(Guid actor)
    {
        if (!CanEdit(actor))
        {
            throw new InvalidOperationException($"The account may not edit document {Id}.");
        }
    }
}
